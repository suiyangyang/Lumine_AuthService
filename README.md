# Lumine AuthService

Lumine AuthService 是认证与授权相关服务的解决方案，包含 AuthServer、AuthPortal 与共享服务项目。

## 项目结构

```text
Lumine_AuthService/
├─ Lumine.AuthServer/
├─ Lumine.AuthPortal/
├─ Lumine.AuthService/
├─ docs/
├─ scripts/
├─ AGENTS.md
└─ Lumine_AuthService.sln
```

## 文档

文档统一放在 [docs/README.md](docs/README.md)，按主题分类维护：

- 部署与本地环境
- API 与后台管理
- 第三方服务接入
- OAuth2.1 / OIDC 当前实现
- 权限与菜单
- Agent 维护说明
- 历史归档

如果使用 coding agent / vibe coding 维护本项目，请先阅读 [AGENTS.md](AGENTS.md)。

## 快速入口

- [Agent 维护入口](AGENTS.md)
- [Agent 文档地图](docs/agent/README.md)
- [部署说明](docs/deployment/Deployment.md)
- [LocalDB 本地环境](docs/deployment/LocalDb.md)
- [管理后台基础接口说明](docs/api/管理后台基础接口说明.md)
- [第三方服务接入说明](docs/integrations/第三方服务接入说明.md)
- [OAuth2.1 / OIDC 当前实现说明](docs/auth/OAuth2.1-OIDC-Avalonia改造说明.md)
- [前端菜单权限清单](docs/permissions/前端菜单权限清单.md)
- [权限系统当前状态](docs/permissions/权限系统检查报告.md)
- [历史文档归档](docs/archive/README.md)
