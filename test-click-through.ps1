$ErrorActionPreference = 'Stop'
# Reuse the production compiler references (WPF, WinRT and Windows SDK).
$setup = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'build.ps1') -Raw).Split('# Pass 1')[0]
$testRoot = $PSScriptRoot
$setup = $setup.Replace('$root = $PSScriptRoot', '$root = $testRoot')
Invoke-Expression $setup
$testExe = Join-Path ([IO.Path]::GetTempPath()) ('CCCC-click-tests-' + [guid]::NewGuid().ToString('N') + '.exe')
$testOpts = @($opts | Where-Object { $_ -notlike '/out:*' -and $_ -ne '/target:winexe' })
& $csc @testOpts /target:exe /main:Cccc.ClickThroughTests "/out:$testExe" @src (Join-Path $PSScriptRoot 'tests\ClickThroughTests.cs')
if ($LASTEXITCODE) { throw 'Test compilation failed' }
& $testExe
if ($LASTEXITCODE) { throw 'Click-through tests failed' }
