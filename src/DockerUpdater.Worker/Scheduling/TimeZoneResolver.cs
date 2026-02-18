namespace DockerUpdater.Worker.Scheduling;

public static class TimeZoneResolver
{
    public static TimeZoneInfo Resolve(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
