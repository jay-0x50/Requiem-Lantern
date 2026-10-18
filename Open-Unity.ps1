param([string]$UnityPath, [switch]$CheckOnly)
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'RequiemLantern'
if (-not (Test-Path -LiteralPath (Join-Path $project 'Assets/Scenes/Main.unity')) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The Unity game project is missing. Restore the RequiemLantern subfolder before opening Unity.'
}
. (Join-Path $PSScriptRoot 'Tools/UnityEditor.ps1')
$unity=Resolve-LanternUnityEditor -ProjectPath $project -UnityPath $UnityPath
if ($CheckOnly) {
    [pscustomobject]@{Project=$project;RequiredVersion=(Get-LanternUnityVersion $project);Editor=$unity;Scene='Assets/Scenes/Main.unity'}
    return
}
Write-Output ('Opening Unity game project: '+$project)
Start-Process -FilePath $unity -ArgumentList ('-projectPath "'+$project+'" -executeMethod RequiemLantern.Editor.ProjectSetup.OpenMainScene') -WindowStyle Normal
