param([switch]$NoBrowser)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$url='http://127.0.0.1:8127/'
try {
    $state=Invoke-RestMethod ($url+'api/state') -TimeoutSec 2
    if ($state.battle.stageId -ne 'chapter1_beacon_defense') { throw 'Port occupied by a different service.' }
    if (-not $NoBrowser) { Start-Process $url }
    Write-Output "Preview is already running: $url"
    return
} catch { }
$dotnet=Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw '.NET SDK 10 is required for the local preview. The Unity project can be opened separately.' }
$output=Join-Path $projectRoot 'PreviewHost\bin\live'
& $dotnet.Source build (Join-Path $projectRoot 'PreviewHost\PreviewHost.csproj') --configuration Release --output $output --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Preview host build failed.' }
$logs=Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$dll=Join-Path $output 'PreviewHost.dll'
$process=Start-Process -FilePath $dotnet.Source -ArgumentList ('"'+$dll+'" "'+$projectRoot+'"') -WindowStyle Hidden -WorkingDirectory $projectRoot -RedirectStandardOutput (Join-Path $logs 'preview-host.log') -RedirectStandardError (Join-Path $logs 'preview-host-errors.log') -PassThru
$process.Id | Set-Content (Join-Path $logs 'preview-host.pid')
$ready=$false
for($attempt=0;$attempt -lt 40;$attempt++) {
    try { $state=Invoke-RestMethod ($url+'api/state') -TimeoutSec 1; if($state.battle){$ready=$true;break} } catch { Start-Sleep -Milliseconds 250 }
}
if (-not $ready) { throw 'Preview did not start. See Logs/preview-host-errors.log.' }
if (-not $NoBrowser) { Start-Process $url }
Write-Output "Preview running: $url"
