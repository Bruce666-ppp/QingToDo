param(
    [string]$AppDirectory = ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))),
    [string]$OutputPath,
    [string]$WorkDirectory = (Join-Path $env:TEMP ('QingTodo-package-' + [guid]::NewGuid().ToString('N'))),
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) { $OutputPath = Join-Path (Split-Path -Parent $AppDirectory) '轻待办-一键安装.exe' }
$buildFolder = Join-Path $WorkDirectory ([guid]::NewGuid().ToString('N'))
$payloadFolder = Join-Path $buildFolder 'payload'
New-Item -ItemType Directory -Force -Path $payloadFolder | Out-Null
foreach ($fileName in @('轻待办.exe','轻待办-便签.ico','使用说明.md','验证记录.md')) {
    Copy-Item -LiteralPath (Join-Path $AppDirectory $fileName) -Destination $payloadFolder
}
Copy-Item -LiteralPath (Join-Path $AppDirectory '源码') -Destination $payloadFolder -Recurse
$payloadZip = Join-Path $buildFolder 'payload.zip'
Compress-Archive -Path (Join-Path $payloadFolder '*') -DestinationPath $payloadZip
$frameworkFolder = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkFolder 'csc.exe'))) { $frameworkFolder = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$compiler = Join-Path $frameworkFolder 'csc.exe'
$references = @('/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll')
$sources = @((Join-Path $PSScriptRoot 'PackageEngine.cs'),(Join-Path $PSScriptRoot 'SetupIntegration.cs'),(Join-Path $PSScriptRoot 'InstallerForm.cs'),(Join-Path $AppDirectory '源码\Core.cs'))
$buildArguments = @('/nologo','/target:winexe','/utf8output','/codepage:65001','/optimize+',('/out:' + $OutputPath),('/win32icon:' + (Join-Path $AppDirectory '轻待办-便签.ico')),('/win32manifest:' + (Join-Path $PSScriptRoot 'setup.manifest')),('/resource:' + $payloadZip + ',QingTodo.Payload.zip')) + $references + $sources
& $compiler @buildArguments
if ($LASTEXITCODE -ne 0) { throw '安装器编译失败' }
if ($Check) {
    $testExecutable = Join-Path $buildFolder 'SetupTests.exe'
    $testArguments = @('/nologo','/target:exe','/utf8output','/codepage:65001',('/out:' + $testExecutable),'/main:QingTodo.Setup.Tests',('/resource:' + $payloadZip + ',QingTodo.Payload.zip')) + $references + @($sources[0],$sources[1],$sources[3],(Join-Path $PSScriptRoot 'SetupTests.cs'))
    & $compiler @testArguments
    if ($LASTEXITCODE -ne 0) { throw '安装器检查编译失败' }
    & $testExecutable $buildFolder
    if ($LASTEXITCODE -ne 0) { throw '安装器检查未通过' }
}
Write-Output "已生成一键安装文件：$OutputPath"
