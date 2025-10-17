namespace BusinessObjects.Common.Results
{
    public sealed record Error(string Code, string Message)
    {
        public static readonly Error None = new("", "");

        public static class Codes
        {
            public const string Cancelled = "cancelled";
            public const string Validation = "validation_error";
            public const string NotFound = "not_found";
            public const string Conflict = "conflict";
            public const string Forbidden = "forbidden";
            public const string Unauthorized = "unauthorized";
            public const string Unexpected = "unexpected_error";
        }
    }
}
