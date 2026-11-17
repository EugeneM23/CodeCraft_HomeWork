# Crops the same region from a before and an after shot and puts them side by side, unscaled.
#   crop_pair.ps1 -Before <png> -After <png> -Out <png> -X 0 -Y 0 -W 640 -H 540
param([string]$Before, [string]$After, [string]$Out, [int]$X, [int]$Y, [int]$W = 640, [int]$H = 540)
Add-Type -AssemblyName System.Drawing
$a = [System.Drawing.Image]::FromFile($Before); $b = [System.Drawing.Image]::FromFile($After)
$sheet = New-Object System.Drawing.Bitmap ($W * 2 + 6), $H
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::Black)
$src = New-Object System.Drawing.Rectangle $X, $Y, $W, $H
$g.DrawImage($a, (New-Object System.Drawing.Rectangle 0, 0, $W, $H), $src, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($b, (New-Object System.Drawing.Rectangle ($W + 6), 0, $W, $H), $src, [System.Drawing.GraphicsUnit]::Pixel)
$sheet.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $sheet.Dispose(); $a.Dispose(); $b.Dispose()
