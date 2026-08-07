# 架构维护说明

本文档给后续 agent 快速建立项目心智模型。具体接口和业务细节以 `docs/` 下对应主题文档与代码为准。

## 解决方案结构

```text
Lumine_AuthService/
├─ Lumine.AuthServer/
├─ Lumine.AuthPortal/
├─ Lumine.AuthService/
├─ docs/
├─ scripts/
├─ docker-compose.localdb.yml
├─ global.json
└─ Lumine_AuthService.sln
```

## 技术基线

- SDK：`global.json` 固定为 .NET SDK `10.0.300`，`rollForward` 为 `latestFeature`。
- 目标框架：主要项目使用 `net10.0`。
- 后端：`Lumine.AuthServer` 是 ASP.NET Core Web API。
- 数据访问：`AuthDbContext` 使用 EF Core，MySQL Provider 为 Pomelo。
- 前端：`Lumine.AuthPortal` 是 Avalonia UI，共享项目配套 Browser 和 Desktop 启动项目。

## 后端边界

`Lumine.AuthServer` 当前承担这些职责：

- 登录、注册、用户信息和后台管理 API。
- 用户、角色、权限三级权限模型。
- `PortalMenus` 持久化后台导航标题、分组、顺序、启停与权限绑定；Key、路由、图标和实现状态由 `PortalMenuCatalog` 注册。
- OIDC 客户端、授权码、刷新令牌和 token 相关服务。
- 用户 API-KEY 的生成、完整值与哈希持久化、删除和按 `client_id` 换取 access token。
- AuthPortal 登录 Cookie 会话，以及第三方系统所需的当前用户权限读取和实时权限检查接口。
- EF Core migration、数据库初始化和种子数据。
- Swagger 权限说明和统一的 401/403 响应处理。
- 构建或发布时把 `Lumine.AuthPortal.Browser` 的前端资源整理到服务端 `wwwroot`。

推荐保持当前分层：

- `Controllers/`：HTTP API 入口，处理请求响应、状态码和权限特性。
- `Api/Auth/`：授权策略、权限特性、权限处理器、Swagger 权限展示。
- `Application/`：DTO、命令、应用服务接口和 handler。
- `Domain/`：实体和仓储接口。
- `Infrastructure/`：EF Core DbContext、仓储实现、密码、OIDC、种子数据等基础设施。
- `Migrations/`：EF Core migration 和模型快照。

## 权限模型

权限链路是：

```text
User -> UserRole -> Role -> RolePermission -> Permission
```

维护注意：

- 新增受保护接口时，优先使用已有权限编码风格，例如 `users.view`、`users.manage`。
- 新增权限后，同步检查 `AuthSeedOptions`、接口权限标注、Swagger 显示、前端菜单权限和文档。
- 新增后台页面时同步更新 `PortalMenuCatalog`、`MainViewModel` 页面工厂和图标注册；启动种子会补齐缺失菜单，但不会覆盖管理员已经保存的可维护字段。
- 用户文档位置：
  - `docs/api/管理后台基础接口说明.md`
  - `docs/permissions/前端菜单权限清单.md`
  - `docs/permissions/权限系统检查报告.md`

## OIDC 边界

OIDC 相关代码主要在：

- `Controllers/OidcController.cs`
- `Controllers/OidcDiscoveryController.cs`
- `Infrastructure/Services/OidcService.cs`
- `Infrastructure/Services/OidcOptions.cs`
- `Infrastructure/Services/OidcScopes.cs`
- `Domain/Entities/OidcClient.cs`
- `Domain/Entities/AuthorizationCode.cs`
- `Domain/Entities/RefreshToken.cs`

第三方权限复用入口：

- `GET /api/auth/me` 与 `GET /api/auth/me/permissions`：从数据库读取当前用户的实时角色、权限。
- `POST /api/auth/check-permission`：按当前 Bearer Token 实时检查单个权限。

API-KEY 入口：

- `GET/POST/DELETE /api/api-keys`：当前用户管理自己的 API-KEY，列表接口返回可复制的完整 key。
- `POST /api/auth/api-key/token`：携带 `apiKey` 和 `clientId` 换取与简化登录一致的 access token；当前支持的 API-KEY 客户端为 `labchrom_knowledge`。

维护注意：

- 修改授权码、token、scope、redirect uri、client secret 逻辑前，先读 `docs/auth/OAuth2.1-OIDC-Avalonia改造说明.md`。
- 不要把演示客户端、签名凭据、scope 和 token 有效期改成隐式规则；配置或种子数据需要可追踪。

## 前端边界

`Lumine.AuthPortal` 负责后台壳、登录注册、权限守卫、用户/角色/权限/客户端管理页面和 OIDC 联调页面。

维护注意：

- API DTO 或响应结构变化时，同步更新 `Lumine.AuthPortal/Lumine.AuthPortal/Models` 和 `PortalApiClient`。
- 菜单、页面、按钮可见性应跟权限编码保持一致。
- 左侧导航的标题、分组、顺序、启停和权限绑定来自 `GET /api/menus`；不要再新增一套独立的静态显示配置。Key、路由、图标和页面实现状态仍以代码注册为准。
- Browser 默认后端地址和运行方式参考 `Lumine.AuthPortal/README.md`。

## ReportMind 集成边界

`ReportMind` 是独立仓库和独立容器，使用已注册的 `reportmind` OIDC 客户端调用 `Lumine.AuthServer` 的 `POST /api/auth/login`；ReportMind Server 不保存员工密码或用户列表，使用 Lumine JWKS 验证 JWT 的 issuer、audience、签名和有效期，并通过 `reportmind.test`、`reportmind.reports.upload` 权限保护测试和上传接口。种子用户、角色和客户端由 `SeedData:Users`、`SeedData:Roles`、`SeedData:Clients` 配置；完整 Docker 与联调说明见 `docs/integrations/ReportMind接入与Docker部署.md`。

## 文档边界

- 用户/部署/接口文档放在 `docs/` 的业务分类目录。
- agent 维护规则放在 `docs/agent/`。
- 根目录只保留 `README.md` 和 `AGENTS.md` 这种入口文档。
