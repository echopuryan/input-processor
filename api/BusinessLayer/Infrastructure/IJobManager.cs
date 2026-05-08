using BusinessLayer.Models;
using System;
using System.Threading;

namespace BusinessLayer.Infrastructure;

/// <summary>
/// Interface for managing long running jobs. This includes things like registration (for tracking), cancellation, status checking
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// Registers existing job with the manager. This is useful for tracking and cancellation of long running jobs.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="cancellationToken">Linked cancellation token.</param>
    /// <returns>The registered cancellation token.</returns>
    CancellationToken Register(Guid jobId, string owner, string requestInput, CancellationToken cancellationToken);
    /// <summary>
    /// Cancel the job with the specified id.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    /// <returns>True if the job was successfully canceled, otherwise false.</returns>
    bool Cancel(Guid jobId);

    /// <summary>
    /// Checks if the job with the specified id is currently running.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    /// <returns>True if the job is running, otherwise false.</returns>
    bool IsRunning(Guid jobId);
    /// <summary>
    /// Removes the job with the specified id from the manager. This should be called when the job is completed or canceled to prevent memory leaks and keep the manager clean.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    void Remove(Guid jobId);
    /// <summary>
    /// Return user's pending job if exists. This is useful for scenarios where we want to prevent multiple concurrent jobs for the same user, or to provide status updates on existing jobs.
    /// </summary>
    /// <param name="username">User's username</param>
    /// <returns>The pending job info if exists, otherwise null.</returns>
    JobInfo? GetPendingJobByUsername(string username);
    /// <summary>
    /// Returns whether the user is the owner of the job with the specified id. This is useful for authorization checks to ensure that users can only access or cancel their own jobs.
    /// </summary>
    /// <param name="jobId">Unique ID for the job.</param>
    /// <param name="username">User's username</param>
    /// <returns>True if the user is the owner of the job, otherwise false.</returns>
    bool DoesUserOwnJob(Guid jobId, string username);

}
