namespace Services.Common.Abstractions
{
    public interface ITimeZoneService : ISingletonService
    {
        DateTime ToVn(DateTime utc);
        DateTimeOffset ToVn(DateTimeOffset utc);
    }
}
