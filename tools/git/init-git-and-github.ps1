param(
  [Parameter(Mandatory=$true)][string]$RepoName,
  [ValidateSet("public", "private", "internal")][string]$Visibility = "private",
  [string]$RemoteName = "origin"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  throw "GitHub CLI 'gh' is not installed. Install it first: https://cli.github.com/"
}

if (-not (Test-Path ".git")) {
  git init
  git branch -M main
}

try {
  gh auth status | Out-Null
} catch {
  gh auth login
}

git add .
if (-not (git status --porcelain)) {
  Write-Host "No changes to commit."
} else {
  git commit -m "chore: bootstrap VisionCell WPF portfolio"
}

$visibilityFlag = "--$Visibility"

gh repo create $RepoName $visibilityFlag --source=. --remote=$RemoteName --push

git checkout -B develop
git push -u $RemoteName develop

Write-Host "GitHub repo created/pushed: $RepoName"
Write-Host "Next: connect the repository in Codex web/app and enable code review if desired."
