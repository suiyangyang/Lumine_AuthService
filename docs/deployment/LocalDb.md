# 本地数据库初始化

当前仓库已经提供一套本地开发数据库方案，默认使用 Docker 启动 MySQL，并复用 `Lumine.AuthServer/appsettings.Development.json` 中的连接配置。

## 默认配置

- 容器名：`lumine-auth-mysql-dev`
- 数据库：从 `.env` 的 `LUMINE_AUTH_MYSQL_DATABASE` 读取
- 主机端口：`14307`（可通过 `.env` 中的 `LUMINE_AUTH_MYSQL_PORT` 覆盖）
- 应用账号：从 `.env` 的 `LUMINE_AUTH_MYSQL_USER` 读取
- 应用密码：从 `.env` 的 `LUMINE_AUTH_MYSQL_PASSWORD` 读取
- Root 密码：从 `.env` 的 `LUMINE_AUTH_MYSQL_ROOT_PASSWORD` 读取
- ReportMind 验证用户密码：从 `.env` 的 `LUMINE_AUTH_REPORTMIND_USER_PASSWORD` 读取
- ReportMind 迁移用户密码：从 `.env` 的 `LUMINE_AUTH_REPORTMIND_DEFAULT_USER_PASSWORD` 读取

对应开发环境连接串：

```text
server=localhost;port=14307;database=<LUMINE_AUTH_MYSQL_DATABASE>;user=<LUMINE_AUTH_MYSQL_USER>;password=<LUMINE_AUTH_MYSQL_PASSWORD>;
```

首次运行前，复制 `.env.example` 为 `.env` 并填入本地密码：

```powershell
Copy-Item .env.example .env
```

## 一键初始化

在 `Lumine_AuthService` 目录下执行：

```powershell
.\scripts\init-local-db.ps1
```

脚本会自动完成以下动作：

1. 启动 `docker-compose.localdb.yml` 中的 MySQL 容器
2. 如果检测到已有 `lumine-auth-mysql-dev` 容器，则直接复用
3. 等待数据库可连接
4. 启动一次 `Lumine.AuthServer`，自动执行 EF Core migrations 和种子数据写入
5. 输出表结构和基础种子数据数量

## 仅启动数据库容器

如果你只想启动数据库，不想触发迁移：

```powershell
.\scripts\init-local-db.ps1 -SkipBootstrap
```

## 手工命令

```powershell
docker compose -f .\docker-compose.localdb.yml up -d

docker compose -f .\docker-compose.localdb.yml down

docker compose -f .\docker-compose.localdb.yml down -v
```

## 初始化完成后的默认数据

- 管理员账号：`admin`
- 管理员密码：从 `.env` 的 `LUMINE_AUTH_ADMIN_PASSWORD` 读取
- 如果需要将已存在的 `admin` 密码同步为 `.env` 当前值，执行 `.\scripts\init-local-db.ps1 -ForceUpdateAdminPassword`
- ReportMind 验证用户：`reportmind-test`
- ReportMind 迁移用户：`陈元`、`隋洋洋`、`郑景帅`、`赵锟`
- ReportMind 验证角色：`ReportMindUser`
- 默认权限数：`10`
- 默认 OIDC Client：`lumine-demo-client`

## 说明

- 本地开发容器使用宿主机 `14307` 端口映射到容器 `3306`；如需调整端口，同时修改 `.env` 中的 `LUMINE_AUTH_MYSQL_PORT`。
- `Lumine.AuthServer` 启动时会自动执行 `Database.MigrateAsync()` 和 `AuthDataSeeder.SeedAsync()`
- 如果使用 Visual Studio 的 `Container (Dockerfile)` 调试配置，服务容器会通过 `host.docker.internal:14307` 访问宿主机上的 MySQL 容器
