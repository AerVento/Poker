Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path "$PSScriptRoot/../../..").Path
$names = @{ Yuyuko='ResurrectionButterfly'; Reimu='FantasySeal'; Marisa='MasterSpark'; Sakuya='TheWorld'; Kaguya='HourOfDaybreak' }
$ids = @{ Yuyuko='resurrection-butterfly'; Reimu='fantasy-seal'; Marisa='master-spark'; Sakuya='the-world'; Kaguya='hour-of-daybreak' }
foreach ($actor in @('Yuyuko','Reimu','Marisa','Sakuya','Kaguya')) {
    $source = [Drawing.Bitmap]::FromFile("$PSScriptRoot/$actor/poses-original.png")
    $dir = "$PSScriptRoot/$actor"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $half = [int]($source.Width / 2)
    foreach ($pose in 0,1) {
        $rect = [Drawing.Rectangle]::new($pose*$half,0,$half,$source.Height)
        $crop = $source.Clone($rect,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $label = if ($pose -eq 0) {'arrive'} else {'cast'}
        $crop.Save("$dir/actor-$label.png",[Drawing.Imaging.ImageFormat]::Png)
        if ($pose -eq 0 -and !(Test-Path "$PSScriptRoot/Icons/$($ids[$actor]).png")) {
            $icon = [Drawing.Bitmap]::new(1024,1024,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [Drawing.Graphics]::FromImage($icon)
            $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $attr = [Drawing.Imaging.ImageAttributes]::new()
            $attr.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $height = 940
            $width = [int]($height*$crop.Width/$crop.Height)
            $g.DrawImage($crop,[Drawing.Rectangle]::new([int]((1024-$width)/2),42,$width,$height),0,0,$crop.Width,$crop.Height,[Drawing.GraphicsUnit]::Pixel,$attr)
            $icon.Save("$root/Assets/Sprites/Items/Gifts/$($ids[$actor]).png",[Drawing.Imaging.ImageFormat]::Png)
            $attr.Dispose(); $g.Dispose(); $icon.Dispose()
        }
        $crop.Dispose()
    }
    $source.Dispose()
}
