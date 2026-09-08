param([string]$PackageRoot = "art-production/test-room-v01")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Clamp-Byte([double]$value) { [byte][Math]::Round([Math]::Max(0, [Math]::Min(255, $value))) }
function Luma([System.Drawing.Color]$c) { 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B }

$jobs = @(
    @{ Id = "TR01-PIL-INTACT-A"; Name = "intact_a"; Source = "source/pillar/tr01_pillar_intact_a_albedo_source.png" },
    @{ Id = "TR01-PIL-BROKEN-A"; Name = "broken_a"; Source = "working/pillar/tr01_pillar_broken_a_background_extracted.png" }
)

foreach ($job in $jobs) {
    $sourcePath = Join-Path $PackageRoot $job.Source
    if (-not (Test-Path $sourcePath)) { throw "Pillar source not found: $sourcePath" }
    $src = [Drawing.Bitmap]::new((Resolve-Path $sourcePath).Path)
    $minX=$src.Width; $minY=$src.Height; $maxX=-1; $maxY=-1
    for ($y=0; $y -lt $src.Height; $y++) { for ($x=0; $x -lt $src.Width; $x++) {
        if ($src.GetPixel($x,$y).A -gt 16) { $minX=[Math]::Min($minX,$x); $maxX=[Math]::Max($maxX,$x); $minY=[Math]::Min($minY,$y); $maxY=[Math]::Max($maxY,$y) }
    }}
    if ($maxX -lt $minX) { throw "$($job.Id) has no visible alpha." }

    $canvasWidth=256; $canvasHeight=512; $bottomPadding=8
    $contentWidth=$maxX-$minX+1; $contentHeight=$maxY-$minY+1
    $scale=[Math]::Min(240.0/$contentWidth, 496.0/$contentHeight)
    $drawWidth=[Math]::Round($contentWidth*$scale); $drawHeight=[Math]::Round($contentHeight*$scale)
    $drawX=[Math]::Round(($canvasWidth-$drawWidth)/2.0); $drawY=$canvasHeight-$bottomPadding-$drawHeight
    $scaled=[Drawing.Bitmap]::new($canvasWidth,$canvasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($scaled); $g.Clear([Drawing.Color]::Transparent)
    $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src,[Drawing.Rectangle]::new($drawX,$drawY,$drawWidth,$drawHeight),[Drawing.Rectangle]::new($minX,$minY,$contentWidth,$contentHeight),[Drawing.GraphicsUnit]::Pixel)
    $g.Dispose(); $src.Dispose()

    $albedo=[Drawing.Bitmap]::new($canvasWidth,$canvasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $normal=[Drawing.Bitmap]::new($canvasWidth,$canvasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $emission=[Drawing.Bitmap]::new($canvasWidth,$canvasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mask=[Drawing.Bitmap]::new($canvasWidth,$canvasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $ao=[Drawing.Bitmap]::new($canvasWidth,$canvasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y=0; $y -lt $canvasHeight; $y++) { for ($x=0; $x -lt $canvasWidth; $x++) {
        $p=$scaled.GetPixel($x,$y); $a=if($p.A-le 4){0}elseif($p.A-ge 220){255}else{Clamp-Byte($p.A*255.0/220.0)}
        if($a-eq 0){ $albedo.SetPixel($x,$y,[Drawing.Color]::Transparent);$normal.SetPixel($x,$y,[Drawing.Color]::Transparent);$emission.SetPixel($x,$y,[Drawing.Color]::Transparent);$mask.SetPixel($x,$y,[Drawing.Color]::Transparent);$ao.SetPixel($x,$y,[Drawing.Color]::Transparent);continue }
        $albedo.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$p.R,$p.G,$p.B))
        $emission.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,0,0,0))
        $steel=($p.B-$p.R -gt 8) -and ($p.B-$p.G -lt 35)
        $metal=if($steel){185}else{35};$gloss=if($steel){58}else{22}
        $mask.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$metal,$gloss,0))
        $av=Clamp-Byte(190+[Math]::Min(65,(Luma $p)*0.45));$ao.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$av,$av,$av))
    }}
    for ($y=0; $y -lt $canvasHeight; $y++) { for ($x=0; $x -lt $canvasWidth; $x++) {
        $p=$albedo.GetPixel($x,$y); if($p.A-eq 0){continue};$xl=[Math]::Max(0,$x-1);$xr=[Math]::Min($canvasWidth-1,$x+1);$yu=[Math]::Max(0,$y-1);$yd=[Math]::Min($canvasHeight-1,$y+1)
        $gx=((Luma $albedo.GetPixel($xr,$y))-(Luma $albedo.GetPixel($xl,$y)))/255.0*2.6;$gy=((Luma $albedo.GetPixel($x,$yd))-(Luma $albedo.GetPixel($x,$yu)))/255.0*2.6
        $nx=-$gx;$ny=$gy;$length=[Math]::Sqrt($nx*$nx+$ny*$ny+1.0)
        $normal.SetPixel($x,$y,[Drawing.Color]::FromArgb($p.A,(Clamp-Byte(($nx/$length*.5+.5)*255)),(Clamp-Byte(($ny/$length*.5+.5)*255)),(Clamp-Byte((1.0/$length*.5+.5)*255))))
    }}
    $outputs=@{albedo="approved/albedo/pillar/tr01_pillar_$($job.Name)_albedo.png";normal="approved/normal/pillar/tr01_pillar_$($job.Name)_normal.png";emission="approved/emission/pillar/tr01_pillar_$($job.Name)_emission.png";mask="approved/mask/pillar/tr01_pillar_$($job.Name)_mask.png";ao="approved/ao/pillar/tr01_pillar_$($job.Name)_ao.png"}
    foreach($key in $outputs.Keys){$path=Join-Path $PackageRoot $outputs[$key];New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($path))|Out-Null;(Get-Variable $key -ValueOnly).Save($path,[Drawing.Imaging.ImageFormat]::Png)}
    $scaled.Dispose();$albedo.Dispose();$normal.Dispose();$emission.Dispose();$mask.Dispose();$ao.Dispose()
    [pscustomobject]@{Asset=$job.Id;Canvas="256x512";Content="$($drawWidth)x$($drawHeight)";PivotPixelsBottomOrigin="128,8";Result="PASS"}
}
