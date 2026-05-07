using BusinessLayer.Channels;
using BusinessLayer.Models;
using Common.Settings;
using Microsoft.Extensions.Options;

namespace API.HostedServices;

/// <summary>
/// Background service to process user input processing jobs.
/// </summary>
public class DataProcessorJobService : BackgroundService
{
    private readonly IDataProcessingRequestChannel _channel;
    private readonly ILogger<DataProcessorJobService> _logger;
    private readonly AppSettings _appSettings;
    private readonly IDataProcessingChannel _dataProcessingChannel;

    public DataProcessorJobService(
        IDataProcessingRequestChannel channel,
        ILogger<DataProcessorJobService> logger,
        IOptions<AppSettings> options,
        IDataProcessingChannel dataProcessingChannel)
    {
        _channel = channel;
        _logger = logger;
        _dataProcessingChannel = dataProcessingChannel;
        _appSettings = options.Value;
    }

    /// <summary>
    /// Executes the background job processing loop
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"User input processing job service is starting.");
        await foreach (var request in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(request, stoppingToken);
        }
        _logger.LogInformation($"User input processing job service stopped.");
    }

    private async Task ProcessAsync(DataProcessingRequest request, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Processing user input job with id: {JobId}", request.Id);

            for (var index = 0; index < request.UserInput.Length; index++)
            {
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
                }, stoppingToken);

                #region SIMULATE_RANDOM_DELAY
                var randomDelay = new Random().Next(_appSettings.RandomDelayRange.Min, _appSettings.RandomDelayRange.Max);
                _logger.LogDebug("Waiting for {RandomDelay}ms before processing character at index {Index} for job id: {JobId}", randomDelay, index, request.Id);
                await Task.Delay(randomDelay, stoppingToken); // Simulate random delay between characters
                #endregion
            }
            // simulate some processing delay
            _logger.LogInformation("Finished processing user input job with id: {JobId}", request.Id);

        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Processing of user input job with id: {JobId} was cancelled.", request.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing user input job with id: {JobId}", request.Id);

            // TODO: notify through the channel
            throw;
        }

    }
}