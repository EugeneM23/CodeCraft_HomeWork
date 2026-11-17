# Builds comparison sheets from two folders of render_shots.cs output.
#   compare_shots.ps1 -Before <dir> -After <dir> -Out <dir> [-Width 960]
# For every PNG present in both folders: <Out>/<name>.png with "before" on the left and "after"
# on the right, each scaled to -Width. With -After omitted: one contact sheet of -Before.
param(
    [Parameter(Mandatory = $true)][string]$Before,
    [string]$After,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Width = 960
)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null

function Draw-Label($g, [string]$text, [int]$x, [int]$y) {
    $font = New-Object System.Drawing.Font('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)
    $size = $g.MeasureString($text, $font)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 0, 0, 0))), $x, $y, $size.Width + 12, $size.Height + 6)
    $g.DrawString($text, $font, [System.Drawing.Brushes]::White, $x + 6, $y + 3)
    $font.Dispose()
}

$files = Get-ChildItem $Before -Filter *.png | Sort-Object Name
if ($After) {
    foreach ($f in $files) {
        $other = Join-Path $After $f.Name
        if (-not (Test-Path $other)) { continue }
        $a = [System.Drawing.Image]::FromFile($f.FullName)
        $b = [System.Drawing.Image]::FromFile($other)
        $h = [int]($a.Height * $Width / $a.Width)
        $sheet = New-Object System.Drawing.Bitmap ($Width * 2 + 8), $h
        $g = [System.Drawing.Graphics]::FromImage($sheet)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.Clear([System.Drawing.Color]::Black)
        $g.DrawImage($a, 0, 0, $Width, $h)
        $g.DrawImage($b, $Width + 8, 0, $Width, $h)
        Draw-Label $g ("BEFORE  " + $f.BaseName) 8 8
        Draw-Label $g ("AFTER  " + $f.BaseName) ($Width + 16) 8
        $sheet.Save((Join-Path $Out $f.Name), [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $sheet.Dispose(); $a.Dispose(); $b.Dispose()
    }
} else {
    $cols = 2
    $first = [System.Drawing.Image]::FromFile($files[0].FullName)
    $h = [int]($first.Height * $Width / $first.Width); $first.Dispose()
    $rows = [Math]::Ceiling($files.Count / $cols)
    $sheet = New-Object System.Drawing.Bitmap ($Width * $cols + 8), ([int]($h * $rows + 8 * ($rows - 1)))
    $g = [System.Drawing.Graphics]::FromImage($sheet)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Black)
    for ($i = 0; $i -lt $files.Count; $i++) {
        $img = [System.Drawing.Image]::FromFile($files[$i].FullName)
        $x = ($i % $cols) * ($Width + 8); $y = [Math]::Floor($i / $cols) * ($h + 8)
        $g.DrawImage($img, $x, $y, $Width, $h)
        Draw-Label $g $files[$i].BaseName ($x + 8) ($y + 8)
        $img.Dispose()
    }
    $sheet.Save((Join-Path $Out 'contact.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $sheet.Dispose()
}
