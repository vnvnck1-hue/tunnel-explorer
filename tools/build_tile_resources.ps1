param(
  [string]$OutputRoot = (Join-Path $PSScriptRoot '..\tile_resource_output')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$savedSourceRoot = Join-Path $OutputRoot 'source_art'
$wallSourcePath = Join-Path $savedSourceRoot 'ai_wall_materials_4x4.png'
$resourceSourcePath = Join-Path $savedSourceRoot 'ai_resources_floor_4x4.png'
$overlaySourcePath = Join-Path $savedSourceRoot 'ai_damage_decal_overlays_4x4.png'
$generatedSourceRoot = 'C:\Users\vnvnc\.codex\generated_images\01a03445-0068-7413-9db4-d49830ae2486'
if (-not (Test-Path -LiteralPath $wallSourcePath)) { $wallSourcePath = Join-Path $generatedSourceRoot 'exec-b425fac4-fb12-4b80-ba22-8ec1d0ee3daf.png' }
if (-not (Test-Path -LiteralPath $resourceSourcePath)) { $resourceSourcePath = Join-Path $generatedSourceRoot 'exec-8153e012-aa5b-430d-9264-41a80edca4b1.png' }
if (-not (Test-Path -LiteralPath $overlaySourcePath)) { $overlaySourcePath = Join-Path $generatedSourceRoot 'exec-82484089-9ba1-4ed4-9dc4-9af4d639f5d9.png' }

function New-ArgbBitmap([int]$Width, [int]$Height) {
  return [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function New-QualityGraphics([System.Drawing.Image]$Image) {
  $g = [System.Drawing.Graphics]::FromImage($Image)
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  return $g
}

function Get-GridCell(
  [System.Drawing.Image]$Source,
  [int]$Column,
  [int]$Row,
  [int]$Columns = 4,
  [int]$Rows = 4,
  [int]$Size = 100
) {
  $out = New-ArgbBitmap $Size $Size
  $g = New-QualityGraphics $out
  try {
    $srcX = [single]($Source.Width * $Column / $Columns)
    $srcY = [single]($Source.Height * $Row / $Rows)
    $srcW = [single]($Source.Width / $Columns)
    $srcH = [single]($Source.Height / $Rows)
    $dest = [System.Drawing.Rectangle]::new(0, 0, $Size, $Size)
    $g.DrawImage($Source, $dest, $srcX, $srcY, $srcW, $srcH, [System.Drawing.GraphicsUnit]::Pixel)
  } finally { $g.Dispose() }
  return $out
}

function Resize-Bitmap([System.Drawing.Image]$Source, [int]$Width, [int]$Height) {
  $out = New-ArgbBitmap $Width $Height
  $g = New-QualityGraphics $out
  try {
    $g.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, $Width, $Height))
  } finally { $g.Dispose() }
  return $out
}

function Remove-Checkerboard([System.Drawing.Bitmap]$Image) {
  $out = New-ArgbBitmap $Image.Width $Image.Height
  for ($y = 0; $y -lt $Image.Height; $y++) {
    for ($x = 0; $x -lt $Image.Width; $x++) {
      $c = $Image.GetPixel($x, $y)
      $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
      $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
      $sat = $max - $min
      if ($sat -lt 18 -and $min -gt 205) {
        $a = [int][Math]::Max(0, [Math]::Min(255, (225 - $min) * 12.75))
      } else { $a = 255 }
      $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B))
    }
  }
  return $out
}

function Convert-HexColor([string]$Hex) {
  return [System.Drawing.ColorTranslator]::FromHtml($Hex)
}

function Mix-Color([System.Drawing.Color]$A, [System.Drawing.Color]$B, [double]$T) {
  $t2 = [Math]::Max(0, [Math]::Min(1, $T))
  return [System.Drawing.Color]::FromArgb(
    255,
    [int][Math]::Round($A.R + ($B.R - $A.R) * $t2),
    [int][Math]::Round($A.G + ($B.G - $A.G) * $t2),
    [int][Math]::Round($A.B + ($B.B - $A.B) * $t2)
  )
}

