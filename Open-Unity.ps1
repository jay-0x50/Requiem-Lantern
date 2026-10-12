$ErrorActionPreference='Stop'
$unity='C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) { throw 'Add the RequiemLantern folder in Unity Hub and select Unity 6000.6.0f1.' }
$project=Join-Path $PSScriptRoot 'RequiemLantern'
Start-Process -FilePath $unity -ArgumentList ('-projectPath "'+$project+'"')
