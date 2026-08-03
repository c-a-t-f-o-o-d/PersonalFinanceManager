[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ExtraTermsFile = ""
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
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    return $relativePath
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

function Get-RepositoryFiles {
    param([string]$RepositoryRoot)

    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git -ne $null) {
        Push-Location $RepositoryRoot
        try {
            $isRepo = $false
            try {
                $null = git rev-parse --is-inside-work-tree 2>$null
                $isRepo = $LASTEXITCODE -eq 0
            }
            catch {
                $isRepo = $false
            }

            if ($isRepo) {
                return @(git ls-files | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { Join-Path $RepositoryRoot $_ })
            }
        }
        finally {
            Pop-Location
        }
    }

    $excludedDirectories = @(
        ".git",
        ".github\workflows\_temp",
        ".vs",
        "artifacts",
        "bin",
        "obj",
        "TestResults"
    )

    return @(Get-ChildItem -Path $RepositoryRoot -File -Recurse -Force |
        Where-Object {
            $relative = Get-RelativeRepositoryPath -BasePath $RepositoryRoot -FullPath $_.FullName
            foreach ($directory in $excludedDirectories) {
                if ($relative.StartsWith($directory + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
                    $relative.StartsWith($directory + [System.IO.Path]::AltDirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
                    return $false
                }
            }

            return $true
        } |
        Select-Object -ExpandProperty FullName)
}

function Add-Finding {
    param(
        [System.Collections.Generic.List[string]]$Findings,
        [string]$Message
    )

    $Findings.Add($Message) | Out-Null
}

$rootPath = (Resolve-Path $Root).Path
$files = Get-RepositoryFiles -RepositoryRoot $rootPath
$findings = [System.Collections.Generic.List[string]]::new()

$forbiddenExtensions = @(".bacpac", ".bak", ".csv", ".dacpac", ".mdf", ".ldf", ".nfo", ".pdf", ".sqlplan", ".xls", ".xlsx", ".zip")
$allowedFixturePattern = [regex]::Escape("tests") + "[\\/]" + ".*" + "[\\/]Fixtures[\\/]"

foreach ($file in $files) {
    $relative = Get-RelativeRepositoryPath -BasePath $rootPath -FullPath $file
    $extension = [System.IO.Path]::GetExtension($file).ToLowerInvariant()

    if ($relative -match "(^|[\\/])docs([\\/]|$)") {
        Add-Finding $findings "docs content must not be committed: $relative"
    }

    if ($relative -match "(^|[\/])(screenshots[\/]private|assets[\/]screenshots[\/]private|sample-data[\/]private)([\/]|$)") {
        Add-Finding $findings "private screenshot/sample-data content must not be committed: $relative"
    }

    if ($relative.EndsWith(".csproj.user", [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.EndsWith("appsettings.Local.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.EndsWith("private-audit-terms.txt", [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.EndsWith(".private.txt", [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.EndsWith(".Local.json", [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Finding $findings "local/user-specific file must not be committed: $relative"
    }

    if ($forbiddenExtensions -contains $extension) {
        $isAllowedFixture = $relative -match $allowedFixturePattern -and ($extension -eq ".csv" -or $extension -eq ".xlsx")
        if (-not $isAllowedFixture) {
            Add-Finding $findings "private/export/archive/database file extension is not allowed: $relative"
        }
    }
}

$textExtensions = @(".cs", ".csproj", ".json", ".md", ".ps1", ".sql", ".txt", ".yml", ".yaml", ".props", ".targets", ".sln", ".editorconfig", ".gitattributes", ".gitignore")
$extraTerms = @()
if (-not [string]::IsNullOrWhiteSpace($ExtraTermsFile) -and (Test-Path $ExtraTermsFile)) {
    $extraTerms = @(Get-Content $ExtraTermsFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.TrimStart().StartsWith("#", [System.StringComparison]::Ordinal) })
}

foreach ($file in $files) {
    $relative = Get-RelativeRepositoryPath -BasePath $rootPath -FullPath $file
    $extension = [System.IO.Path]::GetExtension($file).ToLowerInvariant()
    if ($textExtensions -notcontains $extension) {
        continue
    }

    $content = Get-Content -Path $file -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) {
        continue
    }

    $legacyProductName = "PersonalFinanceManager" + "V2"
    if (Test-TextContainsOrdinalIgnoreCase -Text $content -Value $legacyProductName) {
        Add-Finding $findings "legacy product name found in $relative"
    }

    if ($content -match "\b192\.168\.") {
        Add-Finding $findings "private LAN IP address found in $relative"
    }

    $passwordMatches = [regex]::Matches($content, "Password\s*=\s*([^;`"'\r\n]+)", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($match in $passwordMatches) {
        $value = $match.Groups[1].Value.Trim()
        $allowed = @("YOUR_SQL_PASSWORD", "CHANGE_ME", "<password>", "{password}")
        if ($allowed -notcontains $value) {
            Add-Finding $findings "possible plaintext SQL password found in $relative"
        }
    }

    foreach ($term in $extraTerms) {
        if (Test-TextContainsOrdinalIgnoreCase -Text $content -Value $term) {
            Add-Finding $findings "extra private audit term found in $relative"
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host "Public repository audit failed:" -ForegroundColor Red
    foreach ($finding in $findings) {
        Write-Host " - $finding" -ForegroundColor Red
    }

    exit 1
}

Write-Host "Public repository audit passed." -ForegroundColor Green
$global:LASTEXITCODE = 0
