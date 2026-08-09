[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Repository = "AWAYRA/AWAYRA-WPF"
)

$ErrorActionPreference = "Stop"

function Invoke-GhApi {
    param(
        [Parameter(Mandatory)] [string[]]$Arguments,
        [string]$InputJson
    )

    if ($null -ne $InputJson) {
        $InputJson | & gh api @Arguments --input -
    }
    else {
        & gh api @Arguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API command failed: gh api $($Arguments -join ' ')"
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI is required. Install it from https://cli.github.com/ and run 'gh auth login'."
}

& gh auth status --hostname github.com
if ($LASTEXITCODE -ne 0) {
    throw "Authenticate first with: gh auth login --hostname github.com --web"
}

$repo = Invoke-GhApi -Arguments @("repos/$Repository") | ConvertFrom-Json
if (-not $repo.permissions.admin) {
    throw "The authenticated account does not have admin permission for $Repository."
}

$description = "Free open-source Windows break reminder with a 20-20-20 eye timer, movement breaks, locally generated sounds, and no telemetry."
$homepage = "https://awayra.github.io/AWAYRA-WPF/"
$topics = @(
    "windows", "windows-11", "wpf", "dotnet", "csharp",
    "break-reminder", "eye-strain", "eye-care", "20-20-20", "screen-break",
    "stretch-reminder", "posture", "productivity", "wellness", "open-source",
    "privacy", "desktop-app", "system-tray", "no-telemetry", "work-break-timer"
)

Write-Host "Configuring $Repository for public release..." -ForegroundColor Cyan

if ($PSCmdlet.ShouldProcess($Repository, "Update repository metadata and merge behavior")) {
    Invoke-GhApi -Arguments @(
        "--method", "PATCH", "repos/$Repository",
        "-f", "visibility=public",
        "-f", "description=$description",
        "-f", "homepage=$homepage",
        "-F", "has_issues=true",
        "-F", "has_discussions=true",
        "-F", "has_projects=false",
        "-F", "has_wiki=false",
        "-F", "allow_squash_merge=true",
        "-F", "allow_merge_commit=false",
        "-F", "allow_rebase_merge=true",
        "-F", "allow_auto_merge=true",
        "-F", "delete_branch_on_merge=true"
    ) | Out-Null
}

if ($PSCmdlet.ShouldProcess($Repository, "Set repository topics")) {
    $topicsPayload = @{ names = $topics } | ConvertTo-Json -Compress
    Invoke-GhApi -Arguments @(
        "--method", "PUT", "repos/$Repository/topics",
        "-H", "Accept: application/vnd.github+json"
    ) -InputJson $topicsPayload | Out-Null
}

if ($PSCmdlet.ShouldProcess($Repository, "Enable security features")) {
    foreach ($endpoint in @(
        "repos/$Repository/vulnerability-alerts",
        "repos/$Repository/automated-security-fixes",
        "repos/$Repository/private-vulnerability-reporting"
    )) {
        try {
            Invoke-GhApi -Arguments @("--method", "PUT", $endpoint) | Out-Null
        }
        catch {
            Write-Warning "Could not enable $endpoint. The GitHub plan or organization policy may not support it."
        }
    }
}

if ($PSCmdlet.ShouldProcess($Repository, "Configure GitHub Pages from main/docs")) {
    $pagesPayload = @{
        source = @{
            branch = "main"
            path = "/docs"
        }
    } | ConvertTo-Json -Depth 4 -Compress

    & gh api "repos/$Repository/pages" *> $null
    if ($LASTEXITCODE -eq 0) {
        Invoke-GhApi -Arguments @("--method", "PUT", "repos/$Repository/pages") -InputJson $pagesPayload | Out-Null
    }
    else {
        Invoke-GhApi -Arguments @("--method", "POST", "repos/$Repository/pages") -InputJson $pagesPayload | Out-Null
    }
}

if ($PSCmdlet.ShouldProcess($Repository, "Protect the main branch")) {
    $protectionPayload = @{
        required_status_checks = @{
            strict = $true
            contexts = @("build-and-test", "installer")
        }
        enforce_admins = $false
        required_pull_request_reviews = @{
            dismiss_stale_reviews = $true
            require_code_owner_reviews = $false
            required_approving_review_count = 0
        }
        restrictions = $null
        required_linear_history = $true
        allow_force_pushes = $false
        allow_deletions = $false
        block_creations = $false
        required_conversation_resolution = $true
        lock_branch = $false
        allow_fork_syncing = $true
    } | ConvertTo-Json -Depth 6 -Compress

    try {
        Invoke-GhApi -Arguments @(
            "--method", "PUT", "repos/$Repository/branches/main/protection",
            "-H", "Accept: application/vnd.github+json"
        ) -InputJson $protectionPayload | Out-Null
    }
    catch {
        Write-Warning "Branch protection could not be applied. Check the organization plan and ruleset policy."
    }
}

$final = Invoke-GhApi -Arguments @("repos/$Repository") | ConvertFrom-Json
$pages = $null
try {
    $pages = Invoke-GhApi -Arguments @("repos/$Repository/pages") | ConvertFrom-Json
}
catch {
    Write-Warning "Pages verification was unavailable."
}

Write-Host "Public launch configuration complete." -ForegroundColor Green
[pscustomobject]@{
    Repository = $final.full_name
    Visibility = $final.visibility
    Description = $final.description
    Homepage = $final.homepage
    DiscussionsEnabled = $final.has_discussions
    Forking = if ($final.visibility -eq "public") { "Public repositories can be forked by design" } else { $final.allow_forking }
    PagesUrl = $pages.html_url
    DefaultBranch = $final.default_branch
} | Format-List