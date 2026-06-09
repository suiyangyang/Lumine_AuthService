# Avalonia Browser + ASP.NET Core 一体化部署参考

---

## 参考背景（来自同类项目的施工记录）

这是一个把 `ASP.NET Core Web API`、`Avalonia Browser` 和 `Avalonia Desktop` 组合在一起的示例项目。

当前仓库的目标有两个：

- 让 `Avalonia` 在 Browser / Desktop / Docker 场景下都能正确显示中文。
- 让 `ASP.NET Core Web API` 在 Docker 中既提供 API，也托管 `Avalonia Browser` 前端页面。

本文档不是模板说明，而是**和当前仓库实际代码保持一致**的改造记录。你可以把它当成一份“从模板项目到当前实现”的完整施工手册。

---

## 一、当前方案的最终效果

当前仓库里，运行链路如下：

1. `AvaloniaToDocker.Server` 提供 `/login`、`/weatherforecast`、`/config` 等 API。
2. `AvaloniaToDocker.Browser` 生成 Browser 端的 `_framework`、`_content` 和页面静态资源。
3. `AvaloniaToDocker.Server` 负责把这些前端资源一起对外提供。
4. Docker 镜像里只启动 `Server`，但浏览器访问 `Server` 暴露的端口时，也能得到完整的 Avalonia 前端。

链路可以概括为：

`浏览器 -> ASP.NET Core Server -> Avalonia Browser 静态资源 -> Browser 调用 API`

---

## 二、和当前代码相比，README 里已经同步修正的重点

之前文档里有一些说明已经和当前代码不完全一致，这次已统一到真实实现：

- `AvaloniaToDocker.Server/Dockerfile` 当前**仍然使用** `dotnet build` + 手工拼装 `/app/browser-publish` 的方式，而不是直接复制 Browser 的 `publish` 目录。
- `AvaloniaToDocker.Browser/AvaloniaToDocker.Browser.csproj` 当前已经增加 `StageBrowserPublishOutput`，用于把 `wwwroot`、`_framework`、`_content` 汇总到 `bin/Release/net10.0-browser/publish`。
- `AvaloniaToDocker.Browser/Properties/PublishProfiles/FolderProfile.pubxml` 当前已经固定发布目录为 `bin/Release/net10.0-browser/publish`。
- `AvaloniaToDocker.Server/Program.cs` 当前已经支持：
  - 同时从源码目录、构建目录、发布目录寻找 Browser 资源
  - 补充 `.wasm`、`.dll`、`.dat` 等 MIME 类型
  - `/config` 返回 API 地址
  - SPA fallback
  - 关闭关键资源缓存
- 中文支持当前不是单点改动，而是由以下部分共同完成：
    - 嵌入字体 `SimHei.ttf`
    - Avalonia 控件全局字体样式
    - Browser 启用 `icudt_CJK.dat`
    - `index.html` 加载 `Noto Sans SC`

---

## 三、想要支持中文，需要做哪些改动

支持中文不是只改一个文件，而是要同时处理**Avalonia 控件字体**和**Browser HTML 层字体**。

### 1. 共享 Avalonia 项目要增加中文字体资源

#### 需要新增的文件

- `AvaloniaToDocker/Assets/SimHei.ttf`

#### 需要确认 / 修改的文件

- `AvaloniaToDocker/AvaloniaToDocker.csproj`
- `AvaloniaToDocker/App.axaml`

#### `AvaloniaToDocker/AvaloniaToDocker.csproj`

这段配置的作用是：

- 把 `Assets` 目录下的字体等资源打进程序集。
- 让后续 `avares://` 可以访问字体文件。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <!-- 关键改动：把 Assets 下的字体等静态资源作为 Avalonia 资源打包 -->
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Fonts.Inter" />
    <PackageReference Include="Avalonia.Diagnostics">
      <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
      <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
    </PackageReference>
    <PackageReference Include="CommunityToolkit.Mvvm" />
        <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
```

#### `AvaloniaToDocker/App.axaml`

这段配置的作用是：

- 把嵌入的 `SimHei.ttf` 注册成全局字体资源。
- 让 `TextBlock`、`TextBox`、`Button` 等常用控件默认使用中文字体。
- 避免 Browser / Desktop 中出现中文方块、问号、空白等问题。

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:AvaloniaToDocker"
             x:Class="AvaloniaToDocker.App"
             RequestedThemeVariant="Default">

    <Application.Resources>
        <!-- 关键改动：把嵌入字体注册成全局资源 -->
        <FontFamily x:Key="ChineseFont">avares://AvaloniaToDocker/Assets/SimHei.ttf#SimHei</FontFamily>
    </Application.Resources>

    <Application.DataTemplates>
        <local:ViewLocator/>
    </Application.DataTemplates>

    <Application.Styles>
        <FluentTheme />

        <Style Selector="TextBlock">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
        <Style Selector="TextBox">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
        <Style Selector="Button">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
        <Style Selector="CheckBox">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
        <Style Selector="RadioButton">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
        <Style Selector="ComboBoxItem">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
        <Style Selector="ListBoxItem">
            <Setter Property="FontFamily" Value="{StaticResource ChineseFont}"/>
        </Style>
    </Application.Styles>
</Application>
```

### 2. Browser 项目要启用完整的 CJK 国际化数据

#### Browser 需要修改的文件

- `AvaloniaToDocker.Browser/AvaloniaToDocker.Browser.csproj`

#### Browser 作用

