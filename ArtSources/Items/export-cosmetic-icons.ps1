Add-Type -AssemblyName System.Drawing
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$recordPath = Join-Path $PSScriptRoot 'cosmetic-icon-generation.json'
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
$template = [IO.File]::ReadAllText((Join-Path $root 'Assets/Sprites/Items/Contracts/devil-contract.png.meta'))
foreach ($asset in $record.assets)
{
    $target = Join-Path $root $asset.path
    $source = Join-Path $root $asset.sourceFile
    $directory = Split-Path $target
    if (!(Test-Path -LiteralPath $directory))
    {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    if (!(Test-Path -LiteralPath "$directory.meta"))
    {
        [IO.File]::WriteAllText("$directory.meta", "fileFormatVersion: 2`r`nguid: $([guid]::NewGuid().ToString('N'))`r`nfolderAsset: yes`r`nDefaultImporter:`r`n  externalObjects: {}`r`n  userData: `r`n  assetBundleName: `r`n  assetBundleVariant: `r`n")
    }
    $image = [Drawing.Image]::FromFile($source)
    $bitmap = New-Object Drawing.Bitmap 1024,1024,([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $attributes = New-Object Drawing.Imaging.ImageAttributes
    $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage($image, [Drawing.Rectangle]::new(0,0,1024,1024),0,0,$image.Width,$image.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
    $bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)
    $attributes.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $image.Dispose()
    if (!(Test-Path -LiteralPath "$target.meta"))
    {
        $meta = $template -replace '(?m)^guid: .+$', "guid: $([guid]::NewGuid().ToString('N'))"
        $meta = $meta -replace '(?m)^    spriteID: .+$', "    spriteID: $([guid]::NewGuid().ToString('N'))"
        [IO.File]::WriteAllText("$target.meta",$meta)
    }
}
