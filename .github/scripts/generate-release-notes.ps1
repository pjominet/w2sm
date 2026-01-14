param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputFile = "release-notes.txt",

    [string[]]$IgnoredCommits = @(),

    [string]$GitHubOutputFile,

    [string]$GitHubOutputName = "body"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git is required to generate release notes."
}

$currentTag = if ($Version.StartsWith('v')) { $Version } else { "v$Version" }
$allTags = git tag --sort=-creatordate
$previousTag = $allTags | Where-Object { $_ -ne $currentTag } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($previousTag)) {
    $logEntries = git log --pretty="%s" | Out-String
} else {
    $logEntries = git log "$previousTag..HEAD" --pretty="%s" | Out-String
}

Write-Host "Previous tag: $previousTag"
Write-Host "Current tag: $currentTag"
Write-Host "Ignored commits containing: " + [string]::Join($IgnoredCommits, ", ")

# Split the log entries into an array of lines
$logEntriesArray = $logEntries -split "`r`n" | Where-Object { $_ -match '\S' }

$filteredEntries = @()
foreach ($entry in $logEntriesArray) {
    $trimmed = $entry.Trim()
    if (-not $trimmed) {
        continue
    }

    $skip = $false
    foreach ($ignored in $IgnoredCommits) {
        if ([string]::IsNullOrWhiteSpace($ignored)) {
            continue
        }

        if ($trimmed.Contains($ignored, [StringComparison]::OrdinalIgnoreCase)) {
            $skip = $true
            break
        }
    }

    if (-not $skip) {
        $filteredEntries += $trimmed
    }
}

if ($filteredEntries.Count -eq 0) {
    $filteredEntries = @("No noteworthy changes.")
}

$bulletList = $filteredEntries | ForEach-Object { "* $_" }
$bulletList -join [Environment]::NewLine | Set-Content -LiteralPath $OutputFile -Encoding UTF8

if ($GitHubOutputFile) {
    Add-Content -LiteralPath $GitHubOutputFile -Value "$GitHubOutputName<<EOF"
    Get-Content -LiteralPath $OutputFile | Add-Content -LiteralPath $GitHubOutputFile
    Add-Content -LiteralPath $GitHubOutputFile -Value "EOF"
}
