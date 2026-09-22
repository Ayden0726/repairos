; WorkshopOS Windows installer (Inno Setup 6)
; Build via packaging/build-client.ps1 on a Windows machine.

#ifndef MyAppVersion
  #define MyAppVersion "1.2.0"
#endif
#ifndef PublishDir
  #define PublishDir "out\client"
#endif
#ifndef DistDir
  #define DistDir "dist"
#endif

#define MyAppName "WorkshopOS"
#define MyAppPublisher "WorkshopOS"
#define MyAppExeName "WorkshopOS.Client.exe"

[Setup]
AppId={{A8C3E2F1-9B4D-4E6A-8F21-7C0D5B91A3E2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\WorkshopOS
DefaultGroupName=WorkshopOS
DisableProgramGroupPage=yes
OutputDir={#DistDir}
OutputBaseFilename=WorkshopOS-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch WorkshopOS"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  MsgBox('After install, open WorkshopOS and enter your server URL (for example http://shop-pc:5088).', mbInformation, MB_OK);
end;
