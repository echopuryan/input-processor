using System;

namespace BusinessLayer.Models;

public sealed class ProcessedInputEvent
{
    public Guid RequestId { get; set; }
    public long Id { get; set; }
    public bool IsCompleted { get; set; }
    public int Progress { get; set; }
    public char Data { get; set; }
    public bool IsCancelled { get; set; }
}