- 关闭 `InvariantGlobalization`，避免只用精简国际化数据。
- 指定 `icudt_CJK.dat`，让 Browser/WASM 场景下的中文、CJK 处理更完整。
- 发布时把源码 `wwwroot` 与构建生成的 `_framework`、`_content` 汇总到统一 `publish` 目录，方便本地发布和排查。

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
    <PropertyGroup>
        <TargetFramework>net10.0-browser</TargetFramework>
        <OutputType>Exe</OutputType>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <Nullable>enable</Nullable>

        <!-- 关键改动：启用完整国际化，避免中文显示异常 -->
        <InvariantGlobalization>false</InvariantGlobalization>

        <!-- 关键改动：指定 CJK ICU 数据，保证中文/CJK 字符处理更稳定 -->
        <BlazorIcuDataFileName>icudt_CJK.dat</BlazorIcuDataFileName>

        <PublishTrimmed>false</PublishTrimmed>
    </PropertyGroup>

    <PropertyGroup Condition="'$(Configuration)' == 'Release'">
        <WasmBuildNative>true</WasmBuildNative>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Avalonia.Browser" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\AvaloniaToDocker\AvaloniaToDocker.csproj" />
    </ItemGroup>

    <!-- 关键改动：发布前清空 PublishDir，避免旧文件干扰 -->
    <Target Name="CleanPublishOutput" BeforeTargets="Publish">
        <RemoveDir Directories="$(PublishDir)" Condition="'$(PublishDir)' != '' and Exists('$(PublishDir)')" />
    </Target>

    <!--
      关键改动：发布后把源码 wwwroot、build 输出的 _framework/_content 汇总到 PublishDir。
      解决的问题：当前 Avalonia Browser 发布输出不完全等同于项目源码目录，手动汇总后更便于本地发布检查。
    -->
    <Target Name="StageBrowserPublishOutput" AfterTargets="Publish" Condition="'$(PublishDir)' != ''">
        <ItemGroup>
            <BrowserProjectStaticAssets Include="wwwroot\**\*.*" />
            <BrowserBuildFrameworkAssets Include="$(OutputPath)wwwroot\_framework\**\*.*"
                Condition="Exists('$(OutputPath)wwwroot\_framework')" />
            <BrowserBuildContentAssets Include="$(OutputPath)wwwroot\_content\**\*.*"
                Condition="Exists('$(OutputPath)wwwroot\_content')" />
        </ItemGroup>

        <MakeDir Directories="$(PublishDir)" />

        <Copy
            SourceFiles="@(BrowserProjectStaticAssets)"
            DestinationFiles="@(BrowserProjectStaticAssets->'$(PublishDir)%(RecursiveDir)%(Filename)%(Extension)')"
            SkipUnchangedFiles="true" />

        <Copy
            SourceFiles="@(BrowserBuildFrameworkAssets)"
            DestinationFiles="@(BrowserBuildFrameworkAssets->'$(PublishDir)_framework\%(RecursiveDir)%(Filename)%(Extension)')"
            SkipUnchangedFiles="true"
            Condition="'@(BrowserBuildFrameworkAssets)' != ''" />

        <Copy
            SourceFiles="@(BrowserBuildContentAssets)"
            DestinationFiles="@(BrowserBuildContentAssets->'$(PublishDir)_content\%(RecursiveDir)%(Filename)%(Extension)')"
            SkipUnchangedFiles="true"
            Condition="'@(BrowserBuildContentAssets)' != ''" />
    </Target>

    <!--
      关键改动：把 Avalonia.Browser 和 HotReload 的静态资源复制到 build 输出。
      解决的问题：确保 Browser 的 _framework / _content 在构建阶段就可被后续步骤使用。
    -->
    <Target Name="CopyPackageStaticAssetsToOutput" AfterTargets="Build">
        <ItemGroup>
            <AvaloniaPackageStaticAssets Include="$(NuGetPackageRoot)avalonia.browser\*\staticwebassets\*.*"
                Condition="Exists('$(NuGetPackageRoot)avalonia.browser')" />
            <HotReloadPackageStaticAssets Include="$(NuGetPackageRoot)microsoft.dotnet.hotreload.webassembly.browser\*\staticwebassets\*.*"
                Condition="Exists('$(NuGetPackageRoot)microsoft.dotnet.hotreload.webassembly.browser')" />
        </ItemGroup>

        <Copy
            SourceFiles="@(AvaloniaPackageStaticAssets)"
            DestinationFolder="$(OutputPath)wwwroot\_framework"
            SkipUnchangedFiles="true"
            Condition="'@(AvaloniaPackageStaticAssets)' != ''" />

        <Copy
            SourceFiles="@(HotReloadPackageStaticAssets)"
            DestinationFolder="$(OutputPath)wwwroot\_content\Microsoft.DotNet.HotReload.WebAssembly.Browser"
            SkipUnchangedFiles="true"
            Condition="'@(HotReloadPackageStaticAssets)' != ''" />
    </Target>
</Project>
```

### 3. Browser 的 HTML 启动页也要处理中文字体

#### Docker 部署需要修改的文件

- `AvaloniaToDocker.Browser/wwwroot/index.html`

#### 作用

- 让 HTML 壳页面和 Splash 页面能正常显示中文。
- 避免 Avalonia 画布加载前，页面中文显示风格不统一。

```html
<!DOCTYPE html>
<html>
<head>
    <title>AvaloniaToDocker.Browser</title>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">

    <!-- 关键改动：HTML 层加载中文 Web 字体 -->
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+SC:wght@400;500;700&display=swap" rel="stylesheet">

    <link rel="stylesheet" href="./app.css" />

    <style>
        /* 关键改动：让 body、启动容器、canvas 都有中文字体回退 */
        body, #out, canvas {
            font-family: 'Noto Sans SC', 'Microsoft YaHei', sans-serif;
        }
    </style>
</head>
<body style="margin: 0; overflow: hidden">
    <div id="out">
        <!-- 省略了中间 SVG 启动页内容 -->
    </div>

    <!-- 关键改动：先加载 API 地址解析脚本 -->
    <script src="./api-base.js"></script>

    <!-- 再加载主入口脚本 -->
    <script type='module' src="./main.js"></script>
</body>
</html>
```

---

## 四、想要支持 Docker 部署，需要做哪些改动

支持 Docker 部署不是只写一个 `Dockerfile`，还要让 Browser、ViewModel、Server 和静态资源路径全部配合起来。

### 1. Avalonia Browser 需要知道 API 在哪里

#### Docker 构建上下文需要修改的文件

- `AvaloniaToDocker.Browser/Program.cs`
- `AvaloniaToDocker.Browser/wwwroot/main.js`
- `AvaloniaToDocker.Browser/wwwroot/api-base.js`
- `AvaloniaToDocker/ViewModels/LoginViewModel.cs`

#### `AvaloniaToDocker.Browser/Program.cs`

作用：

- 从 JavaScript 接收 API 基地址。
- 写入共享 `LoginViewModel.BrowserApiBaseUrl`。
- 避免 Browser 独立调试时把请求误发到自己的调试端口。

```csharp
using Avalonia;
using Avalonia.Browser;
using AvaloniaToDocker;
using AvaloniaToDocker.ViewModels;

