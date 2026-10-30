using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lumine.AuthServer.Infrastructure;

// 设计时仅构造模型，不启动 WebHost、不读取 .env、不连接数据库或执行迁移/seed。
public sealed class AuthDbContextDesignFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseMySql(
            "Server=localhost;Database=design_only;User ID=design_only;Password=design_only",
            new MySqlServerVersion(new Version(9, 4, 0))).Options;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<AuthDbContext>());
        return new AuthDbContext(options, services.BuildServiceProvider().GetRequiredService<IMediator>());
    }
}
