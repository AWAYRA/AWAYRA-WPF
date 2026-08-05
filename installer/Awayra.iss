; Awayra Windows x64 per-user installer (self-contained publish payload).
; Build with: powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

; Both are normally supplied by scripts/build-installer.ps1, which reads them from the published
; executable. The fallbacks only apply when ISCC is invoked by hand and must track
; Directory.Build.props; CI fails the build if they drift.
#ifndef MyAppVersion
  #define MyAppVersion "1.3.0"
#endif

#ifndef MyAppVersionInfo
  #define MyAppVersionInfo "1.3.0.0"
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
; AllowNoIcons is deliberately off. With it on, /NOICONS makes {group} expand to the Start Menu
; Programs folder itself, and the upgrade cleanup below would then recurse through every shortcut
; the user owns instead of Awayra's own folder. The group page is hidden anyway, so the option was
; unreachable in the wizard and only ever reachable from a command line.
AllowNoIcons=no
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

; No [Tasks] section on purpose. Inno renders the Select Additional Tasks page with a
; TNewCheckListBox, whose owner-drawn check and radio glyphs are clipped at fractional display
; scaling such as 150%. Every option Awayra needs lives on one custom page built from native
; TNewRadioButton and TNewCheckBox controls, which Windows itself renders correctly at any DPI.

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\awayra.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\awayra.ico"; Check: IconFileExists()
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\awayra.ico"; Check: WantsDesktopIcon() and IconFileExists()

