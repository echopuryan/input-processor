using BusinessLayer.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace BusinessLayer.Infrastructure;

/// <summary>
/// Job manager backed by a ConcurrentDictionary of CancellationTokenSources. This allows for tracking and cancellation of long running jobs based on their unique identifiers.
/// </summary>
public sealed class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<Guid, JobInfo> _jobs = new();
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
        if (_jobs.TryGetValue(jobId, out var jobInfo))
        {
            jobInfo.JobCancellationToken.Cancel();
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
        if (_jobs.TryRemove(jobId, out var jobInfo))
        {
            jobInfo.JobCancellationToken.Dispose();
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
    public CancellationToken Register(Guid jobId, string owner, string requestInput, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!_jobs.TryAdd(jobId, new JobInfo { JobId = jobId, Owner = owner, RequestInput = requestInput, JobCancellationToken = cts }))
        {
            _logger.LogWarning("Job with {idJobId} is already registered. Returning existing cancellation token.", jobId);
            return _jobs[jobId].JobCancellationToken.Token;
        }
        return cts.Token;
    }

    /// <summary>
    /// Return user's pending job if exists. This is useful for scenarios where we want to prevent multiple concurrent jobs for the same user, or to provide status updates on existing jobs.
    /// </summary>
    /// <param name="username">User's username</param>
    /// <returns>The pending job info if exists, otherwise null.</returns>
    public JobInfo? GetPendingJobByUsername(string username)
    {
        var jobInfo = _jobs.Where(v => v.Value?.Owner == username).Select(kv => kv.Value).FirstOrDefault();
        return jobInfo;
    }
    /// <summary>
    /// Returns whether the user is the owner of the job with the specified id. This is useful for authorization checks to ensure that users can only access or cancel their own jobs.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    /// <param name="username">User's username</param>
    /// <returns>True if the user is the owner of the job, otherwise false.</returns>
    public bool DoesUserOwnJob(Guid jobId, string username)
    {
        var jobInfo = _jobs.TryGetValue(jobId, out var info) ? info : null;
        return jobInfo?.Owner == username;
    }
}