$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$bin = Join-Path $root 'bin'
$dist = Join-Path $root 'dist'
$msix = Join-Path $bin 'msix'
$exe = Join-Path $bin 'CCCC.exe'
New-Item -ItemType Directory -Force $msix, $dist | Out-Null

& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE) { throw "build.ps1 failed ($LASTEXITCODE)" }

$ver = [regex]::Match((Get-Content (Join-Path $root 'src\AssemblyInfo.cs') -Raw), 'AssemblyVersion\("([\d.]+)"\)').Groups[1].Value
$kit = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Directory |
    Where-Object { $_.Name -match '^10\.' -and (Test-Path (Join-Path $_.FullName 'x64\makeappx.exe')) } |
    Sort-Object { [version]$_.Name } | Select-Object -Last 1
$makeappx = Join-Path $kit.FullName 'x64\makeappx.exe'
$makepri = Join-Path $kit.FullName 'x64\makepri.exe'

$assets = @(
    @('StoreLogo.scale-100', 50, 50), @('StoreLogo.scale-200', 100, 100),
    @('Square150x150Logo.scale-100', 150, 96), @('Square150x150Logo.scale-200', 300, 192),
    @('Square44x44Logo.scale-100', 44, 44), @('Square44x44Logo.scale-200', 88, 88)
)
foreach ($n in 16, 24, 32, 48, 256) {
    $assets += ,@("Square44x44Logo.targetsize-$n", $n, $n)
    $assets += ,@("Square44x44Logo.targetsize-${n}_altform-unplated", $n, $n)
}
$assetDir = Join-Path $msix 'assets'
New-Item -ItemType Directory -Force $assetDir | Out-Null
foreach ($a in $assets) {
    $png = Join-Path $assetDir ($a[0] + '.png')
    Start-Process $exe -ArgumentList '--export-png', "`"$png`"", $a[1], $a[2] -Wait
    if (-not (Test-Path $png)) { throw "asset not exported: $png" }
}

$cfg = Join-Path $msix 'priconfig.xml'
& $makepri createconfig /cf $cfg /dq en-US_zh-CN /o
if ($LASTEXITCODE) { throw "makepri createconfig failed ($LASTEXITCODE)" }

$archs = 'x64', 'arm64'
$outs = @()
foreach ($arch in $archs) {
    $layout = Join-Path $msix $arch
    Remove-Item $layout -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force (Join-Path $layout 'Assets') | Out-Null
    Copy-Item $exe $layout
    Copy-Item (Join-Path $assetDir '*.png') (Join-Path $layout 'Assets')
    (Get-Content (Join-Path $root 'store\AppxManifest.xml') -Raw).Replace('{VERSION}', $ver).Replace('{ARCH}', $arch) |
        Set-Content (Join-Path $layout 'AppxManifest.xml') -Encoding UTF8

    & $makepri new /pr $layout /cf $cfg /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o
    if ($LASTEXITCODE) { throw "makepri new failed for $arch ($LASTEXITCODE)" }

    $out = Join-Path $msix "CtrlCCCC_${ver}_$arch.msix"
    Remove-Item $out -ErrorAction SilentlyContinue
    & $makeappx pack /d $layout /p $out /o
    if ($LASTEXITCODE) { throw "makeappx pack failed for $arch ($LASTEXITCODE)" }
    $outs += $out
}

$bundleDir = Join-Path $msix 'bundle'
Remove-Item $bundleDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $bundleDir | Out-Null
foreach ($f in $outs) { Copy-Item $f $bundleDir }
$bundle = Join-Path $dist "CtrlCCCC_$ver.msixbundle"
Remove-Item $bundle -ErrorAction SilentlyContinue
& $makeappx bundle /d $bundleDir /p $bundle /bv $ver /o
if ($LASTEXITCODE) { throw "makeappx bundle failed ($LASTEXITCODE)" }

foreach ($f in $outs + $bundle) { '{0}  {1:N1} KB' -f $f, ((Get-Item $f).Length / 1KB) }
