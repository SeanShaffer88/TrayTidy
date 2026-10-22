$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$generator = Join-Path $env:TEMP ('TrayTidy-IconBuild-' + [guid]::NewGuid() + '.exe')
try {
    & $compiler /nologo /reference:System.Drawing.dll /out:$generator "$PSScriptRoot\IconBuild.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Icon generator build failed' }
    & $generator $PSScriptRoot
    if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed' }
} finally { Remove-Item -LiteralPath $generator -ErrorAction SilentlyContinue }
