param([Parameter(Mandatory=$true)][string]$ImagePath, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceImage = [Drawing.Image]::FromFile($ImagePath)
try {
    $sizes = @(16,24,32,48,64,128,256)
    $frames = @()
    foreach ($size in $sizes) {
        $bitmap = New-Object Drawing.Bitmap($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = New-Object IO.MemoryStream
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $ratio = [Math]::Min($size / [double]$sourceImage.Width, $size / [double]$sourceImage.Height)
            $width = [int][Math]::Round($sourceImage.Width * $ratio)
            $height = [int][Math]::Round($sourceImage.Height * $ratio)
            $graphics.DrawImage($sourceImage, [int](($size-$width)/2), [int](($size-$height)/2), $width, $height)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $file = [IO.File]::Create($Destination)
    $writer = New-Object IO.BinaryWriter($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i=0; $i -lt $sizes.Count; $i++) {
            $dimension = [byte]$(if ($sizes[$i] -eq 256) {0} else {$sizes[$i]})
            $writer.Write($dimension); $writer.Write($dimension); $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $file.Dispose() }
} finally { $sourceImage.Dispose() }
