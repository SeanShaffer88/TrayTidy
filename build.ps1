$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:winexe /optimize+ /out:"$PSScriptRoot\TrayTidy.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\TrayTidy.cs" "$PSScriptRoot\Interface.cs" /win32icon:"$PSScriptRoot\TrayTidy.ico" "/resource:$PSScriptRoot\TrayTidy.ico,TrayTidy.ico" "/resource:$PSScriptRoot\TrayTidy-icon.png,TrayTidy.png"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
