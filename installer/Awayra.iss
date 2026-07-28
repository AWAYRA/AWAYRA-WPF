; Awayra Windows x64 per-user installer (self-contained publish payload).
; Build with: powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

#ifndef MyAppVersion
  #define MyAppVersion "1.0.2"
#endif

#ifndef MyAppVersionInfo
  #define MyAppVersionInfo "1.0.2.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#define MyAppName "Awayra"
#define MyAppPublisher "Farzin Alavi"
#define MyAppExeName "Awayra.exe"
#define MyAppUrl "https://github.com/mtalavi/Awayra"
#define MyAppSupportUrl "https://github.com/mtalavi/Awayra/issues"

[Setup]
AppId={{C348E9A2-7E31-4E8D-A638-94A635B813C1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppSupportUrl}
AppUpdatesURL={#MyAppUrl}
DefaultDirName={localappdata}\Programs\Awayra
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
AllowNoIcons=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=Awayra-Setup-{#MyAppVersion}-x64
SetupIconFile=..\src\Awayra.App\Assets\awayra.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
RestartIfNeededByRun=no
Uninstallable=yes
CreateUninstallRegKey=yes
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Awayra Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersionInfo}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\awayra.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\awayra.ico"; Check: IconFileExists()
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\awayra.ico"; Check: IconFileExists()

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IconFileExists(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\awayra.ico'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { Preserve user data under %LocalAppData%\Awayra on uninstall. }
  end;
end;
