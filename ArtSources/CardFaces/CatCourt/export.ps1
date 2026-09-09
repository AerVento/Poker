param([switch]$AllowPartial)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceDir = $PSScriptRoot
$repoRoot = [IO.Path]::GetFullPath((Join-Path $sourceDir '../../..'))
$outputDir = Join-Path $repoRoot 'Assets/Sprites/CardFaces/CatCourt'
$manifest = Get-Content -Raw (Join-Path $sourceDir 'generation.json') | ConvertFrom-Json
if (-not $AllowPartial -and $manifest.cards.Count -ne 53) { throw 'Expected 52 faces and one back.' }
New-Item -ItemType Directory -Force $outputDir, (Join-Path $sourceDir 'Originals') | Out-Null

# Technical export only: resample full generated artwork and standardize the
# transparent corner mask. No illustration, rank, suit, or border is redrawn.
foreach ($card in $manifest.cards) {
    $name = if ($card.rank -eq 'Back') { 'Back' } else { "$($card.suit)_$($card.rank)" }
    $archived = Join-Path $sourceDir "Originals/$name.png"
    if (-not (Test-Path -LiteralPath $archived)) {
        Copy-Item -LiteralPath $card.path -Destination $archived
    }
    $inputImage = [Drawing.Image]::FromFile($archived)
    $bitmap = [Drawing.Bitmap]::new(816, 1152, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(255,252,245))
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($inputImage, [Drawing.Rectangle]::new(0,0,816,1152))
        $graphics.Dispose()
        $graphics = $null
        # Identical 40 px quarter circles on every face and on the back.
        for ($y=0; $y -lt 40; $y++) {
            for ($x=0; $x -lt 40; $x++) {
                $coverage = 0
                foreach ($sy in @(0.125,0.375,0.625,0.875)) {
                    foreach ($sx in @(0.125,0.375,0.625,0.875)) {
                        if ([Math]::Pow(40-$x-$sx,2)+[Math]::Pow(40-$y-$sy,2) -le 1600) { $coverage++ }
                    }
                }
                $alpha = [int][Math]::Round(255*$coverage/16)
                foreach ($px in @($x,(815-$x))) {
                    foreach ($py in @($y,(1151-$y))) {
                        $c = $bitmap.GetPixel($px,$py)
                        $bitmap.SetPixel($px,$py,[Drawing.Color]::FromArgb($alpha,$c.R,$c.G,$c.B))
                    }
                }
            }
        }
        $bitmap.Save((Join-Path $outputDir "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($graphics) { $graphics.Dispose() }
        $bitmap.Dispose()
        $inputImage.Dispose()
    }
}
Write-Output "Exported $($manifest.cards.Count) generated textures to $outputDir (816x1152 RGBA)."
