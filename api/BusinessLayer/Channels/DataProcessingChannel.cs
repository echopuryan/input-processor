using BusinessLayer.Models;
using BusinessLayer.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BusinessLayer.Channels;

/// <summary>
/// Channel backed implementation of the IDataProcessingChannel interface.
/// </summary>
public sealed class DataProcessingChannel : IDataProcessingChannel
{
    private readonly ConcurrentDictionary<Guid, Channel<ProcessedInputEvent>> _channels = new();
    private readonly ILogger<DataProcessingChannel> _logger;
    private readonly AppSettings _appSettings;

    public DataProcessingChannel(ILogger<DataProcessingChannel> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _appSettings = options.Value;
    }

    /// <summary>
    /// Publish the processed event to the channel. The channel will then be responsible for communicating it to the user, for example via SignalR or WebSockets.
    /// </summary>
    /// <param name="processedEvent">The processed input event to be published.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask PublishAsync(ProcessedInputEvent processedEvent, CancellationToken cancellationToken)
    {
        var channel = _channels.GetOrAdd(processedEvent.RequestId, _ =>
        {
            var options= new BoundedChannelOptions(_appSettings.MaxProcessingChannelSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
             };

            return Channel.CreateBounded<ProcessedInputEvent>(options);
        });
        
        await channel.Writer.WriteAsync(processedEvent, cancellationToken);

        if (processedEvent.IsCompleted)
        {
            _logger.LogInformation("Processing of job {JobId} completed. Closing channel.", processedEvent.RequestId);
            channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Reads all events from the channel for a specific job. This can be used by the client to subscribe to the channel and receive updates about the progress and results of the processing.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job for which to read events.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous stream of processed input events.</returns>
    public async IAsyncEnumerable<ProcessedInputEvent> ReadAllAsync(Guid jobId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_channels.TryGetValue(jobId, out var channel))
        {
            _logger.LogWarning("[PROCESSING CHANNEL] No channel found for job {JobId}. Returning empty event stream.", jobId);
            yield break;
        }

        await foreach (var processedEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return processedEvent;

            if (processedEvent.IsCompleted)
            {
                _logger.LogDebug("[PROCESSING CHANNEL] Received completion event for job {JobId}. Removing channel.", jobId);
                _channels.TryRemove(jobId, out _);
                yield break;
            }
        }
    }
}
