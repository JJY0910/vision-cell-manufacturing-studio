# Windows Setup

## Target Environment

- Windows 10 or Windows 11.
- Visual Studio 2022.
- .NET 8 SDK, version `8.0.400` or a later .NET 8 feature-band SDK.
- Git for Windows.
- GitHub CLI.

This repository is a WPF application. Build and test it on Windows. Do not use Ubuntu CI for WPF validation.

## Visual Studio 2022

Install Visual Studio 2022 and select these workloads:

- `.NET desktop development`
- `Desktop development with C++` only if native vision experiments are needed later.

Recommended individual components:

- .NET 8 SDK.
- Windows 10/11 SDK.
- MSBuild tools.

## .NET 8 SDK

Install .NET 8 SDK `8.0.400` or a newer .NET 8 feature-band SDK from Microsoft:

```powershell
dotnet --list-sdks
```

Expected example:

```text
8.0.400 [C:\Program Files\dotnet\sdk]
```

The current `global.json` intentionally targets .NET 8 LTS:

```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

If only .NET 10 or another major SDK is installed, install .NET 8 SDK instead of changing `global.json`.

## Git

Install Git for Windows, then verify:

```powershell
git --version
```

## GitHub CLI

Install GitHub CLI, then authenticate:

```powershell
gh auth login
gh auth status
```

## Restore, Build, Test

From the repository root:

```powershell
cd C:\dev\VisionCell-Pemtron-WPF-Codex
dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Debug --no-restore
dotnet test .\VisionCell.sln -c Debug --no-build
dotnet build .\VisionCell.sln -c Release --no-restore
dotnet test .\VisionCell.sln -c Release --no-build
```

## Run WPF App

```powershell
dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj
```

If WPF build fails with SDK discovery errors, confirm that the .NET 8 SDK and Windows SDK are installed and that the command is running from Windows PowerShell.
