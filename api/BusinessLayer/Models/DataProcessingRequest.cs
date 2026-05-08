using System;

namespace BusinessLayer.Models;

public sealed class DataProcessingRequest
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string UserInput { get; set; }
}
