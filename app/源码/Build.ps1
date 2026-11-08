param(
    [string]$SourceDirectory = $PSScriptRoot,
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) '轻待办.exe'),
    [string]$IconPath = (Join-Path $PSScriptRoot 'app.ico')
)
$ErrorActionPreference = 'Stop'
$frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkDirectory 'csc.exe'))) {
    $frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compilerPath = Join-Path $frameworkDirectory 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '需要 Windows 自带的 .NET Framework 4.x。' }
$references = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output','/codepage:65001',('/out:' + $OutputPath),('/win32icon:' + $IconPath),('/win32manifest:' + (Join-Path $SourceDirectory 'app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $frameworkDirectory $reference) }
foreach ($reference in @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')) { $arguments += '/reference:' + (Join-Path $frameworkDirectory ('WPF\' + $reference)) }
$arguments += Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw "编译失败：$LASTEXITCODE" }
Write-Output "已生成：$OutputPath"
