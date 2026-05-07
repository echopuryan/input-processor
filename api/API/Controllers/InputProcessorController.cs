using API.Models;
using BusinessLayer.Channels;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Common.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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

    /// <summary>
    /// Initializes a new instance of the InputProcessorController class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the controller. Cannot be null.</param>
    public InputProcessorController(
        ILogger<InputProcessorController> logger,
        IInputProcessingService inputProcessingService,
        IOptions<AppSettings> appSettings,
        IDataProcessingChannel dataProcessingChannel)
    {
        _logger = logger;
        _inputProcessingService = inputProcessingService;
        _appSettings = appSettings.Value;
        _dataProcessingChannel = dataProcessingChannel;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessInput([FromBody] UserInputProcessingModel input, CancellationToken cancellationToken)
    {
        var jobId = await _inputProcessingService.StartProcessingAsync(input.UserInput, cancellationToken);
        _logger.LogInformation("Started processing job with id: {JobId}", jobId);
        return Accepted(new { JobId = jobId });
    }

    [HttpGet("{id}/stream")]
    public async Task<ServerSentEventsResult<ProcessedInputEvent>> SubscribeToProcessedInputEvents(string id, CancellationToken cancellationToken)
    {
        var lastEventId = GetLastEventIdFromRequest();

        return TypedResults.ServerSentEvents(StreamEvents(lastEventId, Guid.Parse(id), cancellationToken));
    }

    /// <summary>
    /// Helper method to stream events as they appear in the event store.
    /// </summary>
    /// <param name="lastEventId">Where to start from</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    /// <returns>An asynchronous stream of SSE items representing processed input events.</returns>
    private async IAsyncEnumerable<SseItem<ProcessedInputEvent>> StreamEvents(long lastEventId, Guid jobId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var processedEvent in _dataProcessingChannel.ReadAllAsync(jobId, cancellationToken))
        {
            yield return new SseItem<ProcessedInputEvent>(processedEvent) { EventId = processedEvent.Id.ToString(), ReconnectionInterval = TimeSpan.FromSeconds(5) };
        }
        #region OLD code
        //var eventStream = _eventStore.Subscribe(cancellationToken);
        //var missedEvents = await _eventStore.GetEventsAfter(lastEventId);

        //// replay missed events
        //foreach (var missedEvent in missedEvents)
        //{
        //    yield return new SseItem<ProcessedInputEvent>() { EventId = missedEvent.Id };
        //}
        //// stream new events
        //await foreach (var newEvent in eventStream.ReadAllAsync(cancellationToken))
        //{
        //    yield return new SseItem<ProcessedInputEvent>() { EventId = newEvent.Id };
        //}
        #endregion
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

    #region OLD ENDPOINTS - to be removed or updated
    /// <summary>
    /// Simple get request to imitates the api returning some estimation on how long the processing will take. Currently it's simply the size of the processed string.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("estimate")]
    public async Task<IActionResult> GetEstimatedProcessingTime([FromQuery] string input, CancellationToken cancellationToken)
    {
        var processedInput = await _inputProcessingService.ProcessInputAsync(input, cancellationToken);

        return new JsonResult(new ProcessingEstimateModel { Size = processedInput.Length });
    }

    /// <summary>
    /// Process the user input and stream the processed response back one character at a time with a random delay between each character to simulate a real-time streaming response. 
    /// The client can cancel the request at any time using the cancellation token.
    /// </summary>
    /// <param name="input">The user input to be processed.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    /// <returns></returns>
    [HttpPost(Name = "ProcessInput")]
    public async Task StreamProcessedInput([FromBody] UserInputProcessingModel input, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var processedInput = await _inputProcessingService.ProcessInputAsync(input.UserInput, cancellationToken);
        _logger.LogInformation("Processed input: '{ProcessedInput}'", processedInput);

        try
        {
            foreach (var character in processedInput)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Request cancelled by the client.");
                    break;
                }

                _logger.LogDebug("Streaming character: {Character}", character);
                await Response.WriteAsync(character.ToString(), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(new Random().Next(_appSettings.RandomDelayRange.Min, _appSettings.RandomDelayRange.Max), cancellationToken); // Simulate random delay between characters
            }
        }
        catch (OperationCanceledException)
        {
            // here to simply log or do something else if needed
            _logger.LogInformation("Request cancelled by the client.");
        }
    }
    #endregion
}
