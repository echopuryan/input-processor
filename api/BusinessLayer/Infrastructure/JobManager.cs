using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace BusinessLayer.Infrastructure;

/// <summary>
/// Job manager backed by a ConcurrentDictionary of CancellationTokenSources. This allows for tracking and cancellation of long running jobs based on their unique identifiers.
/// </summary>
public sealed class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobs = new();
    private readonly ILogger<JobManager> _logger;

    public JobManager(ILogger<JobManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Cancel the job with the specified id.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    /// <returns>True if the job was successfully canceled, otherwise false.</returns>
    public bool Cancel(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes the job with the specified id from the manager. This should be called when the job is completed or canceled to prevent memory leaks and keep the manager clean.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    public void Remove(Guid jobId)
    {
        if (_jobs.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }

    /// <summary>
    /// Checks if the job with the specified id is currently running.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    /// <returns>True if the job is running, otherwise false.</returns>
    public bool IsRunning(Guid jobId)
    {
        return _jobs.ContainsKey(jobId);
    }

    /// <summary>
    /// Registers existing job with the manager. This is useful for tracking and cancellation of long running jobs.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="cancellationToken">Linked cancellation token.</param>
    /// <returns>The registered cancellation token.</returns>
    public CancellationToken Register(Guid jobId, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!_jobs.TryAdd(jobId, cts))
        {
            _logger.LogWarning("Job with {idJobId} is already registered. Returning existing cancellation token.", jobId);
            return _jobs[jobId].Token;
        }
        return cts.Token;
    }
}