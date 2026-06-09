namespace Lumine.AuthServer.Application.DTOs
{
    public class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
        public int TotalCount { get; init; }
        public int PageIndex { get; init; }
        public int PageSize { get; init; }
    }
}
