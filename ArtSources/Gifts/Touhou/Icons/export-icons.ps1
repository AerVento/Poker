Add-Type -AssemblyName System.Drawing
$projectRoot = (Resolve-Path "$PSScriptRoot/../../../..").Path
$manifest = Get-Content "$PSScriptRoot/generation.json" -Raw | ConvertFrom-Json
foreach ($asset in $manifest.assets) {
    $source = [Drawing.Bitmap]::FromFile((Join-Path $projectRoot $asset.source))
    $output = [Drawing.Bitmap]::new(1024,1024,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($output)
    $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $attributes = [Drawing.Imaging.ImageAttributes]::new()
    $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    try {
        $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,1024,1024),0,0,$source.Width,$source.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
        $output.Save((Join-Path $projectRoot $asset.output),[Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $attributes.Dispose()
        $graphics.Dispose()
        $output.Dispose()
        $source.Dispose()
    }
}
