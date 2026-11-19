$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$fonts=New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $project 'Assets/Project/Art/Environment/ServiceQuarter/CornerSample/Fonts/Orbitron.ttf'))
$out=Join-Path $project 'Assets/Project/Art/Props/PackingDesk/Textures'
New-Item -ItemType Directory -Path $out -Force | Out-Null
function Rect($hex,$x,$y,$w,$h) {$b=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex));$g.FillRectangle($b,$x,$y,$w,$h);$b.Dispose()}
function Text($s,$size,$hex,$x,$y) {$b=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex));$f=New-Object System.Drawing.Font $fonts.Families[0],$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel);$g.DrawString($s,$f,$b,$x,$y);$f.Dispose();$b.Dispose()}
foreach($kind in @('DispatchPrint','CupCoating')) {
    $bitmap=New-Object System.Drawing.Bitmap 1024,1024
    $g=[System.Drawing.Graphics]::FromImage($bitmap);$g.TextRenderingHint='AntiAliasGridFit';$g.SmoothingMode='AntiAlias'
    try {
        if($kind -eq 'DispatchPrint') {
            $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#c7c0ab'))
            Rect '#a56a47' 65 70 894 14
            Text 'ION / MARKET' 52 '#263739' 65 105
            Text 'DISPATCH / 24-071' 30 '#465353' 69 193
            Rect '#6e7971' 70 265 884 3
            Text 'FIELD RATION' 37 '#263739' 69 304
            Text '02 PACKS       800 g' 29 '#465353' 69 367
            Text 'CHECKED / 06:40' 28 '#465353' 69 435
            Rect '#6e7971' 70 511 884 3
            for($i=0;$i -lt 84;$i++) {if(($i%7) -in @(0,1,3,5)) {Rect '#263739' (74+$i*10) 604 (3+$i%3) 158}}
            Text 'MQ-07 / 24071082' 31 '#263739' 72 804
            Text 'KEEP WITH SHIPMENT' 24 '#59645d' 72 915
        } else {
            $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#46646b'))
            Rect '#b4af99' 0 365 1024 350
            Rect '#98623f' 0 365 1024 16
            foreach($x in @(105,617)) {Text 'ION' 85 '#293d40' $x 410;Text 'NIGHT SHIFT' 22 '#465956' $x 540;Text 'REUSE / 120' 20 '#465956' $x 594}
        }
        $bitmap.Save((Join-Path $out ($kind+'_BaseColor.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally {$g.Dispose();$bitmap.Dispose()}
}
$fonts.Dispose()
Write-Output 'PACKING_DESK_PRINT_READY'
