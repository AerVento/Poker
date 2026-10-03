# Export individual animation sprites without changing the generated alpha.
Add-Type -AssemblyName System.Drawing
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$output = Join-Path $repo 'Assets/Art/Gifts/Cigar'
[IO.Directory]::CreateDirectory($output) | Out-Null
$atlas = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'cigar-atlas.png'))
$parts = @(
    @('lighter', 28, 525, 500, 427),
    @('flame', 759, 525, 165, 420)
)
foreach ($part in $parts) {
    $rectangle = [Drawing.Rectangle]::new($part[1], $part[2], $part[3], $part[4])
    $sprite = $atlas.Clone($rectangle, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sprite.Save((Join-Path $output ($part[0] + '.png')), [Drawing.Imaging.ImageFormat]::Png)
    $sprite.Dispose()
}
$atlas.Dispose()

# The uncut cigar has two closed rounded ends. All three states share one painting.
$rounded = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'cigar-rounded.png'))
foreach ($part in @(@('cigar', 8, 93, 2154, 532), @('cut-cigar', 8, 93, 1926, 532), @('cap', 1934, 93, 228, 532))) {
    $sprite = $rounded.Clone([Drawing.Rectangle]::new($part[1], $part[2], $part[3], $part[4]), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sprite.Save((Join-Path $output ($part[0] + '.png')), [Drawing.Imaging.ImageFormat]::Png)
    $sprite.Dispose()
}
$rounded.Dispose()

# The cutter and blade share a side-on camera angle; preserve their painted depth.
$perspective = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'cutter-perspective.png'))
foreach ($part in @(@('cutter', 238, 33, 258, 1105), @('blade', 841, 92, 178, 1048))) {
    $sprite = $perspective.Clone([Drawing.Rectangle]::new($part[1], $part[2], $part[3], $part[4]), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sprite.Save((Join-Path $output ($part[0] + '.png')), [Drawing.Imaging.ImageFormat]::Png)
    $sprite.Dispose()
}
$perspective.Dispose()