internal sealed partial class Program
{
    // 关键改动：保存 JavaScript 传入的 API 地址
    public static string ApiBaseUrl { get; private set; } = "http://localhost:8080";

    private static Task Main(string[] args)
    {
        // 关键改动：从 args[0] 读取 JS 传进来的 API 地址
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            ApiBaseUrl = args[0].TrimEnd('/');
        }

        // 关键改动：把 Browser API 地址写给共享 ViewModel
        LoginViewModel.BrowserApiBaseUrl = ApiBaseUrl;

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
```

#### `AvaloniaToDocker.Browser/wwwroot/main.js`

作用：

- 在真正启动 `.NET` 主程序集之前，先解析 API 地址。
- 把解析结果作为参数传进 `Program.Main(args)`。

```javascript
import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// 关键改动：清掉 service worker，避免旧缓存影响前端调试
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.getRegistrations()
        .then(registrations => Promise.all(registrations.map(registration => registration.unregister())))
        .catch(() => undefined);
}

// 关键改动：清掉 Cache Storage，避免旧版本资源污染 _framework
if ('caches' in globalThis) {
    caches.keys()
        .then(cacheKeys => Promise.all(cacheKeys.map(cacheKey => caches.delete(cacheKey))))
        .catch(() => undefined);
}

// 关键改动：优先使用 api-base.js 解析出的 API 地址
const resolvedApiBaseUrl = globalThis.resolveApiBaseUrl
    ? await globalThis.resolveApiBaseUrl()
    : globalThis.location.origin;

globalThis.API_BASE_URL = resolvedApiBaseUrl;

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// 关键改动：把 API 地址传给 .NET 主程序集
await dotnetRuntime.runMain(config.mainAssemblyName, [resolvedApiBaseUrl]);
```

#### `AvaloniaToDocker.Browser/wwwroot/api-base.js`

作用：

- 统一计算 Browser 应该调用哪个 API 地址。
- 兼容本地独立调试、Docker 同源访问、以及通过 URL 参数强制指定 API 地址三种场景。

```javascript
function normalizeApiBaseUrl(value) {
    return (value || '').trim().replace(/\/+$/, '');
}

window.resolveApiBaseUrl = async function() {
    const params = new URLSearchParams(window.location.search);

    // 关键改动：优先使用 ?apiBase=http://... 形式的显式参数
    const queryApiBase = normalizeApiBaseUrl(params.get('apiBase'));
    if (queryApiBase) {
        window.API_BASE_URL = queryApiBase;
        try {
            window.localStorage.setItem('apiBaseUrl', queryApiBase);
        } catch {
        }
        return queryApiBase;
    }

    // 关键改动：其次使用 localStorage 中缓存的 API 地址
    try {
        const storedApiBase = normalizeApiBaseUrl(window.localStorage.getItem('apiBaseUrl'));
        if (storedApiBase) {
            window.API_BASE_URL = storedApiBase;
            return storedApiBase;
        }
    } catch {
    }

    // 关键改动：再尝试调用同源的 /config，让 Docker / Server 统一告知 API 地址
    try {
        const resp = await fetch('/config');
        if (resp.ok) {
            const cfg = await resp.json();
            const configuredApiBase = normalizeApiBaseUrl(cfg.apiBase);
            window.API_BASE_URL = configuredApiBase || window.location.origin;
            window.API_PORT = cfg.port || '';
            return window.API_BASE_URL;
        }
    } catch {
    }

    // 关键改动：最后回退到 localhost:8080，方便本地默认联调
    const defaultApiBase = normalizeApiBaseUrl(window.API_BASE_URL) || 'http://localhost:8080';
    window.API_BASE_URL = defaultApiBase;
    window.API_PORT = window.API_PORT || '';
    return defaultApiBase;
};
```

#### `AvaloniaToDocker/ViewModels/LoginViewModel.cs`

作用：

- 同一份 ViewModel 同时支持 Browser 和 Desktop。
- Browser 使用 JavaScript 注入的 API 地址。
- Desktop 使用环境变量读取 API 地址。

```csharp
public partial class LoginViewModel : ViewModelBase
{
    private readonly bool _isBrowser;

    // 关键改动：供 Browser Program.cs 写入 API 地址
    public static string? BrowserApiBaseUrl { get; set; }

    public LoginViewModel()
    {
        // 关键改动：区分当前是否运行在浏览器环境
        _isBrowser = OperatingSystem.IsBrowser();
    }

    private string GetLoginUrl()
    {
        if (_isBrowser)
        {
            // 关键改动：Browser 中使用 JS 传入的 API 地址
            var baseUrl = BrowserApiBaseUrl ?? "http://localhost:8080";
            return baseUrl.TrimEnd('/') + "/login";
        }
        else
        {
            // 关键改动：Desktop 中优先使用 API_BASE_URL，未设置则按端口拼装
            var apiBase = Environment.GetEnvironmentVariable("API_BASE_URL");
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                var port = Environment.GetEnvironmentVariable("API_PORT")
                           ?? Environment.GetEnvironmentVariable("PORT")
                           ?? "8080";
                apiBase = $"http://localhost:{port}";
            }
            return apiBase.TrimEnd('/') + "/login";
        }
    }
}
```

### 2. ASP.NET Core Web API 项目要负责托管前端资源

#### 需要修改 / 新增的文件

- `AvaloniaToDocker.Server/Program.cs`
- `AvaloniaToDocker.Server/Properties/launchSettings.json`
- `AvaloniaToDocker.Server/AvaloniaToDocker.Server.csproj`

#### `AvaloniaToDocker.Server/AvaloniaToDocker.Server.csproj`

作用：

- 启用 Visual Studio 的 Docker 容器调试支持。
- 在构建 Server 之前，顺带先构建 Browser 项目，避免本地运行 Server 时 Browser 产物缺失。

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- 关键改动：告诉 VS 容器调试默认目标是 Linux -->
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />

    <!-- 关键改动：支持 Visual Studio 的 Docker 调试配置 -->
    <PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" />
  </ItemGroup>

  <!--
    关键改动：先构建 Browser。
    解决的问题：Server 本地启动时，如果 Browser 构建产物不存在，前端静态资源就不完整。
  -->
  <Target Name="BuildBrowserProject" BeforeTargets="Build">
    <MSBuild Projects="..\AvaloniaToDocker.Browser\AvaloniaToDocker.Browser.csproj"
             Targets="Build"
             Properties="Configuration=$(Configuration)" />
  </Target>
</Project>
```

