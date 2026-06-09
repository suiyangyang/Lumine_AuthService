# `Lumine.AuthServer` OAuth 2.1 / OpenID Connect + Avalonia Web 改造说明

## 1. 文档目的

本文档用于指导将当前 `Lumine.AuthServer` 从“自定义登录接口 + 直接返回 JWT”的模式，逐步改造成：

- 一个支持第三方系统接入的统一认证中心
- 一个基于 `Avalonia UI` 的跨平台前端项目（当前先落 `Web`）
- 一个支持用户、角色、权限完整管理的后台
- 一个符合 `OAuth 2.1` / `OpenID Connect` 思路的授权中心

本文档同时面向：

- **架构设计人员**：用于明确目标系统边界
- **后端开发 Agent**：用于分阶段实施接口和认证改造
- **前端开发 Agent**：用于实施 `Avalonia Web` 端页面与流程
- **联调 Agent**：用于按步骤执行集成验证

---

## 2. 当前系统现状

根据当前代码与现有文档，`Lumine.AuthServer` 现状如下：

### 2.1 已具备能力

- 已有 `User`、`Role`、`Permission` 三级权限模型
- 已有对应 Repository 和基础权限查询能力
- 已有 `POST /api/Auth/login`
- 登录成功后可签发 JWT
- 已有基础 JWT 校验配置

### 2.2 当前不足

- 登录流程是**业务接口式登录**，不是标准授权协议
- 第三方系统若要接入，只能自己拿账号密码换 token
- 登录页/注册页无法被第三方系统安全唤起并回跳
- JWT 中 claims 较少，无法支撑更完整的身份场景
- 没有 `refresh token`
- 没有授权码、客户端、回调地址、作用域等标准概念
- 没有 `userinfo`、`authorize`、`token`、`logout` 等标准端点
- 密码校验目前仍是明文比对，不适合正式环境

### 2.3 为什么不能继续用“登录后带 token 跳回去”

看起来最直观的做法是：

1. 第三方系统跳到你的登录页
2. 用户登录成功
3. 认证中心把 `token` 直接拼到 URL 上跳回第三方

例如：

```text
https://third-party-app/callback?token=eyJ...
```

这种方式不推荐，核心问题如下：

- **Token 暴露风险高**：URL 会出现在浏览器地址栏、历史记录、代理日志、网关日志中
- **Referer 泄露风险**：从回调页再跳到别的资源时，`Referer` 可能带出完整 URL
- **回调页面脚本可直接拿到 token**：不利于隔离与安全治理
- **无法做标准客户端授权管理**：缺少 `client_id`、`scope`、`state` 等协议概念
- **刷新与撤销机制混乱**：第三方通常会误把 access token 当长期令牌使用
- **无法兼容移动端/桌面端/浏览器端多类客户端**
- **后续接第三方会越来越难**：每个系统都按自定义方式接，运维与审计成本高

因此，应改为：

- 前端只拿到短期授权结果（`code`）
- 由客户端或服务端再去换 token
- 全流程带 `state`、`nonce`、`code_challenge` 等参数做防护

这就是 `OAuth 2.1 / OIDC` 推荐路线。

---

## 3. 技术方案详解：为什么使用“授权码 + PKCE”

## 3.1 核心结论

推荐采用：

- **OAuth 2.1 Authorization Code Flow**
- **Public Client 使用 PKCE**
- **需要用户身份信息时叠加 OpenID Connect**

也就是：

- 第三方系统不直接拿用户密码
- 认证中心不直接把 access token 拼到 URL 回调
- 认证中心先返回一个一次性、短时有效的 `authorization code`
- 客户端再用 `code` 去交换 `token`
- 浏览器类客户端必须带 `PKCE`

---

## 3.2 术语解释

### 1）Authorization Code（授权码）

它是一个：

- 一次性
- 短时有效
- 只能兑换一次 token
- 可以绑定 `client_id`、`redirect_uri`、`code_challenge`

的中间凭证。

它不是最终访问凭证，因此即使在回跳 URL 中出现，也比直接放 `access_token` 安全得多。

### 2）PKCE

`PKCE` 全称是：

> Proof Key for Code Exchange

它的目的，是防止“授权码被截获后被别人拿去换 token”。

做法是：

