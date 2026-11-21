[Setup]
AppName=PulseBoost
AppVersion=1.1.0
AppPublisher=PulseBoost Open Source Contributors
DefaultDirName={autopf}\PulseBoost
DefaultGroupName=PulseBoost
UninstallDisplayIcon={app}\PulseBoost.exe
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=PulseBoost-Setup-v1.1.0
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "PulseBoost\bin\Release\net8.0-windows\win-x64\publish\PulseBoost.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\PulseBoost"; Filename: "{app}\PulseBoost.exe"
Name: "{autodesktop}\PulseBoost"; Filename: "{app}\PulseBoost.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PulseBoost.exe"; Description: "{cm:LaunchProgram,PulseBoost}"; Flags: nowait postinstall skipifsilent