#### `AvaloniaToDocker.Server/Properties/launchSettings.json`

作用：

- 明确本地项目调试端口。
- 明确 Docker 调试端口。

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "dotnetRunMessages": true,
      "applicationUrl": "http://localhost:5154"
    },
    "Container (Dockerfile)": {
      "commandName": "Docker",
      "launchUrl": "{Scheme}://{ServiceHost}:{ServicePort}",
      "environmentVariables": {
        "ASPNETCORE_HTTP_PORTS": "8080",
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "httpPort": 8080,
      "publishAllPorts": false,
      "useSSL": false
    }
  }
}
```

#### `AvaloniaToDocker.Server/Program.cs`

这是当前项目中最关键的一份文件，它负责解决以下问题：

- `Server` 不只提供 API，还要托管 `Browser` 静态资源。
- 本地开发、构建输出、发布输出、Docker 容器中 Browser 资源所在路径不同，需要统一解析。
- `_framework`、`_content`、`index.html`、`main.js` 这些资源必须按正确顺序和来源被提供。

下面按功能列出当前关键改动代码。

##### 启动阶段：解析 Browser 资源并挂接静态文件中间件

```csharp
var contentRoot = builder.Environment.ContentRootPath ?? string.Empty;
var defaultWwwroot = Path.Combine(contentRoot, "wwwroot");

// 关键改动：自动解析 Browser 资源路径
var browserAssets = ResolveBrowserAssets(contentRoot, defaultWwwroot);

if (!string.IsNullOrWhiteSpace(browserAssets.PrimaryWebRoot))
{
    builder.Environment.WebRootPath = browserAssets.PrimaryWebRoot;
}

