[Setup]
AppId={{E00E09A7-86E8-4DA4-A1B7-91D38A8577A5}
AppName=Prompt Clipboard
AppVersion={#AppVersion}
AppPublisher=PromptClipboard
AppPublisherURL=https://github.com/gagharutyunyan1993/PromptClipboard
DefaultDirName={localappdata}\Programs\PromptClipboard
DefaultGroupName=Prompt Clipboard
UninstallDisplayIcon={app}\PromptClipboard.App.exe
OutputDir=..\artifacts
OutputBaseFilename=PromptClipboard-{#AppVersion}-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\PromptClipboard.App\Resources\app.ico
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=none
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Prompt Clipboard"; Filename: "{app}\PromptClipboard.App.exe"
Name: "{autodesktop}\Prompt Clipboard"; Filename: "{app}\PromptClipboard.App.exe"; Tasks: desktopicon
Name: "{userstartup}\Prompt Clipboard"; Filename: "{app}\PromptClipboard.App.exe"; Tasks: autostart

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "Startup:"

[Run]
Filename: "{app}\PromptClipboard.App.exe"; Description: "Launch Prompt Clipboard"; Flags: nowait postinstall skipifsilent
