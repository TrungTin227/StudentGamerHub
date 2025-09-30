namespace BusinessObjects;

public sealed class NotificationPrefs
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // “Thông báo cho tôi về”
    public bool AllNewMessages { get; set; } = true;
    public bool OnlyMentions { get; set; } = false;
    public bool None { get; set; } = false;

    // “Khi app mở”
    public bool ShowEmojiBadge { get; set; } = true;
    public bool ShowPreviewText { get; set; } = false;
    public bool MuteWhenInCall { get; set; } = false;

    public User? User { get; set; }
}