function Colorize-Material(
  [System.Drawing.Bitmap]$Source,
  [string]$ShadowHex,
  [string]$BaseHex,
  [string]$HighlightHex,
  [string]$AccentHex = ''
) {
  $shadow = Convert-HexColor $ShadowHex
  $base = Convert-HexColor $BaseHex
  $highlight = Convert-HexColor $HighlightHex
  $accent = if ($AccentHex) { Convert-HexColor $AccentHex } else { $null }
  $out = New-ArgbBitmap $Source.Width $Source.Height
  for ($y = 0; $y -lt $Source.Height; $y++) {
    for ($x = 0; $x -lt $Source.Width; $x++) {
      $c = $Source.GetPixel($x, $y)
      $lum = (0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B) / 255.0
      $mx = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
      $mn = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
      $sat = if ($mx -eq 0) { 0 } else { ($mx - $mn) / [double]$mx }
      if ($accent -and $sat -gt 0.32 -and $lum -gt 0.18) {
        $darkAccent = Mix-Color (Convert-HexColor '#062830') $accent 0.28
        $lightAccent = Mix-Color $accent (Convert-HexColor '#D8FFFF') 0.42
        $mapped = if ($lum -lt 0.56) { Mix-Color $darkAccent $accent ($lum / 0.56) } else { Mix-Color $accent $lightAccent (($lum - 0.56) / 0.44) }
      } else {
        $mapped = if ($lum -lt 0.48) { Mix-Color $shadow $base ($lum / 0.48) } else { Mix-Color $base $highlight (($lum - 0.48) / 0.52) }
      }
      $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $mapped.R, $mapped.G, $mapped.B))
    }
  }
  return $out
}

function Add-Overlay([System.Drawing.Bitmap]$Base, [System.Drawing.Image]$Overlay, [double]$Opacity = 1.0) {
  $g = New-QualityGraphics $Base
  try {
    if ($Opacity -ge 0.999) {
      $g.DrawImage($Overlay, 0, 0, $Base.Width, $Base.Height)
    } else {
      $matrix = [System.Drawing.Imaging.ColorMatrix]::new()
      $matrix.Matrix33 = [single]$Opacity
      $attrs = [System.Drawing.Imaging.ImageAttributes]::new()
      try {
        $attrs.SetColorMatrix($matrix)
        $g.DrawImage($Overlay, [System.Drawing.Rectangle]::new(0, 0, $Base.Width, $Base.Height), 0, 0, $Overlay.Width, $Overlay.Height, [System.Drawing.GraphicsUnit]::Pixel, $attrs)
      } finally { $attrs.Dispose() }
    }
  } finally { $g.Dispose() }
}

