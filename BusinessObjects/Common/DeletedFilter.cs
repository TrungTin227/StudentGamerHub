namespace BusinessObjects.Common
{
    public enum DeletedFilter
    {
        OnlyActive = 0,   // mặc định: chỉ bản ghi chưa bị xóa (tôn trọng Global Query Filter)
        OnlyDeleted = 1,  // chỉ bản ghi đã xóa mềm
        All = 2   // cả hai (bỏ Global Query Filter)
    }
}
