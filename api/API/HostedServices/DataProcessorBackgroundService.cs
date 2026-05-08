using BusinessLayer.Channels;
using BusinessLayer.Services;
using BusinessLayer.Settings;
using Microsoft.Extensions.Options;

namespace API.HostedServices;

/// <summary>
/// Background service to process user input processing jobs.
/// </summary>
public class DataProcessorBackgroundService : BackgroundService
{
    private readonly IDataProcessingRequestChannel _channel;
    private readonly ILogger<DataProcessorBackgroundService> _logger;
    private readonly AppSettings _appSettings;
    private readonly SemaphoreSlim _semaphore;
    private readonly IDataProcessingJob _dataProcessingJob;

    public DataProcessorBackgroundService(
        IDataProcessingRequestChannel channel,
        ILogger<DataProcessorBackgroundService> logger,
        IOptions<AppSettings> options,
        IDataProcessingJob dataProcessingJob)
    {
        _channel = channel;
        _logger = logger;
        _appSettings = options.Value;
        _semaphore = new SemaphoreSlim(_appSettings.MaxConcurrentJobs);
        _dataProcessingJob = dataProcessingJob;
    }

    /// <summary>
    /// Executes the background job processing loop
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"User input processing job service is starting.");
        var runningTasks = new List<Task>();

        await foreach (var request in _channel.ReadAllAsync(stoppingToken))
        {
            // wait for available slot to process the job
            await _semaphore.WaitAsync(stoppingToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    await _dataProcessingJob.ProcessAsync(request, stoppingToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }, stoppingToken);

            runningTasks.Add(task);

            // Clean up completed tasks periodically
            runningTasks.RemoveAll(t => t.IsCompleted);
        }
        _logger.LogInformation($"User input processing job service stopped.");
    }
}