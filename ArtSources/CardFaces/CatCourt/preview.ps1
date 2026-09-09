$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$textures = Join-Path $repoRoot 'Assets/Sprites/CardFaces/CatCourt'
$ranks = @('A','2','3','4','5','6','7','8','9','10','J','Q','K')
$suits = @('Heart','Diamond','Club','Spade')
$sheet = [Drawing.Bitmap]::new(1454, 712)
$g = [Drawing.Graphics]::FromImage($sheet)
$font = [Drawing.Font]::new('Arial', 10)
try {
    $g.Clear([Drawing.Color]::FromArgb(25,48,43))
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawString('CAT COURT - 52 faces / 102 x 144 readability preview', $font, [Drawing.Brushes]::White, 12, 10)
    for ($s=0; $s -lt 4; $s++) {
        $g.DrawString($suits[$s], $font, [Drawing.Brushes]::White, 12, (36+$s*168))
        for ($r=0; $r -lt 13; $r++) {
            $im = [Drawing.Image]::FromFile((Join-Path $textures "$($suits[$s])_$($ranks[$r]).png"))
            try { $g.DrawImage($im, [Drawing.Rectangle]::new((12+$r*110),(54+$s*168),102,144)) }
            finally { $im.Dispose() }
        }
    }
    $sheet.Save((Join-Path $PSScriptRoot 'contact-sheet.png'), [Drawing.Imaging.ImageFormat]::Png)
}
finally { $g.Dispose(); $font.Dispose(); $sheet.Dispose() }
Write-Output 'Saved contact-sheet.png'
