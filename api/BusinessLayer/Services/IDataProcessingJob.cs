using BusinessLayer.Models;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Long running data processing job
/// </summary>
public interface IDataProcessingJob
{
    /// <summary>
    /// Process the data based on the provided request. This method is expected to be long-running and should support cancellation via the provided CancellationToken.
    /// </summary>
    /// <param name="processRequest">The data processing request.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessAsync(DataProcessingRequest processRequest, CancellationToken cancellationToken);
}
