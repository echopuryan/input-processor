using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Interface for processing user inputs
/// </summary>
public interface IInputProcessingService
{
    /// <summary>
    /// Processes the user input and returns a response
    /// </summary>
    /// <param name="input">User input ot be processed</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>Processed response text</returns>
    Task<string> ProcessInputAsync(string input, CancellationToken cancellationToken);
}
