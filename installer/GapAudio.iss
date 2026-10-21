; Build with package-installer.ps1. Requires Inno Setup 7.1 or later.
#define AppName "Gap Audio"
#define AppVersion "1.0.0"
#ifndef BinDirectory
  #define BinDirectory "..\bin"
#endif
#ifndef WizardAssets
  #define WizardAssets "..\dist\installer-assets"
#endif

[Setup]
AppId={{F637A88B-49D9-4E5A-A8D8-98DE95A1F671}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} V{#AppVersion}
AppPublisher=Gap Audio
VersionInfoVersion=1.0.0.0
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} V{#AppVersion} Installer
DefaultDirName={commonpf32}\{#AppName}
DefaultGroupName={#AppName}
DisableDirPage=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
UsePreviousAppDir=yes
UsePreviousTasks=no
PrivilegesRequired=admin
; This override is available for isolated verification and custom deployment.
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
WizardStyle=modern dynamic windows11 includetitlebar
WizardSizePercent=115,110
WizardImageFile={#WizardAssets}\wizard-light.png
WizardImageFileDynamicDark={#WizardAssets}\wizard-dark.png
WizardSmallImageFile=..\logo.png
WizardSmallImageFileDynamicDark=..\logo.png
SetupIconFile=..\resources\app.ico
UninstallDisplayIcon={app}\GapAudio.exe
UninstallDisplayName={#AppName} V{#AppVersion}
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=GapAudio-V{#AppVersion}-Windows-x64-Setup
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Messages]
WelcomeLabel1=欢迎安装 Gap Audio V1.0.0
WelcomeLabel2=完全离线的音频编辑、视频音轨提取和批量转换工具。%n%n安装向导将帮助您选择安装目录和桌面快捷方式。%n%n点击“下一步”继续。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："

[Dirs]
; Allow unelevated audio processing in the protected installation directory.
Name: "{app}\temp"; Permissions: users-modify; Flags: uninsalwaysuninstall

[UninstallDelete]
; This dedicated cache directory contains no installed application binaries.
Type: filesandordirs; Name: "{app}\temp"

[Files]
Source: "{#BinDirectory}\GapAudio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\GapAudio.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\ffprobe.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\MEDIA-BUILD.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\build-media.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\build-media.sh"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\FFmpeg-BUILD.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\FFmpeg-config.h"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\FFmpeg-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDirectory}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\GapAudio.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\GapAudio.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\GapAudio.exe"; Description: "启动 Gap Audio"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  { .NET Framework 4.8 release on the oldest supported Windows 10 editions. }
  Result := RegQueryDWordValue(HKLM64,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
  if Result then
    Result := Release >= 528040;
  if not Result then
    MsgBox('Gap Audio 需要 .NET Framework 4.8 或 4.8.1。' + #13#10 +
      '请先安装运行库，然后重新运行安装程序。此安装包不会连接网络或自动下载运行库。',
      mbError, MB_OK);
end;
