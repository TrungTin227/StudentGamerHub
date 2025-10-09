namespace DTOs.Dashboard;

/// <summary>
/// User activity metrics for Dashboard
/// </summary>
public sealed record ActivityDto(
    int OnlineFriends,
    int QuestsDoneLast60m
);
