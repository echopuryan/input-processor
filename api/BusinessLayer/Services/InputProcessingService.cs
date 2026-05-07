using BusinessLayer.Channels;
using BusinessLayer.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Processes the user input by sorting the characters in the input string by ascending order of their occurrence in the input string.
/// </summary>
public class InputProcessingService : IInputProcessingService
{
    /// <summary>
    /// Logger
    /// </summary>
    private readonly ILogger<InputProcessingService> _logger;
    /// <summary>
    /// Memory cache. Later on can become an external cache like Redis  
    /// </summary>
    private readonly IMemoryCache _memoryCache;
    /// <summary>
    /// Cache entry options
    /// </summary>
    private MemoryCacheEntryOptions _memoryCacheOptions = new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };
    /// <summary>
    /// Service to hand the processing job to the BG service
    /// </summary>
    private readonly IDataProcessingRequestChannel _jobRequestChannel;

    /// <summary>
    /// Main constructor for the InputProcessingService class that takes in a logger instance for logging purposes.
    /// </summary>
    /// <param name="logger">Logger</param>
    public InputProcessingService(
        ILogger<InputProcessingService> logger,
        IMemoryCache memoryCache,
        IDataProcessingRequestChannel jobRequestChannel)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _jobRequestChannel = jobRequestChannel;
    }

    /// <summary>
    /// Process the input string by sorting the characters in the input string by ascending order of their occurrence in the input string.
    /// </summary>
    /// <param name="input">User input</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>SortedString/base64 string</returns>
    public Task<string> ProcessInputAsync(string input, CancellationToken cancellationToken)
    {
        // check the cache first
        var cacheKey = input;
        if (!_memoryCache.TryGetValue<string>(cacheKey, out var processedText))
        {
            // cache miss, process the input and 
            _logger.LogDebug("Cache miss for input: '{Input}'", input);

            processedText = GetProcessedString(input, cancellationToken);

            _logger.LogDebug("Caching processed response for input: '{Input}'", input);
            _memoryCache.Set(cacheKey, processedText, _memoryCacheOptions);
        }

        _logger.LogDebug("Processed response: {Response}", processedText);
        // cache hit, return it w/o processing
        return Task.FromResult(processedText ?? "");
    }

    /// <summary>
    /// Async-ly process the user input and push events when each character is processed. This method is used for streaming SSE events to the client.
    /// </summary>
    /// <param name="input">User input to be processed</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task<Guid> StartProcessingAsync(string input, CancellationToken cancellationToken)
    {
        var processedString = GetProcessedString(input, cancellationToken);

        _logger.LogDebug("Prepared string: '{ProcessedString}'", processedString);

        var jobId = Guid.NewGuid();
        // push the job request for the BG to process
        await _jobRequestChannel.WriteAsync(new DataProcessingRequest
        {
            Id = jobId,
            UserInput = processedString,
        }, cancellationToken);

        return jobId;
    }

    /// <summary>
    /// Calculate the input string character frequencies, sort by the character code and append the original string as a base64 encoded string at the end.
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>Processed string</returns>
    private string GetProcessedString(string input, CancellationToken cancellationToken)
    {
        var inputCharacterCounts = new Dictionary<char, int>();

        // check if the input is null or empty
        if (string.IsNullOrEmpty(input)) throw new ArgumentException("Input cannot be null or empty", nameof(input));

        // Convert the input string to a base64 encoded string
        var base64EncodedInput = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        _logger.LogInformation("Base64 encoded input: {Base64EncodedInput}", base64EncodedInput);

        foreach (var c in input)
        {
            // update the character count in the dictionary if it already exists, otherwise add it to the dictionary with a count of 1
            if (inputCharacterCounts.ContainsKey(c))
                inputCharacterCounts[c]++;
            else
                inputCharacterCounts[c] = 1;

            // NOTE: This is likely not needed in this loop as string sorting is very fast.
            if (cancellationToken.IsCancellationRequested)
                break;
        }

        // sort the dictionary by the character count in ascending order and then by the character itself in ascending order
        // var sortedCharacters = inputCharacterCounts.OrderBy(c => c.Value).ThenBy(c => c.Key).ToList();
        var sortedCharacters = inputCharacterCounts.OrderBy(c => c.Key).ToList();

        var responseBuilder = new StringBuilder();
        foreach (var c in sortedCharacters)
        {
            responseBuilder.Append($"{c.Key}{c.Value}");
        }

        // append the rest of the response string
        responseBuilder.Append($"/{base64EncodedInput}");
        return responseBuilder.ToString();
    }
}
