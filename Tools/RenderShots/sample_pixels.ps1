# Average colour (0-255) of small boxes at the same points in two shots.
#   sample_pixels.ps1 -Before <png> -After <png> -Points "x,y;x,y" [-R 4]
param([string]$Before, [string]$After, [string]$Points, [int]$R = 4)
Add-Type -AssemblyName System.Drawing
$a = New-Object System.Drawing.Bitmap $Before; $b = New-Object System.Drawing.Bitmap $After
function Avg($bmp, $x, $y) { $s = @(0,0,0); $n = 0
  for ($i = -$R; $i -le $R; $i++) { for ($j = -$R; $j -le $R; $j++) { $c = $bmp.GetPixel($x + $i, $y + $j); $s[0] += $c.R; $s[1] += $c.G; $s[2] += $c.B; $n++ } }
  return "{0,3} {1,3} {2,3}" -f [int]($s[0]/$n), [int]($s[1]/$n), [int]($s[2]/$n) }
foreach ($p in $Points.Split(';')) { $xy = $p.Split(','); $x = [int]$xy[0]; $y = [int]$xy[1]
  "{0,-10} before {1}   after {2}" -f $p, (Avg $a $x $y), (Avg $b $x $y) }
$a.Dispose(); $b.Dispose()
