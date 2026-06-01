param(
  [string]$IssueSeedDirectory = "docs/issue-seeds"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  throw "GitHub CLI 'gh' is not installed."
}

Get-ChildItem $IssueSeedDirectory -Filter "*.md" | ForEach-Object {
  $content = Get-Content $_.FullName -Raw
  $firstLine = ($content -split "`n")[0].Trim()
  $title = $firstLine.TrimStart('#').Trim()
  gh issue create --title $title --body $content
}
