$ErrorActionPreference='Stop'
$unity='C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) { throw 'Add the RequiemLantern folder in Unity Hub and select Unity 6000.6.0f1.' }
$project=Join-Path $PSScriptRoot 'RequiemLantern'
if (-not (Test-Path -LiteralPath (Join-Path $project 'Assets/Scenes/Main.unity')) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The Unity game project is missing. Restore the RequiemLantern subfolder before opening Unity.'
}
Write-Output ('Opening Unity game project: '+$project)
Start-Process -FilePath $unity -ArgumentList ('-projectPath "'+$project+'" -executeMethod RequiemLantern.Editor.ProjectSetup.OpenMainScene') -WindowStyle Normal
