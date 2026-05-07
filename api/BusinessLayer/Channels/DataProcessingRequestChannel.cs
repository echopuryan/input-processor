using BusinessLayer.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BusinessLayer.Channels;

/// <summary>
/// Channel based implementation
/// </summary>
internal class DataProcessingRequestChannel : IDataProcessingRequestChannel
{
    private readonly Channel<DataProcessingRequest> _channel;

    public DataProcessingRequestChannel()
    {
        var options = new BoundedChannelOptions(100)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<DataProcessingRequest>(options);
    }

    /// <summary>
    /// Reads all pending data processing requests from the channel. This is intended to be used by a BG job to process the user input.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous stream of data processing requests.</returns>
    public IAsyncEnumerable<DataProcessingRequest> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Queues a new job request to be processed by a BG job.
    /// </summary>
    /// <param name="request">The data processing request to be queued.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    public ValueTask WriteAsync(DataProcessingRequest request, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }
}