; Deliberately no postinstall flag. That flag puts the entry on the Finished page inside Inno's
; RunList, which is another TNewCheckListBox and clips its check glyph at fractional display
; scaling exactly like the Tasks page did. The choice is offered on the options page as a native
; check box instead, and Awayra starts as installation finishes.
[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifsilent; Check: WantsLaunch()

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
  OptionsPage: TWizardPage;
  KeepDataRadio: TNewRadioButton;
  ResetDataRadio: TNewRadioButton;
  DesktopIconCheck: TNewCheckBox;
  LaunchCheck: TNewCheckBox;
  ForceCleanData: Boolean;
  ForceDesktopIcon: Boolean;
  RemoveDataOnUninstall: Boolean;
  ForceCleanDataOnUninstall: Boolean;

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

{ Every control below is positioned through ScaleX/ScaleY. Controls created from Pascal Script are
  not scaled automatically, and unscaled coordinates are what makes glyphs and captions collide at
  125% or 150% display scaling. }

function AddHeading(APage: TWizardPage; const ACaption: String; ATop: Integer): Integer;
var
  Heading: TNewStaticText;
begin
  Heading := TNewStaticText.Create(APage);
  Heading.Parent := APage.Surface;
  Heading.Left := 0;
  Heading.Top := ATop;
  Heading.AutoSize := True;
  Heading.Font.Style := [fsBold];
  Heading.Caption := ACaption;
  Result := ATop + Heading.Height + ScaleY(6);
end;

function AddParagraph(APage: TWizardPage; const ACaption: String; ATop: Integer): Integer;
var
  Paragraph: TNewStaticText;
begin
  Paragraph := TNewStaticText.Create(APage);
  Paragraph.Parent := APage.Surface;
  Paragraph.Left := 0;
  Paragraph.Top := ATop;
  Paragraph.Width := APage.SurfaceWidth;
  Paragraph.WordWrap := True;
  Paragraph.AutoSize := True;
  Paragraph.Caption := ACaption;
  Result := ATop + Paragraph.Height + ScaleY(10);
end;

function AddRadio(APage: TWizardPage; const ACaption: String; ATop: Integer): TNewRadioButton;
begin
  Result := TNewRadioButton.Create(APage);
  Result.Parent := APage.Surface;
  Result.Left := 0;
  Result.Top := ATop;
  Result.Width := APage.SurfaceWidth;
  Result.Height := ScaleY(20);
  Result.Caption := ACaption;
end;

function AddCheck(APage: TWizardPage; const ACaption: String; ATop: Integer; AChecked: Boolean): TNewCheckBox;
begin
  Result := TNewCheckBox.Create(APage);
  Result.Parent := APage.Surface;
  Result.Left := 0;
  Result.Top := ATop;
  Result.Width := APage.SurfaceWidth;
  Result.Height := ScaleY(20);
  Result.Caption := ACaption;
  Result.Checked := AChecked;
end;

procedure InitializeWizard();
var
  Y: Integer;
begin
  OptionsPage := CreateCustomPage(
    wpLicense,
    'Setup options',
    'Choose how Awayra should handle your data and your shortcuts.');

  Y := 0;

  { The data question is only meaningful when there is something to lose. }
  if PreviousDataExists() then
  begin
    Y := AddHeading(OptionsPage, 'Your existing Awayra data', Y);
    Y := AddParagraph(
      OptionsPage,
      'Awayra always replaces its program files. Your settings, statistics and reminder schedule are yours to keep.',
      Y);

    KeepDataRadio := AddRadio(OptionsPage, 'Keep my settings, statistics and reminder schedule (recommended)', Y);
    KeepDataRadio.Checked := True;
    Y := Y + KeepDataRadio.Height + ScaleY(2);

    ResetDataRadio := AddRadio(OptionsPage, 'Delete my existing data and install a completely fresh copy', Y);
    Y := Y + ResetDataRadio.Height + ScaleY(22);
  end;

  Y := AddHeading(OptionsPage, 'Shortcuts and startup', Y);
  DesktopIconCheck := AddCheck(OptionsPage, 'Create a desktop shortcut', Y, False);
  Y := Y + DesktopIconCheck.Height + ScaleY(2);
  LaunchCheck := AddCheck(OptionsPage, 'Start Awayra when Setup finishes', Y, True);
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
    Result := (ResetDataRadio <> nil) and ResetDataRadio.Checked;
end;

function WantsDesktopIcon(): Boolean;
begin
  if ForceDesktopIcon then
    Result := True
  else if WizardSilent() then
    Result := False
  else
    Result := (DesktopIconCheck <> nil) and DesktopIconCheck.Checked;
end;

function WantsLaunch(): Boolean;
begin
  if WizardSilent() then
    Result := False
  else
    Result := (LaunchCheck <> nil) and LaunchCheck.Checked;
end;

procedure RemoveUserData();
begin
  DeleteDirectoryOrAbort(ExpandConstant('{localappdata}\Awayra'), 'settings and runtime data');
  DeleteDirectoryOrAbort(ExpandConstant('{userappdata}\Awayra'), 'legacy roaming data');
  RegDeleteValue(HKCU, RunKeyPath, RunValueName);
end;

// Removes Awayra's own Start Menu folder, never the Start Menu itself. The group constant collapses
// to the Programs folder whenever no program group was chosen, and deleting that recursively would
// take every other application's shortcuts with it. AllowNoIcons is off so this should be
// unreachable; the guard stays because the cost of being wrong is the user's entire Start Menu.
procedure RemoveStartMenuGroup();
var
  GroupPath: String;
begin
  GroupPath := RemoveBackslash(ExpandConstant('{group}'));

  if (CompareText(GroupPath, RemoveBackslash(ExpandConstant('{userprograms}'))) = 0) or
     (CompareText(GroupPath, RemoveBackslash(ExpandConstant('{commonprograms}'))) = 0) then
  begin
    Log('Start menu group resolved to the Programs root; removing only Awayra shortcuts.');
    DeleteFileIfPresent(AddBackslash(GroupPath) + 'Awayra.lnk');
    DeleteFileIfPresent(AddBackslash(GroupPath) + ExpandConstant('{cm:UninstallProgram,Awayra}') + '.lnk');
    Exit;
  end;

  DeleteDirectoryOrAbort(GroupPath, 'Start menu shortcuts');
end;

procedure CleanPreviousInstallation();
begin
  StopRunningAwayra();

  { Program files and shortcuts are always replaced so a new build never runs against stale binaries. }
  DeleteDirectoryOrAbort(ExpandConstant('{localappdata}\Programs\Awayra'), 'program files');
  RemoveStartMenuGroup();
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
  ForceDesktopIcon := CompareText(ExpandConstant('{param:desktopicon|no}'), 'yes') = 0;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    CleanPreviousInstallation();
end;

function InitializeUninstall(): Boolean;
begin
  ForceCleanDataOnUninstall := CompareText(ExpandConstant('{param:cleandata|no}'), 'yes') = 0;

  { A silent uninstall preserves personal data, matching the silent install. Package managers and
    management tools always uninstall silently, so the old unconditional wipe destroyed settings and
    statistics during routine maintenance. Pass /CLEANDATA=yes to remove them on purpose. }
  if UninstallSilent() then
    RemoveDataOnUninstall := ForceCleanDataOnUninstall
  else if ForceCleanDataOnUninstall then
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