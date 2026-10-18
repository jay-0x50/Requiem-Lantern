param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tools/UnityEditor.ps1')
$project = Join-Path $PSScriptRoot 'RequiemLantern'
$unity = Resolve-LanternUnityEditor -ProjectPath $project -UnityPath $UnityPath
$logDirectory = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory ('unity-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Write-Output 'Close this project in the Unity editor before building from this script.'
Write-Output "Building Windows preview. Log: $logPath"
$buildArgs = '-batchmode -quit -projectPath "' + $project + '" -executeMethod RequiemLantern.Editor.ProjectSetup.BuildWindows -logFile "' + $logPath + '"'
$buildProcess = Start-Process -FilePath $unity -ArgumentList $buildArgs -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
$executable = Join-Path $PSScriptRoot 'Builds/Windows/RequiemLantern.exe'
$succeeded = (Test-Path -LiteralPath $logPath) -and (Select-String -LiteralPath $logPath -SimpleMatch 'Build Finished, Result: Success.' -Quiet)
if ($buildProcess.ExitCode -ne 0 -or -not $succeeded -or -not (Test-Path -LiteralPath $executable)) {
    throw "Unity build failed. Check the editor license, Windows Build Support, and log: $logPath"
}
Write-Output "Windows preview ready: $executable"
Write-Output 'Copy the entire Builds/Windows folder when transferring the playable build.'
