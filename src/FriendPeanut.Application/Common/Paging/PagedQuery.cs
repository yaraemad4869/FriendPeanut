namespace FriendPeanut.Application.Common.Paging;

public sealed record PagedQuery(int Page = 1, int PageSize = 25)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize switch { < 1 => 25, > 100 => 100, _ => PageSize };
}