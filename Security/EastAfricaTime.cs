namespace AMRSurveillance.Security;

public static class EastAfricaTime
{
    private static readonly TimeZoneInfo s_eatZone = ResolveEatTimeZone();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, s_eatZone);

    public static DateTime Today => Now.Date;

    public static DateTime ToEat(DateTime dateTime) =>
        dateTime.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(dateTime, s_eatZone)
            : dateTime;

    private static TimeZoneInfo ResolveEatTimeZone()
    {
        try
        {
            // Windows / IIS identifier
            return TimeZoneInfo.FindSystemTimeZoneById("E. Africa Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux / macOS IANA identifier
                return TimeZoneInfo.FindSystemTimeZoneById("Africa/Nairobi");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "EAT",
                    TimeSpan.FromHours(3),
                    "East Africa Time",
                    "East Africa Time");
            }
        }
    }
}