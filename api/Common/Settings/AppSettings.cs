namespace Common.Settings;

public class AppSettings
{
    public RandomDelayRangeSettings RandomDelayRange { get; set; }
    public int MaxConcurrentJobs { get; set; }
}

public class RandomDelayRangeSettings
{
    public int Min { get; set; }
    public int Max { get; set; }
}
