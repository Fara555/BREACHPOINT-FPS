$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$fonts=New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $project 'Assets/Project/Art/Environment/ServiceQuarter/CornerSample/Fonts/Orbitron.ttf'))
$bitmap=New-Object System.Drawing.Bitmap 1024,344
$g=[System.Drawing.Graphics]::FromImage($bitmap);$g.TextRenderingHint='AntiAliasGridFit'
function Text($value,$size,$x,$y) {$font=New-Object System.Drawing.Font $fonts.Families[0],$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel);$brush=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#bbb9a5'));$g.DrawString($value,$font,$brush,$x,$y);$brush.Dispose();$font.Dispose()}
try {
 $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#24363b'))
 Text 'STORAGE / 06' 73 35 25
 Text 'MAX LOAD / 180 kg' 39 40 136
 $brush=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#9eab9e'))
 for($i=0;$i -lt 91;$i++) {if(($i%7) -in @(0,1,3,5)) {$g.FillRectangle($brush,(42+$i*10),230,(3+$i%3),62)}}
 $brush.Dispose();$bitmap.Save((Join-Path $PSScriptRoot 'CabinetLabel.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally {$g.Dispose();$bitmap.Dispose();$fonts.Dispose()}
