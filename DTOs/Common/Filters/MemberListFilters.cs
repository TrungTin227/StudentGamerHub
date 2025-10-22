namespace DTOs.Common.Filters;

/// <summary>
/// Filtering options for listing community/club members.
/// </summary>
public sealed class MemberListFilter
{
    /// <summary>
    /// Filter members by role. Null keeps all roles.
    /// </summary>
    public MemberRole? Role { get; set; }

    /// <summary>
    /// Free-text search over user full name and username.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Sort key. Supported values: joinedat_desc (default), name_asc, name_desc, role.
    /// </summary>
    public string Sort { get; set; } = MemberListSort.JoinedAtDesc;

    public void Normalize()
    {
        Query = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
        Sort = MemberListSort.Normalize(Sort);
    }
}

/// <summary>
/// Filtering options for listing room members.
/// </summary>
public sealed class RoomMemberListFilter
{
    public RoomRole? Role { get; set; }

    public RoomMemberStatus? Status { get; set; }

    public string? Query { get; set; }

    public string Sort { get; set; } = MemberListSort.JoinedAtDesc;

    public void Normalize()
    {
        Query = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
        Sort = MemberListSort.Normalize(Sort);
    }
}

/// <summary>
/// Supported sort constants for member directory listings.
/// </summary>
public static class MemberListSort
{
    public const string JoinedAtDesc = "joinedat_desc";
    public const string NameAsc = "name_asc";
    public const string NameDesc = "name_desc";
    public const string Role = "role";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return JoinedAtDesc;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            NameAsc => NameAsc,
            NameDesc => NameDesc,
            Role => Role,
            _ => JoinedAtDesc
        };
    }
}
