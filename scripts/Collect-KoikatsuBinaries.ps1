# Collect-KoikatsuBinaries.ps1
# Windows PowerShell 5.1 / PowerShell 7+.
# Copies a *selected* set of assemblies to a local zip. No network calls.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [string]$OutputZip = (Join-Path (Get-Location).Path 'Koikatsu-debug-binaries.zip'),
    [switch]$IncludeExecutables,
    [switch]$IncludeDiagnostics,
    [switch]$IncludeAssetInventory,
    [string[]]$ExtraRelativeFiles = @(),
    [ValidateRange(1, 2000)] [int]$MaxSingleFileMB = 100,
    [ValidateRange(1, 4000)] [int]$MaxTotalMB = 350
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:Root = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $GameRoot).Path)
$script:Root = $script:Root.TrimEnd([char[]]@('\', '/'))
if ($script:Root.Length -eq 2 -and $script:Root[1] -eq ':') { $script:Root += [IO.Path]::DirectorySeparatorChar }
$destination = [IO.Path]::GetFullPath($OutputZip)
$staging = Join-Path ([IO.Path]::GetTempPath()) ('kk-inspect-' + [guid]::NewGuid().ToString('N'))
$script:Staging = $staging
$script:Seen = @{}
$script:Manifest = New-Object System.Collections.Generic.List[object]
$script:TotalBytes = [long]0
$maxSingleBytes = [long]$MaxSingleFileMB * 1MB
$maxTotalBytes = [long]$MaxTotalMB * 1MB

function Add-DebugFile {
    param([System.IO.FileInfo]$File)

    if ($null -eq $File -or -not $File.Exists) { return }
    $full = [IO.Path]::GetFullPath($File.FullName)
    $prefix = $script:Root
    if (-not $prefix.EndsWith([IO.Path]::DirectorySeparatorChar)) {
        $prefix += [IO.Path]::DirectorySeparatorChar
    }
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "File is outside GameRoot: $full"
    }
    $relative = $full.Substring($prefix.Length)
    if ($script:Seen.ContainsKey($relative.ToLowerInvariant())) { return }
    if ($File.Length -gt $maxSingleBytes -or
        $script:TotalBytes + $File.Length -gt $maxTotalBytes) {
        Write-Warning "Skipped due to size limit: $relative ($($File.Length) bytes)"
        return
    }

    $target = Join-Path $script:Staging $relative
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    Copy-Item -LiteralPath $full -Destination $target -Force
    $script:Seen[$relative.ToLowerInvariant()] = $true
    $script:TotalBytes += $File.Length

    $version = ''
    try { $version = $File.VersionInfo.FileVersion } catch {}
    $script:Manifest.Add([pscustomobject]@{
        RelativePath = $relative
        SizeBytes = $File.Length
        FileVersion = $version
        LastWriteUtc = $File.LastWriteTimeUtc.ToString('o')
    })
}

function Add-RelativeFile {
    param([string]$Relative)
    if ([IO.Path]::IsPathRooted($Relative) -or
        $Relative -match '(^|[/\\])\.\.([/\\]|$)') {
        throw "ExtraRelativeFiles must stay within GameRoot: $Relative"
    }
    $p = Join-Path $script:Root ($Relative.Replace('/', '\'))
    if (Test-Path -LiteralPath $p -PathType Leaf) {
        Add-DebugFile (Get-Item -LiteralPath $p)
    } else {
        Write-Warning "Not found (optional): $Relative"
    }
}

New-Item -ItemType Directory -Path $staging -Force | Out-Null
try {
    # Original game managed assemblies (including voice/animation call sites).
    $managedNames = @(
        'Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll',
        'UnityEngine.dll', 'UnityEngine.UI.dll', 'Sirenix.Serialization.dll',
        'TextMeshPro.dll', 'UniRx.dll', 'MessagePack.dll'
    )
    foreach ($dataDir in @('Koikatu_Data', 'Koikatsu Party_Data')) {
        foreach ($name in $managedNames) {
            $p = Join-Path $script:Root ($dataDir + '\Managed\' + $name)
            if (Test-Path -LiteralPath $p -PathType Leaf) {
                Add-DebugFile (Get-Item -LiteralPath $p)
            }
        }
    }

    # BepInEx / Harmony loader binaries.
    foreach ($name in @('BepInEx.dll', '0Harmony.dll', 'BepInEx.Harmony.dll')) {
        foreach ($folder in @('BepInEx\core', 'BepInEx')) {
            $p = Join-Path $script:Root ($folder + '\' + $name)
            if (Test-Path -LiteralPath $p -PathType Leaf) {
                Add-DebugFile (Get-Item -LiteralPath $p)
            }
        }
    }

    # Only relevant mod APIs / compatibility DLLs, not every installed mod.
    $pluginRoot = Join-Path $script:Root 'BepInEx\plugins'
    if (Test-Path -LiteralPath $pluginRoot -PathType Container) {
        $wanted = '^(KKAPI|ExtensibleSaveFormat|Sideloader|KK_UncensorSelector|KKABMX|KK_BetterSquirt|KK_BecomeTrap|KK_MultiTeamH|KK_FemboyMode|KK_ClothingUnlocker|KK_HCtrl|KK_HSceneOptions|KK_MaterialEditor|KKMaterialEditor|KK_AnimationController|BepInEx\.ConfigurationManager|MultiTeamH|UncensorSelector)$'
        Get-ChildItem -LiteralPath $pluginRoot -Filter '*.dll' -Recurse -File |
            Where-Object { $_.BaseName -match $wanted } |
            ForEach-Object { Add-DebugFile $_ }
    }

    # Optional Unity game executables; no game assets.
    if ($IncludeExecutables) {
        Get-ChildItem -LiteralPath $script:Root -Filter '*.exe' -File |
            Where-Object { $_.BaseName -match '(?i)^(Koikatu|Koikatsu|Koikatsu Party)(VR|_VR)?$' } |
            ForEach-Object { Add-DebugFile $_ }
    }

    # Only explicit individual files (for example a needed .unity3d list bundle).
    foreach ($relative in $ExtraRelativeFiles) {
        Add-RelativeFile $relative
    }

    # Optional filename-only index, useful for identifying precise H/voice
    # asset bundles before requesting any of the (possibly large) .unity3d files.
    # These binaries are NOT copied into the archive by this option.
    if ($IncludeAssetInventory) {
        $assetRoot = Join-Path $script:Root 'abdata'
        if (Test-Path -LiteralPath $assetRoot -PathType Container) {
            $prefix = $script:Root
            if (-not $prefix.EndsWith([IO.Path]::DirectorySeparatorChar)) {
                $prefix += [IO.Path]::DirectorySeparatorChar
            }
            $assets = New-Object System.Collections.Generic.List[object]
            foreach ($file in (Get-ChildItem -LiteralPath $assetRoot -Filter '*.unity3d' -Recurse -File)) {
                if ($file.FullName -notmatch '(?i)[\\/](h|sound|action|animation|anim)[\\/]') {
                    continue
                }
                if ($assets.Count -ge 12000) { break }
                $assets.Add([pscustomobject]@{
                    RelativePath = $file.FullName.Substring($prefix.Length)
                    SizeBytes = $file.Length
                })
            }
            $assets | Sort-Object RelativePath | Export-Csv -LiteralPath (Join-Path $staging '_asset_inventory.csv') -NoTypeInformation -Encoding UTF8
            Write-Host ("Indexed {0} candidate H/audio bundles (paths only)." -f $assets.Count)
        } else {
            Write-Warning 'No abdata directory found; asset inventory skipped.'
        }
    }

    if ($IncludeDiagnostics) {
        Add-RelativeFile 'BepInEx\config\MaxMixAlex.FemboyMode.cfg'
        $logPath = Join-Path $script:Root 'BepInEx\LogOutput.log'
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $logOut = Join-Path $staging 'BepInEx\LogOutput.last2000.txt'
            $logDir = Split-Path -Parent $logOut
            if (-not (Test-Path -LiteralPath $logDir)) {
                New-Item -ItemType Directory -Path $logDir -Force | Out-Null
            }
            $lines = Get-Content -LiteralPath $logPath -Tail 2000
            $lines = $lines -replace '([A-Za-z]:\\Users\\)[^\\\r\n ]+', '$1<user>'
            $lines | Set-Content -LiteralPath $logOut -Encoding UTF8
        }
    }

    $script:Manifest | Sort-Object RelativePath |
        Export-Csv -LiteralPath (Join-Path $staging '_files.csv') -NoTypeInformation -Encoding UTF8
    @(
        'Koikatsu modding debug archive'
        'Created locally. This script never uploads files.'
        'No game assets, cards, saves, user configurations or logs are included by default.'
        'IncludeAssetInventory writes only asset paths and file sizes, never bundle bytes.'
        'Review the archive before sharing it privately.'
        ('Collected UTC: ' + [DateTime]::UtcNow.ToString('o'))
        ('Binary count: ' + $script:Manifest.Count)
    ) | Set-Content -LiteralPath (Join-Path $staging '_README.txt') -Encoding UTF8

    $zipDir = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $zipDir)) {
        New-Item -ItemType Directory -Path $zipDir -Force | Out-Null
    }
    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Force
    }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $destination -CompressionLevel Optimal -Force
    Write-Host "Created $destination" -ForegroundColor Green
    Write-Host ("Collected {0} binaries, {1:N1} MiB before compression." -f $script:Manifest.Count, ($script:TotalBytes / 1MB))
    Write-Host 'Review the zip contents before sharing it privately.'
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
) { $script:Root += '\' }
$destination = [IO.Path]::GetFullPath($OutputZip)
$staging = Join-Path ([IO.Path]::GetTempPath()) ('kk-inspect-' + [guid]::NewGuid().ToString('N'))
$script:Staging = $staging
$script:Seen = @{}
$script:Manifest = New-Object System.Collections.Generic.List[object]
$script:TotalBytes = [long]0
$maxSingleBytes = [long]$MaxSingleFileMB * 1MB
$maxTotalBytes = [long]$MaxTotalMB * 1MB

function Add-DebugFile {
    param([System.IO.FileInfo]$File)

    if ($null -eq $File -or -not $File.Exists) { return }
    $full = [IO.Path]::GetFullPath($File.FullName)
    if ($full.Length -le $script:Root.Length -or
        -not $full.StartsWith($script:Root + [IO.Path]::DirectorySeparatorChar,
                             [StringComparison]::OrdinalIgnoreCase)) {
        throw "File is outside GameRoot: $full"
    }
    $relative = $full.Substring($script:Root.Length).TrimStart([char]'\', [char]'/')
    if ($script:Seen.ContainsKey($relative.ToLowerInvariant())) { return }
    if ($File.Length -gt $maxSingleBytes -or
        $script:TotalBytes + $File.Length -gt $maxTotalBytes) {
        Write-Warning "Skipped due to size limit: $relative ($($File.Length) bytes)"
        return
    }

    $target = Join-Path $script:Staging $relative
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    Copy-Item -LiteralPath $full -Destination $target -Force
    $script:Seen[$relative.ToLowerInvariant()] = $true
    $script:TotalBytes += $File.Length

    $version = ''
    try { $version = $File.VersionInfo.FileVersion } catch {}
    $script:Manifest.Add([pscustomobject]@{
        RelativePath = $relative
        SizeBytes = $File.Length
        FileVersion = $version
        LastWriteUtc = $File.LastWriteTimeUtc.ToString('o')
    })
}

function Add-RelativeFile {
    param([string]$Relative)
    if ([IO.Path]::IsPathRooted($Relative) -or
        $Relative -match '(^|[/\\])\.\.([/\\]|$)') {
        throw "ExtraRelativeFiles must stay within GameRoot: $Relative"
    }
    $p = Join-Path $script:Root ($Relative.Replace('/', '\'))
    if (Test-Path -LiteralPath $p -PathType Leaf) {
        Add-DebugFile (Get-Item -LiteralPath $p)
    } else {
        Write-Warning "Not found (optional): $Relative"
    }
}

New-Item -ItemType Directory -Path $staging -Force | Out-Null
try {
    # Original game managed assemblies (including voice/animation call sites).
    $managedNames = @(
        'Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll',
        'UnityEngine.dll', 'UnityEngine.UI.dll', 'Sirenix.Serialization.dll',
        'TextMeshPro.dll', 'UniRx.dll', 'MessagePack.dll'
    )
    foreach ($dataDir in @('Koikatu_Data', 'Koikatsu Party_Data')) {
        foreach ($name in $managedNames) {
            $p = Join-Path $script:Root ($dataDir + '\Managed\' + $name)
            if (Test-Path -LiteralPath $p -PathType Leaf) {
                Add-DebugFile (Get-Item -LiteralPath $p)
            }
        }
    }

    # BepInEx / Harmony loader binaries.
    foreach ($name in @('BepInEx.dll', '0Harmony.dll', 'BepInEx.Harmony.dll')) {
        foreach ($folder in @('BepInEx\core', 'BepInEx')) {
            $p = Join-Path $script:Root ($folder + '\' + $name)
            if (Test-Path -LiteralPath $p -PathType Leaf) {
                Add-DebugFile (Get-Item -LiteralPath $p)
            }
        }
    }

    # Only relevant mod APIs / compatibility DLLs, not every installed mod.
    $pluginRoot = Join-Path $script:Root 'BepInEx\plugins'
    if (Test-Path -LiteralPath $pluginRoot -PathType Container) {
        $wanted = '^(KKAPI|ExtensibleSaveFormat|Sideloader|KK_UncensorSelector|KKABMX|KK_BetterSquirt|KK_BecomeTrap|KK_MultiTeamH|KK_FemboyMode|KK_ClothingUnlocker|KK_HCtrl|KK_HSceneOptions|KK_MaterialEditor|KKMaterialEditor|KK_AnimationController|BepInEx\.ConfigurationManager|MultiTeamH|UncensorSelector)$'
        Get-ChildItem -LiteralPath $pluginRoot -Filter '*.dll' -Recurse -File |
            Where-Object { $_.BaseName -match $wanted } |
            ForEach-Object { Add-DebugFile $_ }
    }

    # Optional Unity game executables; no game assets.
    if ($IncludeExecutables) {
        Get-ChildItem -LiteralPath $script:Root -Filter '*.exe' -File |
            Where-Object { $_.BaseName -match '(?i)^(Koikatu|Koikatsu|Koikatsu Party)(VR|_VR)?$' } |
            ForEach-Object { Add-DebugFile $_ }
    }

    # Only explicit individual files (for example a needed .unity3d list bundle).
    foreach ($relative in $ExtraRelativeFiles) {
        Add-RelativeFile $relative
    }

    if ($IncludeDiagnostics) {
        Add-RelativeFile 'BepInEx\config\MaxMixAlex.FemboyMode.cfg'
        $logPath = Join-Path $script:Root 'BepInEx\LogOutput.log'
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $logOut = Join-Path $staging 'BepInEx\LogOutput.last2000.txt'
            $logDir = Split-Path -Parent $logOut
            if (-not (Test-Path -LiteralPath $logDir)) {
                New-Item -ItemType Directory -Path $logDir -Force | Out-Null
            }
            $lines = Get-Content -LiteralPath $logPath -Tail 2000
            $lines = $lines -replace '([A-Za-z]:\\Users\\)[^\\\r\n ]+', '$1<user>'
            $lines | Set-Content -LiteralPath $logOut -Encoding UTF8
        }
    }

    $script:Manifest | Sort-Object RelativePath |
        Export-Csv -LiteralPath (Join-Path $staging '_files.csv') -NoTypeInformation -Encoding UTF8
    @(
        'Koikatsu modding debug archive'
        'Created locally. This script never uploads files.'
        'No game assets, cards, saves, user configurations or logs are included by default.'
        'Review the archive before sharing it privately.'
        ('Collected UTC: ' + [DateTime]::UtcNow.ToString('o'))
        ('Binary count: ' + $script:Manifest.Count)
    ) | Set-Content -LiteralPath (Join-Path $staging '_README.txt') -Encoding UTF8

    $zipDir = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $zipDir)) {
        New-Item -ItemType Directory -Path $zipDir -Force | Out-Null
    }
    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Force
    }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $destination -CompressionLevel Optimal -Force
    Write-Host "Created $destination" -ForegroundColor Green
    Write-Host ("Collected {0} binaries, {1:N1} MiB before compression." -f $script:Manifest.Count, ($script:TotalBytes / 1MB))
    Write-Host 'Review the zip contents before sharing it privately.'
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
