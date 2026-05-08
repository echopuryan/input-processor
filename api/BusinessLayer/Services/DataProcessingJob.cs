using BusinessLayer.Channels;
using BusinessLayer.Infrastructure;
using BusinessLayer.Models;
using BusinessLayer.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Long running data processing job
/// </summary>
public sealed class DataProcessingJob : IDataProcessingJob
{
    private readonly ILogger<DataProcessingJob> _logger;
    private readonly AppSettings _appSettings;
    private readonly IDataProcessingChannel _dataProcessingChannel;
    private readonly IJobManager _jobManager;

    public DataProcessingJob(
        ILogger<DataProcessingJob> logger,
        IOptions<AppSettings> appSettings,
        IDataProcessingChannel dataProcessingChannel,
        IJobManager jobManager)
    {
        _logger = logger;
        _appSettings = appSettings.Value;
        _dataProcessingChannel = dataProcessingChannel;
        _jobManager = jobManager;
    }

    /// <summary>
    /// Process the data based on the provided request. This method is expected to be long-running and should support cancellation via the provided CancellationToken.
    /// </summary>
    /// <param name="processRequest">The data processing request.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessAsync(DataProcessingRequest request, CancellationToken cancellationToken)
    {
        var jobToken = _jobManager.Register(request.Id, request.Username, request.UserInput, cancellationToken);
        try
        {
            _logger.LogInformation("Processing user input job with id: {JobId}", request.Id);
            var dataToProcess = GetProcessedString(request.UserInput, jobToken);

            for (var index = 0; index < dataToProcess.Length; index++)
            {
                jobToken.ThrowIfCancellationRequested();
                var character = dataToProcess[index];

                var progress = dataToProcess.Length == 0 ? 100 : (index + 1) * 100 / dataToProcess.Length;
                _logger.LogInformation("Job id: {JobId}, Progress: {Progress}%, Processing character: {Character}", request.Id, progress, character);

                await _dataProcessingChannel.PublishAsync(new ProcessedInputEvent
                {
                    RequestId = request.Id,
                    Id = index,
                    Progress = progress,
                    Data = character,
                    IsCompleted = index == dataToProcess.Length - 1
                }, cancellationToken);

                #region SIMULATE_RANDOM_DELAY
                var randomDelay = new Random().Next(_appSettings.RandomDelayRange.Min, _appSettings.RandomDelayRange.Max);
                _logger.LogDebug("Waiting for {RandomDelay}ms before processing character at index {Index} for job id: {JobId}", randomDelay, index, request.Id);
                await Task.Delay(randomDelay, cancellationToken); // Simulate random delay between characters
                #endregion
            }
            // simulate some processing delay
            _logger.LogInformation("Finished processing user input job with id: {JobId}", request.Id);

        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job {JobId} was cancelled by user.", request.Id);
            await _dataProcessingChannel.PublishAsync(new ProcessedInputEvent
            {
                RequestId = request.Id,
                Id = -1,
                Progress = -1,
                Data = default,
                IsCompleted = true,
                IsCancelled = true
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job {JobId} was cancelled due to app shutdown.", request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing user input job with id: {JobId}", request.Id);

            // TODO: notify through the channel

        }
        finally
        {
            _jobManager.Remove(request.Id);
        }
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
