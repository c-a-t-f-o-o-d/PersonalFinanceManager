[CmdletBinding()]
param(
    [string]$SourcePath = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA "Programs\PersonalFinanceManager"),
    [switch]$DesktopShortcut,
    [switch]$NoStartMenuShortcut
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepositoryRoot {
    param([string]$Path)

    $candidate = (Resolve-Path $Path).Path
    while (-not [string]::IsNullOrWhiteSpace($candidate)) {
        if (Test-Path (Join-Path $candidate "PersonalFinanceManager.sln")) {
            return $candidate
        }

        $parent = Split-Path -Parent $candidate
        if ([string]::Equals($parent, $candidate, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $candidate = $parent
    }

    return $null
}

function Get-PersonalFinanceManagerExecutable {
    param([string]$Path)

    $candidates = @(
        (Join-Path $Path "PersonalFinanceManager.exe"),
        (Join-Path $Path "PersonalFinanceManager.App.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function New-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$WorkingDirectory
    )

    $directory = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = "PersonalFinanceManager personal finance tracker"
    $shortcut.Save()
}

$resolvedSourcePath = (Resolve-Path $SourcePath).Path
$sourceExe = Get-PersonalFinanceManagerExecutable -Path $resolvedSourcePath

if ($null -eq $sourceExe) {
    $repositoryRoot = Get-RepositoryRoot -Path $resolvedSourcePath
    if ($null -ne $repositoryRoot) {
        $releaseStagePath = Join-Path $repositoryRoot "artifacts\release\PersonalFinanceManager"
        $sourceExe = if (Test-Path $releaseStagePath) { Get-PersonalFinanceManagerExecutable -Path $releaseStagePath } else { $null }
        if ($null -ne $sourceExe) {
            Write-Host "Detected source checkout. Installing from latest release staging folder:" -ForegroundColor Yellow
            Write-Host $releaseStagePath -ForegroundColor Yellow
            $resolvedSourcePath = (Resolve-Path $releaseStagePath).Path
        }
        else {
            throw @"
This script installs an extracted PersonalFinanceManager release package.

You appear to be running it from a source checkout:
$repositoryRoot

To create and install a release package from source, run:
    .\scripts\package-release.ps1
    .\scripts\install-personal-finance-manager.ps1 -SourcePath .\artifacts\release\PersonalFinanceManager

For development, run the application directly instead:
    dotnet run --project .\src\PersonalFinanceManager.App\PersonalFinanceManager.App.csproj
"@
        }
    }
    else {
        throw "Could not find PersonalFinanceManager.exe or PersonalFinanceManager.App.exe under '$resolvedSourcePath'. Run this script from an extracted PersonalFinanceManager release package."
    }
}

$sourceDirectory = (Resolve-Path (Split-Path -Parent $sourceExe)).Path
$resolvedInstallPath = if (Test-Path $InstallPath) { (Resolve-Path $InstallPath).Path } else { $InstallPath }

if ([string]::Equals($sourceDirectory, $resolvedInstallPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "PersonalFinanceManager is already running from the install path: $InstallPath" -ForegroundColor Yellow
}
else {
    if (Test-Path $InstallPath) {
        Remove-Item $InstallPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
    Copy-Item -Path (Join-Path $resolvedSourcePath "*") -Destination $InstallPath -Recurse -Force
}

$installedExe = Get-PersonalFinanceManagerExecutable -Path $InstallPath
if ($null -eq $installedExe) {
    throw "Installation completed but PersonalFinanceManager.exe could not be found under '$InstallPath'."
}

if (-not $NoStartMenuShortcut) {
    $startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\PersonalFinanceManager\PersonalFinanceManager.lnk"
    New-Shortcut -ShortcutPath $startMenu -TargetPath $installedExe -WorkingDirectory $InstallPath
    Write-Host "Start Menu shortcut created: $startMenu" -ForegroundColor Green
}

if ($DesktopShortcut) {
    $desktop = [Environment]::GetFolderPath("DesktopDirectory")
    $desktopShortcut = Join-Path $desktop "PersonalFinanceManager.lnk"
    New-Shortcut -ShortcutPath $desktopShortcut -TargetPath $installedExe -WorkingDirectory $InstallPath
    Write-Host "Desktop shortcut created: $desktopShortcut" -ForegroundColor Green
}

Write-Host "PersonalFinanceManager installed to: $InstallPath" -ForegroundColor Green
Write-Host "Launch: $installedExe" -ForegroundColor Green
