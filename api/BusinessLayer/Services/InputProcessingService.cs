using BusinessLayer.Channels;
using BusinessLayer.Infrastructure;
using BusinessLayer.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Processes the user input by sorting the characters in the input string by ascending order of their occurrence in the input string.
/// </summary>
public class InputProcessingService : IInputProcessingService
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<InputProcessingService> _logger;
    /// <summary>
    /// Channel for talking to the BG service that processes the input data and returns the processed events
    /// </summary>
    private readonly IDataProcessingChannel _dataProcessingChannel;


    /// <summary>
    /// Service to hand the processing job to the BG service
    /// </summary>
    private readonly IDataProcessingRequestChannel _jobRequestChannel;
    /// <summary>
    /// Tracks jobs and their owners
    /// </summary>
    private readonly ConcurrentDictionary<Guid, string> _jobs = new();

    /// <summary>
    /// Memory cache. Later on can become an external cache like Redis
    /// </summary>
    private readonly IMemoryCache _memoryCache;
    private MemoryCacheEntryOptions _memoryCacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60),
    };

    /// <summary>
    /// Main constructor for the InputProcessingService class that takes in a logger instance for logging purposes.
    /// </summary>
    /// <param name="logger">Logger</param>
    public InputProcessingService(
        ILogger<InputProcessingService> logger,
        IDataProcessingRequestChannel jobRequestChannel,
        IDataProcessingChannel dataProcessingChannel,
        IJobManager jobManager,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _jobRequestChannel = jobRequestChannel;
        _dataProcessingChannel = dataProcessingChannel;
        _jobManager = jobManager;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// Cancel the processing of a job with the given job ID. This method is used to allow clients to cancel long-running processing tasks.
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel.</param>
    /// <param name="username">The username of the user requesting the cancellation.</param>
    /// <param name="cancellationToken">Task cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean result indicating whether the cancellation was successful.</returns>
    public Task<bool> CancelProcessingRequest(Guid jobId, string username, CancellationToken cancellationToken)
    {
        var jobOwner = _jobs.TryGetValue(jobId, out var owner) ? owner : null;
        if (jobOwner == username)
        {
            _jobs.TryRemove(jobId, out _);
            return Task.FromResult(_jobManager.Cancel(jobId));
        }
        else
        {
            _logger.LogWarning("User {Username} attempted to cancel job {JobId} but is not the owner.", username, jobId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Return processed input events for a given job ID, starting from the last event ID. This method is used for streaming SSE events to the client.
    /// </summary>
    /// <param name="jobId">The ID of the job for which to retrieve processed input events.</param>
    /// <param name="lastEventId">The ID of the last event received by the client.</param>
    /// <param name="username">The username of the user requesting the events.</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>An asynchronous stream of processed input events.</returns>
    public async IAsyncEnumerable<ProcessedInputEvent> GetProcessedInputEventsAsync(Guid jobId, long lastEventId, string username, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var jobOwner = _jobs.TryGetValue(jobId, out var owner) ? owner : null;
        if (jobOwner != username)
        {
            _logger.LogWarning("User {Username} attempted to access events for job {JobId} but is not the owner.", username, jobId);
            yield break;
        }
        if (!_memoryCache.TryGetValue<ConcurrentQueue<ProcessedInputEvent>>(jobId, out var cachedEvents))
        {
            cachedEvents = [];
            _memoryCache.Set(jobId, cachedEvents, _memoryCacheOptions);
        }

        var missedEvents = cachedEvents?.Where(e => e.Id > lastEventId)?.ToList() ?? [];

        // replay missed events from cache
        foreach (var missedEvent in missedEvents)
        {
            yield return missedEvent;
        }

        // read new events from the channel
        await foreach (var processedEvent in _dataProcessingChannel.ReadAllAsync(jobId, cancellationToken))
        {
            // add to the cached job events
            cachedEvents?.Enqueue(processedEvent);
            // remove the job from the tracking dictionary if the processing is completed
            if (processedEvent.IsCompleted) _jobs.TryRemove(jobId, out _);
            yield return processedEvent;
        }
    }

    /// <summary>
    /// Async-ly process the user input and push events when each character is processed. This method is used for streaming SSE events to the client.
    /// </summary>
    /// <param name="input">User input to be processed</param>
    /// <param name="username">Username of a user requesting the job.</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task<Guid> StartProcessingAsync(string input, string username, CancellationToken cancellationToken)
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

        _jobs[jobId] = username;

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

        // sort the dictionary by the character in ascending order
        var sortedCharacters = inputCharacterCounts.OrderBy(c => c.Key);

        var responseBuilder = new StringBuilder();
        foreach (var c in sortedCharacters)
        {
            responseBuilder.Append(c.Key);
            responseBuilder.Append(c.Value);
        }

        // append the rest of the response string
        responseBuilder.Append("/");
        responseBuilder.Append(base64EncodedInput);
        return responseBuilder.ToString();
    }
}
