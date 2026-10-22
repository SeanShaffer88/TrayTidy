param([string]$IsccPath)
$ErrorActionPreference = 'Stop'
$version = '0.3.0'
& "$PSScriptRoot\build.ps1"
& "$PSScriptRoot\test.ps1"
$dist = Join-Path $PSScriptRoot 'dist'
$buildRoot = Join-Path $PSScriptRoot ('work\package-build-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $buildRoot 'installed'
$portable = Join-Path $buildRoot 'portable\TrayTidy'
New-Item -ItemType Directory -Force $dist,$stage,$portable | Out-Null
$payload = @('TrayTidy.exe','TrayTidy.exe.config','README.md','LICENSE','THIRD-PARTY-NOTICES.md')
foreach ($file in $payload) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $portable -Force
}
foreach ($folder in @($stage,$portable)) {
    New-Item -ItemType Directory -Force (Join-Path $folder 'docs') | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\screenshot.png') -Destination (Join-Path $folder 'docs')
}
Set-Content -LiteralPath (Join-Path $portable 'portable.flag') -Value 'TrayTidy portable mode: settings are stored in data\settings.json.'
Compress-Archive -Path $portable -DestinationPath (Join-Path $dist "TrayTidy-$version-Portable.zip") -Force
if (-not $IsccPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
    else { $IsccPath = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
}
if (-not (Test-Path -LiteralPath $IsccPath)) { throw 'Inno Setup 6 is required. Pass -IsccPath with the path to ISCC.exe.' }
& $IsccPath "/DStageDir=$stage" "/DReleaseDir=$dist" "$PSScriptRoot\installer\TrayTidy.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
$assets = @("TrayTidy-$version-Portable.zip", "TrayTidy-$version-Setup.exe")
$checksums = foreach ($name in $assets) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $dist $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), $checksums, [Text.Encoding]::ASCII)
Write-Host "Release assets are in $dist"
