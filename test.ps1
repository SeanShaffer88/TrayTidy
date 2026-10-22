$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testExe = Join-Path $env:TEMP ('TrayTidy-tests-' + [guid]::NewGuid() + '.exe')
try {
    & $compiler /nologo /target:exe /main:Tests /out:$testExe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\TrayTidy.cs" "$PSScriptRoot\Interface.cs" /win32icon:"$PSScriptRoot\TrayTidy.ico" "/resource:$PSScriptRoot\TrayTidy.ico,TrayTidy.ico" "/resource:$PSScriptRoot\TrayTidy-icon.png,TrayTidy.png" "$PSScriptRoot\Tests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Remove-Item -LiteralPath $testExe -ErrorAction SilentlyContinue }
