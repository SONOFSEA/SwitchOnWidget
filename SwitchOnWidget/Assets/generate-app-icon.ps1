param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'app.ico')
)

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SwitchOnIconNativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
'@

$size = 64
$bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(22, 28, 38))

$backgroundPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$radius = 12
$diameter = $radius * 2
$backgroundPath.AddArc(3, 3, $diameter, $diameter, 180, 90)
$backgroundPath.AddArc($size - 3 - $diameter, 3, $diameter, $diameter, 270, 90)
$backgroundPath.AddArc($size - 3 - $diameter, $size - 3 - $diameter, $diameter, $diameter, 0, 90)
$backgroundPath.AddArc(3, $size - 3 - $diameter, $diameter, $diameter, 90, 90)
$backgroundPath.CloseFigure()

$backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 22, 28, 38))
$graphics.FillPath($backgroundBrush, $backgroundPath)

$ringPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 53, 69, 83), 3)
$graphics.DrawPath($ringPen, $backgroundPath)

# 스위치가 켜지는 느낌의 민트색 전원 모티프
$ringPen.Color = [System.Drawing.Color]::FromArgb(255, 102, 205, 170)
$ringPen.Width = 7
$graphics.DrawArc($ringPen, 14, 16, 36, 36, -42, 264)
$graphics.FillRectangle([System.Drawing.Brushes]::MediumAquamarine, 28, 8, 8, 26)

$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$iconHandle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$fileStream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
$icon.Save($fileStream)
$fileStream.Dispose()
$icon.Dispose()
[SwitchOnIconNativeMethods]::DestroyIcon($iconHandle) | Out-Null
$ringPen.Dispose()
$backgroundBrush.Dispose()
$backgroundPath.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Output $OutputPath
