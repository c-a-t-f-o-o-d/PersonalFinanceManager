[CmdletBinding()]
param(
    [switch]$SkipPackage,
    [switch]$SelfContainedPackage,
    [switch]$SkipPublicAudit,
    [string]$ExtraTermsFile = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root

Write-Host "PersonalFinanceManager public baseline verification starting from $root" -ForegroundColor Cyan

function Invoke-CheckedPowerShell {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    Write-Host "Running $DisplayName" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    & $Command
    $succeeded = $?
    $exitCode = $global:LASTEXITCODE
    $global:LASTEXITCODE = 0

    if (-not $succeeded -or $exitCode -ne 0) {
        throw "$DisplayName failed with exit code $exitCode."
    }
}

if (-not $SkipPublicAudit) {
    Invoke-CheckedPowerShell -DisplayName "public repository audit" -Command {
        if (-not [string]::IsNullOrWhiteSpace($ExtraTermsFile)) {
            & .\scripts\audit-public-repo.ps1 -ExtraTermsFile $ExtraTermsFile
        }
        else {
            & .\scripts\audit-public-repo.ps1
        }
    }

    Invoke-CheckedPowerShell -DisplayName "source consistency check" -Command {
        & .\scripts\check-source-consistency.ps1
    }
}

Invoke-CheckedPowerShell -DisplayName "clean build and tests" -Command {
    & .\build.ps1 -SkipPublicAudit
}

if (-not $SkipPackage) {
    Invoke-CheckedPowerShell -DisplayName "release package creation and audit" -Command {
        if ($SelfContainedPackage -and -not [string]::IsNullOrWhiteSpace($ExtraTermsFile)) {
            & .\scripts\package-release.ps1 -SkipBuild -SkipPublicAudit -SelfContained -ExtraTermsFile $ExtraTermsFile
        }
        elseif ($SelfContainedPackage) {
            & .\scripts\package-release.ps1 -SkipBuild -SkipPublicAudit -SelfContained
        }
        elseif (-not [string]::IsNullOrWhiteSpace($ExtraTermsFile)) {
            & .\scripts\package-release.ps1 -SkipBuild -SkipPublicAudit -ExtraTermsFile $ExtraTermsFile
        }
        else {
            & .\scripts\package-release.ps1 -SkipBuild -SkipPublicAudit
        }
    }
}

Write-Host "PersonalFinanceManager public baseline verification completed successfully." -ForegroundColor Green
