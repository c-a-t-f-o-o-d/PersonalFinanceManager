[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipBuild,
    [switch]$SkipPublicAudit,
    [string]$ExtraTermsFile = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root

if ($Configuration.StartsWith("-", [System.StringComparison]::Ordinal) -or $Runtime.StartsWith("-", [System.StringComparison]::Ordinal)) {
    throw "Package parameters were parsed incorrectly. Use named parameters such as -SkipBuild and -SkipPublicAudit."
}

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

function Invoke-DotNetPublish {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$ConfigurationName,
        [Parameter(Mandatory = $true)][string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)][string]$SelfContainedValue,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    & dotnet publish $ProjectPath `
        -c $ConfigurationName `
        -r $RuntimeIdentifier `
        --self-contained $SelfContainedValue `
        -o $OutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
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
}

if (-not $SkipBuild) {
    Invoke-CheckedPowerShell -DisplayName "build before packaging" -Command {
        if ($SkipPublicAudit) {
            & .\build.ps1 -SkipPublicAudit
        }
        else {
            & .\build.ps1
        }
    }
}

$releaseRoot = Join-Path $root "artifacts\release"
$stagePath = Join-Path $releaseRoot "PersonalFinanceManager"
$packageName = if ($SelfContained) { "PersonalFinanceManager-$Runtime-self-contained.zip" } else { "PersonalFinanceManager-$Runtime-framework-dependent.zip" }
$packagePath = Join-Path $releaseRoot $packageName
$hashPath = "$packagePath.sha256"

if (Test-Path $stagePath) {
    Remove-Item $stagePath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stagePath | Out-Null

$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }
Write-Host "Publishing PersonalFinanceManager ($Configuration, $Runtime, self-contained=$selfContainedValue)" -ForegroundColor Cyan

Invoke-DotNetPublish `
    -ProjectPath ".\src\PersonalFinanceManager.App\PersonalFinanceManager.App.csproj" `
    -ConfigurationName $Configuration `
    -RuntimeIdentifier $Runtime `
    -SelfContainedValue $selfContainedValue `
    -OutputPath $stagePath

Copy-Item .\LICENSE $stagePath -Force
Copy-Item .\README.md $stagePath -Force
Copy-Item .\PRIVACY.md $stagePath -Force
Copy-Item .\CHANGELOG.md $stagePath -Force
Copy-Item .\INSTALL.md $stagePath -Force
Copy-Item .\IMPORTS.md $stagePath -Force
Copy-Item .\scripts\install-personal-finance-manager.ps1 $stagePath -Force
Copy-Item .\scripts\uninstall-personal-finance-manager.ps1 $stagePath -Force

@"
PersonalFinanceManager Release Package
=====================

Quick start:
1. Extract this ZIP.
2. Run install-personal-finance-manager.ps1 from PowerShell, or launch PersonalFinanceManager.exe directly.
3. On first launch, use Setup > Use LocalDB Now for a local evaluation database.
4. Optional: load demo data only into an empty evaluation database.

See INSTALL.md, README.md, and PRIVACY.md for details.
"@ | Set-Content -Path (Join-Path $stagePath "README-FIRST.txt") -Encoding UTF8

if (Test-Path $packagePath) {
    Remove-Item $packagePath -Force
}

Compress-Archive -Path (Join-Path $stagePath "*") -DestinationPath $packagePath -Force
$hash = Get-FileHash -Algorithm SHA256 -Path $packagePath
"$($hash.Hash)  $packageName" | Set-Content -Path $hashPath -Encoding ASCII

Invoke-CheckedPowerShell -DisplayName "release package audit" -Command {
    if (-not [string]::IsNullOrWhiteSpace($ExtraTermsFile)) {
        & .\scripts\check-release-package.ps1 -PackagePath $packagePath -ExtraTermsFile $ExtraTermsFile
    }
    else {
        & .\scripts\check-release-package.ps1 -PackagePath $packagePath
    }
}

Write-Host "Release package: $packagePath" -ForegroundColor Green
Write-Host "SHA256: $hashPath" -ForegroundColor Green
