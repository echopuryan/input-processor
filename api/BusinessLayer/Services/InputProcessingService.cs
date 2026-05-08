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
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Facade service for handling user input processing requests. The main processor is a BG job that handles it separately
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
        if (!_jobManager.DoesUserOwnJob(jobId, username))
        {
            _logger.LogWarning("User {Username} attempted to cancel job {JobId} but is not the owner.", username, jobId);
            return Task.FromResult(false);
        }

        return Task.FromResult(_jobManager.Cancel(jobId));
    }
    /// <summary>
    /// Returns the pending job ID for a given username, if any. This method is used to check if a user has an ongoing processing job that has not yet completed.
    /// </summary>
    /// <param name="username">The username of the user for whom to check the pending job.</param>
    /// <param name="cancellationToken">Task cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a GUID result indicating the pending job ID, if any.</returns>
    public Task<JobInfo?> GetPendingJobByUsername(string username, CancellationToken cancellationToken)
    {
        return Task.FromResult<JobInfo?>(_jobManager.GetPendingJobByUsername(username));
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
        if (!_jobManager.DoesUserOwnJob(jobId, username))
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
        var jobId = Guid.NewGuid();
        _logger.LogInformation("Starting processing for job {JobId} and user {Username}", jobId, username);
        // push the job request for the BG to process
        await _jobRequestChannel.WriteAsync(new DataProcessingRequest
        {
            Id = jobId,
            Username = username,
            UserInput = input,
        }, cancellationToken);

        return jobId;
    }
}
