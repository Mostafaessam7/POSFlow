namespace PosFlow.Application.Common;

public static class Paging
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize) Clamp(
        int page,
        int pageSize)
    {
        var clampedPage = page < 1 ? 1 : page;

        var clampedPageSize = pageSize switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize
        };

        return (clampedPage, clampedPageSize);
    }
}
