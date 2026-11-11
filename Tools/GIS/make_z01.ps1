# F0-B — Deriva los datos de Z01 a partir de las fuentes CNIG. Reproducible e idempotente:
# vuelve a generar Research/Derived/Z01 y Research/Survey/GroundControlPoints.csv desde cero.
#
# Uso (desde cualquier carpeta):  powershell -File Tools\GIS\make_z01.ps1
# Requiere GDAL (probado con el de QGIS 3.44.15, GDAL 3.13.3). No descarga nada.

param(
    [string]$GdalBin = 'C:\Program Files\QGIS 3.44.15\bin',
    # Origen local (D21/D33): ETRS89 / UTM 29N (EPSG:25829) y cota ortométrica. Coincide con un
    # centro de celda del MDT02 (este y norte pares), así el recorte no remuestrea el relieve.
    [double]$OriginE = 726094,
    [double]$OriginN = 4372732,
    [double]$OriginH = 440,
    [int]$Samples = 513,      # muestras por lado (2^n + 1, lo que pide un Terrain de Unity)
    [double]$Cell = 2         # metros entre muestras = resolución del MDT02
)

$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture
$utf8 = New-Object Text.UTF8Encoding($false)
function F([double]$v, [string]$fmt = '0.###') { $v.ToString($fmt, $inv) }
function Num([string]$s) { [double]::Parse($s.Trim(), $inv) }

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src = Join-Path $root 'Research\Source\CNIG'
$out = Join-Path $root 'Research\Derived\Z01'
$survey = Join-Path $root 'Research\Survey'
$mdtSrc = Join-Path $src 'MDT02-ETRS89-HU29-0704-1-COB2.tif'
$mdsSrc = Join-Path $src 'MDS02_WGS84_PM-2_0704-1.tif'
foreach ($f in $mdtSrc, $mdsSrc) { if (-not (Test-Path $f)) { throw "Falta la fuente: $f" } }

$env:PATH = "$GdalBin;$env:PATH"
if (-not $env:PROJ_LIB) { $env:PROJ_LIB = Join-Path (Split-Path $GdalBin) 'share\proj' }
if (-not $env:GDAL_DATA) {
    $gd = Join-Path (Split-Path $GdalBin) 'apps\gdal\share\gdal'
    if (Test-Path $gd) { $env:GDAL_DATA = $gd }
}

function Gdal([string]$exe, [string[]]$a) {
    $r = & (Join-Path $GdalBin $exe) @a 2>&1
    if ($LASTEXITCODE -ne 0) { throw "$exe fallo: $r" }
    return $r
}

New-Item -ItemType Directory -Force $out, $survey | Out-Null
Get-ChildItem $out -File | Remove-Item -Force

# Las muestras van en los vértices del terreno: la ventana se amplía media celda por cada lado
# para que los centros de píxel caigan en origen ± (Samples-1)/2 * Cell.
$half = ($Samples - 1) / 2 * $Cell + $Cell / 2
$te = @((F ($OriginE - $half)), (F ($OriginN - $half)), (F ($OriginE + $half)), (F ($OriginN + $half)))
$common = @('-overwrite', '-t_srs', 'EPSG:25829', '-te') + $te + @('-tr', (F $Cell), (F $Cell), '-ot', 'Float32', '-of', 'ENVI', '-dstnodata', '-32767')

$mdtBin = Join-Path $out 'z01_mdt_513.bin'
$mdsBin = Join-Path $out 'z01_mds_513.bin'
Gdal 'gdalwarp.exe' ($common + @('-r', 'near', $mdtSrc, $mdtBin)) | Out-Null      # misma rejilla: copia exacta
Gdal 'gdalwarp.exe' ($common + @('-r', 'bilinear', $mdsSrc, $mdsBin)) | Out-Null  # WGS84 -> UTM: remuestreo

function Stats([string]$file) {
    $info = Gdal 'gdalinfo.exe' @('-stats', $file)
    $size = ($info | Select-String 'Size is (\d+), (\d+)').Matches[0]
    $min = Num (($info | Select-String 'STATISTICS_MINIMUM=(.+)').Matches[0].Groups[1].Value)
    $max = Num (($info | Select-String 'STATISTICS_MAXIMUM=(.+)').Matches[0].Groups[1].Value)
    $valid = ($info | Select-String 'STATISTICS_VALID_PERCENT=(.+)').Matches[0].Groups[1].Value
    if ([int]$size.Groups[1].Value -ne $Samples -or [int]$size.Groups[2].Value -ne $Samples) { throw "Size inesperado en $file" }
    if ((Num $valid) -lt 100) { throw "Hay celdas sin dato en $file ($valid % validas)" }
    return @{ Min = $min; Max = $max }
}
$mdt = Stats $mdtBin
$mds = Stats $mdsBin
Get-ChildItem $out -Filter '*.aux.xml' | Remove-Item -Force

