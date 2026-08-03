[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [string]$ExtraTermsFile = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Add-Finding {
    param(
        [System.Collections.Generic.List[string]]$Findings,
        [string]$Message
    )

    $Findings.Add($Message) | Out-Null
}

function Get-RelativePackagePath {
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

function Test-TextContainsOrdinalIgnoreCase {
    param(
        [AllowNull()][string]$Text,
        [AllowNull()][string]$Value
    )

    if ([string]::IsNullOrEmpty($Text) -or [string]::IsNullOrEmpty($Value)) {
        return $false
    }

    return $Text.IndexOf($Value, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("PersonalFinanceManagerPackageAudit_" + [System.Guid]::NewGuid().ToString("N"))
$findings = [System.Collections.Generic.List[string]]::new()

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    Expand-Archive -LiteralPath $resolvedPackagePath -DestinationPath $tempRoot -Force

    $forbiddenExtensions = @(".bacpac", ".bak", ".csv", ".dacpac", ".ldf", ".mdf", ".nfo", ".pdf", ".sqlplan", ".xls", ".xlsx", ".zip")
    $forbiddenNames = @("appsettings.Local.json", "private-audit-terms.txt")
    $files = @(Get-ChildItem -Path $tempRoot -File -Recurse -Force)

    foreach ($file in $files) {
        $relative = Get-RelativePackagePath -BasePath $tempRoot -FullPath $file.FullName
        $extension = [System.IO.Path]::GetExtension($file.FullName).ToLowerInvariant()

        if ($forbiddenExtensions -contains $extension) {
            Add-Finding $findings "release package contains forbidden private/export/archive/database extension: $relative"
        }

        if ($forbiddenNames -contains $file.Name) {
            Add-Finding $findings "release package contains local/private file: $relative"
        }
    }

    $textExtensions = @(".config", ".json", ".md", ".ps1", ".sql", ".txt", ".xml", ".yml", ".yaml")
    $extraTerms = @()
    if (-not [string]::IsNullOrWhiteSpace($ExtraTermsFile) -and (Test-Path $ExtraTermsFile)) {
        $extraTerms = @(Get-Content $ExtraTermsFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.TrimStart().StartsWith("#", [System.StringComparison]::Ordinal) })
    }

    foreach ($file in $files) {
        $extension = [System.IO.Path]::GetExtension($file.FullName).ToLowerInvariant()
        if ($textExtensions -notcontains $extension) {
            continue
        }

        $relative = Get-RelativePackagePath -BasePath $tempRoot -FullPath $file.FullName
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -eq $content) {
            continue
        }

        $legacyProductName = "PersonalFinanceManager" + "V2"
        if (Test-TextContainsOrdinalIgnoreCase -Text $content -Value $legacyProductName) {
            Add-Finding $findings "legacy product name found in release package: $relative"
        }

        if ($content -match "\b192\.168\.") {
            Add-Finding $findings "private LAN IP address found in release package: $relative"
        }

        $passwordMatches = [regex]::Matches($content, 'Password\s*=\s*([^;`"''\r\n]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        foreach ($match in $passwordMatches) {
            $value = $match.Groups[1].Value.Trim()
            $allowed = @("YOUR_SQL_PASSWORD", "CHANGE_ME", "<password>", "{password}")
            if ($allowed -notcontains $value) {
                Add-Finding $findings "possible plaintext SQL password found in release package: $relative"
            }
        }

        foreach ($term in $extraTerms) {
            if (Test-TextContainsOrdinalIgnoreCase -Text $content -Value $term) {
                Add-Finding $findings "extra private audit term found in release package: $relative"
            }
        }
    }
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item $tempRoot -Recurse -Force
    }
}

if ($findings.Count -gt 0) {
    Write-Host "Release package audit failed:" -ForegroundColor Red
    foreach ($finding in $findings) {
        Write-Host " - $finding" -ForegroundColor Red
    }

    exit 1
}

Write-Host "Release package audit passed." -ForegroundColor Green
$global:LASTEXITCODE = 0
