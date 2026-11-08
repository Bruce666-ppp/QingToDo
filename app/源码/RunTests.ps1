param([string]$WorkDirectory = (Join-Path $env:TEMP ('QingTodo-tests-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$testExecutable = Join-Path $WorkDirectory 'CoreTests.exe'
& $compilerPath /nologo /target:exe /utf8output /codepage:65001 ('/out:' + $testExecutable) /reference:System.Runtime.Serialization.dll (Join-Path $PSScriptRoot 'Core.cs') (Join-Path $PSScriptRoot 'Integration.cs') (Join-Path $PSScriptRoot 'tests\CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
& $testExecutable $WorkDirectory
if ($LASTEXITCODE -ne 0) { throw '测试未通过' }
