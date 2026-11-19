$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$fonts=New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $root 'Assets/Project/Art/Environment/ServiceQuarter/CornerSample/Fonts/Orbitron.ttf'))
$bitmap=New-Object System.Drawing.Bitmap 1024,2048
$mask=New-Object System.Drawing.Bitmap 1024,2048
$g=[System.Drawing.Graphics]::FromImage($bitmap);$m=[System.Drawing.Graphics]::FromImage($mask)
$g.SmoothingMode='AntiAlias';$m.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit'
function Rect($hex,$x,$y,$w,$h) {$b=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex));$g.FillRectangle($b,$x,$y,$w,$h);$b.Dispose()}
function Text($s,$size,$hex,$x,$y) {$b=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex));$f=New-Object System.Drawing.Font $fonts.Families[0],$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel);$g.DrawString($s,$f,$b,$x,$y);$f.Dispose();$b.Dispose()}
try {
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#465158'));$m.Clear([System.Drawing.Color]::Black)
    foreach($y in @(150,1190)) {Rect '#c6bba0' 175 $y 674 710;$m.FillRectangle([System.Drawing.Brushes]::White,175,$y,674,710)}
    Rect '#aa6035' 175 150 674 94
    Text 'ION / PROVISIONS' 31 '#17262a' 215 178
    Text 'FIELD' 68 '#1a2a2d' 211 290
    Text 'RATION' 68 '#1a2a2d' 211 372
    Rect '#798178' 215 486 594 3
    Text 'PACK / 01' 37 '#283a3b' 214 526
    Text 'SEALED' 28 '#485858' 215 604
    Text '400 g' 29 '#283a3b' 673 604
    for($i=0;$i -lt 62;$i++) {if(($i%7) -in @(0,1,3,5)) {Rect '#28383a' (216+$i*9) 710 (3+($i%3)) 62}}
    Text 'LOT  24 - 071' 21 '#485858' 216 802
    Text 'FIELD RATION / 01' 36 '#203237' 212 1238
    Text 'FACTORY SEALED' 26 '#425253' 214 1332
    foreach($y in @(1420,1470,1520)) {Rect '#728076' 216 $y 584 5}
    Text '400 g / 01 PACK' 29 '#203237' 214 1604
    Text 'OPEN AT NOTCH' 23 '#425253' 214 1718
    for($i=0;$i -lt 62;$i++) {if(($i%5) -in @(0,2,3)) {Rect '#28383a' (216+$i*9) 1790 4 64}}
    $bitmap.Save((Join-Path $PSScriptRoot 'SupplyRation_Print.png'),[System.Drawing.Imaging.ImageFormat]::Png)
    $mask.Save((Join-Path $PSScriptRoot 'SupplyRation_LabelMask.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally {$g.Dispose();$m.Dispose();$bitmap.Dispose();$mask.Dispose();$fonts.Dispose()}
Write-Output 'SUPPLY_PRINT_READY'
