param(
  [string]$RemoteName = "origin"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$branch = git rev-parse --abbrev-ref HEAD
git add .
git status
$commitMessage = Read-Host "Commit message"
git commit -m $commitMessage
git push -u $RemoteName $branch
