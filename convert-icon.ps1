Add-Type -AssemblyName System.Drawing

$img = [System.Drawing.Image]::FromFile("C:\Users\misae\.gemini\antigravity\brain\36ac84f6-fdd3-4dde-ba25-ecd312a06984\sidekick_icon_1776521039559.png")
$sizes = @(16, 32, 48, 256)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICO header
$bw.Write([int16]0)      # Reserved
$bw.Write([int16]1)      # Type: ICO
$bw.Write([int16]$sizes.Count)

# Render each size to PNG bytes
$imageDataList = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($img, $s, $s)
    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageDataList += , $pngMs.ToArray()
    $pngMs.Dispose()
    $bmp.Dispose()
}

# Calculate starting offset for image data
$dataOffset = 6 + ($sizes.Count * 16)

# Write directory entries
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $w = if ($s -eq 256) { 0 } else { $s }
    $bw.Write([byte]$w)                          # Width
    $bw.Write([byte]$w)                          # Height
    $bw.Write([byte]0)                           # Color palette
    $bw.Write([byte]0)                           # Reserved
    $bw.Write([int16]1)                          # Color planes
    $bw.Write([int16]32)                         # Bits per pixel
    $bw.Write([int32]$imageDataList[$i].Length)   # Size
    $bw.Write([int32]$dataOffset)                # Offset
    $dataOffset += $imageDataList[$i].Length
}

# Write image data
foreach ($d in $imageDataList) {
    $bw.Write($d)
}

[System.IO.File]::WriteAllBytes("f:\Projects\Ajudante\src\Ajudante.App\icon.ico", $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
$img.Dispose()

Write-Host "ICO created successfully with sizes: $($sizes -join ', ')"
