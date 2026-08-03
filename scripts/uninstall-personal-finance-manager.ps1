[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA "Programs\PersonalFinanceManager"),
    [switch]$RemoveUserData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$startMenuFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\PersonalFinanceManager"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "PersonalFinanceManager.lnk"
$userData = Join-Path $env:LOCALAPPDATA "PersonalFinanceManager"

if (Test-Path $startMenuFolder) {
    if ($PSCmdlet.ShouldProcess($startMenuFolder, "Remove Start Menu shortcuts")) {
        Remove-Item $startMenuFolder -Recurse -Force
    }
}

if (Test-Path $desktopShortcut) {
    if ($PSCmdlet.ShouldProcess($desktopShortcut, "Remove Desktop shortcut")) {
        Remove-Item $desktopShortcut -Force
    }
}

if (Test-Path $InstallPath) {
    if ($PSCmdlet.ShouldProcess($InstallPath, "Remove installed application files")) {
        Remove-Item $InstallPath -Recurse -Force
    }
}

if ($RemoveUserData -and (Test-Path $userData)) {
    if ($PSCmdlet.ShouldProcess($userData, "Remove local PersonalFinanceManager user data and settings")) {
        Remove-Item $userData -Recurse -Force
    }
}

Write-Host "PersonalFinanceManager uninstall completed." -ForegroundColor Green
if (-not $RemoveUserData) {
    Write-Host "Local user data was preserved at: $userData" -ForegroundColor Yellow
}
