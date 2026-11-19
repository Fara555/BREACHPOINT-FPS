$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$artRoot = $PSScriptRoot
$projectRoot = Split-Path (Split-Path (Split-Path $artRoot -Parent) -Parent) -Parent
$output = Join-Path $projectRoot 'Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Textures/MarketDeviceDisplay.png'
$fontFile = Join-Path $projectRoot 'Assets/Project/Art/Environment/ServiceQuarter/CornerSample/Fonts/Orbitron.ttf'
$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile($fontFile)
$bitmap = New-Object System.Drawing.Bitmap 1024,1024
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
function Brush($hex) { New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex)) }
function Rect($hex,$x,$y,$w,$h) { $b=Brush $hex; $g.FillRectangle($b,$x,$y,$w,$h); $b.Dispose() }
function Label($value,$size,$hex,$x,$y) {
    $font = New-Object System.Drawing.Font $fonts.Families[0],$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel)
    $b=Brush $hex; $g.DrawString($value,$font,$b,$x,$y); $b.Dispose();$font.Dispose()
}
function Line($hex,$width,$x1,$y1,$x2,$y2) { $pen=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml($hex)),$width; $g.DrawLine($pen,$x1,$y1,$x2,$y2);$pen.Dispose() }
try {
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#08151a'))
    Rect '#10232a' 0 0 1024 116
    Label 'MARKET / 04' 24 '#71989d' 48 26
    Label 'CHECKOUT' 36 '#c2d3d2' 44 62
    Rect '#396267' 860 40 108 5
    Label 'LOCAL' 19 '#71989d' 862 62
    Rect '#10252b' 48 158 928 212
    Label 'ORDER 0007' 22 '#6c9298' 76 181
    Rect '#27434a' 80 244 72 72
    Rect '#6d9699' 98 262 36 36
    Label 'SUPPLY PACK' 28 '#b6cacb' 186 250
    Label '01 ITEM' 20 '#63858a' 188 296
    Label '48.00' 31 '#c5d3ce' 774 264
    Line '#2a454a' 2 48 410 976 410
    Label 'TOTAL' 23 '#73979b' 50 443
    Label '48.00' 64 '#cfdbd6' 48 480
    Label 'CR' 23 '#839d9f' 298 517
    Rect '#1e444a' 716 464 258 92
    Label 'READY' 30 '#a3cbca' 766 491
    Label 'SECURE CONNECTION' 17 '#46666b' 49 598
    # Reader area occupies x384..640 and y672..1024 in the same atlas.
    Rect '#0a1c23' 384 672 256 352
    $pen=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#789fa3')),5
    foreach($radius in @(28,47,66)) { $g.DrawArc($pen,(512-$radius),(820-$radius),($radius*2),($radius*2),-62,124) }
    $pen.Dispose()
    Rect '#7b9c9e' 461 795 9 50
    Rect '#315158' 416 930 192 3
    Label 'TAP' 22 '#819da0' 484 956
    $bitmap.Save($output,[System.Drawing.Imaging.ImageFormat]::Png)
} finally { $g.Dispose();$bitmap.Dispose();$fonts.Dispose() }
Write-Output "DEVICE_DISPLAY_WRITTEN $output"
