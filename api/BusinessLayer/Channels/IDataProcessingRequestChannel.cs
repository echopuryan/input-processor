using BusinessLayer.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Channels;

/// <summary>
/// Service to handle publishing and subscribing to data processing requests. Used to allow for a BG job to process the user input.
/// </summary>
public interface IDataProcessingRequestChannel
{
    /// <summary>
    /// Queues a new job request to be processed by a BG job.
    /// </summary>
    /// <param name="request">The data processing request to be queued.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask WriteAsync(DataProcessingRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Reads all pending data processing requests from the channel. This is intended to be used by a BG job to process the user input.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous stream of data processing requests.</returns>
    IAsyncEnumerable<DataProcessingRequest> ReadAllAsync(CancellationToken cancellationToken);
}
