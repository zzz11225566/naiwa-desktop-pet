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

$idle = '正面'
$side = '侧身站立'
$back = '背面'

Get-ChildItem -LiteralPath $Root -Filter *.png | ForEach-Object {
    $out = Join-Path $Transparent ($_.BaseName + '.png')
    & $bgExe $_.FullName $out 1024
}

$res = @()
$res += '/resource:' + (Join-Path $Transparent ($idle + '.png')) + ',NaiWa.front.png'
$res += '/resource:' + (Join-Path $Transparent ($side + '.png')) + ',NaiWa.side.png'
$res += '/resource:' + (Join-Path $Transparent ($back + '.png')) + ',NaiWa.back.png'

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
$cscArgs += Join-Path $PSScriptRoot 'MeshEngine.cs'
& $csc @cscArgs

Write-Output ('built: ' + $outExe)
