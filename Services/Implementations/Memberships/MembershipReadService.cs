using DTOs.Memberships;
using Microsoft.EntityFrameworkCore;
using Services.Common.Mapping;
using Services.DTOs.Memberships;

namespace Services.Implementations.Memberships;

public sealed class MembershipReadService : IMembershipReadService
{
    private readonly AppDbContext _db;
    private readonly IUserMembershipRepository _userMembershipRepository;

    public MembershipReadService(AppDbContext db, IUserMembershipRepository userMembershipRepository)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _userMembershipRepository = userMembershipRepository ?? throw new ArgumentNullException(nameof(userMembershipRepository));
    }

    public async Task<Result<UserMembershipInfoDto?>> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Result<UserMembershipInfoDto?>.Failure(
                new Error(Error.Codes.Validation, "UserId is required."));
        }

        var utcNow = DateTime.UtcNow;
        var membership = await _userMembershipRepository
            .GetActiveAsync(userId, utcNow, ct)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return Result<UserMembershipInfoDto?>.Success(null);
        }

        var dto = membership.ToInfoDto(utcNow);
        return Result<UserMembershipInfoDto?>.Success(dto);
    }

    public async Task<Result<ClubRoomTreeHybridDto>> GetMyClubRoomTreeAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Result<ClubRoomTreeHybridDto>.Failure(
                new Error(Error.Codes.Validation, "UserId is required."));
        }

        var clubRows = await _db.ClubMembers
            .AsNoTracking()
            .Where(cm => cm.UserId == userId)
            .Select(cm => new ClubMembershipRow(cm.ClubId, cm.Club!.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var roomRows = await _db.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.UserId == userId && rm.Status == RoomMemberStatus.Approved)
            .Select(rm => new RoomMembershipRow(
                rm.RoomId,
                rm.Room!.Name,
                rm.Room!.ClubId,
                rm.Room!.Club!.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var clubNames = new Dictionary<Guid, string?>();
        foreach (var row in clubRows)
        {
            AddOrUpdateClubName(clubNames, row.ClubId, row.ClubName);
        }

        foreach (var row in roomRows)
        {
            if (clubNames.ContainsKey(row.ClubId))
            {
                AddOrUpdateClubName(clubNames, row.ClubId, row.ClubName);
            }
        }

        var roomsByClub = roomRows
            .Where(r => clubNames.ContainsKey(r.ClubId))
            .GroupBy(r => r.ClubId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(r => r.RoomName ?? string.Empty)
                    .ThenBy(r => r.RoomId)
                    .Select(r => new RoomProjection(r.RoomId, r.RoomName))
                    .ToList() as IReadOnlyList<RoomProjection>);

        var clubs = clubNames
            .OrderBy(kvp => kvp.Value ?? string.Empty)
            .ThenBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                var rooms = roomsByClub.TryGetValue(kvp.Key, out var roomList)
                    ? roomList
                    : Array.Empty<RoomProjection>();
                return new ClubProjection(kvp.Key, kvp.Value, rooms);
            })
            .ToList();

        var roomCount = roomRows
            .Where(r => clubNames.ContainsKey(r.ClubId))
            .Select(r => r.RoomId)
            .Distinct()
            .Count();

        var overview = new ClubRoomOverviewProjection(clubs.Count, roomCount);
        var projection = new ClubRoomTreeProjection(clubs, overview);
        var dto = projection.ToHybridDto();

        return Result<ClubRoomTreeHybridDto>.Success(dto);
    }

    private static void AddOrUpdateClubName(IDictionary<Guid, string?> map, Guid id, string? name)
    {
        if (!map.TryGetValue(id, out var existing))
        {
            map[id] = name;
            return;
        }

        if (string.IsNullOrWhiteSpace(existing) && !string.IsNullOrWhiteSpace(name))
        {
            map[id] = name;
        }
    }

    private sealed record ClubMembershipRow(Guid ClubId, string? ClubName);

    private sealed record RoomMembershipRow(
        Guid RoomId,
        string? RoomName,
        Guid ClubId,
        string? ClubName);
}
