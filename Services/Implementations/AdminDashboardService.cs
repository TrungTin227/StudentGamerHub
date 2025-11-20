using BusinessObjects;
using BusinessObjects.Common.Pagination;
using BusinessObjects.Common.Results;
using DTOs.Admin;
using DTOs.Admin.Filters;
using DTOs.Common.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Repositories.Persistence;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminDashboardService> _logger;
    private const string DashboardSummaryCacheKey = "AdminDashboardSummary";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(3);

    public AdminDashboardService(
        AppDbContext context,
        UserManager<User> userManager,
        IMemoryCache cache,
        ILogger<AdminDashboardService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<AdminDashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        try
        {
            // Try to get from cache first
            if (_cache.TryGetValue(DashboardSummaryCacheKey, out AdminDashboardSummaryDto? cachedSummary) && cachedSummary != null)
            {
                return Result<AdminDashboardSummaryDto>.Success(cachedSummary);
            }

            // If not in cache, compute it
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted, ct);
            var newUsersLast30Days = await _context.Users
                .CountAsync(u => !u.IsDeleted && u.CreatedAtUtc >= thirtyDaysAgo, ct);

            var activeCommunities = await _context.Communities
                .CountAsync(c => !c.IsDeleted, ct);

            var totalClubs = await _context.Clubs
                .CountAsync(c => !c.IsDeleted, ct);

            var totalGames = await _context.Games
                .CountAsync(g => !g.IsDeleted, ct);

            var totalEvents = await _context.Events.CountAsync(ct);

            var totalRevenue = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Succeeded && t.Direction == TransactionDirection.In)
                .SumAsync(t => (decimal?)t.AmountCents, ct) ?? 0;

            var revenueThisMonth = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Succeeded
                    && t.Direction == TransactionDirection.In
                    && t.CreatedAtUtc >= monthStart)
                .SumAsync(t => (decimal?)t.AmountCents, ct) ?? 0;

            var successfulTransactions = await _context.Transactions
                .CountAsync(t => t.Status == TransactionStatus.Succeeded, ct);

            var activeMemberships = await _context.UserMemberships
                .CountAsync(um => um.EndDate > now, ct);

            var openBugReports = await _context.BugReports
                .CountAsync(b => b.Status == BugStatus.Open, ct);

            var summary = new AdminDashboardSummaryDto
            {
                TotalUsers = totalUsers,
                NewUsersLast30Days = newUsersLast30Days,
                ActiveCommunities = activeCommunities,
                TotalClubs = totalClubs,
                TotalGames = totalGames,
                TotalEvents = totalEvents,
                TotalRevenueVND = totalRevenue / 100m, // Convert cents to VND
                RevenueThisMonthVND = revenueThisMonth / 100m,
                SuccessfulTransactions = successfulTransactions,
                ActiveMemberships = activeMemberships,
                OpenBugReports = openBugReports,
                GeneratedAtUtc = now
            };

            // Cache the result for 3 minutes
            _cache.Set(DashboardSummaryCacheKey, summary, CacheDuration);

            return Result<AdminDashboardSummaryDto>.Success(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard summary");
            return Result<AdminDashboardSummaryDto>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get dashboard summary"));
        }
    }

    public async Task<Result<PagedResult<AdminUserStatsDto>>> GetUsersAsync(
        AdminUserFilter filter,
        PageRequest pageRequest,
        CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var query = _context.Users.AsNoTracking();

            // Apply filters
            if (!filter.IncludeDeleted)
            {
                query = query.Where(u => !u.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.ToLower();
                query = query.Where(u =>
                    u.UserName!.ToLower().Contains(keyword) ||
                    (u.Email != null && u.Email.ToLower().Contains(keyword)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(keyword)));
            }

            if (filter.CreatedFrom.HasValue)
            {
                query = query.Where(u => u.CreatedAtUtc >= filter.CreatedFrom.Value);
            }

            if (filter.CreatedTo.HasValue)
            {
                query = query.Where(u => u.CreatedAtUtc <= filter.CreatedTo.Value);
            }

            if (filter.HasActiveMembership.HasValue)
            {
                if (filter.HasActiveMembership.Value)
                {
                    query = query.Where(u => u.Membership != null && u.Membership.EndDate > now);
                }
                else
                {
                    query = query.Where(u => u.Membership == null || u.Membership.EndDate <= now);
                }
            }

            // Total count
            var totalCount = await query.CountAsync(ct);

            // Get paged users with only basic fields (no scalar subqueries)
            var users = await query
                .OrderByDescending(u => u.CreatedAtUtc)
                .Skip((pageRequest.Page - 1) * pageRequest.Size)
                .Take(pageRequest.Size)
                .Select(u => new
                {
                    User = u,
                    WalletBalance = u.Wallet != null ? u.Wallet.BalanceCents : 0,
                    MembershipPlan = u.Membership != null ? u.Membership.MembershipPlan!.Name : null,
                    MembershipExpiresAt = u.Membership != null ? u.Membership.EndDate : (DateTime?)null
                })
                .ToListAsync(ct);

            var userIds = users.Select(u => u.User.Id).ToList();

            // Batch load all stats in parallel to avoid scalar subqueries
            var eventsCreatedTask = _context.Events
                .AsNoTracking()
                .Where(e => userIds.Contains(e.OrganizerId))
                .GroupBy(e => e.OrganizerId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

            var eventsAttendedTask = _context.EventRegistrations
                .AsNoTracking()
                .Where(er => userIds.Contains(er.UserId) && er.Status == EventRegistrationStatus.Confirmed)
                .GroupBy(er => er.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

            var communitiesJoinedTask = _context.CommunityMembers
                .AsNoTracking()
                .Where(cm => userIds.Contains(cm.UserId))
                .GroupBy(cm => cm.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

            var totalSpentTask = _context.Transactions
                .AsNoTracking()
                .Where(t => userIds.Contains(t.Wallet.UserId)
                    && t.Direction == TransactionDirection.Out
                    && t.Status == TransactionStatus.Succeeded)
                .GroupBy(t => t.Wallet.UserId)
                .Select(g => new { UserId = g.Key, Total = g.Sum(t => t.AmountCents) })
                .ToDictionaryAsync(x => x.UserId, x => x.Total, ct);

            var userRolesTask = _context.UserRoles
                .AsNoTracking()
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name! })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleName).ToList(), ct);

            // Wait for all parallel tasks to complete
            await Task.WhenAll(eventsCreatedTask, eventsAttendedTask, communitiesJoinedTask, totalSpentTask, userRolesTask);

            var eventsCreatedDict = await eventsCreatedTask;
            var eventsAttendedDict = await eventsAttendedTask;
            var communitiesJoinedDict = await communitiesJoinedTask;
            var totalSpentDict = await totalSpentTask;
            var userRolesDict = await userRolesTask;

            // Map to DTOs
            var userStats = users.Select(item => new AdminUserStatsDto
            {
                UserId = item.User.Id,
                UserName = item.User.UserName!,
                Email = item.User.Email,
                FullName = item.User.FullName,
                AvatarUrl = item.User.AvatarUrl,
                Level = item.User.Level,
                Points = item.User.Points,
                WalletBalanceCents = item.WalletBalance,
                CurrentMembership = item.MembershipPlan,
                MembershipExpiresAt = item.MembershipExpiresAt,
                EventsCreated = eventsCreatedDict.GetValueOrDefault(item.User.Id, 0),
                EventsAttended = eventsAttendedDict.GetValueOrDefault(item.User.Id, 0),
                CommunitiesJoined = communitiesJoinedDict.GetValueOrDefault(item.User.Id, 0),
                TotalSpentCents = totalSpentDict.GetValueOrDefault(item.User.Id, 0),
                Roles = userRolesDict.GetValueOrDefault(item.User.Id, new List<string>()),
                CreatedAtUtc = item.User.CreatedAtUtc,
                UpdatedAtUtc = item.User.UpdatedAtUtc,
                IsDeleted = item.User.IsDeleted
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageRequest.Size);
            var hasPrevious = pageRequest.Page > 1;
            var hasNext = pageRequest.Page < totalPages;

            var result = new PagedResult<AdminUserStatsDto>(
                userStats,
                pageRequest.Page,
                pageRequest.Size,
                totalCount,
                totalPages,
                hasPrevious,
                hasNext,
                pageRequest.Sort ?? "CreatedAtUtc",
                pageRequest.Desc
            );

            return Result<PagedResult<AdminUserStatsDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return Result<PagedResult<AdminUserStatsDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get users"));
        }
    }

    public async Task<Result<AdminUserStatsDto>> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Get basic user info first (no scalar subqueries)
            var userQuery = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    User = u,
                    WalletBalance = u.Wallet != null ? u.Wallet.BalanceCents : 0,
                    MembershipPlan = u.Membership != null ? u.Membership.MembershipPlan!.Name : null,
                    MembershipExpiresAt = u.Membership != null ? u.Membership.EndDate : (DateTime?)null
                })
                .FirstOrDefaultAsync(ct);

            if (userQuery == null)
            {
                return Result<AdminUserStatsDto>.Failure(
                    new Error(Error.Codes.NotFound, "User not found"));
            }

            // Load stats in parallel (more efficient than scalar subqueries)
            var eventsCreatedTask = _context.Events
                .AsNoTracking()
                .CountAsync(e => e.OrganizerId == userId, ct);

            var eventsAttendedTask = _context.EventRegistrations
                .AsNoTracking()
                .CountAsync(er => er.UserId == userId && er.Status == EventRegistrationStatus.Confirmed, ct);

            var communitiesJoinedTask = _context.CommunityMembers
                .AsNoTracking()
                .CountAsync(cm => cm.UserId == userId, ct);

            var totalSpentTask = _context.Transactions
                .AsNoTracking()
                .Where(t => t.Wallet.UserId == userId
                    && t.Direction == TransactionDirection.Out
                    && t.Status == TransactionStatus.Succeeded)
                .SumAsync(t => (long?)t.AmountCents, ct);

            var rolesTask = _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Join(_context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => r.Name!)
                .ToListAsync(ct);

            // Wait for all parallel tasks
            await Task.WhenAll(eventsCreatedTask, eventsAttendedTask, communitiesJoinedTask, totalSpentTask, rolesTask);

            var userStats = new AdminUserStatsDto
            {
                UserId = userQuery.User.Id,
                UserName = userQuery.User.UserName!,
                Email = userQuery.User.Email,
                FullName = userQuery.User.FullName,
                AvatarUrl = userQuery.User.AvatarUrl,
                Level = userQuery.User.Level,
                Points = userQuery.User.Points,
                WalletBalanceCents = userQuery.WalletBalance,
                CurrentMembership = userQuery.MembershipPlan,
                MembershipExpiresAt = userQuery.MembershipExpiresAt,
                EventsCreated = await eventsCreatedTask,
                EventsAttended = await eventsAttendedTask,
                CommunitiesJoined = await communitiesJoinedTask,
                TotalSpentCents = await totalSpentTask ?? 0,
                Roles = await rolesTask,
                CreatedAtUtc = userQuery.User.CreatedAtUtc,
                UpdatedAtUtc = userQuery.User.UpdatedAtUtc,
                IsDeleted = userQuery.User.IsDeleted
            };

            return Result<AdminUserStatsDto>.Success(userStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
            return Result<AdminUserStatsDto>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get user"));
        }
    }

    public async Task<Result<AdminRevenueStatsDto>> GetRevenueStatsAsync(
        string period,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            DateTime periodStart;
            DateTime periodEnd;
            string periodType;

            // Determine period
            switch (period.ToLower())
            {
                case "week":
                    periodType = "Week";
                    periodStart = now.AddDays(-(int)now.DayOfWeek).Date;
                    periodEnd = periodStart.AddDays(7);
                    break;
                case "month":
                    periodType = "Month";
                    periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    periodEnd = periodStart.AddMonths(1);
                    break;
                case "year":
                    periodType = "Year";
                    periodStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    periodEnd = periodStart.AddYears(1);
                    break;
                case "custom":
                    periodType = "Custom";
                    if (!startDate.HasValue || !endDate.HasValue)
                    {
                        return Result<AdminRevenueStatsDto>.Failure(
                            new Error(Error.Codes.Validation, "Start date and end date are required for custom period"));
                    }
                    periodStart = startDate.Value;
                    periodEnd = endDate.Value;
                    break;
                default:
                    return Result<AdminRevenueStatsDto>.Failure(
                        new Error(Error.Codes.Validation, "Invalid period. Use 'week', 'month', 'year', or 'custom'"));
            }

            // Get all stats in SQL queries instead of loading into memory
            var transactionQuery = _context.Transactions
                .AsNoTracking()
                .Where(t => t.CreatedAtUtc >= periodStart && t.CreatedAtUtc < periodEnd);

            var successfulTransactionQuery = transactionQuery
                .Where(t => t.Status == TransactionStatus.Succeeded);

            // Calculate revenue by purpose (all in SQL)
            var paymentIntentQuery = _context.PaymentIntents
                .AsNoTracking()
                .Where(pi => pi.CreatedAtUtc >= periodStart && pi.CreatedAtUtc < periodEnd
                    && pi.Status == PaymentIntentStatus.Succeeded);

            var membershipRevenue = await paymentIntentQuery
                .Where(pi => pi.Purpose == PaymentPurpose.Membership)
                .SumAsync(pi => (long?)pi.AmountCents, ct) ?? 0;

            var eventRevenue = await paymentIntentQuery
                .Where(pi => pi.Purpose == PaymentPurpose.EventTicket)
                .SumAsync(pi => (long?)pi.AmountCents, ct) ?? 0;

            var topUpRevenue = await paymentIntentQuery
                .Where(pi => pi.Purpose == PaymentPurpose.WalletTopUp || pi.Purpose == PaymentPurpose.TopUp)
                .SumAsync(pi => (long?)pi.AmountCents, ct) ?? 0;

            var totalRevenue = await successfulTransactionQuery
                .Where(t => t.Direction == TransactionDirection.In)
                .SumAsync(t => (long?)t.AmountCents, ct) ?? 0;

            // Daily breakdown - all in SQL
            var dailyBreakdown = await successfulTransactionQuery
                .Where(t => t.Direction == TransactionDirection.In)
                .GroupBy(t => t.CreatedAtUtc.Date)
                .Select(g => new DailyRevenueDto
                {
                    Date = g.Key,
                    RevenueCents = g.Sum(t => t.AmountCents),
                    TransactionCount = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToListAsync(ct);

            var totalTransactionCount = await transactionQuery.CountAsync(ct);
            var successfulCount = await successfulTransactionQuery.CountAsync(ct);
            var failedCount = await transactionQuery.CountAsync(t => t.Status == TransactionStatus.Failed, ct);

            var stats = new AdminRevenueStatsDto
            {
                PeriodType = periodType,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalRevenueCents = totalRevenue,
                MembershipRevenueCents = membershipRevenue,
                EventRevenueCents = eventRevenue,
                TopUpRevenueCents = topUpRevenue,
                TransactionCount = totalTransactionCount,
                SuccessfulCount = successfulCount,
                FailedCount = failedCount,
                DailyBreakdown = dailyBreakdown
            };

            return Result<AdminRevenueStatsDto>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue stats for period: {Period}", period);
            return Result<AdminRevenueStatsDto>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get revenue stats"));
        }
    }

    public async Task<Result<PagedResult<AdminTransactionDto>>> GetTransactionsAsync(
        AdminTransactionFilter filter,
        PageRequest pageRequest,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.Transactions
                .AsNoTracking();

            // Apply filters
            if (filter.UserId.HasValue)
            {
                query = query.Where(t => t.Wallet.UserId == filter.UserId.Value);
            }

            if (filter.EventId.HasValue)
            {
                query = query.Where(t => t.EventId == filter.EventId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(t => t.Status == filter.Status.Value);
            }

            if (filter.Direction.HasValue)
            {
                query = query.Where(t => t.Direction == filter.Direction.Value);
            }

            if (filter.Method.HasValue)
            {
                query = query.Where(t => t.Method == filter.Method.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(t => t.CreatedAtUtc >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(t => t.CreatedAtUtc <= filter.ToDate.Value);
            }

            if (filter.MinAmountCents.HasValue)
            {
                query = query.Where(t => t.AmountCents >= filter.MinAmountCents.Value);
            }

            if (filter.MaxAmountCents.HasValue)
            {
                query = query.Where(t => t.AmountCents <= filter.MaxAmountCents.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Provider))
            {
                query = query.Where(t => t.Provider == filter.Provider);
            }

            // Handle period filter
            if (!string.IsNullOrWhiteSpace(filter.Period))
            {
                var now = DateTime.UtcNow;
                DateTime periodStart;

                switch (filter.Period.ToLower())
                {
                    case "week":
                        periodStart = now.AddDays(-(int)now.DayOfWeek).Date;
                        query = query.Where(t => t.CreatedAtUtc >= periodStart);
                        break;
                    case "month":
                        periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(t => t.CreatedAtUtc >= periodStart);
                        break;
                    case "year":
                        periodStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(t => t.CreatedAtUtc >= periodStart);
                        break;
                }
            }

            // Total count
            var totalCount = await query.CountAsync(ct);

            // Project directly in query to only load needed fields
            var transactions = await query
                .OrderByDescending(t => t.CreatedAtUtc)
                .Skip((pageRequest.Page - 1) * pageRequest.Size)
                .Take(pageRequest.Size)
                .Select(t => new AdminTransactionDto
                {
                    Id = t.Id,
                    WalletId = t.WalletId ?? Guid.Empty,
                    UserId = t.Wallet != null ? t.Wallet.UserId : (Guid?)null,
                    UserName = t.Wallet != null ? t.Wallet.User!.UserName : null,
                    UserEmail = t.Wallet != null ? t.Wallet.User!.Email : null,
                    AmountCents = t.AmountCents,
                    Currency = t.Currency,
                    Direction = t.Direction,
                    Method = t.Method,
                    Status = t.Status,
                    EventId = t.EventId,
                    EventTitle = t.Event != null ? t.Event.Title : null,
                    Provider = t.Provider,
                    ProviderRef = t.ProviderRef,
                    Metadata = t.Metadata != null ? t.Metadata.RootElement.ToString() : null,
                    CreatedAtUtc = t.CreatedAtUtc,
                    CompletedAtUtc = t.UpdatedAtUtc
                })
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageRequest.Size);
            var hasPrevious = pageRequest.Page > 1;
            var hasNext = pageRequest.Page < totalPages;

            var result = new PagedResult<AdminTransactionDto>(
                transactions,
                pageRequest.Page,
                pageRequest.Size,
                totalCount,
                totalPages,
                hasPrevious,
                hasNext,
                pageRequest.Sort ?? "CreatedAtUtc",
                pageRequest.Desc
            );

            return Result<PagedResult<AdminTransactionDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions");
            return Result<PagedResult<AdminTransactionDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get transactions"));
        }
    }

    public async Task<Result<PagedResult<AdminPaymentIntentDto>>> GetPaymentIntentsAsync(
        AdminPaymentIntentFilter filter,
        PageRequest pageRequest,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.PaymentIntents
                .AsNoTracking();

            // Apply filters
            if (filter.UserId.HasValue)
            {
                query = query.Where(pi => pi.UserId == filter.UserId.Value);
            }

            if (filter.EventId.HasValue)
            {
                query = query.Where(pi => pi.EventId == filter.EventId.Value);
            }

            if (filter.MembershipPlanId.HasValue)
            {
                query = query.Where(pi => pi.MembershipPlanId == filter.MembershipPlanId.Value);
            }

            if (filter.Purpose.HasValue)
            {
                query = query.Where(pi => pi.Purpose == filter.Purpose.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(pi => pi.Status == filter.Status.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(pi => pi.CreatedAtUtc >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(pi => pi.CreatedAtUtc <= filter.ToDate.Value);
            }

            if (filter.MinAmountCents.HasValue)
            {
                query = query.Where(pi => pi.AmountCents >= filter.MinAmountCents.Value);
            }

            if (filter.MaxAmountCents.HasValue)
            {
                query = query.Where(pi => pi.AmountCents <= filter.MaxAmountCents.Value);
            }

            if (filter.OrderCode.HasValue)
            {
                query = query.Where(pi => pi.OrderCode == filter.OrderCode.Value);
            }

            // Handle period filter
            if (!string.IsNullOrWhiteSpace(filter.Period))
            {
                var now = DateTime.UtcNow;
                DateTime periodStart;

                switch (filter.Period.ToLower())
                {
                    case "week":
                        periodStart = now.AddDays(-(int)now.DayOfWeek).Date;
                        query = query.Where(pi => pi.CreatedAtUtc >= periodStart);
                        break;
                    case "month":
                        periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(pi => pi.CreatedAtUtc >= periodStart);
                        break;
                    case "year":
                        periodStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(pi => pi.CreatedAtUtc >= periodStart);
                        break;
                }
            }

            // Total count
            var totalCount = await query.CountAsync(ct);

            // Project directly in query to only load needed fields
            var paymentIntentDtos = await query
                .OrderByDescending(pi => pi.CreatedAtUtc)
                .Skip((pageRequest.Page - 1) * pageRequest.Size)
                .Take(pageRequest.Size)
                .Select(pi => new AdminPaymentIntentDto
                {
                    Id = pi.Id,
                    UserId = pi.UserId,
                    UserName = pi.User != null ? pi.User.UserName : null,
                    UserEmail = pi.User != null ? pi.User.Email : null,
                    AmountCents = pi.AmountCents,
                    Purpose = pi.Purpose,
                    PurposeDisplay = pi.Purpose.ToString(),
                    EventId = pi.EventId,
                    EventTitle = pi.Event != null ? pi.Event.Title : null,
                    EventRegistrationId = pi.EventRegistrationId,
                    MembershipPlanId = pi.MembershipPlanId,
                    MembershipPlanName = pi.MembershipPlan != null ? pi.MembershipPlan.Name : null,
                    Status = pi.Status,
                    StatusDisplay = pi.Status.ToString(),
                    OrderCode = pi.OrderCode,
                    ExpiresAt = pi.ExpiresAt,
                    CreatedAtUtc = pi.CreatedAtUtc,
                    UpdatedAtUtc = pi.UpdatedAtUtc
                })
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageRequest.Size);
            var hasPrevious = pageRequest.Page > 1;
            var hasNext = pageRequest.Page < totalPages;

            var result = new PagedResult<AdminPaymentIntentDto>(
                paymentIntentDtos,
                pageRequest.Page,
                pageRequest.Size,
                totalCount,
                totalPages,
                hasPrevious,
                hasNext,
                pageRequest.Sort ?? "CreatedAtUtc",
                pageRequest.Desc
            );

            return Result<PagedResult<AdminPaymentIntentDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment intents");
            return Result<PagedResult<AdminPaymentIntentDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get payment intents"));
        }
    }

    public async Task<Result<List<AdminMembershipStatsDto>>> GetMembershipStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var plans = await _context.MembershipPlans
                .AsNoTracking()
                .Select(p => new
                {
                    Plan = p,
                    ActiveSubscribers = _context.UserMemberships.Count(um =>
                        um.MembershipPlanId == p.Id && um.EndDate > now),
                    PurchasesThisMonth = _context.PaymentIntents.Count(pi =>
                        pi.MembershipPlanId == p.Id
                        && pi.Status == PaymentIntentStatus.Succeeded
                        && pi.CreatedAtUtc >= monthStart),
                    TotalRevenue = _context.PaymentIntents
                        .Where(pi => pi.MembershipPlanId == p.Id && pi.Status == PaymentIntentStatus.Succeeded)
                        .Sum(pi => (long?)pi.AmountCents) ?? 0
                })
                .ToListAsync(ct);

            var stats = plans.Select(p => new AdminMembershipStatsDto
            {
                PlanId = p.Plan.Id,
                PlanName = p.Plan.Name,
                PriceCents = (long)(p.Plan.Price * 100),  // Convert decimal Price to cents
                DurationMonths = p.Plan.DurationMonths,
                MonthlyEventLimit = p.Plan.MonthlyEventLimit,
                IsActive = p.Plan.IsActive,
                ActiveSubscribers = p.ActiveSubscribers,
                TotalRevenueCents = p.TotalRevenue,
                PurchasesThisMonth = p.PurchasesThisMonth
            }).ToList();

            return Result<List<AdminMembershipStatsDto>>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting membership stats");
            return Result<List<AdminMembershipStatsDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get membership stats"));
        }
    }

    public async Task<Result<PagedResult<AdminCommunityStatsDto>>> GetCommunityStatsAsync(
        PageRequest pageRequest,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.Communities.AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(c => !c.IsDeleted);
            }

            var totalCount = await query.CountAsync(ct);

            var communities = await query
                .OrderByDescending(c => c.MembersCount)
                .ThenByDescending(c => c.CreatedAtUtc)
                .Skip((pageRequest.Page - 1) * pageRequest.Size)
                .Take(pageRequest.Size)
                .Select(c => new AdminCommunityStatsDto
                {
                    CommunityId = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    School = c.School,
                    IsPublic = c.IsPublic,
                    CachedMembersCount = c.MembersCount,
                    ClubsCount = _context.Clubs.Count(club => club.CommunityId == c.Id && !club.IsDeleted),
                    EventsCount = _context.Events.Count(e => e.CommunityId == c.Id),
                    ActualMembersCount = _context.CommunityMembers.Count(cm => cm.CommunityId == c.Id),
                    GamesCount = _context.CommunityGames.Count(cg => cg.CommunityId == c.Id),
                    CreatedAtUtc = c.CreatedAtUtc,
                    UpdatedAtUtc = c.UpdatedAtUtc,
                    IsDeleted = c.IsDeleted
                })
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageRequest.Size);
            var hasPrevious = pageRequest.Page > 1;
            var hasNext = pageRequest.Page < totalPages;

            var result = new PagedResult<AdminCommunityStatsDto>(
                communities,
                pageRequest.Page,
                pageRequest.Size,
                totalCount,
                totalPages,
                hasPrevious,
                hasNext,
                pageRequest.Sort ?? "CachedMembersCount",
                pageRequest.Desc
            );

            return Result<PagedResult<AdminCommunityStatsDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting community stats");
            return Result<PagedResult<AdminCommunityStatsDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get community stats"));
        }
    }

    public async Task<Result<PagedResult<AdminClubStatsDto>>> GetClubStatsAsync(
        PageRequest pageRequest,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.Clubs
                .AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(c => !c.IsDeleted);
            }

            var totalCount = await query.CountAsync(ct);

            var clubs = await query
                .OrderByDescending(c => c.CreatedAtUtc)
                .Skip((pageRequest.Page - 1) * pageRequest.Size)
                .Take(pageRequest.Size)
                .Select(c => new AdminClubStatsDto
                {
                    ClubId = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsPublic = c.IsPublic,
                    CommunityId = c.CommunityId,
                    CommunityName = c.Community.Name,
                    RoomsCount = _context.Rooms.Count(r => r.ClubId == c.Id && !r.IsDeleted),
                    MembersCount = _context.ClubMembers.Count(cm => cm.ClubId == c.Id),
                    CreatedAtUtc = c.CreatedAtUtc,
                    UpdatedAtUtc = c.UpdatedAtUtc,
                    IsDeleted = c.IsDeleted
                })
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageRequest.Size);
            var hasPrevious = pageRequest.Page > 1;
            var hasNext = pageRequest.Page < totalPages;

            var result = new PagedResult<AdminClubStatsDto>(
                clubs,
                pageRequest.Page,
                pageRequest.Size,
                totalCount,
                totalPages,
                hasPrevious,
                hasNext,
                pageRequest.Sort ?? "CreatedAtUtc",
                pageRequest.Desc
            );

            return Result<PagedResult<AdminClubStatsDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting club stats");
            return Result<PagedResult<AdminClubStatsDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get club stats"));
        }
    }

    public async Task<Result<PagedResult<AdminGameStatsDto>>> GetGameStatsAsync(
        PageRequest pageRequest,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.Games.AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(g => !g.IsDeleted);
            }

            var totalCount = await query.CountAsync(ct);

            var games = await query
                .OrderBy(g => g.Name)
                .Skip((pageRequest.Page - 1) * pageRequest.Size)
                .Take(pageRequest.Size)
                .Select(g => new AdminGameStatsDto
                {
                    GameId = g.Id,
                    Name = g.Name,
                    PlayersCount = _context.UserGames.Count(ug => ug.GameId == g.Id),
                    CommunitiesCount = _context.CommunityGames.Count(cg => cg.GameId == g.Id),
                    CreatedAtUtc = g.CreatedAtUtc,
                    IsDeleted = g.IsDeleted
                })
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageRequest.Size);
            var hasPrevious = pageRequest.Page > 1;
            var hasNext = pageRequest.Page < totalPages;

            var result = new PagedResult<AdminGameStatsDto>(
                games,
                pageRequest.Page,
                pageRequest.Size,
                totalCount,
                totalPages,
                hasPrevious,
                hasNext,
                pageRequest.Sort ?? "Name",
                pageRequest.Desc
            );

            return Result<PagedResult<AdminGameStatsDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting game stats");
            return Result<PagedResult<AdminGameStatsDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get game stats"));
        }
    }

    public async Task<Result<List<AdminRoleStatsDto>>> GetRoleStatsAsync(CancellationToken ct = default)
    {
        try
        {
            // Use GroupJoin to batch load user counts for all roles in one query
            var stats = await _context.Roles
                .AsNoTracking()
                .GroupJoin(
                    _context.UserRoles,
                    role => role.Id,
                    userRole => userRole.RoleId,
                    (role, userRoles) => new AdminRoleStatsDto
                    {
                        RoleId = role.Id,
                        RoleName = role.Name!,
                        UsersCount = userRoles.Count(),
                        CreatedAtUtc = DateTime.UtcNow // Roles don't have CreatedAt in identity
                    })
                .OrderBy(r => r.RoleName)
                .ToListAsync(ct);

            return Result<List<AdminRoleStatsDto>>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role stats");
            return Result<List<AdminRoleStatsDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to get role stats"));
        }
    }
}
