param([string]$PackageRoot = "art-production/test-room-v01")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
function Clamp-Byte([double]$v){[byte][Math]::Round([Math]::Max(0,[Math]::Min(255,$v)))}
function Luma([Drawing.Color]$c){0.2126*$c.R+0.7152*$c.G+0.0722*$c.B}

$jobs=@(
 @{Id="TR01-LGT-WORKLAMP-A";Name="worklamp_a";Source="source/light/tr01_light_worklamp_a_albedo_source.png";Mode="amber";Anchor="ground"},
 @{Id="TR01-LGT-WARNING-A";Name="warning_a";Source="source/light/tr01_light_warning_a_albedo_source.png";Mode="magenta";Anchor="center"},
 @{Id="TR01-LGT-CRYSTAL-A";Name="crystal_a";Source="source/light/tr01_light_crystal_a_albedo_source.png";Mode="cyan";Anchor="ground"}
)

foreach($job in $jobs){
 $src=[Drawing.Bitmap]::new((Resolve-Path (Join-Path $PackageRoot $job.Source)).Path)
 $minX=$src.Width;$minY=$src.Height;$maxX=-1;$maxY=-1
 for($y=0;$y-lt$src.Height;$y++){for($x=0;$x-lt$src.Width;$x++){if($src.GetPixel($x,$y).A-gt16){$minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x);$minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)}}}
 if($maxX-lt$minX){throw "$($job.Id) has no visible alpha"}
 $cw=256;$ch=256;$sw=$maxX-$minX+1;$sh=$maxY-$minY+1;$scale=[Math]::Min(240.0/$sw,240.0/$sh);$dw=[Math]::Round($sw*$scale);$dh=[Math]::Round($sh*$scale);$dx=[Math]::Round(($cw-$dw)/2.0);$dy=if($job.Anchor-eq"ground"){$ch-8-$dh}else{[Math]::Round(($ch-$dh)/2.0)}
 $scaled=[Drawing.Bitmap]::new($cw,$ch,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$g=[Drawing.Graphics]::FromImage($scaled);$g.Clear([Drawing.Color]::Transparent);$g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy;$g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality;$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$g.DrawImage($src,[Drawing.Rectangle]::new($dx,$dy,$dw,$dh),[Drawing.Rectangle]::new($minX,$minY,$sw,$sh),[Drawing.GraphicsUnit]::Pixel);$g.Dispose();$src.Dispose()
 $albedo=[Drawing.Bitmap]::new($cw,$ch,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$normal=[Drawing.Bitmap]::new($cw,$ch,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$emission=[Drawing.Bitmap]::new($cw,$ch,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$mask=[Drawing.Bitmap]::new($cw,$ch,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$ao=[Drawing.Bitmap]::new($cw,$ch,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 for($y=0;$y-lt$ch;$y++){for($x=0;$x-lt$cw;$x++){
  $p=$scaled.GetPixel($x,$y);$a=if($p.A-le4){0}elseif($p.A-ge220){255}else{Clamp-Byte($p.A*255.0/220.0)}
  if($a-eq0){$albedo.SetPixel($x,$y,[Drawing.Color]::Transparent);$normal.SetPixel($x,$y,[Drawing.Color]::Transparent);$emission.SetPixel($x,$y,[Drawing.Color]::Transparent);$mask.SetPixel($x,$y,[Drawing.Color]::Transparent);$ao.SetPixel($x,$y,[Drawing.Color]::Transparent);continue}
  $lit=switch($job.Mode){"amber"{$p.R-gt100-and$p.R-gt$p.G*1.2-and$p.G-gt$p.B*1.35};"magenta"{$p.R-gt80-and$p.B-gt55-and$p.R-gt$p.G*1.35-and$p.B-gt$p.G*1.2};"cyan"{$p.G-gt90-and$p.B-gt90-and$p.G-gt$p.R*1.15-and$p.B-gt$p.R*1.15}}
  if($lit){$peak=[Math]::Max($p.R,[Math]::Max($p.G,$p.B));$factor=[Math]::Min(1.0,110.0/[Math]::Max(1,$peak));$albedo.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,(Clamp-Byte($p.R*$factor)),(Clamp-Byte($p.G*$factor)),(Clamp-Byte($p.B*$factor))));$emission.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$p.R,$p.G,$p.B))}else{$albedo.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$p.R,$p.G,$p.B));$emission.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,0,0,0))}
  $steel=($p.B-$p.R-gt6)-and($p.B-$p.G-lt40);$metal=if($steel){185}elseif($lit){20}else{45};$gloss=if($steel){62}elseif($lit){125}else{25};$mask.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$metal,$gloss,0));$av=Clamp-Byte(192+[Math]::Min(63,(Luma $p)*.42));$ao.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$av,$av,$av))
 }}
 for($y=0;$y-lt$ch;$y++){for($x=0;$x-lt$cw;$x++){$p=$albedo.GetPixel($x,$y);if($p.A-eq0){continue};$xl=[Math]::Max(0,$x-1);$xr=[Math]::Min($cw-1,$x+1);$yu=[Math]::Max(0,$y-1);$yd=[Math]::Min($ch-1,$y+1);$gx=((Luma $albedo.GetPixel($xr,$y))-(Luma $albedo.GetPixel($xl,$y)))/255.0*2.4;$gy=((Luma $albedo.GetPixel($x,$yd))-(Luma $albedo.GetPixel($x,$yu)))/255.0*2.4;$nx=-$gx;$ny=$gy;$len=[Math]::Sqrt($nx*$nx+$ny*$ny+1);$normal.SetPixel($x,$y,[Drawing.Color]::FromArgb($p.A,(Clamp-Byte(($nx/$len*.5+.5)*255)),(Clamp-Byte(($ny/$len*.5+.5)*255)),(Clamp-Byte((1/$len*.5+.5)*255))))}}
 $outs=@{albedo="approved/albedo/light/tr01_light_$($job.Name)_albedo.png";normal="approved/normal/light/tr01_light_$($job.Name)_normal.png";emission="approved/emission/light/tr01_light_$($job.Name)_emission.png";mask="approved/mask/light/tr01_light_$($job.Name)_mask.png";ao="approved/ao/light/tr01_light_$($job.Name)_ao.png"};foreach($key in $outs.Keys){$path=Join-Path $PackageRoot $outs[$key];New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($path))|Out-Null;(Get-Variable $key -ValueOnly).Save($path,[Drawing.Imaging.ImageFormat]::Png)}
 $scaled.Dispose();$albedo.Dispose();$normal.Dispose();$emission.Dispose();$mask.Dispose();$ao.Dispose();[pscustomobject]@{Asset=$job.Id;Canvas="256x256";Content="$($dw)x$($dh)";PivotPixels=if($job.Anchor-eq"ground"){"128,248"}else{"128,128"};Result="PASS"}
}
