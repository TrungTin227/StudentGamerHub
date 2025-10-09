using Application.Friends;
using BusinessObjects.Common.Pagination;
using BusinessObjects.Common.Results;
using DTOs.Friends;
using Microsoft.EntityFrameworkCore;
using Services.Application.Quests;
using Services.Common.Extensions;

namespace Services.Friends;

public sealed class FriendService : IFriendService
{
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromHours(24);

    private readonly IGenericUnitOfWork _uow;
    private readonly IGenericRepository<User, Guid> _users;
    private readonly IGenericRepository<FriendLink, Guid> _friendLinks;
    private readonly IQuestService? _quests;

    public FriendService(
        IGenericUnitOfWork uow,
        IQuestService? quests = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _users = _uow.GetRepository<User, Guid>();
        _friendLinks = _uow.GetRepository<FriendLink, Guid>();
        _quests = quests;
    }

    public Task<Result> InviteAsync(Guid requesterId, Guid targetUserId, CancellationToken ct = default)
    {
        if (requesterId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Requester id is required.")));
        }

        if (targetUserId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Target user id is required.")));
        }

        if (requesterId == targetUserId)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Bạn không thể tự gửi lời mời kết bạn.")));
        }

        return _uow.ExecuteTransactionAsync(async innerCt =>
        {
            innerCt.ThrowIfCancellationRequested();

            var target = await _users.GetByIdAsync(targetUserId, ct: innerCt).ConfigureAwait(false);
            if (target is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Người dùng không tồn tại."));
            }

            var (pairMin, pairMax) = NormalizePair(requesterId, targetUserId);

            var existing = await _friendLinks
                .GetQueryable(asNoTracking: false)
                .FirstOrDefaultAsync(
                    link => link.PairMinUserId == pairMin && link.PairMaxUserId == pairMax,
                    innerCt)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                return await HandleExistingInviteAsync(existing, requesterId, targetUserId, innerCt).ConfigureAwait(false);
            }

            var nowUtc = TimeExtensions.UtcNow();

            var invite = new FriendLink
            {
                Id = Guid.NewGuid(),
                SenderId = requesterId,
                RecipientId = targetUserId,
                Status = FriendStatus.Pending,
                RespondedAt = null,
                CreatedAtUtc = nowUtc,
                CreatedBy = requesterId,
                UpdatedAtUtc = nowUtc,
                UpdatedBy = requesterId,
            };

            await _friendLinks.AddAsync(invite, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct);
    }