function Apply-DamageMask([System.Drawing.Bitmap]$Image, [int]$Stage, [int]$Seed) {
  if ($Stage -le 0) { return }
  $rnd = [System.Random]::new($Seed)
  $g = [System.Drawing.Graphics]::FromImage($Image)
  try {
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $clear = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::Transparent)
    try {
      $count = $Stage * 3
      for ($i = 0; $i -lt $count; $i++) {
        $side = $rnd.Next(0, 4)
        $radius = 6.2 + $rnd.NextDouble() * 7.2
        $radius *= 0.78 + $Stage * 0.11
        $along = 8 + $rnd.NextDouble() * 84
        switch ($side) {
          0 { $cx = $along; $cy = 0 }
          1 { $cx = 100; $cy = $along }
          2 { $cx = $along; $cy = 100 }
          3 { $cx = 0; $cy = $along }
        }
        if ($rnd.NextDouble() -lt 0.30) {
          $g.FillEllipse($clear, [single]($cx-$radius), [single]($cy-$radius), [single]($radius*2), [single]($radius*2))
        } else {
          $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
          try {
            $points = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
            $vertices = 3 + $rnd.Next(0, 2)
            $rot = $rnd.NextDouble() * [Math]::PI * 2
            for ($v = 0; $v -lt $vertices; $v++) {
              $a = $rot + $v / [double]$vertices * [Math]::PI * 2
              $rr = $radius * (0.55 + $rnd.NextDouble() * 1.05)
              $points.Add([System.Drawing.PointF]::new([single]($cx+[Math]::Cos($a)*$rr), [single]($cy+[Math]::Sin($a)*$rr)))
            }
            $path.AddPolygon($points.ToArray())
            $g.FillPath($clear, $path)
          } finally { $path.Dispose() }
        }
      }
      if ($Stage -ge 3) {
        $corner = $rnd.Next(0, 4)
        $size = 24 + $rnd.NextDouble() * 10
        $corners = @(@(0,0,1,1),@(100,0,-1,1),@(100,100,-1,-1),@(0,100,1,-1))
        $c = $corners[$corner]
        $pts = @(
          [System.Drawing.PointF]::new($c[0],$c[1]),
          [System.Drawing.PointF]::new([single]($c[0]+$c[2]*$size),$c[1]),
          [System.Drawing.PointF]::new([single]($c[0]+$c[2]*$size*.35),[single]($c[1]+$c[3]*$size*.55)),
          [System.Drawing.PointF]::new($c[0],[single]($c[1]+$c[3]*$size))
        )
        $g.FillPolygon($clear, $pts)
      }
    } finally { $clear.Dispose() }
  } finally { $g.Dispose() }
}

function Save-Png([System.Drawing.Image]$Image, [string]$Path) {
  $dir = Split-Path -Parent $Path
  [System.IO.Directory]::CreateDirectory($dir) | Out-Null
  $Image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-CoreSide([System.Drawing.Bitmap]$CoreTop, [string]$BottomHex) {
  $side = Resize-Bitmap $CoreTop 50 13
  $g = New-QualityGraphics $side
  try {
    $bottom = [System.Drawing.SolidBrush]::new((Convert-HexColor $BottomHex))
    $hi = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(72, 190, 210, 255))
    try {
      $g.FillRectangle($bottom, 0, 9, 50, 4)
      $g.FillRectangle($hi, 0, 0, 50, 2)
    } finally { $bottom.Dispose(); $hi.Dispose() }
  } finally { $g.Dispose() }
  return $side
}

