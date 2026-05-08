using BusinessLayer.Channels;
using BusinessLayer.Infrastructure;
using BusinessLayer.Models;
using BusinessLayer.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
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
        var jobToken = _jobManager.Register(request.Id, cancellationToken);
        try
        {
            _logger.LogInformation("Processing user input job with id: {JobId}", request.Id);

            for (var index = 0; index < request.UserInput.Length; index++)
            {
                jobToken.ThrowIfCancellationRequested();
                var character = request.UserInput[index];

                var progress = request.UserInput.Length == 0 ? 100 : (index + 1) * 100 / request.UserInput.Length;
                _logger.LogInformation("Job id: {JobId}, Progress: {Progress}%, Processing character: {Character}", request.Id, progress, character);

                await _dataProcessingChannel.PublishAsync(new ProcessedInputEvent
                {
                    RequestId = request.Id,
                    Id = index,
                    Progress = progress,
                    Data = character,
                    IsCompleted = index == request.UserInput.Length - 1
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
}