$gdalVersion = ((Gdal 'gdalinfo.exe' @('--version')) -join ' ').Replace('"', "'")
$meta = @"
{
  "zone": "Z01",
  "crs": "EPSG:25829",
  "originEasting": $(F $OriginE),
  "originNorthing": $(F $OriginN),
  "originHeight": $(F $OriginH),
  "samples": $Samples,
  "cellSize": $(F $Cell),
  "rowOrder": "north-to-south",
  "format": "float32 little-endian, sin cabecera",
  "terrainFile": "z01_mdt_513.bin",
  "surfaceFile": "z01_mds_513.bin",
  "terrainMin": $(F $mdt.Min '0.000'),
  "terrainMax": $(F $mdt.Max '0.000'),
  "surfaceMin": $(F $mds.Min '0.000'),
  "surfaceMax": $(F $mds.Max '0.000'),
  "terrainSource": "MDT02-ETRS89-HU29-0704-1-COB2.tif (DATA-001)",
  "surfaceSource": "MDS02_WGS84_PM-2_0704-1.tif (DATA-003)",
  "tool": "$gdalVersion"
}
"@
[IO.File]::WriteAllText((Join-Path $out 'z01_meta.json'), $meta, $utf8)

# --- Puntos de control -> ETRS89 UTM 29N -> metros locales, con cotas leidas de las FUENTES
# (no del recorte), para que la comprobacion en Unity sea independiente de este script.
# Cada fila trae easting/northing (puntos sacados del LiDAR) o, si faltan, lat/lon (WGS84).
# role = control (se usa) | descartado (se conserva solo como registro).
$rows = Import-Csv (Join-Path $survey 'gcp_input.csv') -Encoding UTF8
$lines = @('id,name,role,lat,lon,easting,northing,mdt_height,mds_height,local_x,local_y,local_z,inside_zone,size_ew,size_ns,height_m,confidence,source,source_url,date_obtained,notes')
foreach ($r in $rows) {
    if ($r.easting -and $r.northing) {
        $e = Num $r.easting; $n = Num $r.northing
    } else {
        # WGS84 y ETRS89 difieren menos de 1 m en la Peninsula: se toman como equivalentes y se anota.
        # Se pasan como argumentos (-coord, orden longitud latitud): por stdin PowerShell puede anteponer un BOM.
        $xy = ((Gdal 'gdaltransform.exe' @('-s_srs', 'EPSG:4326', '-t_srs', 'EPSG:25829', '-coord', $r.lon, $r.lat)) -join ' ').Trim() -split '\s+'
        $e = Num $xy[0]; $n = Num $xy[1]
    }
    $hT = Num ((Gdal 'gdallocationinfo.exe' @('-valonly', '-r', 'bilinear', '-geoloc', $mdtSrc, (F $e '0.000'), (F $n '0.000'))) -join '')
    $hS = Num ((Gdal 'gdallocationinfo.exe' @('-valonly', '-r', 'bilinear', '-l_srs', 'EPSG:25829', $mdsSrc, (F $e '0.000'), (F $n '0.000'))) -join '')
    $lx = $e - $OriginE; $lz = $n - $OriginN; $ly = $hT - $OriginH
    $limit = ($Samples - 1) / 2 * $Cell
    $inside = ([Math]::Abs($lx) -le $limit) -and ([Math]::Abs($lz) -le $limit)
    $lines += (@($r.id, $r.name, $r.role, $r.lat, $r.lon, (F $e '0.000'), (F $n '0.000'), (F $hT '0.000'), (F $hS '0.000'),
            (F $lx '0.000'), (F $ly '0.000'), (F $lz '0.000'), $inside.ToString().ToLower(), $r.size_ew, $r.size_ns, $r.height_m, $r.confidence,
            $r.source, $r.source_url, $r.date_obtained, $r.notes) | ForEach-Object { if ("$_" -match '[,"]') { '"' + ("$_" -replace '"', '""') + '"' } else { "$_" } }) -join ','
}
[IO.File]::WriteAllText((Join-Path $survey 'GroundControlPoints.csv'), ($lines -join "`n") + "`n", $utf8)

"Z01 derivado en $out"
"  relieve  (MDT02): $(F $mdt.Min '0.00') .. $(F $mdt.Max '0.00') m"
"  superficie (MDS02): $(F $mds.Min '0.00') .. $(F $mds.Max '0.00') m"
"  puntos de control: $($rows.Count) -> Research\Survey\GroundControlPoints.csv"
