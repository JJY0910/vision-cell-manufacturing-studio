param(
  [string]$Root = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Initializing VisionCell solution in $Root"
Set-Location $Root

if (-not (Test-Path "VisionCell.sln")) {
  dotnet new sln -n VisionCell
}

$currentProjects = dotnet sln VisionCell.sln list

$projects = @(
  "src/VisionCell.Core/VisionCell.Core.csproj",
  "src/VisionCell.Equipment/VisionCell.Equipment.csproj",
  "src/VisionCell.Motion/VisionCell.Motion.csproj",
  "src/VisionCell.Vision/VisionCell.Vision.csproj",
  "src/VisionCell.Application/VisionCell.Application.csproj",
  "src/VisionCell.Persistence/VisionCell.Persistence.csproj",
  "src/VisionCell.Simulator/VisionCell.Simulator.csproj",
  "src/VisionCell.Telemetry/VisionCell.Telemetry.csproj",
  "src/VisionCell.App/VisionCell.App.csproj",
  "tests/VisionCell.Core.Tests/VisionCell.Core.Tests.csproj",
  "tests/VisionCell.Application.Tests/VisionCell.Application.Tests.csproj",
  "tests/VisionCell.Equipment.Tests/VisionCell.Equipment.Tests.csproj",
  "tests/VisionCell.Motion.Tests/VisionCell.Motion.Tests.csproj",
  "tests/VisionCell.Vision.Tests/VisionCell.Vision.Tests.csproj",
  "tests/VisionCell.Persistence.Tests/VisionCell.Persistence.Tests.csproj",
  "tests/VisionCell.App.Tests/VisionCell.App.Tests.csproj"
)

foreach ($project in $projects) {
  if ($currentProjects -notmatch [regex]::Escape($project)) {
    dotnet sln VisionCell.sln add $project
  }
}

Write-Host "Restore/build/test starting..."
dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Debug
Write-Host "Solution initialized."
