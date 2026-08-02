[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'artifacts\publish'
}

$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$repositoryRoot = [IO.Path]::GetFullPath($repositoryRoot)
$relativeOutput = [IO.Path]::GetRelativePath($repositoryRoot, $OutputRoot)
$outputIsOutsideRepository = [string]::IsNullOrWhiteSpace($relativeOutput) -or $relativeOutput.StartsWith('..' + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($relativeOutput)
if ($outputIsOutsideRepository) {
    throw "OutputRoot 必须位于仓库目录内：$repositoryRoot"
}

$browserPublish = Join-Path $OutputRoot 'browser'
$serverPublish = Join-Path $OutputRoot 'server'
$browserProject = Join-Path $repositoryRoot 'Lumine.AuthPortal\Lumine.AuthPortal.Browser\Lumine.AuthPortal.Browser.csproj'
$serverProject = Join-Path $repositoryRoot 'Lumine.AuthServer\Lumine.AuthServer.csproj'

if (Test-Path -LiteralPath $OutputRoot) {
    Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') 执行失败，退出码：$LASTEXITCODE"
    }
}

Invoke-Dotnet @('publish', $browserProject, '-c', $Configuration, '-o', $browserPublish, '--no-restore')
Invoke-Dotnet @('publish', $serverProject, '-c', $Configuration, '-o', $serverPublish, '/p:SkipBrowserBuild=true', '/p:UseAppHost=false', '--no-restore')

$browserSiteRoot = Join-Path $browserPublish 'wwwroot'
if (-not (Test-Path -LiteralPath (Join-Path $browserSiteRoot 'index.html'))) {
    $browserSiteRoot = $browserPublish
}

$browserIndex = Join-Path $browserSiteRoot 'index.html'
$browserRuntime = Join-Path $browserSiteRoot '_framework\dotnet.js'
if (-not (Test-Path -LiteralPath $browserIndex) -or -not (Test-Path -LiteralPath $browserRuntime)) {
    throw "Browser publish 不是可部署站点根目录：$browserSiteRoot"
}

$serverWwwroot = Join-Path $serverPublish 'wwwroot'
if (Test-Path -LiteralPath $serverWwwroot) {
    Remove-Item -LiteralPath $serverWwwroot -Recurse -Force
}
New-Item -ItemType Directory -Path $serverWwwroot -Force | Out-Null
Copy-Item -Path (Join-Path $browserSiteRoot '*') -Destination $serverWwwroot -Recurse -Force

$wasmFile = Get-ChildItem -LiteralPath (Join-Path $serverWwwroot '_framework') -Recurse -File -Filter '*.wasm' | Select-Object -First 1
if ($null -eq $wasmFile) {
    throw "合站发布结果缺少 WASM 文件：$serverWwwroot"
}

Write-Host "Browser site: $browserSiteRoot"
Write-Host "Combined server publish: $serverPublish"
Write-Host "WASM: $($wasmFile.Name)"
