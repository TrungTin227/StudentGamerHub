namespace DTOs.Quests;

/// <summary>
/// Response ch?a t?t c? quests trong ngày hi?n t?i (Asia/Ho_Chi_Minh)
/// </summary>
public sealed record QuestTodayDto(
    int Points,
    QuestItemDto[] Quests
);

/// <summary>
/// Chi ti?t 1 quest MVP
/// </summary>
public sealed record QuestItemDto(
    string Code,
    string Title,
    int Reward,
    bool Done
);