builder.Services.AddCors(options =>
{
    // 关键改动：开发期允许 Browser 独立调试时跨域调用 API
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
```

##### 静态文件配置：补充 WASM 相关 MIME 类型

```csharp
var contentTypeProvider = new FileExtensionContentTypeProvider();

// 关键改动：补充 Browser/WASM 运行所需 MIME 映射
contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
contentTypeProvider.Mappings[".blat"] = "application/octet-stream";
contentTypeProvider.Mappings[".dll"] = "application/octet-stream";
contentTypeProvider.Mappings[".wasm"] = "application/wasm";
contentTypeProvider.Mappings[".symbols"] = "application/octet-stream";

var staticFilesOptions = new StaticFileOptions
{
    FileProvider = staticFileProvider,
    ContentTypeProvider = contentTypeProvider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = context =>
    {
        // 关键改动：关键前端资源禁用缓存，避免调试期旧脚本被缓存
        if (ShouldDisableCache(context.File.PhysicalPath, physicalWebRoot))
        {
            ApplyNoCacheHeaders(context.Context.Response.Headers);
        }
    }
};

app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles(staticFilesOptions);
```

##### API 端点：登录、配置、天气

```csharp
app.MapPost("/login", (LoginRequest req) =>
{
    // 关键改动：提供一个最小登录接口给 Browser / Desktop 共用
    if (req is null)
    {
        return Results.BadRequest();
    }

    if (req.Username == "User" && req.Password == "Pwd")
    {
        return Results.Ok(new { message = "登录成功" });
    }

    return Results.Unauthorized();
});

app.MapGet("/config", (HttpRequest req) =>
{
    // 关键改动：统一告诉前端当前 API 的访问地址和端口
    var envPort = Environment.GetEnvironmentVariable("PORT");
    var scheme = req.Scheme ?? "http";
    var host = req.Host.HasValue ? req.Host.Value : ("localhost:" + (envPort ?? "8080"));
    var apiBase = $"{scheme}://{host}";
    var port = req.Host.Port?.ToString() ?? envPort ?? "8080";
    return Results.Ok(new { apiBase, port });
});
```

##### SPA fallback：非 API 路由回退到 `index.html`

```csharp
app.MapFallback(async context =>
{
    // 关键改动：把未知路由回退到 index.html
    // 解决的问题：前端页面或未来前端路由访问时，不会直接 404
    var index = Path.Combine(webRoot, "index.html");
    if (File.Exists(index))
    {
        context.Response.ContentType = "text/html";
        ApplyNoCacheHeaders(context.Response.Headers);
        await context.Response.SendFileAsync(index);
    }
    else
    {
        context.Response.StatusCode = 404;
    }
});
```

##### Browser 资源完整性判断：区分“完整前端”和“仅运行时资源”

这两个辅助函数的作用是：

- `HasBrowserRuntimeAssets()`：判断某个目录是否已经是一套完整的 Browser 前端目录。
- `HasBrowserRuntimeRuntimeAssets()`：判断某个目录里是否至少有 `_framework/dotnet.js`，也就是是否具备 Browser 运行时资源。

```csharp
private static bool HasBrowserRuntimeAssets(string webRoot)
{
    // 关键改动：要求目录中既有页面入口，也有运行时入口，才算完整前端目录
    return Directory.Exists(webRoot)
        && File.Exists(Path.Combine(webRoot, "index.html"))
        && File.Exists(Path.Combine(webRoot, "main.js"))
        && File.Exists(Path.Combine(webRoot, "_framework", "dotnet.js"));
}

private static bool HasBrowserRuntimeRuntimeAssets(string webRoot)
{
    // 关键改动：只检查 _framework/dotnet.js 是否存在，用于识别 build 输出
    return Directory.Exists(webRoot)
        && File.Exists(Path.Combine(webRoot, "_framework", "dotnet.js"));
}
```

##### Browser 资源解析：同时兼容源码目录、build 输出、publish 输出

```csharp
private static BrowserAssets ResolveBrowserAssets(string contentRoot, string defaultWwwroot)
{
    // 关键改动：如果 Server 自己的 wwwroot 已经完整，直接使用
    if (HasBrowserRuntimeAssets(defaultWwwroot))
    {
        return new BrowserAssets(defaultWwwroot, new[] { defaultWwwroot }, null, null);
    }

    var possibleBrowserRoots = new List<string>();
    possibleBrowserRoots.Add(Path.GetFullPath(Path.Combine(contentRoot, "AvaloniaToDocker.Browser")));

    if (!contentRoot.EndsWith("publish", StringComparison.OrdinalIgnoreCase))
    {
        possibleBrowserRoots.Add(Path.GetFullPath(Path.Combine(contentRoot, "..", "AvaloniaToDocker.Browser")));
    }

    if (Directory.Exists("/src"))
    {
        // 关键改动：兼容 Docker build 阶段源码路径
        possibleBrowserRoots.Add("/src/AvaloniaToDocker.Browser");
    }

    foreach (var browserRoot in possibleBrowserRoots.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var sourceWwwroot = Path.Combine(browserRoot, "wwwroot");
        var developmentAssets = ResolveBrowserDevelopmentAssets(browserRoot);

        // 关键改动：优先尝试使用 build 输出的 _framework / _content
        var buildRoots = new[]
        {
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "wwwroot")
        };

        // 关键改动：如果存在 build 输出，则组合“源码 wwwroot + build 运行时资源”
        var runtimeRoot = buildRoots.FirstOrDefault(HasBrowserRuntimeRuntimeAssets);
        if (!string.IsNullOrWhiteSpace(runtimeRoot) && Directory.Exists(sourceWwwroot))
        {
            return new BrowserAssets(
                sourceWwwroot,
                new[] { sourceWwwroot, runtimeRoot },
                developmentAssets.AvaloniaBrowserStaticWebAssetsRoot,
                developmentAssets.HotReloadStaticWebAssetsRoot);
        }

        // 关键改动：如果有 publish 输出，也允许回退到 publish 路径
        var publishRoots = new[]
        {
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "publish", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "publish", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "browser-wasm", "publish", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "browser-wasm", "publish", "wwwroot")
        };

        var publishRoot = publishRoots.FirstOrDefault(HasBrowserRuntimeAssets);
        if (!string.IsNullOrWhiteSpace(publishRoot) && Directory.Exists(sourceWwwroot))
        {
            return new BrowserAssets(
                sourceWwwroot,
                new[] { sourceWwwroot, publishRoot },
                developmentAssets.AvaloniaBrowserStaticWebAssetsRoot,
                developmentAssets.HotReloadStaticWebAssetsRoot);
        }

        var publishOnlyRoot = publishRoot;
        if (!string.IsNullOrWhiteSpace(publishOnlyRoot))
        {
            return new BrowserAssets(
                publishOnlyRoot,
                new[] { publishOnlyRoot },
                developmentAssets.AvaloniaBrowserStaticWebAssetsRoot,
                developmentAssets.HotReloadStaticWebAssetsRoot);
        }
    }

    return new BrowserAssets(
        defaultWwwroot,
        Directory.Exists(defaultWwwroot) ? new[] { defaultWwwroot } : Array.Empty<string>(),
        null,
        null);
}
```

##### 组合静态文件提供器：把多个目录合成一个前端根

这个辅助函数的作用是：

- 当 `sourceWwwroot` 和 `runtimeRoot` 同时存在时，把它们拼成一个统一的 `IFileProvider`。
- 让 `index.html`、`main.js` 之类页面文件来自源码目录，而 `_framework`、`_content` 等运行时文件来自 build/publish 目录。

```csharp
private static IFileProvider? CreateStaticFileProvider(BrowserAssets browserAssets, string fallbackRoot)
{
    var providerRoots = browserAssets.ProviderRoots
        .Where(Directory.Exists)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    // 关键改动：如果没有任何可用路径，就退回到 Server 自己的 wwwroot
    if (providerRoots.Length == 0)
    {
        return Directory.Exists(fallbackRoot) ? new PhysicalFileProvider(fallbackRoot) : null;
    }

    // 关键改动：只有一个根目录时直接使用 PhysicalFileProvider
    if (providerRoots.Length == 1)
    {
        return new PhysicalFileProvider(providerRoots[0]);
    }

    // 关键改动：多个根目录时使用 CompositeFileProvider 合并
    return new CompositeFileProvider(providerRoots.Select(path => new PhysicalFileProvider(path)).ToArray<IFileProvider>());
}
```

##### 解析开发态包静态资源：让 `/_framework` 和 `/_content` 可从 NuGet 包补齐

这个辅助函数组的作用是：

- 从 `staticwebassets.development.json` 中解析 Avalonia Browser / Hot Reload 包的静态资源根目录。
- 如果 manifest 不存在或路径失效，再回退到本机 / 容器的 NuGet 包缓存目录中查找。
- 保证开发态下即使 `_framework`、`_content` 不完全在项目目录，也能由 `Server` 正确提供。

```csharp
private static DevelopmentAssets ResolveBrowserDevelopmentAssets(string browserRoot)
{
    var manifestPaths = new[]
    {
        Path.Combine(browserRoot, "obj", "Debug", "net10.0-browser", "staticwebassets.development.json"),
        Path.Combine(browserRoot, "obj", "Release", "net10.0-browser", "staticwebassets.development.json")
    };

    foreach (var manifestPath in manifestPaths)
    {
        if (!File.Exists(manifestPath))
        {
            continue;
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("ContentRoots", out var contentRootsElement)
                || contentRootsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var contentRoots = contentRootsElement
                .EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString())
                .Select(NormalizeContentRootPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                .Cast<string>()
                .ToArray();

            if (contentRoots.Length == 0)
            {
                continue;
            }

            var avaloniaAssetsRoot = contentRoots.FirstOrDefault(path =>
                path.Contains("\\.nuget\\packages\\avalonia.browser\\", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/.nuget/packages/avalonia.browser/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/root/.nuget/packages/avalonia.browser/", StringComparison.OrdinalIgnoreCase));

            var hotReloadAssetsRoot = contentRoots.FirstOrDefault(path =>
                path.Contains("\\.nuget\\packages\\microsoft.dotnet.hotreload.webassembly.browser\\", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/.nuget/packages/microsoft.dotnet.hotreload.webassembly.browser/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/root/.nuget/packages/microsoft.dotnet.hotreload.webassembly.browser/", StringComparison.OrdinalIgnoreCase));

            avaloniaAssetsRoot ??= FindPackageStaticAssetsRoot("avalonia.browser");
            hotReloadAssetsRoot ??= FindPackageStaticAssetsRoot("microsoft.dotnet.hotreload.webassembly.browser");

            return new DevelopmentAssets(avaloniaAssetsRoot, hotReloadAssetsRoot);
        }
        catch
        {
        }
    }

    return new DevelopmentAssets(
        FindPackageStaticAssetsRoot("avalonia.browser"),
        FindPackageStaticAssetsRoot("microsoft.dotnet.hotreload.webassembly.browser"));
}

private static string? NormalizeContentRootPath(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    if (Directory.Exists(path))
    {
        return path;
    }

    var normalizedPath = path.Replace('\\', '/');

    // 关键改动：当 manifest 中路径不直接可用时，尝试回落到当前机器的 NuGet 包路径
    if (normalizedPath.Contains("/.nuget/packages/avalonia.browser/", StringComparison.OrdinalIgnoreCase))
    {
        return FindPackageStaticAssetsRoot("avalonia.browser") ?? path;
    }

    if (normalizedPath.Contains("/.nuget/packages/microsoft.dotnet.hotreload.webassembly.browser/", StringComparison.OrdinalIgnoreCase))
    {
        return FindPackageStaticAssetsRoot("microsoft.dotnet.hotreload.webassembly.browser") ?? path;
    }

    return path;
}

private static string? FindPackageStaticAssetsRoot(string packageId)
{
    var candidateRoots = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
        Path.Combine(Path.DirectorySeparatorChar.ToString(), "root", ".nuget", "packages"),
        Path.Combine(Path.DirectorySeparatorChar.ToString(), ".nuget", "packages")
    }
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(Directory.Exists);

    foreach (var packagesRoot in candidateRoots)
    {
        var packageRoot = Path.Combine(packagesRoot, packageId);
        if (!Directory.Exists(packageRoot))
        {
            continue;
        }

        var versionRoots = Directory.GetDirectories(packageRoot)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var versionRoot in versionRoots)
        {
            var staticAssetsRoot = Path.Combine(versionRoot, "staticwebassets");
            if (Directory.Exists(staticAssetsRoot))
            {
                return staticAssetsRoot;
            }
        }
    }

    return null;
}
```

##### 包静态资源：补挂 Avalonia Browser 和 Hot Reload 资源

```csharp
private static void MapPackageStaticAssets(WebApplication app, FileExtensionContentTypeProvider contentTypeProvider, BrowserAssets browserAssets)
{
    // 关键改动：把 avalonia.browser 包自带静态资源挂到 /_framework
    if (Directory.Exists(browserAssets.AvaloniaBrowserStaticWebAssetsRoot))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(browserAssets.AvaloniaBrowserStaticWebAssetsRoot),
            RequestPath = "/_framework",
            ContentTypeProvider = contentTypeProvider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
            OnPrepareResponse = context => ApplyNoCacheHeaders(context.Context.Response.Headers)
        });
    }

    // 关键改动：把 HotReload 包资源挂到 /_content/...
    if (Directory.Exists(browserAssets.HotReloadStaticWebAssetsRoot))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(browserAssets.HotReloadStaticWebAssetsRoot),
            RequestPath = "/_content/Microsoft.DotNet.HotReload.WebAssembly.Browser",
            ContentTypeProvider = contentTypeProvider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
            OnPrepareResponse = context => ApplyNoCacheHeaders(context.Context.Response.Headers)
        });
    }
}
```

##### 关闭关键前端资源缓存：避免调试期浏览器拿到旧版本脚本

这两个辅助函数的作用是：

- `ShouldDisableCache()`：判断当前返回的文件是不是 `index.html`、`main.js`、`api-base.js`、`_framework/dotnet.js`、`_framework/sw.js` 这类必须禁用缓存的关键资源。
- `ApplyNoCacheHeaders()`：统一给响应头写入 `Cache-Control`、`Pragma`、`Expires`，确保浏览器重新请求最新版本。

```csharp
private static bool ShouldDisableCache(string? physicalPath, string webRoot)
{
    if (string.IsNullOrWhiteSpace(physicalPath) || string.IsNullOrWhiteSpace(webRoot))
    {
        return false;
    }

    var relativePath = Path.GetRelativePath(webRoot, physicalPath)
        .Replace(Path.DirectorySeparatorChar, '/')
        .Replace(Path.AltDirectorySeparatorChar, '/');

    return relativePath.Equals("index.html", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("main.js", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("api-base.js", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("_framework/dotnet.js", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("_framework/sw.js", StringComparison.OrdinalIgnoreCase);
}

private static void ApplyNoCacheHeaders(IHeaderDictionary headers)
{
    headers.CacheControl = "no-store, no-cache, max-age=0, must-revalidate";
    headers.Pragma = "no-cache";
    headers.Expires = "0";
}
```

##### 结尾的记录类型：把 Browser 静态资源路径打包成结构化对象

这两个记录类型的作用是：

- `BrowserAssets`：统一保存主 WebRoot、候选 ProviderRoots，以及包静态资源根目录。
- `DevelopmentAssets`：统一保存开发期从 NuGet 包解析到的静态资源目录。

```csharp
private sealed record BrowserAssets(
    string PrimaryWebRoot,
    IReadOnlyList<string> ProviderRoots,
    string? AvaloniaBrowserStaticWebAssetsRoot,
    string? HotReloadStaticWebAssetsRoot);

private sealed record DevelopmentAssets(
    string? AvaloniaBrowserStaticWebAssetsRoot,
    string? HotReloadStaticWebAssetsRoot);
```

### 3. Dockerfile 要同时构建 Server 和 Browser

#### 需要修改的文件

- `AvaloniaToDocker.Server/Dockerfile`
- `.dockerignore`

#### 当前 `Dockerfile` 的最终产物是什么

当前 Dockerfile 的最终镜像里：

- 应用入口是 `AvaloniaToDocker.Server.dll`
- `/app` 下放的是 `Server` 发布产物
- `/app/wwwroot` 下放的是**手工拼出来**的 Browser 静态资源

也就是说，最终镜像里只跑一个进程：

```text
dotnet AvaloniaToDocker.Server.dll
```

但这个进程同时具备两种能力：

- 提供 Web API
- 提供 Avalonia Browser 前端页面和运行时文件

#### 为什么当前 Dockerfile 不直接复制 Browser 的 `publish` 目录

当前仓库里，Dockerfile 仍使用如下策略：

- 对 `Browser` 执行 `dotnet build -c Release`
- 从源码 `wwwroot` 复制页面文件
- 从 `bin/Release/net10.0-browser/wwwroot/_framework` 复制运行时文件
- 如果存在 `bin/Release/net10.0-browser/wwwroot/_content`，再把它一并复制过去

这么做的原因是：

- 当前项目已经验证这套路径组合和运行方式可用。
- 它明确区分了“项目源码页面文件”和“构建生成运行时文件”。
- 前面尝试过直接精简为 `publish` 路径复制，但用户本地验证镜像运行会报错，所以当前文档先保留和现有实现一致的方案。

#### 当前 `AvaloniaToDocker.Server/Dockerfile`

下面是当前实现对应的完整 Dockerfile，并在关键处补了说明：

```dockerfile
# 同时构建并发布 Server + Browser，由 Server 托管 Browser 静态资源。

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 关键改动：WASM 工具链当前依赖 Python 运行时，安装 `python3` 即可
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*

# 关键改动：先复制项目描述文件，提高 restore 缓存命中率
# 当前仓库根目录已经没有 `NuGet.config`，因此这里不再复制它
COPY ["Directory.Packages.props", "."]
COPY ["local-packages", "local-packages/"]
COPY ["AvaloniaToDocker/AvaloniaToDocker.csproj", "AvaloniaToDocker/"]
COPY ["AvaloniaToDocker.Browser/AvaloniaToDocker.Browser.csproj", "AvaloniaToDocker.Browser/"]
COPY ["AvaloniaToDocker.Server/AvaloniaToDocker.Server.csproj", "AvaloniaToDocker.Server/"]

# 关键改动：先还原 Server；因为 Server 引用了浏览器构建链，后面会继续构建 Browser
RUN dotnet restore "AvaloniaToDocker.Server/AvaloniaToDocker.Server.csproj"

# 关键改动：安装 wasm-tools，解决 Browser/WASM 构建依赖
RUN dotnet workload install wasm-tools

COPY . .

# 关键改动：先构建 Browser，并把前端资源手工整理到 /app/browser-publish
# 解决的问题：让源码 wwwroot 与 build 输出的 `_framework` 组合成一套可交给 Server 托管的前端资源；如果 `_content` 存在则一并复制
RUN dotnet build "AvaloniaToDocker.Browser/AvaloniaToDocker.Browser.csproj" -c Release \
    && mkdir -p /app/browser-publish/_framework \
    && cp -r AvaloniaToDocker.Browser/wwwroot/. /app/browser-publish/ \
    && cp -r AvaloniaToDocker.Browser/bin/Release/net10.0-browser/wwwroot/_framework/. /app/browser-publish/_framework/ \
    && if [ -d AvaloniaToDocker.Browser/bin/Release/net10.0-browser/wwwroot/_content ]; then mkdir -p /app/browser-publish/_content && cp -r AvaloniaToDocker.Browser/bin/Release/net10.0-browser/wwwroot/_content/. /app/browser-publish/_content/; fi

# 关键改动：发布 Server 到 /app/publish
RUN dotnet publish "AvaloniaToDocker.Server/AvaloniaToDocker.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# 关键改动：支持运行时通过 PORT 改端口
ARG PORT=8080
ENV PORT=$PORT
ENV ASPNETCORE_URLS="http://+:${PORT}"

EXPOSE 8080

# 关键改动：复制 Server 发布产物
COPY --from=build /app/publish .

# 关键改动：把 Browser 静态资源放到最终镜像的 wwwroot
COPY --from=build /app/browser-publish/. ./wwwroot/

ENTRYPOINT ["dotnet", "AvaloniaToDocker.Server.dll"]
```

#### `.dockerignore`

作用：

- 避免临时文件进入 Docker build 上下文。
- 避免 `bin`、`obj`、`docs`、临时 HTML/JS/JSON 混进镜像。
- 但仍保留 Browser / Server 在必要场景下需要用到的 Release 输出目录。

```ignore
**/bin
**/obj
docs
temp-browser-publish
temp_*.html
temp_*.js
temp_*.json
README.md

!AvaloniaToDocker.Server/bin/Release/net10.0/**
!AvaloniaToDocker.Browser/bin/Release/net10.0-browser/**
```

---

## 五、按照项目类型拆分：到底修改了哪些文件

### 1. Avalonia 共享项目需要添加 / 修改的文件

- 新增 `AvaloniaToDocker/Assets/SimHei.ttf`
- 修改 `AvaloniaToDocker/AvaloniaToDocker.csproj`
- 修改 `AvaloniaToDocker/App.axaml`
- 修改 `AvaloniaToDocker/ViewModels/LoginViewModel.cs`

### 2. Avalonia Browser 项目需要添加 / 修改的文件

- 修改 `AvaloniaToDocker.Browser/AvaloniaToDocker.Browser.csproj`
- 修改 `AvaloniaToDocker.Browser/Program.cs`
- 新增 `AvaloniaToDocker.Browser/wwwroot/api-base.js`
- 修改 `AvaloniaToDocker.Browser/wwwroot/main.js`
- 修改 `AvaloniaToDocker.Browser/wwwroot/index.html`
- 修改 `AvaloniaToDocker.Browser/Properties/PublishProfiles/FolderProfile.pubxml`
- 确认 `AvaloniaToDocker.Browser/Properties/launchSettings.json`

### 3. ASP.NET Core Web API 项目需要添加 / 修改的文件

- 修改 `AvaloniaToDocker.Server/AvaloniaToDocker.Server.csproj`
- 修改 `AvaloniaToDocker.Server/Program.cs`
- 修改 `AvaloniaToDocker.Server/Properties/launchSettings.json`
- 新增 / 修改 `AvaloniaToDocker.Server/Dockerfile`

### 4. 解决 Docker 构建上下文污染需要修改的文件

- 修改 `.dockerignore`

---

## 六、Browser 发布路径和 Docker 构建路径的关系

当前仓库里同时存在两套和 Browser 产物相关的思路：

### 1. 本地发布路径

`AvaloniaToDocker.Browser/Properties/PublishProfiles/FolderProfile.pubxml`：

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>

    <!-- 关键改动：把发布输出固定到 publish 目录 -->
    <PublishDir>bin\Release\net10.0-browser\publish\</PublishDir>

    <PublishProtocol>FileSystem</PublishProtocol>
    <_TargetId>Folder</_TargetId>
  </PropertyGroup>
</Project>
```

这套路径的意义是：

- 本地手动执行 `dotnet publish` 时，开发者可以在一个稳定目录看到完整产物。
- 便于检查 `index.html`、`main.js`、`_framework/dotnet.js` 是否已正确生成。

### 2. Docker 构建路径

当前 Dockerfile **没有直接使用**上面的 `publish` 目录，而是使用：

- 源码目录的 `wwwroot`
- 构建输出的 `bin/Release/net10.0-browser/wwwroot/_framework`
- 构建输出中可选的 `bin/Release/net10.0-browser/wwwroot/_content`

然后手工拼成：

- `/app/browser-publish`

这样做的意义是：

- 最终由 `Server` 拿到一份明确的 Browser 静态资源目录。
- Dockerfile 的行为和当前镜像可运行方式保持一致。
- 当前已验证：`python3` 需要保留，`python` 软链接不需要保留。

---

## 七、本地开发与验证命令

### 1. 本地发布 Browser

```powershell
Push-Location 'E:\Work\Code\TestProjects\Avalonia\AvaloniaToDocker'
dotnet publish '.\AvaloniaToDocker.Browser\AvaloniaToDocker.Browser.csproj' -c Release /p:PublishProfile=FolderProfile -v minimal
Pop-Location
```

当前发布目录为：

- `AvaloniaToDocker.Browser/bin/Release/net10.0-browser/publish`

### 2. 本地运行 Server

```powershell
Push-Location 'E:\Work\Code\TestProjects\Avalonia\AvaloniaToDocker'
dotnet run --project '.\AvaloniaToDocker.Server\AvaloniaToDocker.Server.csproj'
Pop-Location
```

### 3. 本地独立调试 Browser 并指定 API 地址

Browser 调试地址当前来自：

- `AvaloniaToDocker.Browser/Properties/launchSettings.json`
- `http://localhost:5235`
- `https://localhost:7169`

可直接使用：

```text
http://localhost:5235/?apiBase=http://localhost:8080
```

### 4. Docker 构建镜像

```powershell
Push-Location 'E:\Work\Code\LumineSolution'
docker build -t lumine-authserver-vsdebug-verify -f '.\Lumine_AuthService\Lumine.AuthServer\Dockerfile' .
Pop-Location
```

说明：

- 当前 `Lumine.AuthServer/Dockerfile` 会同时复制 `Lumine_AuthService` 和同级 `Lumine_BuildingBlocks` 的项目文件。
- 因此 `docker build` 的上下文必须是工作区根目录 `E:\Work\Code\LumineSolution`，不能直接在 `Lumine_AuthService` 目录下执行 `docker build ... .`。
- 这一点已经按上面的命令实际验证通过。

### 5. Docker 运行镜像

```powershell
docker run --rm -p 8080:8080 lumine-authserver-vsdebug-verify
```

如需改端口：

```powershell
docker run --rm -e PORT=8090 -p 8090:8090 lumine-authserver-vsdebug-verify
```

---

## 八、为什么这些改动是必须的

### 1. 为什么中文支持不能只改一个文件

因为当前项目横跨三层：

- Avalonia 控件层
- Browser HTML 层
- WASM 运行时国际化层

只改其中一个，常见结果是：

- 控件能显示中文，但 HTML 启动页乱码
- HTML 正常，但 Avalonia 控件中文变方块
- 中文大部分正常，但某些 CJK 文本、区域格式或字体回退异常

### 2. 为什么 Docker 部署不能只写 Dockerfile

因为 Docker 里真正的问题不只是“能打镜像”，还包括：

- 前端静态资源从哪里来
- `_framework` 是否完整
- Browser 调 API 时地址是否正确
- Server 是否能正确返回 `index.html`
- `.wasm`、`.dll`、`.dat` 是否带正确 MIME 类型

所以要同时修改：

- Browser 入口
- API 地址解析脚本
- 共享 ViewModel
- Server 静态文件中间件
- Dockerfile
- `.dockerignore`

### 3. 为什么 Dockerfile 里最终产物是 `Server + Browser 静态资源`

因为当前设计不是在容器里同时跑两个进程，而是：

- 只启动 `AvaloniaToDocker.Server.dll`
- 让这个 ASP.NET Core 进程同时提供 API 和 Browser 前端

这样做的好处是：

- 部署简单
- 暴露端口少
- 前端和 API 同源，`/config` 更容易工作

---

## 九、开发者照着做时的最小检查清单

如果你要从零实现“中文 + Docker”，建议按下面顺序检查：

1. `Assets/SimHei.ttf` 是否存在。
2. `AvaloniaResource Include="Assets\**"` 是否存在。
3. `App.axaml` 是否给常用控件设置了 `ChineseFont`。
4. Browser `.csproj` 是否启用了 `InvariantGlobalization=false` 和 `BlazorIcuDataFileName=icudt_CJK.dat`。
5. Browser 是否有 `api-base.js`。
6. `main.js` 是否把 API 地址传给 `Program.Main(args)`。
7. `LoginViewModel` 是否区分 Browser / Desktop 的登录地址来源。
8. `Server/Program.cs` 是否已经启用静态文件、MIME 映射、`/config`、fallback。
9. `Dockerfile` 是否已经把 Browser 产物复制到最终镜像的 `wwwroot`。
10. `.dockerignore` 是否排除了临时文件但保留了必要的 Browser / Server 输出。

---

## 十、结论

当前仓库里，“支持中文”和“支持 Docker 部署”并不是两件独立的事，而是一套联合改造：

- 中文支持解决的是 `Avalonia + Browser + WASM` 三层的字体与国际化问题。
- Docker 支持解决的是 `Server + Browser` 联合打包、静态资源托管、API 地址发现和运行时一致性问题。

如果你后续要基于 Avalonia 模板重新做一套相同方案，只要按本文中列出的文件逐个修改，就能复现当前仓库的整体能力。
