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
    (Join-Path $project 'ControllerCore.cs')
    (Join-Path $project 'BuildInfo.cs')
    (Join-Path $project 'StatusLabels.cs')
    (Join-Path $project 'ControllerRumble.cs')
    (Join-Path $project 'RumbleProfessional.cs')
    (Join-Path $project 'RumbleStudioPage.cs')
    (Join-Path $project 'JoystickAnalyzer.cs')
    (Join-Path $project 'JoystickTestViewModel.cs')
    (Join-Path $project 'JoystickTestPage.cs')
    (Join-Path $project 'ControllerHealthReport.cs')
    (Join-Path $project 'ControllerHealthCheckViewModel.cs')
    (Join-Path $project 'ControllerHealthCheckPage.cs')
    (Join-Path $project 'DualSenseMotion.cs')
    (Join-Path $project 'DualSenseMotionVisual.cs')
    (Join-Path $project 'DualSenseAdvanced.cs')
    (Join-Path $project 'DualSenseAdvancedPage.cs')
    (Join-Path $project 'XboxOverlay.cs')
    (Join-Path $project 'ControllerLabTheme.cs')
    (Join-Path $project 'ProductExperience.cs')
    (Join-Path $project 'ControllerLab.cs')
)
& (Join-Path $framework 'csc.exe') $compilerArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built: $(Join-Path $project $OutputName) [$Configuration]"
