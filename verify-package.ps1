param([string]$DistPath = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$version = '0.3.0'
$checks = 0
function Assert-Package([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
    Write-Host "PASS: $Name"
}
$workspace = Join-Path $PSScriptRoot ('work\package-check-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $workspace 'installed'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{EFDB5094-3138-46A2-BDA0-0F781A05F5BA}_is1'
if (Test-Path $uninstallKey) { throw 'An installed TrayTidy is registered. Run packaging tests in a clean Windows user profile.' }
if (Get-Process TrayTidy -ErrorAction SilentlyContinue) { throw 'Exit TrayTidy before running packaging checks.' }
New-Item -ItemType Directory -Force $workspace | Out-Null
$process = $null
$uninstaller = Join-Path $install 'unins000.exe'
try {
    foreach ($line in Get-Content (Join-Path $DistPath 'SHA256SUMS.txt')) {
        $parts = $line -split '  ',2
        Assert-Package ((Get-FileHash (Join-Path $DistPath $parts[1])).Hash.ToLowerInvariant() -eq $parts[0]) "Checksum: $($parts[1])"
    }
    Expand-Archive -LiteralPath (Join-Path $DistPath "TrayTidy-$version-Portable.zip") -DestinationPath (Join-Path $workspace 'extracted')
    $portable = Join-Path $workspace 'extracted\TrayTidy'
    Assert-Package (Test-Path (Join-Path $portable 'portable.flag')) 'Portable mode marker present'
    Assert-Package ((Get-ChildItem $portable -File).Count -eq 6) 'Portable ZIP contains only runtime files and documentation'
    Assert-Package (-not (Test-Path (Join-Path $portable 'data'))) 'Portable ZIP contains no user settings'
    $process = Start-Process -FilePath (Join-Path $portable 'TrayTidy.exe') -WindowStyle Hidden -PassThru
    Assert-Package ($process.WaitForInputIdle(10000) -and -not $process.HasExited) 'Portable executable starts'
    Stop-Process -Id $process.Id
    $process.WaitForExit()
    $process = $null
    $installer = Join-Path $DistPath "TrayTidy-$version-Setup.exe"
    $setup = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOICONS','/TASKS=""',('/DIR="' + $install + '"'),('/LOG="' + (Join-Path $workspace 'install.log') + '"')) -WindowStyle Hidden -PassThru -Wait
    Assert-Package ($setup.ExitCode -eq 0) 'Per-user silent install completes'
    Assert-Package ((Get-FileHash (Join-Path $install 'TrayTidy.exe')).Hash -eq (Get-FileHash (Join-Path $portable 'TrayTidy.exe')).Hash) 'Installer and portable executable hashes match'
    Assert-Package (-not (Test-Path (Join-Path $install 'portable.flag'))) 'Installer uses installed settings mode'
    Assert-Package (Test-Path $uninstallKey) 'Per-user uninstall registration exists'
    # Keep this disposable smoke launch isolated from any real user configuration.
    Set-Content -LiteralPath (Join-Path $install 'portable.flag') -Value 'Isolated smoke test'
    $process = Start-Process -FilePath (Join-Path $install 'TrayTidy.exe') -WindowStyle Hidden -PassThru
    Assert-Package ($process.WaitForInputIdle(10000) -and -not $process.HasExited) 'Installed executable starts'
    Stop-Process -Id $process.Id
    $process.WaitForExit()
    $process = $null
    Remove-Item -LiteralPath (Join-Path $install 'portable.flag')
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $workspace 'uninstall.log') + '"')) -WindowStyle Hidden -PassThru -Wait
    Assert-Package ($uninstall.ExitCode -eq 0) 'Silent uninstall completes'
    Assert-Package (-not (Test-Path (Join-Path $install 'TrayTidy.exe'))) 'Uninstall removes installed executable'
    Assert-Package (-not (Test-Path $uninstallKey)) 'Uninstall removes its registration'
    Write-Host "$checks packaging checks passed. Logs: $workspace"
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
    if (Test-Path $uninstaller) {
        $cleanup = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
        if ($cleanup.ExitCode -ne 0) { Write-Warning "Test installation cleanup failed. Uninstaller: $uninstaller" }
    }
}
