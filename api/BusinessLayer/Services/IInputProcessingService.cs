using BusinessLayer.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Interface for processing user inputs
/// </summary>
public interface IInputProcessingService
{
    /// <summary>
    /// Async-ly process the user input and push events when each character is processed. This method is used for streaming SSE events to the client.
    /// </summary>
    /// <param name="input">User input to be processed</param>
    /// <param name="username">Username of a user requesting the job.</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task<Guid> StartProcessingAsync(string input, string username, CancellationToken cancellationToken);

    /// <summary>
    /// Return processed input events for a given job ID, starting from the last event ID. This method is used for streaming SSE events to the client.
    /// </summary>
    /// <param name="jobId">The ID of the job for which to retrieve processed input events.</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>An asynchronous stream of processed input events.</returns>
    IAsyncEnumerable<ProcessedInputEvent> GetProcessedInputEventsAsync(Guid jobId, long lastEventId, string username, CancellationToken cancellationToken);

    /// <summary>
    /// Cancel the processing of a job with the given job ID. This method is used to allow clients to cancel long-running processing tasks.
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel.</param>
    /// <param name="username">The username of the user requesting the cancellation.</param>
    /// <param name="cancellationToken">Task cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean result indicating whether the cancellation was successful.</returns>
    Task<bool> CancelProcessingRequest(Guid jobId, string username, CancellationToken cancellationToken);
    /// <summary>
    /// Returns the pending job ID for a given username, if any. This method is used to check if a user has an ongoing processing job that has not yet completed.
    /// </summary>
    /// <param name="username">The username of the user for whom to check the pending job.</param>
    /// <param name="cancellationToken">Task cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a GUID result indicating the pending job ID, if any.</returns>
    Task<JobInfo?> GetPendingJobByUsername(string username, CancellationToken cancellationToken);
}
