Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Debug --no-restore
dotnet test .\VisionCell.sln -c Debug --no-build
