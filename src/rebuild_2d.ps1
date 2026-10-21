$ErrorActionPreference = 'Stop'

$Root = Split-Path $PSScriptRoot -Parent
$Build = Join-Path $Root 'build'
$Transparent = Join-Path $Build 'transparent2d'
$Release = Join-Path $Root 'release'
New-Item -ItemType Directory -Path $Transparent -Force | Out-Null
New-Item -ItemType Directory -Path $Release -Force | Out-Null

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'
$icon = Join-Path $Release ([string][char]0x5976 + [char]0x86d9 + '.ico')
if (-not (Test-Path $icon)) { $icon = Join-Path $Build 'app.ico' }

$bgExe = Join-Path $Build 'BgRemover.exe'
& $csc /nologo /target:exe /platform:anycpu /out:$bgExe /r:System.Drawing.dll (Join-Path $PSScriptRoot 'BgRemover.cs')

$idle = '1cf29af9d41f68214ee0685836edeb15'
$actions = @(
    '311f175adb77e9631f22563fd6836574',
    '57dfd05b4c222f7791a8862511cc49c5',
    '7de971eab803c29bdf9d5aa02d534753',
    'b28905a25359fa53a2398d84b044fbaf',
    'c983d67208f32f6ee421396ab909b1e7',
    'cbcb30d396e340b995aa7eaeaea4d7e0',
    'd0983a8acc755553b76f38f781ebb7f1',
    'eb8d6007a40f631747204487204dfba7'
)

Get-ChildItem -LiteralPath $Root -Filter *.png | ForEach-Object {
    $out = Join-Path $Transparent ($_.BaseName + '.png')
    & $bgExe $_.FullName $out 1024
}

$res = @()
$res += '/resource:' + (Join-Path $Transparent ($idle + '.png')) + ',NaiWa.idle.png'
for ($i = 0; $i -lt $actions.Count; $i++) {
    $res += '/resource:' + (Join-Path $Transparent ($actions[$i] + '.png')) + ',NaiWa.action.' + $i + '.png'
}

$outExe = Join-Path $Release ([string][char]0x5976 + [char]0x86d9 + '.exe')
$cscArgs = @(
    '/nologo','/target:winexe','/platform:anycpu','/codepage:65001',
    '/win32icon:' + $icon,
    '/out:' + $outExe,
    '/r:' + (Join-Path $wpf 'PresentationFramework.dll'),
    '/r:' + (Join-Path $wpf 'PresentationCore.dll'),
    '/r:' + (Join-Path $wpf 'WindowsBase.dll'),
    '/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll'
)
$cscArgs += $res
$cscArgs += Join-Path $PSScriptRoot 'NaiWa.cs'
& $csc @cscArgs

Write-Output ('built: ' + $outExe)
