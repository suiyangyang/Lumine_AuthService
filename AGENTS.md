# AGENTS

本文件是当前目录下 coding agent / vibe coding 的第一入口。开始任何改动前，先阅读本文件，再按任务类型阅读 `docs/agent/` 中的细分文档。

## 必读顺序

1. `README.md`：了解项目入口和用户文档索引。
2. `docs/agent/README.md`：了解 agent 文档地图。
3. `docs/agent/architecture.md`：涉及结构、认证、权限、数据库、前端联动时必须阅读。
4. `docs/agent/conventions.md`：涉及代码修改、命名、分层、文档维护时必须阅读。
5. `docs/agent/workflows.md`：涉及构建、运行、数据库初始化、部署验证时必须阅读。

## 工作边界

- 尊重现有分层：`Lumine.AuthServer` 负责认证授权 API、权限、OIDC、EF Core 数据访问；`Lumine.AuthPortal` 负责 Avalonia 前端；`docs/` 负责项目文档。
- 不要随意重写 `Program.cs`、认证中间件、权限处理器、EF Core migration、OIDC token/authorization code 逻辑；这些区域需要先读架构文档和相关业务文档。
- 不要提交 `.env`、`.vs/`、`bin/`、`obj/`、`artifacts/`、本地日志和发布产物。
- 如果修改接口、权限、OIDC、部署或本地数据库流程，必须同步更新对应文档。
- 如果发现用户已有未提交修改，先识别是否和任务相关；不要回滚用户改动。

## 常用验证

在仓库根目录执行：

```powershell
dotnet build .\Lumine_AuthService.sln
```

本地数据库初始化参考：

```powershell
.\scripts\init-local-db.ps1
```

仅启动数据库：

```powershell
.\scripts\init-local-db.ps1 -SkipBootstrap
```

## 文档同步规则

- 面向使用者的说明放入 `docs/api`、`docs/auth`、`docs/deployment`、`docs/integrations`、`docs/permissions`。
- 面向 agent 的维护规则放入 `docs/agent`。
- 根目录只保留入口级文档，例如 `README.md` 和 `AGENTS.md`。
- 新增或移动文档后，更新 `README.md` 和 `docs/README.md` 的链接。
