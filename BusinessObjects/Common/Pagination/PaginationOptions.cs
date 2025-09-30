namespace BusinessObjects.Common.Pagination
{
    public static class PaginationOptions
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 200;
        public const string DefaultSort = "Id"; // sẽ fallback nếu không truyền sort
    }

    public sealed record PageRequest(
        int Page = 1,
        int Size = PaginationOptions.DefaultPageSize,
        string? Sort = null,
        bool Desc = false)
    {
        public int PageSafe => Page < 1 ? 1 : Page;
        public int SizeSafe => Math.Clamp(Size, 1, PaginationOptions.MaxPageSize);
        public string SortSafe => string.IsNullOrWhiteSpace(Sort) ? PaginationOptions.DefaultSort : Sort!;
    }

    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items,
        int Page,
        int Size,
        int TotalCount,
        int TotalPages,
        bool HasPrevious,
        bool HasNext,
        string Sort,
        bool Desc);

    public enum CursorDirection { Next = 0, Prev = 1 }

    public sealed record CursorRequest(
        string? Cursor = null,                // token Base64
        CursorDirection Direction = CursorDirection.Next,
        int Size = PaginationOptions.DefaultPageSize,
        string? Sort = null,                  // tên property key (ví dụ "Id", "CreatedAtUtc")
        bool Desc = false)
    {
        public int SizeSafe => Math.Clamp(Size, 1, PaginationOptions.MaxPageSize);
        public string SortSafe => string.IsNullOrWhiteSpace(Sort) ? PaginationOptions.DefaultSort : Sort!;
    }

    public sealed record CursorPageResult<T>(
        IReadOnlyList<T> Items,
        string? NextCursor,                   // token để lấy trang kế
        string? PrevCursor,                   // token để quay lại
        int Size,
        string Sort,
        bool Desc);
}
