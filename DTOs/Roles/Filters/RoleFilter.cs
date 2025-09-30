namespace DTOs.Roles.Filters
{
    public sealed class RoleFilter
    {
        public string? Keyword { get; set; }                 // lọc theo Name/Description

        public DeletedFilter Deleted { get; set; } = DeletedFilter.OnlyActive;

        public DateTime? CreatedFromUtc { get; set; }
        public DateTime? CreatedToUtc { get; set; }

        // (tuỳ chọn) tiện chuẩn hoá input
        public void Normalize()
        {
            if (!string.IsNullOrWhiteSpace(Keyword))
                Keyword = Keyword.Trim();

            if (CreatedFromUtc.HasValue && CreatedToUtc.HasValue &&
                CreatedFromUtc > CreatedToUtc)
            {
                (CreatedFromUtc, CreatedToUtc) = (CreatedToUtc, CreatedFromUtc);
            }
        }
    }

}
