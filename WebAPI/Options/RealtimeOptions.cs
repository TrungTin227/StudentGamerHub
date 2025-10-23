namespace WebApi.Options;

public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";
    public const string DefaultChatPath = "/ws/chat";

    private string _chatPath = DefaultChatPath;

    public string ChatPath
    {
        get => _chatPath;
        set => _chatPath = NormalizePath(value);
    }

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultChatPath;
        }

        var path = value.Trim();

        if (!path.StartsWith('/'))
        {
            path = $"/{path}";
        }

        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        return path;
    }
}
