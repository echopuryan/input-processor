using API.Models;
using BusinessLayer.Channels;
using BusinessLayer.Infrastructure;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Common.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace API.Controllers;

/// <summary>
/// Input processing controller that handles the user input processing requests and returns the processed response to the client.
/// </summary>
[ApiController]
[Route("[controller]")]
public class InputProcessorController : ControllerBase
{
    /// <summary>
    /// Service for processing user inputs
    /// </summary>
    private readonly IInputProcessingService _inputProcessingService;
    private readonly AppSettings _appSettings;
    private readonly IDataProcessingChannel _dataProcessingChannel;
    private readonly ILogger<InputProcessorController> _logger;
    private readonly IJobManager _jobManager;
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
    /// Initializes a new instance of the InputProcessorController class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the controller. Cannot be null.</param>
    public InputProcessorController(
        ILogger<InputProcessorController> logger,
        IInputProcessingService inputProcessingService,
        IOptions<AppSettings> appSettings,
        IDataProcessingChannel dataProcessingChannel,
        IJobManager jobManager,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _inputProcessingService = inputProcessingService;
        _appSettings = appSettings.Value;
        _dataProcessingChannel = dataProcessingChannel;
        _jobManager = jobManager;
        _memoryCache = memoryCache;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessInput([FromBody] UserInputProcessingModel input, CancellationToken cancellationToken)
    {
        var jobId = await _inputProcessingService.StartProcessingAsync(input.UserInput, cancellationToken);
        _logger.LogInformation("Started processing job with id: {JobId}", jobId);
        return Accepted(new { JobId = jobId });
    }

    [HttpGet("{id:guid}/stream")]
    public async Task<ServerSentEventsResult<ProcessedInputEvent>> SubscribeToProcessedInputEvents(Guid id, CancellationToken cancellationToken)
    {
        var lastEventId = GetLastEventIdFromRequest();

        return TypedResults.ServerSentEvents(StreamEvents(lastEventId, id, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public IActionResult Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!_jobManager.Cancel(id))
        {
            return NotFound(new { Message = $"Job with id {id} not found or already completed." });
        }
        var cancelled = _jobManager.Cancel(id);

        return cancelled
            ? Ok(new { Message = $"Job {id} cancellation requested." })
            : NotFound(new { Message = $"Job {id} not found or already completed." });
    }

    /// <summary>
    /// Helper method to stream events as they appear in the event store.
    /// </summary>
    /// <param name="lastEventId">Where to start from</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    /// <returns>An asynchronous stream of SSE items representing processed input events.</returns>
    private async IAsyncEnumerable<SseItem<ProcessedInputEvent>> StreamEvents(long lastEventId, Guid jobId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_memoryCache.TryGetValue<ConcurrentBag<ProcessedInputEvent>>(jobId, out var cachedEvents))
        {
            cachedEvents = [];
            _memoryCache.Set(jobId, cachedEvents, _memoryCacheOptions);
        }

        var missedEvents = cachedEvents?.Where(e => e.Id > lastEventId)?.ToList() ?? [];

        // replay missed events from cache
        foreach (var missedEvent in missedEvents)
        {
            var sseItem = new SseItem<ProcessedInputEvent>(missedEvent) { EventId = missedEvent.Id.ToString(), ReconnectionInterval = TimeSpan.FromSeconds(5) };
            yield return sseItem;
        }
        // continue from the event store for new events and update cache
        await foreach (var processedEvent in _dataProcessingChannel.ReadAllAsync(jobId, cancellationToken))
        {
            // add to the cached job events
            cachedEvents?.Add(processedEvent);
            yield return new SseItem<ProcessedInputEvent>(processedEvent) { EventId = processedEvent.Id.ToString(), ReconnectionInterval = TimeSpan.FromSeconds(5) };
        }
    }

    /// <summary>
    /// Helper method to extract the last event ID from SSE request headers.
    /// </summary>
    /// <returns></returns>
    private long GetLastEventIdFromRequest()
    {
        if (Request.Headers.TryGetValue("Last-Event-ID", out var lastEventIdHeader) && long.TryParse(lastEventIdHeader, out var lastEventId))
        {
            return lastEventId;
        }
        return -1; // Default to -1 if header is missing or invalid
    }
}
