param(
    [string]$OutputName = 'ControllerLab.exe',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$isDebug = $Configuration -eq 'Debug'
$compilerArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:x64'
    $(if ($isDebug) { '/optimize-' } else { '/optimize+' })
    $(if ($isDebug) { '/debug:full' } else { '/debug:pdbonly' })
    '/codepage:65001'
    "/win32manifest:$(Join-Path $project 'app.manifest')"
    "/out:$(Join-Path $project $OutputName)"
    "/resource:$(Join-Path $project 'Assets\controller.png'),ControllerLab.Assets.controller.png"
    "/resource:$(Join-Path $project 'Assets\stick-cap.png'),ControllerLab.Assets.stick-cap.png"
    "/resource:$(Join-Path $project 'Assets\dualsense.png'),ControllerLab.Assets.dualsense.png"
    "/resource:$(Join-Path $project 'Assets\dualsense-left-stick-cap.png'),ControllerLab.Assets.dualsense-left-stick-cap.png"
    "/resource:$(Join-Path $project 'Assets\dualsense-right-stick-cap.png'),ControllerLab.Assets.dualsense-right-stick-cap.png"
    "/resource:$(Join-Path $project 'Assets\dualSenseRegions.json'),ControllerLab.Assets.dualSenseRegions.json"
    "/resource:$(Join-Path $project 'Assets\dualSenseVisualStyles.json'),ControllerLab.Assets.dualSenseVisualStyles.json"
    "/resource:$(Join-Path $project 'Assets\xboxRegions.json'),ControllerLab.Assets.xboxRegions.json"
    "/resource:$(Join-Path $project 'Assets\LeftTopTriggerMask.png'),ControllerLab.Assets.LeftTopTriggerMask.png"
    "/resource:$(Join-Path $project 'Assets\RightTopTriggerMask.png'),ControllerLab.Assets.RightTopTriggerMask.png"
    "/reference:$(Join-Path $wpf 'PresentationFramework.dll')"
    "/reference:$(Join-Path $wpf 'PresentationCore.dll')"
    "/reference:$(Join-Path $wpf 'WindowsBase.dll')"
    "/reference:$(Join-Path $wpf 'UIAutomationProvider.dll')"
    "/reference:$(Join-Path $wpf 'UIAutomationTypes.dll')"
    "/reference:$(Join-Path $framework 'System.Xaml.dll')"
    "/reference:$(Join-Path $framework 'System.dll')"
    "/reference:$(Join-Path $framework 'System.Core.dll')"
    "/reference:$(Join-Path $framework 'System.Runtime.Serialization.dll')"
    "/reference:$(Join-Path $framework 'System.Xml.dll')"
    # Source set: every .cs in the project, recursively, minus build output,
    # published packages, docs and audit snapshots.
    #
    # Before 2026-09-22 this was a hard-coded list of the 20 root-level files.
    # That list is what kept every type in the project root: the structural split
    # (see docs/redesign/ControllerLab-结构拆分施工图.md) needs sources under
    # Models/ Controls/ Services/ Views/, and a hard-coded list cannot see them.
    # The glob below is behaviour-identical for the flat layout: the 20 listed
    # files were exactly the 20 root-level .cs files, and there are no .cs files
    # in any subdirectory, so nothing new is picked up today.
    (Get-ChildItem -LiteralPath $project -Filter '*.cs' -Recurse -File -ErrorAction Stop |
        Where-Object {
            $rel = $_.FullName.Substring($project.Length).TrimStart([char]'\', [char]'/')
            ($rel -notmatch '^(bin|obj|release|docs|Assets)([\\/]|$)') -and
            ($rel -notmatch '^audit(-[^\\/]+)?([\\/]|$)')
        } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName })
)
& (Join-Path $framework 'csc.exe') $compilerArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built: $(Join-Path $project $OutputName) [$Configuration]"
