# Lumine AuthService OIDC 当前实现说明

> 状态：Current
> 更新时间：2026-06-09
> 适用范围：`Lumine.AuthServer` 的 OIDC 端点与 `Lumine.AuthPortal` 的联调能力

本文档描述当前仓库已经实现的认证与 OIDC 能力，不再记录历史改造计划。

## 摘要

- 当前服务已提供 `/.well-known/openid-configuration`、`/jwks.json`、`/connect/authorize`、`/connect/token`、`/connect/userinfo`。
- 当前授权流以 `authorization code + PKCE` 为主，适配默认公共客户端 `lumine-demo-client`。
- `POST /api/auth/login` 仍保留，供后台登录和简化联调用途。
- `Lumine.AuthPortal` 已包含登录、注册、授权确认、客户端管理、OIDC Discovery 与授权码联调页面。
- 服务会签发 `access_token`、`id_token` 和 `refresh_token`，但 `token` 端点当前只接受 `authorization_code`，尚未开放 `refresh_token` 续期。

## 当前端点

| 端点 | 方法 | 说明 |
| --- | --- | --- |
| `/.well-known/openid-configuration` | `GET` | 返回 issuer、端点地址、支持的 scope 和 claims |
| `/jwks.json` | `GET` | 返回当前签名公钥 |
| `/connect/authorize` | `GET` | 发起授权请求，支持授权确认与回跳 |
| `/connect/token` | `POST` | 用授权码换取 token |
| `/connect/userinfo` | `GET`/`POST` | 使用 access token 读取用户信息 |
| `/api/auth/login` | `POST` | 后台登录与简化联调入口 |
| `/api/auth/register` | `POST` | 注册用户 |

## 当前支持的核心能力

### 1. 登录与后台会话

`POST /api/auth/login` 返回：

- `access_token`
- `id_token`
- `token_type`
- `expires_in`
- `scope`
- 当前用户基础信息、角色与权限摘要

这个接口适合：

- Portal 后台登录
- Swagger 调试
- 不走标准授权码跳转时的开发联调

### 2. 授权码 + PKCE

`/connect/authorize` 当前要求：

- `response_type=code`
- `client_id`
- `redirect_uri`
- 客户端开启 PKCE 时提供 `code_challenge`
- `code_challenge_method` 支持 `S256` 与 `plain`

授权请求首次命中时，会返回一个“可继续确认授权”的响应；带 `consent=approve` 再次调用后才会签发授权码。

### 3. Token 交换

`/connect/token` 当前只支持：

- `grant_type=authorization_code`

成功响应会返回：

- `access_token`
- `id_token`
- `refresh_token`
- `expires_in`
- `scope`

注意：

- 当前代码会生成并保存 `refresh_token`
- 但 `grant_type=refresh_token` 续期逻辑尚未开放

### 4. UserInfo

`/connect/userinfo` 需要：

- 已登录 Bearer Token
- Token 中包含 `openid` scope

当前可返回的典型 claims 包括：

- `sub`
- `preferred_username`
- `name`
- `nickname`
- `email`
- `email_verified`
- `roles`
- `permissions`

## 默认配置

默认 OIDC 配置位于 `Lumine.AuthServer/appsettings.json`：

- `Oidc:Issuer`：默认 `http://localhost:5115`
- `SeedData:Clients[0].ClientId`：默认 `lumine-demo-client`
- `SeedData:Clients[0].RedirectUris`：默认 `http://localhost:5173/signin-oidc`
- `SeedData:Clients[0].RequirePkce`：默认 `true`

如果部署地址变化，至少同步检查：

1. `Oidc:Issuer`
2. 客户端白名单回调地址
3. 第三方应用或联调页面中的 `redirect_uri`

## Portal 联动

`Lumine.AuthPortal` 当前已接入：

- 登录页
- 注册页
- OAuth 客户端管理页
- OIDC Discovery / Playground 页面
- 授权确认页

## 当前限制

以下能力仍未完全形成标准 OIDC 产品形态：

- `refresh_token` 换新 token
- 客户端认证方式扩展
- 更完整的授权确认 UI 流程
- 更细的 scope 与 consent 持久化治理

另外，Discovery 文档当前声明了 `refresh_token` grant 类型；但 `token` 端点实际仅支持 `authorization_code`。阅读和对外说明时以实际控制器行为为准。

## 历史方案

早期改造规划已归档到：

- `docs/archive/auth/OAuth2.1-OIDC-Avalonia改造说明-历史方案.md`
