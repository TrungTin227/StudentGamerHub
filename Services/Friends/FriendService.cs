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
    private readonly IPresenceService? _presence;

    public FriendService(
        IGenericUnitOfWork uow,
        IQuestService? quests = null,
        IPresenceService? presence = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _users = _uow.GetRepository<User, Guid>();
        _friendLinks = _uow.GetRepository<FriendLink, Guid>();
        _quests = quests;
        _presence = presence;
    }

    public async Task<Result<PagedResponse<UserSearchItemDto>>> SearchUsersAsync(
        Guid currentUserId,
        string? keyword,
        PageRequest request,
        CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result<PagedResponse<UserSearchItemDto>>.Failure(
                new Error(Error.Codes.Validation, "User id is required."));
        }

        var page = request.PageSafe;
        var size = Math.Clamp(request.SizeSafe, 1, 50);
        var sort = string.IsNullOrWhiteSpace(request.Sort) ? nameof(User.FullName) : request.Sort!;
        var sanitized = new PageRequest { Page = page, Size = size, Sort = sort, Desc = request.Desc };

        try
        {
            var baseQuery = _users
                .GetQueryable(asNoTracking: true)
                .Where(u => u.Id != currentUserId)
                .ApplyKeyword(keyword);

            var pagedUsers = await baseQuery
                .ToPagedResultAsync(sanitized, ct)
                .ConfigureAwait(false);

            var userIds = pagedUsers.Items.Select(u => u.Id).ToArray();

            List<FriendLink> relatedLinks = new();
            if (userIds.Length > 0)
            {
                relatedLinks = await _friendLinks
                    .GetQueryable(asNoTracking: true)
                    .Where(link =>
                        (link.SenderId == currentUserId && userIds.Contains(link.RecipientId)) ||
                        (link.RecipientId == currentUserId && userIds.Contains(link.SenderId)))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }

            var items = pagedUsers.Items
                .Select(user =>
                {
                    var link = relatedLinks.FirstOrDefault(l =>
                        (l.SenderId == currentUserId && l.RecipientId == user.Id) ||
                        (l.RecipientId == currentUserId && l.SenderId == user.Id));

                    // ✅ FIX: Check both directions for IsFriend (bidirectional relationship)
                    var isFriend = link?.Status == FriendStatus.Accepted;
                    
                    // ✅ FIX: IsPending should be true for BOTH outgoing and incoming requests
                    // This fixes the "một bên là bạn, bên kia chưa" bug
                    var isPending = link?.Status == FriendStatus.Pending;

                    return user.ToUserSearchItemDto(isFriend, isPending);
                })
                .ToList();

            var response = new PagedResponse<UserSearchItemDto>(
                items,
                pagedUsers.Page,
                pagedUsers.Size,
                pagedUsers.TotalCount,
                pagedUsers.TotalPages,
                pagedUsers.HasPrevious,
                pagedUsers.HasNext,
                pagedUsers.Sort,
                pagedUsers.Desc);

            return Result<PagedResponse<UserSearchItemDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<PagedResponse<UserSearchItemDto>>.Failure(
                new Error(Error.Codes.Unexpected, $"Không thể tìm kiếm người dùng: {ex.Message}"));
        }
    }

    public async Task<Result<PagedResponse<FriendRequestItemDto>>> GetIncomingRequestsAsync(
        Guid currentUserId,
        PageRequest request,
        CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result<PagedResponse<FriendRequestItemDto>>.Failure(
                new Error(Error.Codes.Validation, "User id is required."));
        }

        var page = request.PageSafe;
        var size = Math.Clamp(request.SizeSafe, 1, 50);
        var sort = string.IsNullOrWhiteSpace(request.Sort) ? nameof(FriendLink.CreatedAtUtc) : request.Sort!;
        var sanitized = new PageRequest { Page = page, Size = size, Sort = sort, Desc = request.Desc };

        try
        {
            var query = _friendLinks
                .GetQueryable(asNoTracking: true)
                .Include(link => link.Sender)
                .Where(link =>
                    link.Status == FriendStatus.Pending &&
                    link.RecipientId == currentUserId);

            var paged = await query
                .ToPagedResultAsync(sanitized, ct)
                .ConfigureAwait(false);

            var items = paged.Items
                .Select(link => link.ToFriendRequestItemDtoFor(currentUserId))
                .ToList();

            var response = new PagedResponse<FriendRequestItemDto>(
                items,
                paged.Page,
                paged.Size,
                paged.TotalCount,
                paged.TotalPages,
                paged.HasPrevious,
                paged.HasNext,
                paged.Sort,
                paged.Desc);

            return Result<PagedResponse<FriendRequestItemDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<PagedResponse<FriendRequestItemDto>>.Failure(
                new Error(Error.Codes.Unexpected, $"Không thể lấy lời mời đến: {ex.Message}"));
        }
    }

    public async Task<Result<PagedResponse<FriendRequestItemDto>>> GetOutgoingRequestsAsync(
        Guid currentUserId,
        string? status,
        PageRequest request,
        CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result<PagedResponse<FriendRequestItemDto>>.Failure(
                new Error(Error.Codes.Validation, "User id is required."));
        }

        FriendStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, true, out FriendStatus parsed))
            {
                return Result<PagedResponse<FriendRequestItemDto>>.Failure(
                    new Error(Error.Codes.Validation, $"Trạng thái '{status}' không hợp lệ."));
            }

            statusFilter = parsed;
        }

        var page = request.PageSafe;
        var size = Math.Clamp(request.SizeSafe, 1, 50);
        var sort = string.IsNullOrWhiteSpace(request.Sort) ? nameof(FriendLink.CreatedAtUtc) : request.Sort!;
        var sanitized = new PageRequest { Page = page, Size = size, Sort = sort, Desc = request.Desc };

        try
        {
            var query = _friendLinks
                .GetQueryable(asNoTracking: true)
                .Include(link => link.Recipient)
                .Where(link => link.SenderId == currentUserId);

            if (statusFilter.HasValue)
            {
                query = query.Where(link => link.Status == statusFilter.Value);
            }

            var paged = await query
                .ToPagedResultAsync(sanitized, ct)
                .ConfigureAwait(false);

            var items = paged.Items
                .Select(link => link.ToFriendRequestItemDtoFor(currentUserId))
                .ToList();

            var response = new PagedResponse<FriendRequestItemDto>(
                items,
                paged.Page,
                paged.Size,
                paged.TotalCount,
                paged.TotalPages,
                paged.HasPrevious,
                paged.HasNext,
                paged.Sort,
                paged.Desc);

            return Result<PagedResponse<FriendRequestItemDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<PagedResponse<FriendRequestItemDto>>.Failure(
                new Error(Error.Codes.Unexpected, $"Không thể lấy lời mời đã gửi: {ex.Message}"));
        }
    }

    public async Task<Result<FriendRequestsOverviewDto>> GetRequestsOverviewAsync(
        Guid currentUserId,
        int take,
        CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result<FriendRequestsOverviewDto>.Failure(
                new Error(Error.Codes.Validation, "User id is required."));
        }

        var limit = Math.Clamp(take <= 0 ? PaginationOptions.DefaultPageSize : take, 1, 50);

        try
        {
            var incomingTask = _friendLinks
                .GetQueryable(asNoTracking: true)
                .Include(link => link.Sender)
                .Where(link =>
                    link.Status == FriendStatus.Pending &&
                    link.RecipientId == currentUserId)
                .OrderByDescending(link => link.CreatedAtUtc)
                .Take(limit)
                .ToListAsync(ct);

            var outgoingTask = _friendLinks
                .GetQueryable(asNoTracking: true)
                .Include(link => link.Recipient)
                .Where(link =>
                    link.Status == FriendStatus.Pending &&
                    link.SenderId == currentUserId)
                .OrderByDescending(link => link.CreatedAtUtc)
                .Take(limit)
                .ToListAsync(ct);

            await Task.WhenAll(incomingTask, outgoingTask).ConfigureAwait(false);

            var incoming = incomingTask.Result
                .Select(link => link.ToFriendRequestItemDtoFor(currentUserId))
                .ToList();
            var outgoing = outgoingTask.Result
                .Select(link => link.ToFriendRequestItemDtoFor(currentUserId))
                .ToList();

            var dto = new FriendRequestsOverviewDto(incoming, outgoing);
            return Result<FriendRequestsOverviewDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<FriendRequestsOverviewDto>.Failure(
                new Error(Error.Codes.Unexpected, $"Không thể tổng hợp lời mời kết bạn: {ex.Message}"));
        }
    }

    public async Task<Result<PagedResponse<UserSearchItemDto>>> GetSuggestedFriendsAsync(
        Guid currentUserId,
        PageRequest request,
        CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result<PagedResponse<UserSearchItemDto>>.Failure(
                new Error(Error.Codes.Validation, "User id is required."));
        }

        var page = request.PageSafe;
        var size = Math.Clamp(request.SizeSafe, 1, 20);
        var sanitized = new PageRequest { Page = page, Size = size, Sort = request.Sort, Desc = request.Desc };

        try
        {
            var currentUser = await _users
                .GetByIdAsync(currentUserId, ct: ct)
                .ConfigureAwait(false);

            if (currentUser is null)
            {
                return Result<PagedResponse<UserSearchItemDto>>.Failure(
                    new Error(Error.Codes.NotFound, "Người dùng không tồn tại."));
            }

            var relatedLinks = await _friendLinks
                .GetQueryable(asNoTracking: true)
                .Where(link => link.SenderId == currentUserId || link.RecipientId == currentUserId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var excludedIds = new HashSet<Guid>
            {
                currentUserId
            };

            foreach (var link in relatedLinks)
            {
                excludedIds.Add(link.SenderId == currentUserId ? link.RecipientId : link.SenderId);
            }

            var currentFriendIds = relatedLinks
                .Where(link => link.Status == FriendStatus.Accepted)
                .Select(link => link.SenderId == currentUserId ? link.RecipientId : link.SenderId)
                .Distinct()
                .ToList();

            var baseCandidates = _users
                .GetQueryable(asNoTracking: true)
                .Where(u => !excludedIds.Contains(u.Id));

            var acceptedLinks = _friendLinks
                .GetQueryable(asNoTracking: true)
                .Where(link => link.Status == FriendStatus.Accepted);

            var query = baseCandidates
                .Select(u => new
                {
                    User = u,
                    MutualCount = acceptedLinks.Count(link =>
                        (link.SenderId == u.Id && currentFriendIds.Contains(link.RecipientId)) ||
                        (link.RecipientId == u.Id && currentFriendIds.Contains(link.SenderId))),
                    SameUniversity = !string.IsNullOrWhiteSpace(u.University) &&
                        !string.IsNullOrWhiteSpace(currentUser.University) &&
                        u.University == currentUser.University
                });

            var ordered = query
                .OrderByDescending(x => x.MutualCount)
                .ThenByDescending(x => x.SameUniversity)
                .ThenBy(x => x.User.FullName);

            var total = await ordered.CountAsync(ct).ConfigureAwait(false);
            var totalPages = (int)Math.Ceiling(total / (double)size);
            var hasPrevious = page > 1;
            var hasNext = page < totalPages;

            var items = await ordered
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => x.User)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var resultItems = items
                .Select(u => u.ToUserSearchItemDto(isFriend: false, isPending: false))
                .ToList();

            var response = new PagedResponse<UserSearchItemDto>(
                resultItems,
                page,
                size,
                total,
                totalPages,
                hasPrevious,
                hasNext,
                sanitized.SortSafe,
                sanitized.Desc);

            return Result<PagedResponse<UserSearchItemDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<PagedResponse<UserSearchItemDto>>.Failure(
                new Error(Error.Codes.Unexpected, $"Không thể gợi ý bạn bè: {ex.Message}"));
        }
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
            link.RespondedAt = TimeExtensions.UtcNow();
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
            link.RespondedAt = TimeExtensions.UtcNow();
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

        if (filter != FriendsFilter.All && filter != FriendsFilter.Online)
        {
            var filterName = filter.ToString().ToLowerInvariant();
            return Result<CursorPageResult<FriendDto>>.Failure(
                new Error(Error.Codes.Validation, $"Filter '{filterName}' is not supported."));
        }

        if (filter == FriendsFilter.Online && _presence is null)
        {
            return Result<CursorPageResult<FriendDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Presence service is not configured."));
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

            if (filter == FriendsFilter.Online && _presence is not null && items.Count > 0)
            {
                var onlineResult = await _presence
                    .BatchIsOnlineAsync(items.Select(i => i.User.Id).ToArray(), ct)
                    .ConfigureAwait(false);

                if (!onlineResult.IsSuccess)
                {
                    return Result<CursorPageResult<FriendDto>>.Failure(onlineResult.Error);
                }

                var onlineItems = items
                    .Where(item => onlineResult.Value.TryGetValue(item.User.Id, out var online) && online)
                    .ToList();

                var onlinePaged = new CursorPageResult<FriendDto>(
                    onlineItems,
                    result.NextCursor,
                    result.PrevCursor,
                    result.Size,
                    result.Sort,
                    result.Desc);

                return Result<CursorPageResult<FriendDto>>.Success(onlinePaged);
            }

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
            var elapsed = TimeExtensions.UtcNow() - existing.RespondedAt.Value;
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
