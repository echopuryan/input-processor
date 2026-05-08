using API.Models;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<InputProcessorController> _logger;

    /// <summary>
    /// Initializes a new instance of the InputProcessorController class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the controller. Cannot be null.</param>
    public InputProcessorController(
        ILogger<InputProcessorController> logger,
        IInputProcessingService inputProcessingService)
    {
        _logger = logger;
        _inputProcessingService = inputProcessingService;
    }

    [Authorize]
    [HttpPost("process")]
    public async Task<IActionResult> ProcessInput([FromBody] UserInputProcessingModel input, CancellationToken cancellationToken)
    {
        var currentUser = User.Identity!.Name!;
        var jobId = await _inputProcessingService.StartProcessingAsync(input.UserInput, currentUser, cancellationToken);
        _logger.LogInformation("User {Username} started processing job with id: {JobId}", currentUser, jobId);
        return Accepted(new ProcessInputResponse(jobId));
    }

    [Authorize]
    [HttpGet("{id:guid}/stream")]
    public async Task<ServerSentEventsResult<ProcessedInputEvent>> SubscribeToProcessedInputEvents(Guid id, CancellationToken cancellationToken)
    {
        var lastEventId = GetLastEventIdFromRequest();

        return TypedResults.ServerSentEvents(StreamEvents(lastEventId, id, cancellationToken));
    }

    [Authorize]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var cancelled = await _inputProcessingService.CancelProcessingRequest(id, User.Identity!.Name!, cancellationToken);

        return cancelled
            ? Ok(new CancelResponse($"Job {id} cancellation requested."))
            : NotFound(new CancelResponse($"Job {id} not found or already completed."));
    }

    /// <summary>
    /// Helper method to stream events as they appear in the event store.
    /// </summary>
    /// <param name="lastEventId">Where to start from</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    /// <returns>An asynchronous stream of SSE items representing processed input events.</returns>
    private async IAsyncEnumerable<SseItem<ProcessedInputEvent>> StreamEvents(long lastEventId, Guid jobId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // continue from the event store for new events and update cache
        await foreach (var processedEvent in _inputProcessingService.GetProcessedInputEventsAsync(jobId, lastEventId, User.Identity!.Name!, cancellationToken))
        {
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

    public sealed record ProcessInputResponse(Guid JobId);
    public sealed record CancelResponse(string Message);
}
