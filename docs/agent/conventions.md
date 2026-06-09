# 代码与文档约定

本文档描述后续 agent 修改项目时应遵守的约定。

## 总体原则

- 优先延续现有目录、命名和分层，不为单个小需求引入新的架构层。
- 修改认证、授权、OIDC、数据库迁移和密码相关代码时，先确认调用链和文档约束。
- 面向用户的行为变化必须更新文档；仅内部重构通常只需更新 agent 文档或不更新文档。
- 不提交本地环境、IDE、构建输出和发布产物。

## 后端约定

- Controller 保持薄入口：校验请求、调用服务或仓储、返回清晰状态码。
- 业务 DTO 放在 `Application/DTOs`。
- 实体放在 `Domain/Entities`，仓储接口放在 `Domain/Abstractions`。
- EF Core 查询和持久化实现放在 `Infrastructure/Repositories`。
- 认证授权相关基础设施放在 `Api/Auth` 或 `Infrastructure/Services`，不要散落到 Controller 中。
- 新增可配置项时优先使用 options 类和 `appsettings.*.json`，不要硬编码环境差异。

## 权限约定

- 权限编码使用小写点分风格：`module.action`。
- 读取类权限使用 `.view`，写入、分配、删除等变更类权限使用 `.manage`，除非已有模块存在更具体规则。
- 新增后台接口时检查是否需要 `[Permission(...)]`。
- 修改权限集合时同步：
  - `Infrastructure/Services/AuthSeedOptions.cs`
  - `docs/api/管理后台基础接口说明.md`
  - `docs/permissions/前端菜单权限清单.md`
  - 前端导航和按钮权限守卫

## 数据库约定

- 修改实体关系、字段、索引、级联删除时，检查 `AuthDbContext` 和 migrations。
- 不要手写生产数据库变更说明来代替 EF Core migration。
- 本地数据库流程以 `docs/deployment/LocalDb.md` 和 `scripts/init-local-db.ps1` 为准。

## 前端约定

- `Lumine.AuthPortal` 使用 Avalonia 和 CommunityToolkit.Mvvm。
- ViewModel 中已有的会话、导航、权限守卫模式应优先复用。
- API 调用统一通过 `PortalApiClient`，不要在页面或 ViewModel 中复制 HTTP 细节。
- 前端新增页面或按钮权限时，同步更新 `docs/permissions/前端菜单权限清单.md`。

## 文档约定

- `README.md`：项目入口和高频链接。
- `AGENTS.md`：agent 第一入口和硬性维护规则。
- `docs/README.md`：用户文档索引。
- `docs/agent/README.md`：agent 文档索引。
- `docs/deployment`：部署、本地环境、Docker、数据库启动。
- `docs/api`：接口说明。
- `docs/auth`：OAuth2.1、OIDC、认证流程。
- `docs/integrations`：第三方接入。
- `docs/permissions`：权限模型、菜单和权限清单。

新增文档时，文件名可以保留中文，但目录名保持英文小写，便于跨平台脚本和链接维护。
