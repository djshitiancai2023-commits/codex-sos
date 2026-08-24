#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef SourceDir
  #error SourceDir must be provided by scripts/build-release.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be provided by scripts/build-release.ps1
#endif

[Setup]
AppId={{4DD43852-FA7A-43A8-A4BC-2A68B24E1F06}
AppName=Codex SOS
AppVersion={#MyAppVersion}
AppPublisher=Codex SOS community contributors
AppPublisherURL=https://github.com/djshitiancai2023-commits/codex-sos
AppSupportURL=https://github.com/djshitiancai2023-commits/codex-sos/issues
AppUpdatesURL=https://github.com/djshitiancai2023-commits/codex-sos/releases
DefaultDirName={localappdata}\Programs\Codex SOS
DefaultGroupName=Codex SOS
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Codex-SOS-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\codex-sos.ico
InfoBeforeFile=privacy-notice.txt
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\CodexSOS.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany=Codex SOS community contributors
VersionInfoDescription=Codex SOS local per-user installer
VersionInfoProductName=Codex SOS

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "在桌面创建快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Codex SOS"; Filename: "{app}\CodexSOS.exe"; WorkingDir: "{app}"; IconFilename: "{app}\CodexSOS.exe"
Name: "{autodesktop}\Codex SOS"; Filename: "{app}\CodexSOS.exe"; WorkingDir: "{app}"; IconFilename: "{app}\CodexSOS.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CodexSOS.exe"; Description: "打开 Codex SOS"; Flags: nowait postinstall skipifsilent
