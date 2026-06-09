# Lumine.AuthPortal

`Lumine.AuthPortal` 是 `Lumine.AuthServer` 的 `Avalonia Web` 前端骨架，当前覆盖：

- 登录页框架
- 注册页框架
- 授权确认页
- 后台布局、导航与权限守卫
- 用户/角色/权限/用户组/客户端/Token/菜单/审计日志管理页
- OIDC 授权码联调页
- 统一 API 调用层
- Browser 启动项目

## 项目结构

- `Lumine.AuthPortal/`：共享 UI、ViewModel、服务层
- `Lumine.AuthPortal.Browser/`：WebAssembly 启动项目
- `Lumine.AuthPortal.Desktop/`：桌面调试入口

## 默认后端地址

默认指向 `http://localhost:5115/`，可在首页“仪表盘”中修改。

## 运行方式

先启动后端，再启动 Browser：

```powershell
dotnet run --project "e:\Work\Code\LumineSolution\Lumine_AuthService\Lumine.AuthPortal\Lumine.AuthPortal.Browser\Lumine.AuthPortal.Browser.csproj"
```

如仅调试共享 UI，也可以运行桌面入口：

```powershell
dotnet run --project "e:\Work\Code\LumineSolution\Lumine_AuthService\Lumine.AuthPortal\Lumine.AuthPortal.Desktop\Lumine.AuthPortal.Desktop.csproj"
```

## 当前状态

- 可访问基础页面与后台壳布局
- 默认启动时仅展示登录/注册入口，不显示后台导航壳
- 注册成功后会自动回到登录页，并回填刚注册的用户名
- 浏览器端和桌面端都会尝试恢复最近一次有效登录会话
- 退出登录会调用后端接口并记录登出审计日志
- 可调用后端 `discovery`、`login`、`register`、`userinfo`、授权确认与后台管理 API
- 可通过 Web 端执行用户、角色、权限、用户组、客户端、Token、菜单、审计日志等后台操作或查询
- 可在 `OIDC Discovery` 页面执行 `authorize -> token -> userinfo` 调试链路
