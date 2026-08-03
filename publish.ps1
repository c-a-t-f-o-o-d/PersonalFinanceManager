param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$publishRoot = Join-Path $root "artifacts\publish"
$output = Join-Path $publishRoot "PersonalFinanceManager"
$zipPath = Join-Path $publishRoot "PersonalFinanceManager-$Runtime.zip"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }

Write-Host "Publishing PersonalFinanceManager ($Configuration, $Runtime, self-contained=$selfContainedValue)" -ForegroundColor Cyan

dotnet publish .\src\PersonalFinanceManager.App\PersonalFinanceManager.App.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedValue `
    -o $output

Copy-Item .\LICENSE $output -Force
Copy-Item .\README.md $output -Force
Copy-Item .\PRIVACY.md $output -Force
Copy-Item .\INSTALL.md $output -Force
Copy-Item .\scripts\install-personal-finance-manager.ps1 $output -Force
Copy-Item .\scripts\uninstall-personal-finance-manager.ps1 $output -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zipPath -Force

Write-Host "Published to: $output" -ForegroundColor Green
Write-Host "Package: $zipPath" -ForegroundColor Green
