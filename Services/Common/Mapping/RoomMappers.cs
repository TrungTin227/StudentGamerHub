using Repositories.Models;

namespace Services.Common.Mapping;

/// <summary>
/// Mapping helpers for room entities and DTOs.
/// </summary>
public static class RoomMappers
{
    /// <summary>
    /// Maps <see cref="Room"/> to <see cref="RoomDetailDto"/>.
    /// </summary>
    public static RoomDetailDto ToRoomDetailDto(this RoomDetailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new RoomDetailDto(
            model.Id,
            model.ClubId,
            model.Name,
            model.Description,
            model.JoinPolicy,
            model.Capacity,
            model.MembersCount,
            model.OwnerId,
            model.IsMember,
            model.IsOwner,
            model.MembershipStatus,
            model.CreatedAtUtc,
            model.UpdatedAtUtc);
    }

}
