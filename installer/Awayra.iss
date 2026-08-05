; Awayra Windows x64 per-user installer (self-contained publish payload).
; Build with: powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

#ifndef MyAppVersion
  #define MyAppVersion "1.2.0"
#endif

#ifndef MyAppVersionInfo
  #define MyAppVersionInfo "1.2.0.0"
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

; Personal data is intentionally NOT listed here. Whether settings, statistics and logs are
; removed is decided at uninstall time in CurUninstallStepChanged so the user can keep them.
[UninstallDelete]
Type: files; Name: "{autodesktop}\Awayra.lnk"
Type: files; Name: "{userstartup}\Awayra.lnk"

[Code]
const
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValueName = 'Awayra';

var
  DataChoicePage: TInputOptionWizardPage;
  ForceCleanData: Boolean;
  RemoveDataOnUninstall: Boolean;

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

function PreviousDataExists(): Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{localappdata}\Awayra')) or
    DirExists(ExpandConstant('{userappdata}\Awayra'));
end;

procedure InitializeWizard();
begin
  DataChoicePage := CreateInputOptionPage(
    wpSelectTasks,
    'Your existing Awayra data',
    'Awayra found settings and statistics from a previous installation.',
    'Awayra always replaces its program files. Choose what should happen to your personal data.',
    True,
    False);
  DataChoicePage.Add('Keep my settings, statistics and reminder schedule (recommended)');
  DataChoicePage.Add('Delete my existing data and install a completely fresh copy');
  DataChoicePage.SelectedValueIndex := 0;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (DataChoicePage <> nil) and (PageID = DataChoicePage.ID) then
    Result := not PreviousDataExists();
end;

{ Interactive installs follow the wizard choice. Silent installs preserve data unless the caller
  explicitly passes /CLEANDATA=yes, so unattended upgrades can never destroy user data by accident. }
function ShouldResetUserData(): Boolean;
begin
  if ForceCleanData then
    Result := True
  else if not PreviousDataExists() then
    Result := False
  else if WizardSilent() then
    Result := False
  else
    Result := DataChoicePage.SelectedValueIndex = 1;
end;

procedure RemoveUserData();
begin
  DeleteDirectoryOrAbort(ExpandConstant('{localappdata}\Awayra'), 'settings and runtime data');
  DeleteDirectoryOrAbort(ExpandConstant('{userappdata}\Awayra'), 'legacy roaming data');
  RegDeleteValue(HKCU, RunKeyPath, RunValueName);
end;

procedure CleanPreviousInstallation();
begin
  StopRunningAwayra();

  { Program files and shortcuts are always replaced so a new build never runs against stale binaries. }
  DeleteDirectoryOrAbort(ExpandConstant('{localappdata}\Programs\Awayra'), 'program files');
  DeleteDirectoryOrAbort(ExpandConstant('{group}'), 'Start menu shortcuts');
  DeleteFileIfPresent(ExpandConstant('{autodesktop}\Awayra.lnk'));
  DeleteFileIfPresent(ExpandConstant('{userstartup}\Awayra.lnk'));

  if ShouldResetUserData() then
  begin
    Log('Fresh install requested: removing existing Awayra settings, statistics and logs.');
    RemoveUserData();
  end
  else
    Log('Upgrade: existing Awayra settings, statistics and reminder schedule are preserved.');
end;

function InitializeSetup(): Boolean;
begin
  ForceCleanData := CompareText(ExpandConstant('{param:cleandata|no}'), 'yes') = 0;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    CleanPreviousInstallation();
end;

function InitializeUninstall(): Boolean;
begin
  { Silent uninstall keeps the historical behaviour of a complete removal so automation stays
    deterministic. An interactive uninstall asks, and defaults to keeping personal data. }
  if UninstallSilent() then
    RemoveDataOnUninstall := True
  else if not PreviousDataExists() then
    RemoveDataOnUninstall := False
  else
    RemoveDataOnUninstall :=
      MsgBox(
        'Do you also want to delete your Awayra settings, statistics and logs?' + #13#10#13#10 +
        'Choose No to keep them, so they are restored if you install Awayra again.',
        mbConfirmation,
        MB_YESNO or MB_DEFBUTTON2) = IDYES;

  Result := True;
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
    if RemoveDataOnUninstall then
    begin
      Log('Uninstall: removing Awayra settings, statistics and logs.');
      DelTree(ExpandConstant('{localappdata}\Awayra'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\Awayra'), True, True, True);
    end
    else
      Log('Uninstall: Awayra settings, statistics and logs were kept at the user''s request.');
  end;
end;