1. 客户端本地生成一个随机字符串：`code_verifier`
2. 对它做哈希得到：`code_challenge`
3. 发起授权请求时只提交 `code_challenge`
4. 换 token 时再提交原始 `code_verifier`
5. 认证中心验证两者是否匹配

因此，即使攻击者截获了 `code`，没有 `code_verifier`，也无法换出 token。

### 3）OpenID Connect（OIDC）

OAuth 本质是“授权协议”，不是“身份协议”。

如果你的第三方系统不仅要“能访问接口”，还要知道：

- 当前是谁登录了
- 用户名是什么
- 邮箱是什么
- 显示名是什么
- 用户会话是否仍有效

那就应在 OAuth 之上叠加 `OIDC`，提供：

- `openid` scope
- `id_token`
- `userinfo` 端点
- 标准 claims

---

## 3.3 标准流程说明

下面以“第三方 Web 系统调用认证中心登录”为例。

### 步骤 A：第三方系统发起授权请求

第三方将用户浏览器重定向到认证中心：

```http
GET /connect/authorize?
    response_type=code&
    client_id=lumine-erp&
    redirect_uri=https%3A%2F%2Ferp.lumine.com%2Fsignin-oidc&
    scope=openid%20profile%20roles%20permissions&
    state=af2K...&
    nonce=d91A...&
    code_challenge=Jm3X...&
    code_challenge_method=S256
```

参数含义：

- `response_type=code`：表示走授权码模式
- `client_id`：第三方系统身份标识
- `redirect_uri`：登录后回跳地址，必须提前登记白名单
- `scope`：申请的权限范围
- `state`：防 CSRF，用于请求与回调配对
- `nonce`：OIDC 场景下防重放
- `code_challenge`：PKCE 挑战值
- `code_challenge_method=S256`：推荐哈希方式

### 步骤 B：认证中心检查登录态

认证中心收到请求后：

- 如果用户未登录，显示登录页
- 如果用户已登录但未授权该客户端，显示授权确认页
- 如果用户已登录且已授权，可直接进入签发授权码阶段

### 步骤 C：用户登录 / 注册

这时你要的 `Avalonia Web` 登录页、注册页就发挥作用：

- 支持用户名密码登录
- 支持注册
- 支持忘记密码（后续可加）
- 登录成功后恢复原始授权请求上下文

重点：

- 登录页不直接返回 token 给浏览器地址栏
- 登录页只负责完成用户认证
- 真正的授权结果由 `/connect/authorize` 继续处理

### 步骤 D：认证中心签发授权码并回跳

认证中心验证通过后，重定向回：

```text
https://erp.lumine.com/signin-oidc?code=SplxlOBeZQQYbYS6WxSbIA&state=af2K...
```

此时回跳 URL 里只有：

- `code`
- `state`

没有 access token，因此安全性显著更高。

### 步骤 E：第三方用授权码换 token

