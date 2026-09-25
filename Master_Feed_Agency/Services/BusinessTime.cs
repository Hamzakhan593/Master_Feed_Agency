namespace Master_Feed_Agency.Services;

/// <summary>
/// Business clock for Master Feed Agency. Financial timestamps are stored in UTC;
/// business dates are interpreted in Pakistan Standard Time.
/// </summary>
public static class BusinessTime
{
    private static readonly TimeZoneInfo Zone = ResolvePakistanZone();

    public static DateTime Today => ToLocal(DateTime.UtcNow).Date;

    public static DateTime ToLocal(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(value, Zone);
    }

    public static DateTime ToUtcStart(DateTime businessDate)
    {
        var local = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    public static DateTime ToUtcEndExclusive(DateTime businessDate)
    {
        var local = DateTime.SpecifyKind(businessDate.Date.AddDays(1), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    private static TimeZoneInfo ResolvePakistanZone()
    {
        foreach (var id in new[] { "Pakistan Standard Time", "Asia/Karachi" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Pakistan does not currently observe DST; this fallback preserves the intended UTC+05 business clock.
        return TimeZoneInfo.CreateCustomTimeZone("MasterFeed-PK", TimeSpan.FromHours(5), "Pakistan Standard Time", "Pakistan Standard Time");
    }
}
