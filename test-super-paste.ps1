$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$setup = (Get-Content -LiteralPath (Join-Path $testRoot 'build.ps1') -Raw).Split('# Pass 1')[0]
Invoke-Expression ($setup.Replace('$root = $PSScriptRoot', '$root = $testRoot'))
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('CCCC-superpaste-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$exe = Join-Path $testDir 'Tests.exe'
$testOpts = @($opts | Where-Object { $_ -notlike '/out:*' -and $_ -ne '/target:winexe' })
& $csc @testOpts /target:exe /main:Cccc.SuperPasteTests "/out:$exe" @src (Join-Path $testRoot 'tests/SuperPasteTests.cs')
if ($LASTEXITCODE) { throw 'Test compilation failed' }
& $exe $testDir
if ($LASTEXITCODE) { throw 'Super Paste tests failed' }
Write-Output "UI preview: $testDir"
