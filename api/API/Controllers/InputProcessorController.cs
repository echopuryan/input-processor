using API.Models;
using API.Settings;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Controllers;

/// <summary>
/// Input processing controller that handles the user input processing requests and returns the processed response to the client.
/// </summary>
[ApiController]
[Route("[controller]")]
public class InputProcessorController : ControllerBase
{
    /// <summary>
    /// Logger
    /// </summary>
    private readonly ILogger<InputProcessorController> _logger;
    /// <summary>
    /// Service for processing user inputs
    /// </summary>
    private readonly IInputProcessingService _inputProcessingService;
    /// <summary>
    /// App settings
    /// </summary>
    private readonly AppSettings _appSettings;

    /// <summary>
    /// Initializes a new instance of the InputProcessorController class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the controller. Cannot be null.</param>
    public InputProcessorController(ILogger<InputProcessorController> logger, IInputProcessingService inputProcessingService, IOptions<AppSettings> appSettings)
    {
        _logger = logger;
        _inputProcessingService = inputProcessingService;
        _appSettings = appSettings.Value;
    }

    /// <summary>
    /// Simple get request to imitates the api returning some estimation on how long the processing will take. Currently it's simply the size of the processed string.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("estimate")]
    public async Task<IActionResult> GetEstimatedProcessingTime([FromQuery] string input, CancellationToken cancellationToken)
    {
        // TODO: implement with memory cache only
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
}
