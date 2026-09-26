#ifndef AppVersion
  #error Run scripts/release.ps1 to supply version and publish paths.
#endif

[Setup]
AppId={{BF170F3C-558B-460D-88EF-B8B82A639A24}
AppName=LimitBar
AppVersion={#AppVersion}
VersionInfoVersion={#BinaryVersion}
AppPublisher=LimitBar contributors
AppPublisherURL=https://github.com/ibneturabhassan/LimitBar
AppSupportURL=https://github.com/ibneturabhassan/LimitBar/issues
AppUpdatesURL=https://github.com/ibneturabhassan/LimitBar/releases
DefaultDirName={localappdata}\Programs\LimitBar
DefaultGroupName=LimitBar
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=LimitBar-{#AppVersion}-win-x64-setup
SetupIconFile=..\LimitBar.Desktop\Assets\LimitBar.ico
UninstallDisplayIcon={app}\LimitBar.Desktop.exe
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\LimitBar.Desktop
CloseApplications=no
RestartApplications=no

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LimitBar"; Filename: "{app}\LimitBar.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\LimitBar"; Filename: "{app}\LimitBar.Desktop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LimitBar.Desktop.exe"; Description: "Launch LimitBar"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LimitBar', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\LimitBar.Desktop.exe') + '" --background') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LimitBar');
end;
