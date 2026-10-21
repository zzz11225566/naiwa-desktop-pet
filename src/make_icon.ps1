param(
    [string]$InputImage = 'C:\Users\86139\Desktop\奶蛙\311f175adb77e9631f22563fd6836574.png',
    [string]$PngOut = 'C:\Users\86139\Desktop\奶蛙\build\cover.png',
    [string]$IcoOut = 'C:\Users\86139\Desktop\奶蛙\build\app.ico'
)

Add-Type -AssemblyName System.Drawing

$src = New-Object System.Drawing.Bitmap($InputImage)
$minX = $src.Width; $minY = $src.Height; $maxX = -1; $maxY = -1
for ($y = 0; $y -lt $src.Height; $y += 4) {
    for ($x = 0; $x -lt $src.Width; $x += 4) {
        $c = $src.GetPixel($x, $y)
        if (($c.R -ge 240) -and ($c.G -ge 240) -and ($c.B -ge 240)) { continue }
        if ($x -lt $minX) { $minX = $x }
        if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
    }
}

$padX = [int](($maxX - $minX) * 0.03)
$cropX = [Math]::Max(0, $minX - $padX)
$cropW = [Math]::Min($src.Width - $cropX, ($maxX - $minX) + 2 * $padX)
$cropY = [Math]::Max(0, $minY - [int](($maxY - $minY) * 0.02))
$cropH = [Math]::Min($src.Height - $cropY, [int](($maxY - $minY) * 0.62))

$bitmap = New-Object System.Drawing.Bitmap(256, 256)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::FromArgb(255, 250, 242, 222))
$srcRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)
$dstRect = New-Object System.Drawing.Rectangle(0, 0, 256, 256)
$g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()

$bitmap.Save($PngOut, [System.Drawing.Imaging.ImageFormat]::Png)

$hIcon = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = New-Object System.IO.FileStream($IcoOut, [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()

$src.Dispose()
$bitmap.Dispose()
Write-Output "icon=$IcoOut png=$PngOut"
