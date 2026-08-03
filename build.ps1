param(
    [switch]$SkipPublicAudit
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host "PersonalFinanceManager build starting from $root"
$version = dotnet --version
Write-Host "Detected .NET SDK: $version"

function Invoke-PowerShellScript {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][string]$ScriptPath
    )

    Write-Host "Running $DisplayName"
    $global:LASTEXITCODE = 0
    & $ScriptPath
    $succeeded = $?
    $exitCode = $LASTEXITCODE
    $global:LASTEXITCODE = 0

    if (-not $succeeded -or $exitCode -ne 0) {
        throw "$DisplayName failed with exit code $exitCode."
    }
}

if (-not $SkipPublicAudit) {
    Invoke-PowerShellScript -DisplayName "public repository audit" -ScriptPath ".\scripts\audit-public-repo.ps1"
    Invoke-PowerShellScript -DisplayName "source consistency check" -ScriptPath ".\scripts\check-source-consistency.ps1"
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet @("restore", ".\PersonalFinanceManager.sln")
Invoke-DotNet @("build", ".\PersonalFinanceManager.sln", "-c", "Debug", "--no-restore")
Invoke-DotNet @("build", ".\PersonalFinanceManager.sln", "-c", "Release", "--no-restore")
Invoke-DotNet @("test", ".\PersonalFinanceManager.sln", "-c", "Release", "--no-build")
Write-Host "PersonalFinanceManager build completed successfully."
