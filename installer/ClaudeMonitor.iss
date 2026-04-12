#define MyAppName "Claude Monitor"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "Aliaksandr Rubis"
#define MyAppCopyright "Copyright (c) 2026 Aliaksandr Rubis"
#define MyAppExeName "ClaudeMonitor.exe"
#define MyAppURL "https://github.com/sanyastem/ClaudeMonitor"

[Setup]
AppId={{B8F3A2D1-5E7C-4A9B-8D6F-1C2E3F4A5B6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppCopyright={#MyAppCopyright}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=output
OutputBaseFilename=ClaudeMonitor-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\ClaudeMonitor\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
CloseApplications=force
CloseApplicationsFilter=ClaudeMonitor.exe
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (Widget + Statusline)"
Name: "statuslineonly"; Description: "Statusline only (no widget)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "widget"; Description: "Claude Monitor Widget"; Types: full custom; Flags: checkablealone
Name: "statusline"; Description: "Claude Code Statusline Script"; Types: full statuslineonly custom

[Tasks]
Name: "autostart"; Description: "Start widget with Windows"; GroupDescription: "Additional options:"; Components: widget

[InstallDelete]
Type: files; Name: "{app}\ClaudeMonitor.exe"; Components: widget

[Files]
; Widget application
Source: "..\src\ClaudeMonitor\bin\Release\net10.0-windows\win-x64\publish\ClaudeMonitor.exe"; DestDir: "{app}"; Flags: ignoreversion restartreplace uninsrestartdelete; Components: widget
; Statusline script
Source: "statusline.js"; DestDir: "{app}"; Flags: ignoreversion; Components: statusline
; Setup helper
Source: "setup-statusline.js"; DestDir: "{app}"; Flags: ignoreversion; Components: statusline

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Components: widget
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Autostart
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClaudeMonitor"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; Configure statusline after install. Skip silently if node is not on PATH.
Filename: "node"; Parameters: """{app}\setup-statusline.js"" ""{app}"""; StatusMsg: "Configuring Claude Code statusline..."; Flags: runhidden shellexec nowait skipifdoesntexist; Components: statusline; Check: NodeJsInstalled
; Launch after install (interactive)
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; Components: widget
; Launch after silent install (auto-update)
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifdoesntexist runasoriginaluser; Components: widget; Check: WizardSilent

[UninstallRun]
; Clean up autostart registry on uninstall
Filename: "reg"; Parameters: "delete ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ClaudeMonitor /f"; Flags: runhidden; RunOnceId: "RemoveAutostart"

[Code]
function NodeJsInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd', '/c node --version', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function DotNetInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd', '/c dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 10"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  NodeStillMissing: Boolean;
begin
  Result := '';

  // CloseApplicationsFilter already sends WM_CLOSE; no need for a hard taskkill here.

  // Check Node.js (needed for statusline)
  if WizardIsComponentSelected('statusline') and (not NodeJsInstalled()) then
  begin
    if MsgBox('Node.js is required for the statusline but was not found.' + #13#10 +
              'Would you like to install it now via winget?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec('cmd', '/c winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
      // Winget updates PATH only for future shells. Re-check; if node still not on PATH show a clear note.
      NodeStillMissing := not NodeJsInstalled();
      if NodeStillMissing then
        MsgBox('Node.js was installed but is not yet visible to this session. The statusline configuration will be skipped — log out and back in, then re-run the installer to finish setup.', mbInformation, MB_OK);
    end;
  end;

  // Check .NET Desktop Runtime (needed for widget)
  if WizardIsComponentSelected('widget') and (not DotNetInstalled()) then
  begin
    if MsgBox('.NET 10 Desktop Runtime is required for the widget.' + #13#10 +
              'Would you like to install it now via winget?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec('cmd', '/c winget install Microsoft.DotNet.DesktopRuntime.10 --accept-package-agreements --accept-source-agreements', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
      if ResultCode <> 0 then
        MsgBox('.NET Runtime installation may have failed. You can install it manually from https://dotnet.microsoft.com', mbInformation, MB_OK);
    end;
  end;
end;