第三方后台（或受控客户端）调用：

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&
client_id=lumine-erp&
code=SplxlOBeZQQYbYS6WxSbIA&
redirect_uri=https%3A%2F%2Ferp.lumine.com%2Fsignin-oidc&
code_verifier=Q9xA...
```

认证中心验证：

- `code` 是否有效
- 是否过期
- 是否已使用
- `redirect_uri` 是否匹配
- `client_id` 是否匹配
- `code_verifier` 是否与 `code_challenge` 匹配

成功后返回：

```json
{
  "access_token": "...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "...",
  "id_token": "...",
  "scope": "openid profile roles permissions"
}
```

### 步骤 F：第三方拿用户信息

如果第三方需要标准身份信息，可调用：

```http
GET /connect/userinfo
Authorization: Bearer {access_token}
```

返回：

```json
{
  "sub": "user-id",
  "preferred_username": "admin",
  "name": "管理员",
  "email": "admin@lumine.local",
  "role": ["Admin"],
  "permission": ["user.read", "user.write"]
}
```

---

## 3.4 为什么“授权码 + PKCE”比“token 直跳回去”更好

| 维度 | 直接带 token 回跳 | 授权码 + PKCE |
| ------ | ------------------ | -------------- |
| URL 暴露风险 | 高 | 低 |
| 浏览器历史泄露 | 高 | 低 |
| Referer 泄露 | 高 | 低 |
| 可绑定客户端 | 弱 | 强 |
| 可防授权码截获 | 无 | 有 |
| 适配浏览器/桌面端 | 差 | 好 |
| 是否便于标准化接入 | 差 | 好 |
| 是否可扩展 OIDC | 差 | 好 |
| 是否支持作用域授权 | 弱 | 强 |
| 是否利于审计与治理 | 差 | 好 |

结论：

> 若要让第三方系统“拉起登录页/注册页，再安全跳回去”，就应把“回跳结果”定义为 `code`，而不是 `token`。

---

## 4. 目标系统改造蓝图

## 4.1 总体目标

将系统拆成两个协作部分：

### A. `Lumine.AuthServer`

职责：

- 提供认证与授权协议端点
- 提供用户、角色、权限管理 API
- 提供客户端管理 API
- 提供令牌签发与校验能力
- 托管前端静态资源（可选）

### B. `Lumine.AuthPortal`

职责：

- 提供登录页
- 提供注册页
- 提供授权确认页
- 提供用户/角色/权限后台管理页
- 提供个人中心页
- 作为 `Avalonia Web` 前端入口

---

## 4.2 推荐项目结构

建议在 `Lumine_AuthService` 目录下新增：

```text
Lumine_AuthService/
├─ Lumine.AuthServer/
├─ Lumine.AuthPortal/
├─ OAuth2.1-OIDC-Avalonia改造说明.md
├─ 第三方服务接入说明.md
└─ 权限系统检查报告.md
```

后续如需进一步清晰拆层，可扩展为：

```text
Lumine_AuthService/
├─ Lumine.AuthServer/
│  ├─ Api/
│  ├─ Application/
│  ├─ Domain/
│  ├─ Infrastructure/
│  └─ OpenId/
├─ Lumine.AuthPortal/
│  ├─ Views/
│  ├─ ViewModels/
│  ├─ Services/
│  ├─ Models/
│  └─ Assets/
└─ docs/
```

---

## 5. 后端改造范围

## 5.1 新增核心能力

### 1）客户端管理（Client）

新增“第三方应用客户端”概念，至少包含：

- `ClientId`
- `ClientName`
- `ClientSecret`（仅机密客户端）
- `ClientType`（Public / Confidential）
- `RedirectUris`
- `PostLogoutRedirectUris`
- `AllowedScopes`
- `AllowedGrantTypes`
- `RequirePkce`
- `IsActive`
- `CreatedTime`

### 2）授权码存储

新增授权码表或持久化结构，至少包含：

- `Code`
- `ClientId`
- `UserId`
- `RedirectUri`
- `Scopes`
- `CodeChallenge`
- `CodeChallengeMethod`
- `Nonce`
- `ExpiresAt`
- `ConsumedAt`

### 3）Refresh Token 存储

新增刷新令牌表，至少包含：

- `RefreshToken`
- `UserId`
- `ClientId`
- `Scopes`
- `ExpiresAt`
- `RevokedAt`
- `ReplacedByToken`
- `CreatedByIp`

### 4）用户会话 / 授权记录

建议新增：

- 登录会话表
- 用户授权同意记录表（consent）
- 审计日志表

---

## 5.2 新增协议端点

建议逐步引入以下端点：

### 协议端点

- `GET /connect/authorize`
- `POST /connect/token`
- `GET /connect/userinfo`
- `POST /connect/logout`
- `GET /.well-known/openid-configuration`
- `GET /.well-known/jwks.json`（后续）

### 认证业务端点

- `POST /api/auth/register`
- `POST /api/auth/login/password`
- `POST /api/auth/refresh-token`
- `GET /api/auth/me`
- `POST /api/auth/change-password`

### 管理端点

- `GET /api/admin/users`
- `POST /api/admin/users`
- `PUT /api/admin/users/{id}`
- `POST /api/admin/users/{id}/roles`
- `GET /api/admin/roles`
- `POST /api/admin/roles`
- `POST /api/admin/roles/{id}/permissions`
- `GET /api/admin/permissions`
- `GET /api/admin/clients`
- `POST /api/admin/clients`

---

## 5.3 Token 建议内容

### Access Token Claims

建议至少包含：

- `sub`
- `client_id`
- `scope`
- `username`
- `role`
- `permission`
- `iss`
- `aud`
- `iat`
- `exp`

### ID Token Claims

OIDC 场景建议包含：

- `sub`
- `name`
- `preferred_username`
- `email`
- `auth_time`
- `nonce`

---

## 5.4 安全要求

必须逐步完成以下改造：

- 密码改为哈希存储，不允许明文比较
- 客户端回调地址必须白名单校验
- `state` 必须原样回传并校验
- 浏览器类客户端必须启用 `PKCE`
- 授权码必须一次性使用
- `refresh_token` 必须支持撤销
- Access Token 有效期缩短（例如 1 小时）
- Refresh Token 独立失效策略
- 所有授权结果与管理操作写审计日志

---

## 6. 前端 `Avalonia Web` 改造范围

## 6.1 为什么前端适合 `Avalonia`

你的目标是跨平台，而当前先做 Web。`Avalonia` 的优势是：

- 后续可复用 UI / ViewModel 到桌面端
- 可以先以 Browser/WASM 模式交付
- 与 .NET 技术栈一致，适合现有后端团队持续维护

注意：

- 当前阶段建议明确定位为 **管理后台 + 登录门户 Web 端**
- 不建议在第一阶段追求复杂富客户端能力

---

## 6.2 前端页面清单

### 认证门户页面

- `/login`
- `/register`
- `/forgot-password`（可后补）
- `/authorize/consent`
- `/logout-complete`
- `/access-denied`

### 管理后台页面

- `/admin/dashboard`
- `/admin/users`
- `/admin/users/{id}`
- `/admin/roles`
- `/admin/roles/{id}`
- `/admin/permissions`
- `/admin/clients`
- `/admin/audit-logs`

### 个人中心页面

- `/profile`
- `/profile/security`
- `/profile/sessions`

---

## 6.3 前端模块拆分建议

### 模块 A：认证模块

负责：

- 登录
- 注册
- 恢复授权请求上下文
- 显示授权确认页
- 退出登录

### 模块 B：管理后台模块

负责：

- 用户管理
- 角色管理
- 权限管理
- 客户端管理
- 审计日志查看

### 模块 C：授权交互模块

负责：

- 解析授权请求参数
- 维护 `state` / `nonce` / `client_id`
- 确认授权后回调后端继续处理

---

## 7. 第三方系统接入设计

## 7.1 目标场景

第三方系统希望：

- 调起统一登录页
- 必要时调起注册页
- 登录/注册完成后跳回原系统
- 由认证中心统一返回安全凭证

这个目标应通过协议化方式实现，而不是自定义前端跳转协议。

---

## 7.2 第三方接入规范

每个第三方系统接入前，需要在认证中心登记：

- `ClientId`
- 客户端名称
- 回调地址列表
- 允许的作用域
- 是否必须 PKCE
- 是否允许 refresh token
- 登出回调地址

### 示例

```json
{
  "clientId": "lumine-erp",
  "clientName": "Lumine ERP",
  "clientType": "Public",
  "redirectUris": [
    "https://erp.lumine.com/signin-oidc"
  ],
  "postLogoutRedirectUris": [
    "https://erp.lumine.com/signout-callback-oidc"
  ],
  "allowedScopes": [
    "openid",
    "profile",
    "roles",
    "permissions"
  ],
  "requirePkce": true,
  "isActive": true
}
```

---

## 7.3 推荐的作用域（Scopes）

建议初始定义：

- `openid`：启用 OIDC 身份能力
- `profile`：用户名、显示名等基础资料
- `email`：邮箱
- `roles`：角色信息
- `permissions`：权限信息
- `offline_access`：允许签发 refresh token

---

## 8. 分阶段改造建议

## 阶段 1：先把认证服务改造成“可管理”

目标：

- 用户、角色、权限管理 API 完整化
- 密码哈希替换明文对比
- 管理后台基础接口齐备

交付标准：

- 可在后台新增用户、角色、权限
- 可给用户分配角色
- 可给角色分配权限
- 登录仍可继续工作

---

## 阶段 2：引入客户端与授权码模型

目标：

- 新增客户端实体
- 新增授权码存储
- 新增 `authorize` / `token` 基础流程

交付标准：

- 可登记第三方客户端
- 可发起授权码流程
- 可通过 code 换 token

---

## 阶段 3：引入 OIDC 身份能力

目标：

- 新增 `userinfo`
- 新增 `id_token`
- 支持 `openid profile email` 等 scope

交付标准：

- 第三方可获取标准用户身份信息
- 可按 scope 决定 claims 返回内容

---

## 阶段 4：建设 `Avalonia Web` 门户

目标：

- 登录页
- 注册页
- 授权确认页
- 管理后台页

交付标准：

- 第三方可拉起登录
- 用户可登录并回跳
- 管理员可在 Web 后台管用户角色权限

---

## 阶段 5：完善生产级能力

目标：

- 审计日志
- refresh token 轮换
- 登出联动
- 会话管理
- 公钥发现/JWKS

交付标准：

- 支持更规范的第三方接入
- 支持审计与安全追踪

---

## 9. 推荐实施顺序（适合 Agent 逐步执行）

下面给出适合 Agent 使用的改造步骤。每一步都尽量独立、可验证、可提交。

## Step 1：收口当前认证模型

### Step 1 Agent 任务

- 阅读 `Program.cs`、`AuthController.cs`
- 盘点现有 JWT 配置、登录接口、权限实体
- 输出当前认证能力差距清单

### Step 1 产出物

- 差距说明文档
- 当前接口清单
- 当前 claims 清单

### Step 1 验收标准

- 能明确列出当前与目标协议的差异

---

## Step 2：引入密码哈希与用户认证服务

### Step 2 Agent 任务

- 新增密码哈希服务接口与实现
- 将登录逻辑从 Controller 中抽离到应用服务
- 替换明文密码比对
- 补注册接口

### Step 2 涉及范围

- `Application/`
- `Infrastructure/Services/`
- `Controllers/`

### Step 2 验收标准

- 新老用户可按新规则认证
- 不再出现明文比对逻辑

---

## Step 3：补齐用户/角色/权限管理 API

### Step 3 Agent 任务

- 新增用户 CRUD
- 新增角色 CRUD
- 新增权限查询/分配接口
- 新增用户分配角色接口
- 新增角色分配权限接口

### Step 3 验收标准

- 后台管理所需 API 可用
- 具备分页、查询、详情、分配能力

---

## Step 4：新增 Client 实体与管理 API

### Step 4 Agent 任务

- 新增第三方客户端实体与表
- 新增客户端 Repository
- 新增客户端管理 Controller
- 支持登记回调地址、作用域、客户端类型

### Step 4 验收标准

- 可创建与查询第三方客户端
- 可对白名单回调地址做配置

---

## Step 5：实现授权码模型

### Step 5 Agent 任务

- 新增 `AuthorizationCode` 实体
- 增加持久化表与索引
- 实现授权码签发、消费、过期、一次性使用

### Step 5 验收标准

- 授权码只能兑换一次
- 过期码不可用

---

## Step 6：实现 `/connect/authorize`

### Step 6 Agent 任务

- 解析 `client_id`、`redirect_uri`、`scope`、`state`、`nonce`
- 校验客户端是否合法
- 校验回调地址是否命中白名单
- 若未登录则跳登录页
- 若已登录则继续授权流程

### Step 6 验收标准

- 非法客户端与回调地址请求会被拒绝
- 合法请求可进入授权确认流程

---

## Step 7：实现 `/connect/token`

### Step 7 Agent 任务

- 支持 `grant_type=authorization_code`
- 校验 `code`、`redirect_uri`、`client_id`
- 校验 `code_verifier`
- 签发 `access_token`、`refresh_token`、`id_token`

### Step 7 验收标准

- 正常 code 可换 token
- 错误 verifier 不能换 token
- 已消费 code 不能重复兑换

---

## Step 8：实现 `userinfo` 与 OIDC claims

### Step 8 Agent 任务

- 增加 `openid/profile/email/roles/permissions` scope 处理
- 根据 scope 动态返回 claims
- 实现 `/connect/userinfo`

### Step 8 验收标准

- 第三方能按 scope 获取用户信息
- 未授权 scope 不返回对应 claims

---

## Step 9：建设 `Avalonia Web` 项目骨架

### Step 9 Agent 任务

- 新建 `Lumine.AuthPortal`
- 建立登录页、注册页、授权确认页框架
- 建立后台布局、导航和鉴权守卫
- 抽象 API 调用层

### Step 9 验收标准

- Web 端可启动
- 基础页面可访问
- 能调用后端 API

---

## Step 10：接入后台管理页面

### Step 10 Agent 任务

- 实现用户列表/编辑页
- 实现角色列表/权限分配页
- 实现客户端管理页
- 实现审计日志页占位

### Step 10 验收标准

- 管理员可通过 Web 端进行基础管理

---

## Step 11：实现第三方回跳全链路联调

### Step 11 Agent 任务

- 准备一个测试客户端
- 走一遍完整 `authorize -> login -> consent -> callback -> token -> userinfo`
- 校验 `state`、`nonce`、`PKCE`、`scope`

### Step 11 验收标准

- 全链路成功
- 异常路径也能正确报错

---

## Step 12：上线前安全收尾

### Step 12 Agent 任务

- 加访问日志与审计日志
- 加 refresh token 撤销/轮换
- 检查令牌有效期策略
- 检查 Cookie / CORS / HTTPS / SameSite

### Step 12 验收标准

- 达到可测试环境部署标准

---

## 10. Agent 执行注意事项

### 1）优先级原则

Agent 应按以下优先级执行：

1. 先保证认证模型正确
2. 再保证协议流正确
3. 再建设前端页面
4. 最后做体验增强与审计能力

### 2）每一步都应独立可验证

每个 Agent 任务结束后，至少要给出：

- 改动文件列表
- 新增接口列表
- 数据模型变化
- 验证方式
- 下一步建议

### 3）避免一步到位式大改

不要一次性同时重写：

- 登录逻辑
- 用户管理
- 协议端点
- 前端门户
- 部署脚本

推荐按阶段提交，便于回滚与联调。

---

## 11. 技术选型建议

## 11.1 协议实现方式

有两条路：

### 路线 A：基于成熟库实现（推荐）

例如引入 OpenID / OAuth 服务器相关成熟组件，减少协议细节手工实现。

优点：

- 协议正确性更强
- 少踩坑
- 后续支持 OIDC discovery、JWKS 更容易

缺点：

- 需要适配现有项目结构
- 初次引入成本略高

### 路线 B：手工实现最小闭环

自己实现：

- authorize
- token
- userinfo
- code / refresh token 存储与校验

优点：

- 完全可控
- 适合最小可用版本落地

缺点：

- 协议细节容易遗漏
- 后续维护成本高

### 推荐结论

- **MVP 阶段**：可手工实现最小授权码 + PKCE 闭环
- **生产阶段**：建议逐步迁移到成熟 OIDC 服务能力

---

## 11.2 部署建议

### 推荐方式：同域集成部署

例如：

- `https://auth.lumine.com/api/*` → 后端 API
- `https://auth.lumine.com/app/*` → Avalonia Web 静态资源

