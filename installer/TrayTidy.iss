#define AppVersion "0.3.0"
#ifndef StageDir
  #define StageDir "..\dist\installed"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\dist"
#endif
[Setup]
AppId={{EFDB5094-3138-46A2-BDA0-0F781A05F5BA}
AppName=TrayTidy
AppVersion={#AppVersion}
AppPublisher=Sean Shaffer
AppPublisherURL=https://github.com/SeanShaffer88/TrayTidy
AppSupportURL=https://github.com/SeanShaffer88/TrayTidy/issues
AppUpdatesURL=https://github.com/SeanShaffer88/TrayTidy/releases
DefaultDirName={localappdata}\Programs\TrayTidy
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
AppMutex=Local\TrayTidy-{username}
SetupIconFile=..\TrayTidy.ico
UninstallDisplayIcon={app}\TrayTidy.exe
OutputDir={#ReleaseDir}
OutputBaseFilename=TrayTidy-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
CloseApplications=no
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=Sean Shaffer
VersionInfoDescription=TrayTidy per-user installer

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
Name: startup; Description: "Start TrayTidy when I sign in"; Flags: unchecked

[Files]
Source: "{#StageDir}\TrayTidy.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\TrayTidy.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\docs\screenshot.png"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\TrayTidy"; Filename: "{app}\TrayTidy.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\TrayTidy"; Filename: "{app}\TrayTidy.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\TrayTidy"; Filename: "{app}\TrayTidy.exe"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\TrayTidy.exe"; Description: "Launch TrayTidy"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('TrayTidy requires Microsoft .NET Framework 4.8 or later. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48 and run Setup again.', mbError, MB_OK);
end;
