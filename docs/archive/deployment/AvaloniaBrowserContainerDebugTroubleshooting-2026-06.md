# Avalonia Browser Container Debug Troubleshooting

本文记录一次 `Avalonia Browser + ASP.NET Core Server + Visual Studio Container (Dockerfile)` 调试时 Web 端无法正常显示 UI 的排障过程。后续类似项目可以直接按本文检查。

## 适用场景

- ASP.NET Core Server 同时托管 API 和 Avalonia Browser 静态资源。
- Visual Studio 使用 `Container (Dockerfile)` 配置启动 Server。
- Browser 页面停在 `Powered by Avalonia` splash。
- DevTools 控制台出现 WebAssembly / SkiaSharp / HotReload 相关错误。

## 典型症状

浏览器页面只显示 Avalonia splash，控制台可能出现：

```text
Failed to load resource: 404
Microsoft.DotNet.HotReload.WebAssembly.Browser.*.lib.module.js

System.DllNotFoundException: libSkiaSharp
SkiaSharp.SKImageInfo..cctor()

ManagedError: TypeInitialization_Type, SkiaSharp.SKImageInfo
```

这些错误容易被误判为静态资源拷贝问题。实际排障时要同时确认两个方向：

- `_content` / `_framework` 文件是否确实能被 Server 返回。
- `dotnet.native.*.wasm` 是否已经链接 SkiaSharp / HarfBuzz 等 native assets。

## 根因

本次根因是 Browser 项目只在 `Release` 配置下启用了 `WasmBuildNative`：

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <WasmBuildNative>true</WasmBuildNative>
</PropertyGroup>
```

而 Visual Studio 的 `Container (Dockerfile)` 默认使用 `Debug` 配置。Debug 构建没有链接 SkiaSharp native assets，浏览器运行时就会抛出 `DllNotFoundException: libSkiaSharp`。

构建日志中通常会有线索：

```text
@(NativeFileReference) is not empty, but the native references won't be linked in,
because neither $(WasmBuildNative), nor $(RunAOTCompilation) are 'true'.
```

## 修复方案

### 1. Debug / Release 都启用 native wasm 构建

在 Browser 项目中将 `WasmBuildNative` 放到通用 `PropertyGroup`：

```xml
<PropertyGroup>
  <TargetFramework>net10.0-browser</TargetFramework>
  <OutputType>Exe</OutputType>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <InvariantGlobalization>false</InvariantGlobalization>
  <BlazorIcuDataFileName>icudt_CJK.dat</BlazorIcuDataFileName>
  <PublishTrimmed>false</PublishTrimmed>
  <WasmBuildNative>true</WasmBuildNative>
</PropertyGroup>
```

本项目对应文件：

```text
Lumine.AuthPortal/Lumine.AuthPortal.Browser/Lumine.AuthPortal.Browser.csproj
```

### 2. 安装 wasm-tools 工作负载

启用 `WasmBuildNative` 后，本机 Debug 构建也需要 `wasm-tools`：

```powershell
dotnet workload install wasm-tools --skip-manifest-update
```

如果提示 `正在进行另一个安装操作`，先确认是否有残留的 `dotnet workload restore` 或 Visual Studio Installer 进程。不要反复并发安装。

检查工作负载：

```powershell
dotnet workload list
```

### 3. 更新入口版本号避免旧缓存

如果浏览器仍请求旧的 `dotnet.native.*.wasm` 指纹，更新 `index.html` 中入口脚本版本：

```html
<script type='module' src="./main.js?v=20260609"></script>
```

然后强制刷新浏览器，必要时清理站点数据或注销 service worker。

### 4. VS Container 调试凭据隔离

Container 调试时不要把 `.env` 烤进镜像。使用 Visual Studio Container Tools 的运行时 env-file：

```xml
<DockerfileRunEnvironmentFiles>$(MSBuildProjectDirectory)\..\.env</DockerfileRunEnvironmentFiles>
```

`.env` 必须被 `.gitignore` 忽略，只在本机保存真实 MySQL 用户名密码。

## 验证步骤

### 1. 构建 Browser Debug

```powershell
dotnet build .\Lumine.AuthPortal\Lumine.AuthPortal.Browser\Lumine.AuthPortal.Browser.csproj -c Debug
```

期望：

```text
Compiling native assets ...
Linking with emcc ...
0 个警告
0 个错误
```

如果仍看到 `NativeFileReference won't be linked`，说明 `WasmBuildNative` 没有在当前配置生效。

