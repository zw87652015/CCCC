$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('CCCC-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$testKey = 'Software\CCCC-Tests-' + [guid]::NewGuid().ToString('N')
# Compile against an isolated preferences key; never touch the user's settings.
$settings = [IO.File]::ReadAllText((Join-Path $root 'src\Settings.cs')).Replace('Software\CCCC', $testKey)
[IO.File]::WriteAllText((Join-Path $scratch 'Settings.cs'), $settings)
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs |
    Where-Object Name -ne 'Settings.cs' | ForEach-Object FullName)
$sources += (Join-Path $scratch 'Settings.cs'), (Join-Path $root 'tests\DisplaySettingsTests.cs')
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $scratch 'Tests.exe'
& $csc /nologo /target:exe /main:Cccc.DisplaySettingsTests /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "/win32manifest:$root\app.manifest" "/out:$exe" @sources
if ($LASTEXITCODE) { throw 'Test compilation failed' }
& $exe $scratch
if ($LASTEXITCODE) { throw 'Display settings tests failed' }
Write-Host "Visual samples: $scratch\display-settings.png"
