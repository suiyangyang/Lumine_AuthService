param(
    [switch]$SkipBootstrap,
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Split-Path -Parent $scriptRoot
$composeFile = Join-Path $solutionRoot 'docker-compose.localdb.yml'
$serverProject = Join-Path $solutionRoot 'Lumine.AuthServer\Lumine.AuthServer.csproj'
$containerName = 'lumine-auth-mysql-dev'
$stdoutLog = Join-Path $scriptRoot '.init-local-db.stdout.log'
$stderrLog = Join-Path $scriptRoot '.init-local-db.stderr.log'
$envFile = Join-Path $solutionRoot '.env'

if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq '' -or $line.StartsWith('#')) {
            return
        }

        $name, $value = $line -split '=', 2
        if ($name -and $value) {
            [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), 'Process')
        }
    }
}

$mysqlPassword = $env:LUMINE_AUTH_MYSQL_PASSWORD
$mysqlUser = $env:LUMINE_AUTH_MYSQL_USER
$mysqlDatabase = $env:LUMINE_AUTH_MYSQL_DATABASE
$adminPassword = $env:LUMINE_AUTH_ADMIN_PASSWORD

if ([string]::IsNullOrWhiteSpace($mysqlDatabase)) {
    throw 'Set LUMINE_AUTH_MYSQL_DATABASE in .env before running this script.'
}

if ([string]::IsNullOrWhiteSpace($mysqlUser)) {
    throw 'Set LUMINE_AUTH_MYSQL_USER in .env before running this script.'
}

if ([string]::IsNullOrWhiteSpace($mysqlPassword)) {
    throw 'Set LUMINE_AUTH_MYSQL_PASSWORD in .env before running this script.'
}

if ([string]::IsNullOrWhiteSpace($env:LUMINE_AUTH_MYSQL_ROOT_PASSWORD)) {
    throw 'Set LUMINE_AUTH_MYSQL_ROOT_PASSWORD in .env before running this script.'
}

if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw 'Set LUMINE_AUTH_ADMIN_PASSWORD in .env before running this script.'
}

$connectionString = "server=localhost;port=3307;database=$mysqlDatabase;user=$mysqlUser;password=$mysqlPassword;"

function Get-FileTextOrEmpty {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        return Get-Content $Path -Raw
    }

    return ''
}

function Test-MySqlReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerName
    )

    & docker exec -e MYSQL_PWD=$mysqlPassword $ContainerName mysql -h 127.0.0.1 -P 3306 "-u$mysqlUser" -N -s -e "SELECT 1;" 1>$null 2>$null
    return ($LASTEXITCODE -eq 0)
}

if (-not (Test-Path $composeFile)) {
    throw "Compose file not found: $composeFile"
}

if (-not (Test-Path $serverProject)) {
    throw "Server project not found: $serverProject"
}

Write-Host '==> Preparing local MySQL container...'
$existingContainerId = docker ps -a --filter "name=^/$containerName$" --format "{{.ID}}"
if ($existingContainerId) {
    Write-Host '==> Reusing existing container...'
    docker start $containerName | Out-Host
}
else {
    docker compose -f $composeFile up -d auth-mysql-dev | Out-Host
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if (Test-MySqlReady -ContainerName $containerName) {
        break
    }

    Start-Sleep -Seconds 2
}

$readyCheckFailed = -not (Test-MySqlReady -ContainerName $containerName)

if ($readyCheckFailed) {
    docker logs $containerName --tail 120 | Out-Host
    throw 'MySQL container did not become reachable in time.'
}

Write-Host '==> MySQL is ready on localhost:3307'

if ($SkipBootstrap) {
    Write-Host '==> Bootstrap skipped. Database container is running.'
    exit 0
}

if (Test-Path $stdoutLog) { Remove-Item $stdoutLog -Force }
if (Test-Path $stderrLog) { Remove-Item $stderrLog -Force }

Write-Host '==> Starting AuthServer for migrations and seed data...'
$process = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $serverProject) `
    -WorkingDirectory $solutionRoot `
    -Environment @{
        'ConnectionStrings__DefaultConnection' = $connectionString
        'SeedData__AdminPassword' = $adminPassword
    } `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog `
    -PassThru

$started = $false
try {
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            $stdout = Get-FileTextOrEmpty -Path $stdoutLog
            $stderr = Get-FileTextOrEmpty -Path $stderrLog
            throw "AuthServer exited before bootstrap completed.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
        }

        $stdout = Get-FileTextOrEmpty -Path $stdoutLog
        if ($stdout -match 'Now listening on:' -or $stdout -match 'Application started\.') {
            $started = $true
            break
        }

        Start-Sleep -Seconds 2
        $process.Refresh()
    }

    if (-not $started) {
        throw 'AuthServer did not finish bootstrap in time.'
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

Write-Host '==> Migrations and seed completed. Verifying result...'
docker exec -e MYSQL_PWD=$mysqlPassword $containerName mysql -h 127.0.0.1 -P 3306 "-u$mysqlUser" -D $mysqlDatabase -e "SHOW TABLES; SELECT COUNT(*) AS UserCount FROM Users; SELECT COUNT(*) AS RoleCount FROM Roles; SELECT COUNT(*) AS PermissionCount FROM Permissions; SELECT COUNT(*) AS ClientCount FROM OidcClients;" | Out-Host

Write-Host ''
Write-Host 'Local database initialization completed.'
Write-Host "Connection string: $connectionString"
Write-Host 'Default admin: admin / value from LUMINE_AUTH_ADMIN_PASSWORD'
