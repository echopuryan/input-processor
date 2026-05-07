using System;
using System.Collections.Generic;
using System.Text;

namespace BusinessLayer.Models;

public sealed class DataProcessingRequest
{
    public Guid Id { get; set; }
    public string UserInput { get; set; }
}