[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$sourceOut = Join-Path $OutputRoot 'source_art'
[System.IO.Directory]::CreateDirectory($sourceOut) | Out-Null
if ((Resolve-Path -LiteralPath $wallSourcePath).Path -ne (Join-Path $sourceOut 'ai_wall_materials_4x4.png')) { Copy-Item -LiteralPath $wallSourcePath -Destination (Join-Path $sourceOut 'ai_wall_materials_4x4.png') -Force }
if ((Resolve-Path -LiteralPath $resourceSourcePath).Path -ne (Join-Path $sourceOut 'ai_resources_floor_4x4.png')) { Copy-Item -LiteralPath $resourceSourcePath -Destination (Join-Path $sourceOut 'ai_resources_floor_4x4.png') -Force }
if ((Resolve-Path -LiteralPath $overlaySourcePath).Path -ne (Join-Path $sourceOut 'ai_damage_decal_overlays_4x4.png')) { Copy-Item -LiteralPath $overlaySourcePath -Destination (Join-Path $sourceOut 'ai_damage_decal_overlays_4x4.png') -Force }

$wallSource = [System.Drawing.Bitmap]::new($wallSourcePath)
$resourceSource = [System.Drawing.Bitmap]::new($resourceSourcePath)
$overlayRaw = [System.Drawing.Bitmap]::new($overlaySourcePath)
$overlaySource = Remove-Checkerboard $overlayRaw
$overlayRaw.Dispose()

$wallCells = @{}
$resourceCells = @{}
$overlayCells = @{}
try {
  for ($row=0; $row -lt 4; $row++) {
    for ($col=0; $col -lt 4; $col++) {
      $wallCells["$row,$col"] = Get-GridCell $wallSource $col $row
      $resourceCells["$row,$col"] = Get-GridCell $resourceSource $col $row
      $overlayCells["$row,$col"] = Get-GridCell $overlaySource $col $row
    }
  }

  $biomes = @(
    [pscustomobject]@{
      Id='purple'
      Dirt=@(
        @('#1A1430','#2E2248','#3D2E5E'), @('#211A38','#352851','#463568'),
        @('#161028','#2A1E44','#382A58'), @('#120E24','#241A3C','#322650'))
      Stone=@('#2A2040','#3A2E58','#4E3E70'); Rock=@('#080C18','#161C32','#6A7EB0'); Core=@('#0E121C','#1E2438','#7A8CB8')
      Floor=@('#534334','#6A5744','#8A7460'); Resource=@('#F06AB8','#FF4FA8','#5EE8FF')
      Seam=@('#6B3A8A','#4A2868','#2A1848')
    },
    [pscustomobject]@{
      Id='brine'
      Dirt=@(
        @('#102830','#1A3840','#254850'), @('#143038','#1E4048','#2A545C'),
        @('#0E2830','#163440','#224850'), @('#0C2028','#123038','#1E4048'))
      Stone=@('#1A3840','#2A5058','#3A6870'); Rock=@('#040C14','#0A1824','#4A90A8'); Core=@('#061018','#0E2030','#5AA8C0')
      Floor=@('#2C423A','#3A554C','#5A7868'); Resource=@('#5EE8D0','#3AD0C0','#7FFFF0')
      Seam=@('#2A8890','#1A6068','#0E4048')
    }
  )

  $manifest = @()
  foreach ($biome in $biomes) {
    $biomeRoot = Join-Path $OutputRoot $biome.Id
    $individualDir = Join-Path $biomeRoot 'individual_50'
    $overlayDir = Join-Path $biomeRoot 'overlays'
    [System.IO.Directory]::CreateDirectory($individualDir) | Out-Null
    [System.IO.Directory]::CreateDirectory($overlayDir) | Out-Null
    $atlas1 = New-ArgbBitmap 800 850
    $atlas2 = New-ArgbBitmap 1600 1700
    $g1 = New-QualityGraphics $atlas1
    $g2 = New-QualityGraphics $atlas2
    try {
      $typeNames = @('dirt','stone','ore','gem','crys')
      for ($typeIndex=0; $typeIndex -lt 5; $typeIndex++) {
        $type = $typeNames[$typeIndex]
        for ($band=0; $band -lt 4; $band++) {
          for ($surface=0; $surface -lt 3; $surface++) {
            for ($damage=0; $damage -lt 4; $damage++) {
              if ($typeIndex -eq 0) { $baseSource = $wallCells["$band,0"] }
              elseif ($typeIndex -eq 1) { $baseSource = $wallCells["$band,1"] }
              else { $baseSource = $resourceCells["$band,$($typeIndex-2)"] }

              if ($biome.Id -eq 'purple') {
                $tile100 = Resize-Bitmap $baseSource 100 100
              } else {
                if ($typeIndex -eq 0) { $pal = $biome.Dirt[$band]; $accent = '' }
                elseif ($typeIndex -eq 1) { $pal = $biome.Stone; $accent = '' }
                else { $pal = $biome.Dirt[$band]; $accent = $biome.Resource[$typeIndex-2] }
                $tile100 = Colorize-Material $baseSource $pal[0] $pal[1] $pal[2] $accent
              }
              try {
                if ($surface -eq 1) { Add-Overlay $tile100 $overlayCells["3,$(($typeIndex+$band+$damage)%2)"] 0.72 }
                elseif ($surface -eq 2) { Add-Overlay $tile100 $overlayCells["3,$(2+(($typeIndex+$band+$damage)%2))"] 0.60 }
                if ($damage -gt 0) {
                  $ovCol = ($typeIndex*3 + $band + $surface + $damage) % 4
                  Add-Overlay $tile100 $overlayCells["$($damage-1),$ovCol"] 0.82
                  Apply-DamageMask $tile100 $damage (1009 + $typeIndex*1000 + $band*100 + $surface*10 + $damage)
                }
                $tile50 = Resize-Bitmap $tile100 50 50
                try {
                  $index = $typeIndex*48 + $band*12 + $surface*4 + $damage
                  $col = $index % 16; $row = [Math]::Floor($index / 16)
                  $name = '{0}_{1}_b{2}_s{3}_d{4}.png' -f $biome.Id,$type,$band,$surface,$damage
                  Save-Png $tile50 (Join-Path $individualDir $name)
                  $g1.DrawImage($tile50, $col*50, $row*50, 50, 50)
                  $g2.DrawImage($tile100, $col*100, $row*100, 100, 100)
                  $manifest += [pscustomobject]@{biome=$biome.Id;index=$index;file=$name;type=$type;band=$band;surface=$surface;damage=$damage;width=50;height=50;atlasX=$col*50;atlasY=$row*50}
                } finally { $tile50.Dispose() }
              } finally { $tile100.Dispose() }
            }
          }
        }
      }

      for ($band=0; $band -lt 4; $band++) {
        $baseSource = $wallCells["$band,2"]
        $tile100 = if ($biome.Id -eq 'purple') { Resize-Bitmap $baseSource 100 100 } else { Colorize-Material $baseSource $biome.Rock[0] $biome.Rock[1] $biome.Rock[2] }
        try {
          $tile50 = Resize-Bitmap $tile100 50 50
          try {
            $index=240+$band;$col=$index%16;$row=[Math]::Floor($index/16)
            $name='{0}_rock_b{1}.png' -f $biome.Id,$band
            Save-Png $tile50 (Join-Path $individualDir $name)
            $g1.DrawImage($tile50,$col*50,$row*50,50,50);$g2.DrawImage($tile100,$col*100,$row*100,100,100)
            $manifest += [pscustomobject]@{biome=$biome.Id;index=$index;file=$name;type='rock';band=$band;surface=0;damage=0;width=50;height=50;atlasX=$col*50;atlasY=$row*50}
          } finally {$tile50.Dispose()}
        } finally {$tile100.Dispose()}
      }

      for ($band=0; $band -lt 4; $band++) {
        $baseSource = $wallCells["$band,3"]
        for ($variant=0; $variant -lt 6; $variant++) {
          $tile100 = if ($biome.Id -eq 'purple') { Resize-Bitmap $baseSource 100 100 } else { Colorize-Material $baseSource $biome.Core[0] $biome.Core[1] $biome.Core[2] }
          try {
            if ($variant -gt 0) { Add-Overlay $tile100 $overlayCells["0,$(($variant+$band)%4)"] (0.14 + $variant*0.018) }
            $tile50=Resize-Bitmap $tile100 50 50
            try {
              $index=244+$band*6+$variant;$col=$index%16;$row=[Math]::Floor($index/16)
              $name='{0}_core_top_b{1}_v{2}.png' -f $biome.Id,$band,$variant
              Save-Png $tile50 (Join-Path $individualDir $name)
              $g1.DrawImage($tile50,$col*50,$row*50,50,50);$g2.DrawImage($tile100,$col*100,$row*100,100,100)
              $manifest += [pscustomobject]@{biome=$biome.Id;index=$index;file=$name;type='core_top';band=$band;variant=$variant;width=50;height=50;atlasX=$col*50;atlasY=$row*50}
            } finally {$tile50.Dispose()}
          } finally {$tile100.Dispose()}
        }
      }
    } finally { $g1.Dispose(); $g2.Dispose() }
    Save-Png $atlas1 (Join-Path $biomeRoot ('{0}_strict_atlas_800x850.png' -f $biome.Id))
    Save-Png $atlas2 (Join-Path $biomeRoot ('{0}_strict_atlas_master_1600x1700.png' -f $biome.Id))
    $atlas1.Dispose();$atlas2.Dispose()

    $floorSheet = New-ArgbBitmap 150 50
    $floorG = New-QualityGraphics $floorSheet
    try {
      for($i=0;$i -lt 3;$i++) {
        $sourceRow = $i
        $baseFloor = $resourceCells["$sourceRow,3"]
        $floor100 = if($biome.Id -eq 'purple'){Resize-Bitmap $baseFloor 100 100}else{Colorize-Material $baseFloor $biome.Floor[0] $biome.Floor[1] $biome.Floor[2]}
        try {$floor50=Resize-Bitmap $floor100 50 50;try{$fn='{0}_floor_{1}.png'-f $biome.Id,@('dark','base','rim')[$i];Save-Png $floor50 (Join-Path $individualDir $fn);$floorG.DrawImage($floor50,$i*50,0,50,50)}finally{$floor50.Dispose()}}finally{$floor100.Dispose()}
      }
    } finally {$floorG.Dispose()}
    Save-Png $floorSheet (Join-Path $biomeRoot ('{0}_floor_sheet_150x50.png'-f $biome.Id));$floorSheet.Dispose()

    for($band=0;$band -lt 4;$band++){
      $coreBase=$wallCells["$band,3"]
      $coreForSide=if($biome.Id -eq 'purple'){Resize-Bitmap $coreBase 100 100}else{Colorize-Material $coreBase $biome.Core[0] $biome.Core[1] $biome.Core[2]}
      try{$side=New-CoreSide $coreForSide $biome.Core[0];try{Save-Png $side (Join-Path $overlayDir ('{0}_core_side_b{1}_50x13.png'-f $biome.Id,$band))}finally{$side.Dispose()}}finally{$coreForSide.Dispose()}
    }
    for($i=0;$i -lt 3;$i++){
      $seam=New-ArgbBitmap 50 6;$sg=[System.Drawing.Graphics]::FromImage($seam);try{$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(77,(Convert-HexColor $biome.Seam[$i])));try{$sg.FillRectangle($brush,0,0,50,6)}finally{$brush.Dispose()}}finally{$sg.Dispose()};Save-Png $seam (Join-Path $overlayDir ('{0}_seam_{1}_50x6.png'-f $biome.Id,$i));$seam.Dispose()
    }
    $shadow=New-ArgbBitmap 50 11;$shg=[System.Drawing.Graphics]::FromImage($shadow);try{$b1=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(66,24,14,10));$b2=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(31,24,14,10));try{$shg.FillRectangle($b1,2,0,46,7);$shg.FillRectangle($b2,0,7,50,4)}finally{$b1.Dispose();$b2.Dispose()}}finally{$shg.Dispose()};Save-Png $shadow (Join-Path $overlayDir ('{0}_core_bottom_shadow_50x11.png'-f $biome.Id));$shadow.Dispose()
  }

  $manifest | Sort-Object biome,index | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot 'tile_manifest.json') -Encoding UTF8
  $manifest | Sort-Object biome,index | Export-Csv -LiteralPath (Join-Path $OutputRoot 'tile_manifest.csv') -NoTypeInformation -Encoding UTF8
} finally {
  foreach($v in $wallCells.Values){$v.Dispose()};foreach($v in $resourceCells.Values){$v.Dispose()};foreach($v in $overlayCells.Values){$v.Dispose()}
  $wallSource.Dispose();$resourceSource.Dispose();$overlaySource.Dispose()
}

Write-Output "Built tile resources at $OutputRoot"
