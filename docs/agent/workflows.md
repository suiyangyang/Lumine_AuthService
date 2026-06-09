# 开发工作流

本文档记录后续 agent 常用命令和验证路径。命令默认在仓库根目录执行。

## 基础检查

查看工作区状态：

```powershell
git status --short
```

构建整个解决方案：

```powershell
dotnet build .\Lumine_AuthService.sln
```

构建后端：

```powershell
dotnet build .\Lumine.AuthServer\Lumine.AuthServer.csproj
```

构建 Avalonia Browser 前端：

```powershell
dotnet build .\Lumine.AuthPortal\Lumine.AuthPortal.Browser\Lumine.AuthPortal.Browser.csproj
```

## 本地数据库

首次运行前复制环境变量模板：

```powershell
Copy-Item .env.example .env
```

一键初始化 MySQL、本地迁移和种子数据：

```powershell
.\scripts\init-local-db.ps1
```

只启动数据库容器：

```powershell
.\scripts\init-local-db.ps1 -SkipBootstrap
```

更多说明见 `docs/deployment/LocalDb.md`。

## 运行后端

```powershell
dotnet run --project .\Lumine.AuthServer\Lumine.AuthServer.csproj
```

开发环境默认配置见 `Lumine.AuthServer/appsettings.Development.json`。Swagger 和具体端口以运行输出或 `Properties/launchSettings.json` 为准。

## 运行前端

Browser：

```powershell
dotnet run --project .\Lumine.AuthPortal\Lumine.AuthPortal.Browser\Lumine.AuthPortal.Browser.csproj
```

Desktop：

```powershell
dotnet run --project .\Lumine.AuthPortal\Lumine.AuthPortal.Desktop\Lumine.AuthPortal.Desktop.csproj
```

更多说明见 `Lumine.AuthPortal/README.md`。

## 数据库迁移

涉及实体或 `AuthDbContext` 变化时，先确认是否需要 migration。常用命令：

```powershell
dotnet ef migrations add <MigrationName> --project .\Lumine.AuthServer\Lumine.AuthServer.csproj
```

```powershell
dotnet ef database update --project .\Lumine.AuthServer\Lumine.AuthServer.csproj
```

如果当前环境没有 `dotnet-ef`，先确认用户是否允许安装或使用已有工具链，不要静默改全局环境。

## 文档检查

新增或移动 Markdown 后检查链接：

```powershell
rg -n "\]\(" README.md docs -g "*.md"
```

修改接口、权限、认证、部署、本地数据库或前端菜单后，至少检查对应文档：

- `docs/api/管理后台基础接口说明.md`
- `docs/auth/OAuth2.1-OIDC-Avalonia改造说明.md`
- `docs/deployment/Deployment.md`
- `docs/deployment/LocalDb.md`
- `docs/permissions/前端菜单权限清单.md`

## 收尾

完成任务前：

1. 运行和改动风险匹配的构建或检查命令。
2. 查看 `git status --short`。
3. 明确说明是否有未验证项。
4. 不处理与任务无关的用户已有修改。
