$ErrorActionPreference = 'Stop'

function Get-EntriesFromXmlFile {
    param([string]$Path)

    $lines = [System.IO.File]::ReadAllLines($Path)
    $entries = [ordered]@{}

    foreach ($line in $lines) {
        if ($line -match '^\s*<(CS_[A-Za-z0-9_]+)>(.*)</\1>\s*$') {
            $entries[$matches[1]] = $matches[2]
        }
    }

    return $entries
}

function Get-FileOrder {
    return @(
        'CS_Keys_AbilityEditor.xml',
        'CS_Keys_AbilityEditor_Supplement.xml',
        'CS_Keys_Attributes.xml',
        'CS_Keys_Common.xml',
        'CS_Keys_Equipment.xml',
        'CS_Keys_ExportImport.xml',
        'CS_Keys_Face.xml',
        'CS_Keys_LLM.xml',
        'CS_Keys_SkinEditor.xml'
    )
}

function Get-CurrentFileMap {
    param([string]$Dir)

    $map = @{}
    $fileOrder = Get-FileOrder
    $filePriority = @{}

    for ($i = 0; $i -lt $fileOrder.Count; $i++) {
        $filePriority[$fileOrder[$i]] = $i
    }

    $files = Get-ChildItem -Path $Dir -Filter '*.xml' | Sort-Object `
        @{ Expression = { if ($filePriority.ContainsKey($_.Name)) { $filePriority[$_.Name] } else { [int]::MaxValue } } }, `
        Name

    foreach ($file in $files) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        foreach ($line in $lines) {
            if ($line -match '^\s*<(CS_[A-Za-z0-9_]+)>(.*)</\1>\s*$') {
                if (-not $map.ContainsKey($matches[1])) {
                    $map[$matches[1]] = $file.Name
                }
            }
        }
    }

    return $map
}

function Classify-MissingKey {
    param([string]$Key)

    if (
        $Key -like 'CS_Ability_*' -or
        $Key -like 'CS_Studio_Ability_*' -or
        $Key -like 'CS_Studio_Effect_*' -or
        $Key -like 'CS_Studio_Runtime_*' -or
        $Key -like 'CS_Studio_VFX_*'
    ) {
        return 'CS_Keys_AbilityEditor.xml'
    }

    if (
        $Key -like 'CS_Studio_Export_*' -or
        $Key -like 'CS_Studio_File_*' -or
        $Key -like 'CS_Studio_Import_*' -or
        $Key -like 'CS_Studio_Browser_*'
    ) {
        return 'CS_Keys_ExportImport.xml'
    }

    return 'CS_Keys_Common.xml'
}

function Escape-XmlText {
    param([string]$Text)

    $escaped = $Text.Replace('&', '&')
    $escaped = $escaped.Replace('<', '<')
    $escaped = $escaped.Replace('>', '>')
    return $escaped
}

function Write-SplitFiles {
    param(
        [string]$LanguageRoot,
        [hashtable]$BaseEntries,
        [hashtable]$CurrentEntries,
        [hashtable]$CurrentFileMap
    )

    $fileOrder = Get-FileOrder

    $merged = [ordered]@{}

    foreach ($key in $BaseEntries.Keys) {
        $merged[$key] = $BaseEntries[$key]
    }

    foreach ($key in $CurrentEntries.Keys) {
        if (-not $merged.Contains($key)) {
            $merged[$key] = $CurrentEntries[$key]
        }
    }

    $bucket = @{}
    foreach ($name in $fileOrder) {
        $bucket[$name] = New-Object System.Collections.Generic.List[object]
    }

    foreach ($key in ($merged.Keys | Sort-Object)) {
        $targetFile = $null
        if ($CurrentFileMap.ContainsKey($key)) {
            $targetFile = $CurrentFileMap[$key]
        } else {
            $targetFile = Classify-MissingKey -Key $key
        }

        if (-not $bucket.ContainsKey($targetFile)) {
            $targetFile = 'CS_Keys_Common.xml'
        }

        $bucket[$targetFile].Add([PSCustomObject]@{
            Key = $key
            Value = $merged[$key]
        })
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    foreach ($name in $fileOrder) {
        $path = Join-Path $LanguageRoot $name
        $lines = New-Object System.Collections.Generic.List[string]
        $lines.Add('<?xml version="1.0" encoding="utf-8"?>')
        $lines.Add('<LanguageData>')

        foreach ($entry in $bucket[$name]) {
            $escapedValue = Escape-XmlText -Text ([string]$entry.Value)
            $lines.Add("    <$($entry.Key)>$escapedValue</$($entry.Key)>")
        }

        $lines.Add('</LanguageData>')
        [System.IO.File]::WriteAllLines($path, $lines, $utf8NoBom)
    }
}

$root = Resolve-Path '.'
$zhOldPath = Join-Path $root '.roo\tmp\CS_Keys_zh_raw_test.xml'
$enOldPath = Join-Path $root '.roo\tmp\CS_Keys_en_15f4e52.xml'
$zhDir = Join-Path $root 'Languages\ChineseSimplified\Keyed'
$enDir = Join-Path $root 'Languages\English\Keyed'

$zhOldEntries = Get-EntriesFromXmlFile -Path $zhOldPath
$enOldEntries = Get-EntriesFromXmlFile -Path $enOldPath
$zhCurrentMap = Get-CurrentFileMap -Dir $zhDir
$enCurrentMap = Get-CurrentFileMap -Dir $enDir

$zhCurrentEntries = @{}
foreach ($file in Get-ChildItem -Path $zhDir -Filter '*.xml') {
    foreach ($pair in (Get-EntriesFromXmlFile -Path $file.FullName).GetEnumerator()) {
        $zhCurrentEntries[$pair.Key] = $pair.Value
    }
}

$enCurrentEntries = @{}
foreach ($file in Get-ChildItem -Path $enDir -Filter '*.xml') {
    foreach ($pair in (Get-EntriesFromXmlFile -Path $file.FullName).GetEnumerator()) {
        $enCurrentEntries[$pair.Key] = $pair.Value
    }
}

Write-SplitFiles -LanguageRoot $zhDir -BaseEntries $zhOldEntries -CurrentEntries $zhCurrentEntries -CurrentFileMap $zhCurrentMap
Write-SplitFiles -LanguageRoot $enDir -BaseEntries $enOldEntries -CurrentEntries $enCurrentEntries -CurrentFileMap $enCurrentMap

Write-Output 'Rebuilt split localization files from raw git snapshot plus split-only extras.'
Write-Output ('ZH_OLD=' + $zhOldEntries.Count)
Write-Output ('EN_OLD=' + $enOldEntries.Count)
Write-Output ('ZH_CUR=' + $zhCurrentEntries.Count)
Write-Output ('EN_CUR=' + $enCurrentEntries.Count)