[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$FailOnWarnings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RelativeRepositoryPath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$FullPath
    )

    $resolvedBase = (Resolve-Path -LiteralPath $BasePath).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $resolvedFull = (Resolve-Path -LiteralPath $FullPath).Path
    $baseUri = [System.Uri]::new($resolvedBase + [System.IO.Path]::DirectorySeparatorChar)
    $fullUri = [System.Uri]::new($resolvedFull)
    $relativeUri = $baseUri.MakeRelativeUri($fullUri).ToString()
    return [System.Uri]::UnescapeDataString($relativeUri).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Add-Finding {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Message
    )

    $List.Add($Message) | Out-Null
}

$rootPath = (Resolve-Path $Root).Path
$sourceFiles = @(Get-ChildItem -Path (Join-Path $rootPath "src") -Filter *.cs -File -Recurse -ErrorAction SilentlyContinue)
$testFiles = @(Get-ChildItem -Path (Join-Path $rootPath "tests") -Filter *.cs -File -Recurse -ErrorAction SilentlyContinue)
$projectFiles = @(Get-ChildItem -Path $rootPath -Filter *.csproj -File -Recurse -ErrorAction SilentlyContinue)
$sqlResourceFiles = @(Get-ChildItem -Path (Join-Path $rootPath "src\PersonalFinanceManager.Data\Sql\Queries") -Filter *.sql -File -Recurse -ErrorAction SilentlyContinue)
$migrationFiles = @(Get-ChildItem -Path (Join-Path $rootPath "sql\migrations") -Filter *.sql -File -Recurse -ErrorAction SilentlyContinue)
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

foreach ($file in $sourceFiles) {
    $relative = Get-RelativeRepositoryPath -BasePath $rootPath -FullPath $file.FullName
    $content = Get-Content -Path $file.FullName -Raw

    if ($relative.StartsWith("src\PersonalFinanceManager.Core\", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($content -match "using\s+PersonalFinanceManager\.(App|Data|Application)" -or
            $content -match "Microsoft\.Data\.SqlClient" -or
            $content -match "System\.Windows\.Forms") {
            Add-Finding $errors "Core project contains forbidden infrastructure/UI reference: $relative"
        }
    }

    if ($relative.StartsWith("src\PersonalFinanceManager.Application\", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($content -match "using\s+PersonalFinanceManager\.(App|Data)" -or
            $content -match "Microsoft\.Data\.SqlClient" -or
            $content -match "System\.Windows\.Forms") {
            Add-Finding $errors "Application contracts project contains forbidden UI/data reference: $relative"
        }
    }

    if ($relative.StartsWith("src\PersonalFinanceManager.Data\", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($content -match "using\s+PersonalFinanceManager\.App(\.|;)" -or $content -match "System\.Windows\.Forms") {
            Add-Finding $errors "Data project contains forbidden UI reference: $relative"
        }
    }

    if ($relative.StartsWith("src\PersonalFinanceManager.App\UI\Views\", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($content -match "using\s+PersonalFinanceManager\.Data\.Repositories") {
            Add-Finding $errors "View references a repository directly instead of a workflow service: $relative"
        }
    }

    if ($content -match "System\.Windows\.Forms\.DataVisualization") {
        Add-Finding $errors "Removed WinForms charting namespace found: $relative"
    }

    if ($content -match "AddWithValue") {
        Add-Finding $errors "SqlParameter.AddWithValue found in source: $relative"
    }

    if ($content -match 'CommandText\s*=\s*\$?"""') {
        Add-Finding $warnings "large inline SQL block may remain in C# source: $relative"
    }
}

foreach ($file in $projectFiles) {
    $relative = Get-RelativeRepositoryPath -BasePath $rootPath -FullPath $file.FullName
    $content = Get-Content -Path $file.FullName -Raw
    if ($relative -ieq "src\PersonalFinanceManager.Application\PersonalFinanceManager.Application.csproj" -and ($content -match "PersonalFinanceManager\.Data" -or $content -match "PersonalFinanceManager\.App")) {
        Add-Finding $errors "PersonalFinanceManager.Application project must not reference App or Data."
    }
}

Write-Host "Source consistency summary:" -ForegroundColor Cyan
Write-Host " - Source C# files: $($sourceFiles.Count)"
Write-Host " - Test C# files: $($testFiles.Count)"
Write-Host " - Project files: $($projectFiles.Count)"
Write-Host " - Embedded SQL resources: $($sqlResourceFiles.Count)"
Write-Host " - Runtime migrations: $($migrationFiles.Count)"

if ($warnings.Count -gt 0) {
    Write-Host "Source consistency warnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host " - $warning" -ForegroundColor Yellow
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Source consistency check failed:" -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host " - $error" -ForegroundColor Red
    }

    exit 1
}

if ($FailOnWarnings -and $warnings.Count -gt 0) {
    Write-Host "Source consistency warnings were treated as failures." -ForegroundColor Red
    exit 1
}

Write-Host "Source consistency check passed." -ForegroundColor Green
$global:LASTEXITCODE = 0