### 2. 构建 Server

```powershell
dotnet build .\Lumine.AuthServer\Lumine.AuthServer.csproj -c Debug
```

确认 Server 托管目录里有完整 Browser 资源：

```powershell
Get-ChildItem .\Lumine.AuthServer\bin\Debug\net10.0\wwwroot\_framework\dotnet.native*.wasm
Get-ChildItem .\Lumine.AuthServer\bin\Debug\net10.0\wwwroot\_framework\SkiaSharp*.wasm
Get-ChildItem .\Lumine.AuthServer\bin\Debug\net10.0\wwwroot\_content\Microsoft.DotNet.HotReload.WebAssembly.Browser\*.js
```

`dotnet.native.*.wasm` 链接 native assets 后通常会明显变大。本次从约 3 MB 变成约 24 MB。

### 3. 用干净容器验证静态资源

```powershell
$mountPath = (Resolve-Path '.\Lumine.AuthServer\bin\Debug\net10.0').Path

docker run -d --name lumine-authserver-ui-verify `
  --env-file .env `
  -e ASPNETCORE_HTTP_PORTS=8080 `
  -v "${mountPath}:/app" `
  -w /app `
  -p 18080:8080 `
  mcr.microsoft.com/dotnet/aspnet:10.0 `
  dotnet Lumine.AuthServer.dll
```

验证资源：

```powershell
$nativeName = docker exec lumine-authserver-ui-verify sh -c 'basename /app/wwwroot/_framework/dotnet.native*.wasm'

curl.exe -fsS "http://localhost:18080/_framework/$nativeName" -o NUL
curl.exe -fsS "http://localhost:18080/_framework/SkiaSharp.8dph0bad52.wasm" -o NUL
curl.exe -fsS "http://localhost:18080/_content/Microsoft.DotNet.HotReload.WebAssembly.Browser/Microsoft.DotNet.HotReload.WebAssembly.Browser.99zm1jdh75.lib.module.js" -o NUL
```

清理：

```powershell
docker rm -f lumine-authserver-ui-verify
```

### 4. 检查 VS 快速模式容器

Visual Studio Container Debug 快速模式通常会把项目目录挂载到 `/app`，输出目录在：

```text
/app/bin/Debug/net10.0/wwwroot
```

进入容器检查：

```powershell
docker ps --format "{{.Names}} {{.Ports}}"
docker exec <container-name> sh -c "find /app/bin/Debug/net10.0/wwwroot -maxdepth 3 -type f | sort | grep -E 'Skia|HotReload|dotnet.native|main.js'"
```

如果容器里只有 `DistrolessHelper` 进程、HTTP 请求返回 `Empty reply from server`，说明 VS 调试会话处于不一致状态。停止当前调试会话后重新启动 `Container (Dockerfile)`。

## 排障清单

遇到 Avalonia Browser 容器调试空白页时，按顺序检查：

1. 控制台是否有 `DllNotFoundException: libSkiaSharp`。
2. Debug 构建是否启用了 `WasmBuildNative`。
3. 本机是否安装 `wasm-tools`。
4. Browser Debug 构建日志是否出现 `Linking with emcc`。
5. Server 输出目录是否包含 `_framework/dotnet.native*.wasm`、`SkiaSharp*.wasm`、`HarfBuzzSharp*.wasm`。
6. Server 输出目录是否包含 `_content/Microsoft.DotNet.HotReload.WebAssembly.Browser/*.lib.module.js`。
7. 浏览器是否还在请求旧的 wasm 指纹，必要时更新 `main.js?v=...` 并强制刷新。
8. VS Container 调试容器是否需要重启。
9. `.env` 是否通过 `DockerfileRunEnvironmentFiles` 注入，而不是提交到仓库或复制进镜像。

## 本次项目最终改动

- `Lumine.AuthPortal.Browser.csproj`：Debug/Release 都启用 `WasmBuildNative`。
- `Lumine.AuthPortal.Browser/wwwroot/index.html`：更新 `main.js` 版本号。
- `Lumine.AuthServer.csproj`：通过 `DockerfileRunEnvironmentFiles` 注入本地 `.env`。

## 经验结论

Avalonia Browser 项目只要依赖 SkiaSharp WebAssembly native assets，Debug 容器调试也必须链接 native wasm。不要只在 Release 配置启用 `WasmBuildNative`，否则 Docker/VS 调试中最容易出现“资源看似存在，但运行时找不到 native library”的问题。
