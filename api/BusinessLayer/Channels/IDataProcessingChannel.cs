using BusinessLayer.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Channels;

/// <summary>
/// Way for the job or service that processes the user input to communicate the progress and results
/// </summary>
public interface IDataProcessingChannel
{
    /// <summary>
    /// Publish the processed event to the channel. The channel will then be responsible for communicating it to the user, for example via SignalR or WebSockets.
    /// </summary>
    /// <param name="processedEvent">The processed input event to be published.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask PublishAsync(ProcessedInputEvent processedEvent, CancellationToken cancellationToken);
    /// <summary>
    /// Reads all events from the channel for a specific job. This can be used by the client to subscribe to the channel and receive updates about the progress and results of the processing.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job for which to read events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous stream of processed input events.</returns>
    IAsyncEnumerable<ProcessedInputEvent> ReadAllAsync(Guid jobId, CancellationToken cancellationToken);
}
