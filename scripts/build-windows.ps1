param([string]$WorkDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) '.build'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$appRoot = Join-Path $repoRoot 'app'
$sourceRoot = Join-Path $appRoot '源码'
$downloadRoot = Join-Path $repoRoot 'site\downloads'
New-Item -ItemType Directory -Force -Path $WorkDirectory,$downloadRoot | Out-Null
& (Join-Path $sourceRoot 'Build.ps1')
& (Join-Path $sourceRoot 'RunTests.ps1') -WorkDirectory (Join-Path $WorkDirectory 'app-tests')
$installer = Join-Path $WorkDirectory 'QingToDo-Setup.exe'
& (Join-Path $sourceRoot 'tools\安装器\BuildInstaller.ps1') -AppDirectory $appRoot -OutputPath $installer -WorkDirectory (Join-Path $WorkDirectory 'setup-build') -Check
Copy-Item -LiteralPath $installer -Destination (Join-Path $downloadRoot 'QingToDo-Setup.exe') -Force
$bundle = Join-Path $WorkDirectory ('bundle-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $bundle | Out-Null
Copy-Item -LiteralPath $installer -Destination (Join-Path $bundle '一键安装.exe')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\安装说明.txt') -Destination (Join-Path $bundle '开始使用.txt')
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath (Join-Path $downloadRoot 'QingToDo-Windows.zip') -Force
$checksums = Get-ChildItem -LiteralPath $downloadRoot -File | Where-Object { $_.Extension -in @('.exe','.zip') } | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name }
[System.IO.File]::WriteAllText((Join-Path $downloadRoot 'SHA256SUMS.txt'),(($checksums -join "`n") + "`n"),(New-Object System.Text.UTF8Encoding $false))
Write-Output 'Application, installer and download bundle built and checked.'
