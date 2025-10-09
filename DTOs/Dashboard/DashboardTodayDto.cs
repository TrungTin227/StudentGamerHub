using DTOs.Quests;

namespace DTOs.Dashboard;

/// <summary>
/// Dashboard Today response DTO aggregating points, quests, events, and activity
/// </summary>
public sealed record DashboardTodayDto(
    int Points,
    QuestTodayDto Quests,
    EventBriefDto[] EventsToday,
    ActivityDto Activity
);
