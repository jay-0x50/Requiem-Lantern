param([string]$EditorRoot = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dataRoot = Join-Path $EditorRoot 'Data'
$sdkRoot = Join-Path $dataRoot 'DotNetSdk'
$compiler = Get-ChildItem (Join-Path $sdkRoot 'sdk') -Filter csc.dll -Recurse | Select-Object -First 1
$outDir = Join-Path $projectRoot 'Logs\SourceValidation'
New-Item -ItemType Directory -Force $outDir | Out-Null
$references = @()
$references += Get-ChildItem (Join-Path $dataRoot 'NetStandard\ref\2.1.0') -Filter *.dll
$references += Get-ChildItem (Join-Path $dataRoot 'Managed\UnityEngine') -Filter *.dll
$references += Get-ChildItem (Join-Path $dataRoot 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.2d-cross-platform-2d-7.0.0\ScriptAssemblies') -Filter *.dll
$sourceFiles = Get-ChildItem (Join-Path $projectRoot 'RequiemLantern\Assets') -Filter *.cs -Recurse
$scriptGuids = @{}
foreach ($source in $sourceFiles) {
    $metaPath = $source.FullName + '.meta'
    if (-not (Test-Path -LiteralPath $metaPath)) { throw "Missing Unity script metadata: $metaPath" }
    $match = [regex]::Match([IO.File]::ReadAllText($metaPath), '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$')
    if (-not $match.Success) { throw "Invalid Unity script GUID: $metaPath" }
    $guid = $match.Groups[1].Value.ToLowerInvariant()
    if ($scriptGuids.ContainsKey($guid)) { throw "Duplicate Unity script GUID: $metaPath and $($scriptGuids[$guid])" }
    $scriptGuids[$guid] = $metaPath
}
$response = @('-nologo','-target:library','-nostdlib+','-langversion:latest','-define:UNITY_EDITOR;UNITY_6000_0_OR_NEWER')
$response += '-out:"' + (Join-Path $outDir 'RequiemLantern.SourceCheck.dll') + '"'
$response += $references | ForEach-Object { '-r:"' + $_.FullName + '"' }
$response += $sourceFiles | ForEach-Object { '"' + $_.FullName + '"' }
$responsePath = Join-Path $outDir 'compile.rsp'
[IO.File]::WriteAllLines($responsePath,$response,[Text.UTF8Encoding]::new($false))
& (Join-Path $sdkRoot 'dotnet.exe') $compiler.FullName ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Unity reference-assembly compilation failed.' }
Write-Output 'PASS: All Unity script metadata GUIDs are valid and unique; C# sources compiled against installed Unity and UGUI/URP assemblies. Editor runtime and build still require an active Unity license.'
