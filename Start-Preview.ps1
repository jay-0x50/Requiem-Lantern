param([switch]$NoBrowser)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$url='http://127.0.0.1:8127/'
$state=$null
try { $state=Invoke-RestMethod ($url+'api/state') -TimeoutSec 2 } catch { }
if ($state) {
    if ($state.application -eq 'RequiemLantern' -and $state.runtimeRevision -eq 'sync-material-v15') {
        if (-not $NoBrowser) { Start-Process $url }
        Write-Output "Preview is already running: $url"
        return
    }
    $pidFile=Join-Path $projectRoot 'Logs/preview-host.pid'
    $previousHost=$null
    if (Test-Path -LiteralPath $pidFile) {
        $previousPid=0
        if ([int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(),[ref]$previousPid)) {
            $previousHost=Get-CimInstance Win32_Process -Filter "ProcessId=$previousPid" -ErrorAction SilentlyContinue
        }
    }
    if (-not $previousHost -or -not $previousHost.CommandLine.Contains($projectRoot) -or -not $previousHost.CommandLine.Contains('PreviewHost.dll')) {
        throw 'Port 8127 is occupied. Close the older preview process before starting this version.'
    }
    Stop-Process -Id $previousHost.ProcessId -ErrorAction Stop
}
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
