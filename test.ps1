$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('CCCC-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$buildInfo = Join-Path $scratch 'BuildInfo.cs'
Set-Content -LiteralPath $buildInfo -Encoding UTF8 'namespace Cccc { static class BuildInfo { public const string Commit = "test"; public const string Date = "test"; public static readonly string[] Contributors = new string[0]; } }'
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
$sources += $buildInfo, (Join-Path $root 'tests\SettingsFeaturesTests.cs')
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $fw 'WPF'
$exe = Join-Path $scratch 'Tests.exe'
& (Join-Path $fw 'csc.exe') /nologo /target:exe /main:Cccc.SettingsFeaturesTests /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" "/r:$fw\System.Xaml.dll" "/r:$wpf\WindowsFormsIntegration.dll" "/resource:$root\src\Customize.xaml,Cccc.Customize.xaml" "/win32manifest:$root\app.manifest" "/out:$exe" @sources
if ($LASTEXITCODE) { throw 'Test compilation failed' }
& $exe $scratch
if ($LASTEXITCODE) { throw 'Settings feature tests failed' }
