using System;
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
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task<Guid> StartProcessingAsync(string input, CancellationToken cancellationToken);
}
