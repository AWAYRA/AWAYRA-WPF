; Awayra Windows x64 per-user installer (self-contained publish payload).
; Build with: powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif

#ifndef MyAppVersionInfo
  #define MyAppVersionInfo "1.1.1.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#define MyAppName "Awayra"
#define MyAppPublisher "Farzin Alavi"
#define MyAppExeName "Awayra.exe"
#define MyAppUrl "https://github.com/AWAYRA/AWAYRA-WPF"
#define MyAppSupportUrl "https://github.com/AWAYRA/AWAYRA-WPF/issues"

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
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=lowest
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=no
UsePreviousTasks=no
CloseApplications=force
CloseApplicationsFilter=Awayra.exe
RestartApplications=no
RestartIfNeededByRun=no
Uninstallable=yes
CreateUninstallRegKey=yes
UninstallLogMode=new
SetupLogging=yes
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

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Awayra"
Type: filesandordirs; Name: "{userappdata}\Awayra"
Type: files; Name: "{autodesktop}\Awayra.lnk"
Type: files; Name: "{userstartup}\Awayra.lnk"

[Code]
const
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValueName = 'Awayra';

function IconFileExists(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\awayra.ico'));
end;

procedure StopRunningAwayra();
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /T /IM {#MyAppExeName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
end;

procedure DeleteDirectoryOrAbort(const DirectoryPath: String; const Description: String);
begin
  if not DirExists(DirectoryPath) then
    Exit;

  Log('Clean upgrade: deleting ' + Description + ': ' + DirectoryPath);
  if not DelTree(DirectoryPath, True, True, True) then
  begin
    MsgBox(
      'Awayra could not remove old ' + Description + '.' + #13#10 +
      'Close any remaining Awayra process and run the installer again.' + #13#10#13#10 +
      DirectoryPath,
      mbError,
      MB_OK);
    Abort;
  end;
end;

procedure DeleteFileIfPresent(const FilePath: String);
begin
  if FileExists(FilePath) then
  begin
    Log('Clean upgrade: deleting legacy file: ' + FilePath);
    if not DeleteFile(FilePath) then
    begin
      MsgBox('Awayra could not remove a legacy shortcut:' + #13#10 + FilePath, mbError, MB_OK);
      Abort;
    end;
  end;
end;

procedure CleanPreviousInstallation();
begin
  StopRunningAwayra();

  DeleteDirectoryOrAbort(ExpandConstant('{localappdata}\Programs\Awayra'), 'program files');
  DeleteDirectoryOrAbort(ExpandConstant('{localappdata}\Awayra'), 'settings and runtime data');
  DeleteDirectoryOrAbort(ExpandConstant('{userappdata}\Awayra'), 'legacy roaming data');
  DeleteDirectoryOrAbort(ExpandConstant('{group}'), 'Start menu shortcuts');

  DeleteFileIfPresent(ExpandConstant('{autodesktop}\Awayra.lnk'));
  DeleteFileIfPresent(ExpandConstant('{userstartup}\Awayra.lnk'));
  RegDeleteValue(HKCU, RunKeyPath, RunValueName);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    CleanPreviousInstallation();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopRunningAwayra();
    RegDeleteValue(HKCU, RunKeyPath, RunValueName);
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    DelTree(ExpandConstant('{localappdata}\Awayra'), True, True, True);
    DelTree(ExpandConstant('{userappdata}\Awayra'), True, True, True);
  end;
end;