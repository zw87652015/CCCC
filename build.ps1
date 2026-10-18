$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $root 'bin'
$dist = Join-Path $root 'dist'
$exe = Join-Path $bin 'CCCC.exe'
$ico = Join-Path $bin 'cccc.ico'
New-Item -ItemType Directory -Force $bin, $dist | Out-Null

$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $fw 'WPF'
$opts = @('/nologo', '/target:winexe', '/optimize+', '/debug-', "/win32manifest:$root\app.manifest",
          '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll',
          "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll",
          "/r:$fw\System.Xaml.dll", "/r:$wpf\WindowsFormsIntegration.dll",
          "/resource:$root\src\Customize.xaml,Cccc.Customize.xaml", "/out:$exe")

# Pass 1 builds the app, which then draws its own icon; pass 2 embeds it.
& $csc @opts @src
if ($LASTEXITCODE) { exit $LASTEXITCODE }
Start-Process $exe -ArgumentList '--export-icon', "`"$ico`"" -Wait
& $csc @opts "/win32icon:$ico" @src
if ($LASTEXITCODE) { exit $LASTEXITCODE }

# Portable release: one self-contained exe (manifest and icon embedded), plus a zip for sharing.
$portable = Join-Path $dist 'CCCC.exe'
$zip = Join-Path $dist 'CCCC-portable.zip'
Copy-Item $exe $portable -Force
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $portable -DestinationPath $zip

foreach ($f in $portable, $zip) { '{0}  {1:N1} KB' -f $f, ((Get-Item $f).Length / 1KB) }
