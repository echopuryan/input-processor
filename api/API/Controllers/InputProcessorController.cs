using API.Models;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Mvc;

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
    /// Initializes a new instance of the InputProcessorController class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the controller. Cannot be null.</param>
    public InputProcessorController(ILogger<InputProcessorController> logger, IInputProcessingService inputProcessingService)
    {
        _logger = logger;
        _inputProcessingService = inputProcessingService;
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

        foreach(var character in processedInput)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Request cancelled by the client.");
                break;
            }

            _logger.LogDebug("Streaming character: {Character}", character);
            await Response.WriteAsync(character.ToString());
            await Response.Body.FlushAsync();
            await Task.Delay(new Random().Next(1000, 5000), cancellationToken); // Simulate random delay between characters
        }

        // NOTE: Remove this, might not be needed
        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
