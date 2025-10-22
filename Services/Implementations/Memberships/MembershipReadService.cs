using Microsoft.EntityFrameworkCore;
using Services.DTOs.Memberships;

namespace Services.Implementations.Memberships;

public sealed class MembershipReadService : IMembershipReadService
{
    private readonly AppDbContext _db;

    public MembershipReadService(AppDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<MembershipTreeHybridDto>> GetMyMembershipTreeAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Result<MembershipTreeHybridDto>.Failure(
                new Error(Error.Codes.Validation, "UserId is required."));
        }

        var communityRows = await _db.CommunityMembers
            .AsNoTracking()
            .Where(cm => cm.UserId == userId)
            .Select(cm => new CommunityMembershipRow(cm.CommunityId, cm.Community!.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var clubRows = await _db.ClubMembers
            .AsNoTracking()
            .Where(cm => cm.UserId == userId)
            .Select(cm => new ClubMembershipRow(
                cm.ClubId,
                cm.Club!.Name,
                cm.Club!.CommunityId,
                cm.Club!.Community!.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var roomRows = await _db.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.UserId == userId && rm.Status == RoomMemberStatus.Approved)
            .Select(rm => new RoomMembershipRow(
                rm.RoomId,
                rm.Room!.Name,
                rm.Room!.ClubId,
                rm.Room!.Club!.Name,
                rm.Room!.Club!.CommunityId,
                rm.Room!.Club!.Community!.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var communityNames = new Dictionary<Guid, string?>();
        foreach (var row in communityRows)
        {
            UpdateCommunityName(communityNames, row.CommunityId, row.CommunityName);
        }

        var clubMap = new Dictionary<Guid, ClubInfo>();
        foreach (var row in clubRows)
        {
            UpdateCommunityName(communityNames, row.CommunityId, row.CommunityName);
            clubMap[row.ClubId] = new ClubInfo(row.ClubId, row.ClubName, row.CommunityId);
        }

        foreach (var row in roomRows)
        {
            UpdateCommunityName(communityNames, row.CommunityId, row.CommunityName);
            if (!clubMap.ContainsKey(row.ClubId))
            {
                clubMap[row.ClubId] = new ClubInfo(row.ClubId, row.ClubName, row.CommunityId);
            }
        }

        var roomsByClub = roomRows
            .GroupBy(r => r.ClubId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(r => r.RoomName ?? string.Empty)
                    .ThenBy(r => r.RoomId)
                    .Select(r => new MembershipRoomProjection(r.RoomId, r.RoomName))
                    .ToList() as IReadOnlyList<MembershipRoomProjection>);

        var clubsByCommunity = clubMap.Values
            .GroupBy(c => c.CommunityId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(c => c.ClubName ?? string.Empty)
                    .ThenBy(c => c.ClubId)
                    .Select(c =>
                    {
                        var rooms = roomsByClub.TryGetValue(c.ClubId, out var roomList)
                            ? roomList
                            : Array.Empty<MembershipRoomProjection>();
                        return new MembershipClubProjection(c.ClubId, c.ClubName, rooms);
                    })
                    .ToList() as IReadOnlyList<MembershipClubProjection>);

        var communities = communityNames
            .Select(kvp =>
            {
                var clubs = clubsByCommunity.TryGetValue(kvp.Key, out var communityClubs)
                    ? communityClubs
                    : Array.Empty<MembershipClubProjection>();
                return new MembershipCommunityProjection(kvp.Key, kvp.Value, clubs);
            })
            .OrderBy(c => c.CommunityName ?? string.Empty)
            .ThenBy(c => c.CommunityId)
            .ToList();

        var overview = new MembershipOverviewProjection(
            communities.Count,
            clubMap.Count,
            roomRows
                .Select(r => r.RoomId)
                .Distinct()
                .Count());

        var projection = new MembershipTreeProjection(communities, overview);
        var dto = projection.ToHybridDto();

        return Result<MembershipTreeHybridDto>.Success(dto);
    }

    private static void UpdateCommunityName(IDictionary<Guid, string?> map, Guid id, string? name)
    {
        if (map.TryGetValue(id, out var existing))
        {
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return;
            }
        }

        map[id] = name;
    }

    private sealed record CommunityMembershipRow(Guid CommunityId, string? CommunityName);

    private sealed record ClubMembershipRow(
        Guid ClubId,
        string? ClubName,
        Guid CommunityId,
        string? CommunityName);

    private sealed record RoomMembershipRow(
        Guid RoomId,
        string? RoomName,
        Guid ClubId,
        string? ClubName,
        Guid CommunityId,
        string? CommunityName);

    private sealed record ClubInfo(Guid ClubId, string? ClubName, Guid CommunityId);
}