优点：

- 避免跨域复杂度
- 登录态与回调处理更直观
- 运维更简单

### 次优方式：前后端分域部署

例如：

- `https://auth-api.lumine.com`
- `https://auth-web.lumine.com`

这种方式需要更仔细处理：

- CORS
- Cookie
- SameSite
- 回调域名一致性

---

## 12. 最终改造目标总结

当改造完成后，系统应具备：

- 一个统一认证中心
- 一个 `Avalonia Web` 管理后台
- 用户、角色、权限完整管理能力
- 第三方系统拉起登录/注册并安全回跳能力
- 基于 `authorization code + PKCE` 的标准授权流
- 基于 `OIDC` 的用户身份获取能力

换句话说，最终第三方系统拿到的应当是：

- 安全回跳得到的 `code`
- 再通过标准接口兑换到 `token`
- 再按 scope 获取用户资料

而不是：

- 登录页直接把 `token` 拼进 URL 回跳

---

## 13. 下一步建议

推荐你的实际推进顺序如下：

1. 先补后台管理 API 与密码哈希
2. 再引入 Client / AuthorizationCode / RefreshToken 模型
3. 再实现 `authorize + token + userinfo`
4. 再建 `Avalonia Web` 门户
5. 最后做第三方联调与生产加固

如果后续要继续推进，建议下一份文档直接进入：

- **数据库表设计**
- **API 合同清单**
- **Avalonia 页面与路由清单**
- **Agent 分任务清单（逐步可执行）**
