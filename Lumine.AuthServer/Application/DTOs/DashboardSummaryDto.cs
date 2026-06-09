namespace Lumine.AuthServer.Application.DTOs
{
    public class DashboardSummaryDto
    {
        public DashboardMetricDto Users { get; set; } = new();
        public DashboardMetricDto Roles { get; set; } = new();
        public DashboardMetricDto Permissions { get; set; } = new();
        public DashboardMetricDto Clients { get; set; } = new();
        public List<DashboardTrendPointDto> LoginTrend { get; set; } = new();
        public List<DashboardTrendPointDto> TokenIssueTrend { get; set; } = new();
        public List<RecentLoginUserDto> RecentLoginUsers { get; set; } = new();
        public DateTime GeneratedAtUtc { get; set; }
    }

    public class DashboardMetricDto
    {
        public string Title { get; set; } = string.Empty;
        public int? Value { get; set; }
        public bool IsVisible { get; set; }
        public string? Description { get; set; }
    }

    public class DashboardTrendPointDto
    {
        public DateTime Date { get; set; }
        public int Value { get; set; }
    }

    public class RecentLoginUserDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? ClientId { get; set; }
        public string? ClientName { get; set; }
        public DateTime LoginAtUtc { get; set; }
    }
}