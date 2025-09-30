namespace DTOs.Users
{
    public enum UserSortBy
    {
        CreatedAt = 0,
        UpdatedAt = 1,
        UserName = 2,
        Email = 3,
        FullName = 4,
        Level = 5
    }

    public sealed record UserFilter
    (
        string? Keyword,          // áp cho UserName/Email/FullName/University
        string[]? Roles,
        Gender? Gender,
        bool? EmailConfirmed,
        bool? LockedOnly,
        string? University,
        int? LevelMin,
        int? LevelMax,
        DateTime? CreatedFromUtc,
        DateTime? CreatedToUtc,
        UserSortBy SortBy = UserSortBy.CreatedAt,
        bool Desc = true
    );
}
