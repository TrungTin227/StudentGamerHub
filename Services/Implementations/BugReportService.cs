using FluentValidation;
using DTOs.Bugs;
using Services.Common.Results;

namespace Services.Implementations;

/// <summary>
/// Service implementation for managing bug reports.
/// </summary>
public sealed class BugReportService : IBugReportService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IBugReportRepository _bugReports;
    private readonly IValidator<BugReportCreateRequest> _createValidator;
    private readonly IValidator<BugReportStatusPatchRequest> _statusValidator;

    public BugReportService(
        IGenericUnitOfWork uow,
        IBugReportRepository bugReports,
        IValidator<BugReportCreateRequest> createValidator,
        IValidator<BugReportStatusPatchRequest> statusValidator)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _bugReports = bugReports ?? throw new ArgumentNullException(nameof(bugReports));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _statusValidator = statusValidator ?? throw new ArgumentNullException(nameof(statusValidator));
    }

    public async Task<Result<BugReportDto>> CreateAsync(
        Guid userId,
        BugReportCreateRequest request,
        CancellationToken ct = default)
    {
        return await _createValidator.ValidateToResultAsync(request, ct)
            .BindAsync(async _ => await _uow.ExecuteTransactionAsync(async innerCt =>
            {
                var bugReport = new BugReport
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Category = request.Category.Trim(),
                    Description = request.Description.Trim(),
                    ImageUrl = request.ImageUrl?.Trim(),
                    Status = BugStatus.Open,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = userId
                };

                await _bugReports.AddAsync(bugReport, innerCt);
                await _uow.SaveChangesAsync(innerCt);

                return Result<BugReportDto>.Success(bugReport.ToDto());
            }, ct: ct))
            .ConfigureAwait(false);
    }

    public async Task<Result<BugReportDto>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var bugReport = await _bugReports.GetByIdAsync(id, asNoTracking: true, ct: ct);

        return Result<BugReportDto>.FromNullable(
            bugReport?.ToDto(),
            new Error(Error.Codes.NotFound, "Bug report not found."));
    }

    public async Task<Result<PagedResult<BugReportDto>>> GetByUserIdAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken ct = default)
    {
        var pagedReports = await _bugReports.GetByUserIdPagedAsync(userId, paging, ct);

        var items = pagedReports.Items.Select(br => br.ToDto()).ToList();
        var result = new PagedResult<BugReportDto>(
            items,
            pagedReports.Page,
            pagedReports.Size,
            pagedReports.TotalCount,
            pagedReports.TotalPages,
            pagedReports.HasPrevious,
            pagedReports.HasNext,
            pagedReports.Sort,
            pagedReports.Desc);

        return Result<PagedResult<BugReportDto>>.Success(result);
    }

    public async Task<Result<PagedResult<BugReportDto>>> ListAsync(
        PageRequest paging,
        CancellationToken ct = default)
    {
        var pagedReports = await _bugReports.GetPagedAsync(
            paging,
            orderBy: q => q.OrderByDescending(b => b.CreatedAtUtc),
            asNoTracking: true,
            ct: ct);

        var items = pagedReports.Items.Select(br => br.ToDto()).ToList();
        var result = new PagedResult<BugReportDto>(
            items,
            pagedReports.Page,
            pagedReports.Size,
            pagedReports.TotalCount,
            pagedReports.TotalPages,
            pagedReports.HasPrevious,
            pagedReports.HasNext,
            pagedReports.Sort,
            pagedReports.Desc);

        return Result<PagedResult<BugReportDto>>.Success(result);
    }

    public async Task<Result<PagedResult<BugReportDto>>> GetByStatusAsync(
        string status,
        PageRequest paging,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<BugStatus>(status, ignoreCase: true, out _))
        {
            return Result<PagedResult<BugReportDto>>.Failure(
                new Error(Error.Codes.Validation, $"Invalid status: {status}"));
        }

        var pagedReports = await _bugReports.GetByStatusPagedAsync(status, paging, ct);

        var items = pagedReports.Items.Select(br => br.ToDto()).ToList();
        var result = new PagedResult<BugReportDto>(
            items,
            pagedReports.Page,
            pagedReports.Size,
            pagedReports.TotalCount,
            pagedReports.TotalPages,
            pagedReports.HasPrevious,
            pagedReports.HasNext,
            pagedReports.Sort,
            pagedReports.Desc);

        return Result<PagedResult<BugReportDto>>.Success(result);
    }

    public async Task<Result<BugReportDto>> UpdateStatusAsync(
        Guid id,
        BugReportStatusPatchRequest request,
        CancellationToken ct = default)
    {
        return await _statusValidator.ValidateToResultAsync(request, ct)
            .BindAsync(async _ => await _uow.ExecuteTransactionAsync(async innerCt =>
            {
                var bugReport = await _bugReports.GetByIdAsync(id, asNoTracking: false, ct: innerCt);

                if (bugReport is null)
                {
                    return Result<BugReportDto>.Failure(
                        new Error(Error.Codes.NotFound, "Bug report not found."));
                }

                if (!Enum.TryParse<BugStatus>(request.Status, ignoreCase: true, out var newStatus))
                {
                    return Result<BugReportDto>.Failure(
                        new Error(Error.Codes.Validation, $"Invalid status: {request.Status}"));
                }

                bugReport.Status = newStatus;
                bugReport.UpdatedAtUtc = DateTime.UtcNow;

                await _bugReports.UpdateAsync(bugReport, innerCt);
                await _uow.SaveChangesAsync(innerCt);

                return Result<BugReportDto>.Success(bugReport.ToDto());
            }, ct: ct))
            .ConfigureAwait(false);
    }
}

// Mapper extension
file static class BugReportMappers
{
    public static BugReportDto ToDto(this BugReport bugReport)
    {
        ArgumentNullException.ThrowIfNull(bugReport);

        return new BugReportDto(
            Id: bugReport.Id,
            UserId: bugReport.UserId,
            Category: bugReport.Category,
            Description: bugReport.Description,
            ImageUrl: bugReport.ImageUrl,
            Status: bugReport.Status.ToString(),
            CreatedAtUtc: bugReport.CreatedAtUtc);
    }
}
