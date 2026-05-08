using System;
using System.Threading;

namespace BusinessLayer.Models;

public class JobInfo
{
    public Guid JobId { get; set; }
    /// <summary>
    /// Username of the user who owns the job. This is important for authorization purposes, to ensure that only the owner of a job can access its details or cancel it.
    /// </summary>
    public string Owner { get; set; }
    public string RequestInput { get; set; }
    public CancellationTokenSource JobCancellationToken { get; set; }
}
