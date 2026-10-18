# Shared by the Windows entry points. The required version comes from the project.
function Get-LanternUnityVersion {
    param([Parameter(Mandatory=$true)][string]$ProjectPath)
    $versionFile = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
    $versionText = [IO.File]::ReadAllText($versionFile)
    $match = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)\s*$')
    if (-not $match.Success) { throw "Cannot read the required Unity version: $versionFile" }
    return $match.Groups[1].Value
}

function Resolve-LanternUnityEditor {
    param(
        [Parameter(Mandatory=$true)][string]$ProjectPath,
        [string]$UnityPath
    )
    $requiredVersion = Get-LanternUnityVersion -ProjectPath $ProjectPath
    $candidates = [Collections.Generic.List[string]]::new()
    $override = $UnityPath
    if (-not $override) { $override = $env:UNITY_EDITOR_PATH }
    if ($override) {
        $candidates.Add($override)
        $candidates.Add([IO.Path]::Combine($override, 'Unity.exe'))
        $candidates.Add([IO.Path]::Combine($override, 'Editor', 'Unity.exe'))
    } else {
        foreach ($programRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
            if ($programRoot) { $candidates.Add((Join-Path $programRoot "Unity/Hub/Editor/$requiredVersion/Editor/Unity.exe")) }
        }
        # Older Hub installations keep custom editor locations in these files.
        if ($env:APPDATA) {
            foreach ($file in @('editors-v2.json','editors.json')) {
                $hubFile = Join-Path $env:APPDATA "UnityHub/$file"
                if (-not (Test-Path -LiteralPath $hubFile -PathType Leaf)) { continue }
                try {
                    $hub = Get-Content -LiteralPath $hubFile -Raw -Encoding UTF8 | ConvertFrom-Json
                    $entries = if ($hub.data) { @($hub.data) } else { @($hub) }
                    foreach ($entry in $entries) {
                        if ($entry.version -eq $requiredVersion -and $entry.location) {
                            $candidates.Add($entry.location)
                            $candidates.Add((Join-Path $entry.location 'Editor/Unity.exe'))
                            $candidates.Add((Join-Path $entry.location 'Unity.exe'))
                        }
                    }
                } catch { Write-Verbose "Could not read optional Hub editor list: $hubFile" }
            }
        }
        $onPath = Get-Command Unity.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($onPath) { $candidates.Add($onPath.Source) }
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf -ErrorAction SilentlyContinue)) { continue }
        $editor = Get-Item -LiteralPath $candidate
        if ($editor.Name -ne 'Unity.exe') { continue }
        $productVersion = $editor.VersionInfo.ProductVersion
        if ($productVersion -and ($productVersion -eq $requiredVersion -or $productVersion.StartsWith($requiredVersion + '_'))) {
            return $editor.FullName
        }
    }
    throw "Unity $requiredVersion was not found. Install this version in Unity Hub, or pass -UnityPath 'D:\Unity\$requiredVersion\Editor\Unity.exe'. UNITY_EDITOR_PATH also accepts a custom installation. See NOTEBOOK_SETUP.md."
}