    public Task<Result> AcceptAsync(Guid requesterId, Guid targetUserId, CancellationToken ct = default)
    {
        if (requesterId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Requester id is required.")));
        }

        if (targetUserId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Target user id is required.")));
        }

        return _uow.ExecuteTransactionAsync(async innerCt =>
        {
            innerCt.ThrowIfCancellationRequested();

            var (pairMin, pairMax) = NormalizePair(requesterId, targetUserId);

            var link = await _friendLinks
                .GetQueryable(asNoTracking: false)
                .FirstOrDefaultAsync(
                    x => x.PairMinUserId == pairMin && x.PairMaxUserId == pairMax,
                    innerCt)
                .ConfigureAwait(false);

            if (link is null || link.Status != FriendStatus.Pending)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Không tìm thấy lời mời kết bạn."));
            }

            if (link.RecipientId != requesterId)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Chỉ người nhận mới có thể chấp nhận lời mời."));
            }

            link.Status = FriendStatus.Accepted;
            link.RespondedAt = TimeExtensions.UtcNowOffset();
            link.UpdatedAtUtc = TimeExtensions.UtcNow();
            link.UpdatedBy = requesterId;

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            
            // ✅ HOOK: Trigger quest InviteAccepted cho người gửi lời mời (SenderId)
            // Best-effort: không fail transaction nếu quest trigger lỗi
            if (_quests is not null)
            {
                _ = await _quests.MarkInviteAcceptedAsync(link.SenderId, link.RecipientId, innerCt).ConfigureAwait(false);
            }

            return Result.Success();
        }, ct: ct);
    }

    public Task<Result> DeclineAsync(Guid requesterId, Guid targetUserId, CancellationToken ct = default)
    {
        if (requesterId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Requester id is required.")));
        }

        if (targetUserId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Target user id is required.")));
        }

        return _uow.ExecuteTransactionAsync(async innerCt =>
        {
            innerCt.ThrowIfCancellationRequested();

            var (pairMin, pairMax) = NormalizePair(requesterId, targetUserId);

            var link = await _friendLinks
                .GetQueryable(asNoTracking: false)
                .FirstOrDefaultAsync(
                    x => x.PairMinUserId == pairMin && x.PairMaxUserId == pairMax,
                    innerCt)
                .ConfigureAwait(false);

            if (link is null || link.Status != FriendStatus.Pending)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Không tìm thấy lời mời kết bạn."));
            }

            if (link.RecipientId != requesterId)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Chỉ người nhận mới có thể từ chối lời mời."));
            }

            link.Status = FriendStatus.Declined;
            link.RespondedAt = TimeExtensions.UtcNowOffset();
            link.UpdatedAtUtc = TimeExtensions.UtcNow();
            link.UpdatedBy = requesterId;

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct);
    }

    public Task<Result> CancelAsync(Guid requesterId, Guid targetUserId, CancellationToken ct = default)
    {
        if (requesterId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Requester id is required.")));
        }

        if (targetUserId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "Target user id is required.")));
        }

        return _uow.ExecuteTransactionAsync(async innerCt =>
        {
            innerCt.ThrowIfCancellationRequested();

            var (pairMin, pairMax) = NormalizePair(requesterId, targetUserId);

            var link = await _friendLinks
                .GetQueryable(asNoTracking: false)
                .FirstOrDefaultAsync(
                    x => x.PairMinUserId == pairMin && x.PairMaxUserId == pairMax,
                    innerCt)
                .ConfigureAwait(false);

            if (link is null || link.Status != FriendStatus.Pending)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Không tìm thấy lời mời kết bạn."));
            }

            if (link.SenderId != requesterId)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Chỉ người gửi mới có thể hủy lời mời."));
            }

            await _friendLinks.DeleteAsync(link.Id, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct);
    }

    public async Task<Result<CursorPageResult<FriendDto>>> ListAsync(
        Guid requesterId,
        FriendsFilter filter,
        CursorRequest request,
        CancellationToken ct = default)
    {
        if (requesterId == Guid.Empty)
        {
            return Result<CursorPageResult<FriendDto>>.Failure(
                new Error(Error.Codes.Validation, "Requester id is required."));
        }

        if (filter != FriendsFilter.All)
        {
            var filterName = filter.ToString().ToLowerInvariant();
            return Result<CursorPageResult<FriendDto>>.Failure(
                new Error(Error.Codes.Validation, $"Filter '{filterName}' is not supported."));
        }

        try
        {
            var query = _friendLinks
                .GetQueryable(asNoTracking: true)
                .Include(link => link.Sender)
                .Include(link => link.Recipient)
                .Where(link => link.Status == FriendStatus.Accepted &&
                              (link.SenderId == requesterId || link.RecipientId == requesterId));

            var result = await query.ToCursorPageAsync(
                request,
                link => link.Id,
                ct).ConfigureAwait(false);

            var items = result.Items
                .Select(link => link.ToFriendDtoFor(requesterId))
                .ToList();

            var pagedResult = new CursorPageResult<FriendDto>(
                Items: items,
                NextCursor: result.NextCursor,
                PrevCursor: result.PrevCursor,
                Size: result.Size,
                Sort: result.Sort,
                Desc: result.Desc
            );

            return Result<CursorPageResult<FriendDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<CursorPageResult<FriendDto>>.Failure(
                new Error(Error.Codes.Unexpected, $"Lỗi khi lấy danh sách bạn bè: {ex.Message}"));
        }
    }

    private async Task<Result> HandleExistingInviteAsync(
        FriendLink existing,
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct)
    {
        switch (existing.Status)
        {
            case FriendStatus.Accepted:
                return Result.Failure(new Error(Error.Codes.Conflict, "Hai bạn đã là bạn bè."));

            case FriendStatus.Pending when existing.SenderId == requesterId:
                return Result.Failure(new Error(Error.Codes.Conflict, "Bạn đã gửi lời mời trước đó."));

            case FriendStatus.Pending:
                return Result.Failure(new Error(Error.Codes.Conflict, "Đối phương đã mời bạn trước."));

            case FriendStatus.Declined:
                return await HandleDeclinedInviteAsync(existing, requesterId, targetUserId, ct).ConfigureAwait(false);

            default:
                return Result.Failure(new Error(Error.Codes.Unexpected, "Trạng thái lời mời không hợp lệ."));
        }
    }

    private async Task<Result> HandleDeclinedInviteAsync(
        FriendLink existing,
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct)
    {
        if (existing.RespondedAt.HasValue)
        {
            var elapsed = TimeExtensions.UtcNowOffset() - existing.RespondedAt.Value;
            if (elapsed < ResendCooldown)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Bạn chỉ có thể gửi lại sau 24 giờ."));
            }
        }

        existing.SenderId = requesterId;
        existing.RecipientId = targetUserId;
        existing.Status = FriendStatus.Pending;
        existing.RespondedAt = null;
        existing.UpdatedAtUtc = TimeExtensions.UtcNow();
        existing.UpdatedBy = requesterId;

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }

    private static (Guid Min, Guid Max) NormalizePair(Guid first, Guid second)
        => first.CompareTo(second) <= 0 ? (first, second) : (second, first);
}
