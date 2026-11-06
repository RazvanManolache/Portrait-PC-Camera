#define AppVersion "0.3.0"
#include "..\build\filter-metadata.iss"
#define SourceDir "..\dist\app"
#define CameraClsid "{{89084F72-48E3-4CC4-93B5-4F0E8155EA71}"
#define CameraCategory "{{860BB310-5D01-11D0-BD3B-00A0C911CE86}"
#define WindowsCameraClsid "{{ADE8BACE-D898-461E-845D-F7E255527B4F}"

#define WindowsCameraClsid360 "{{F85FD47C-A5D3-467C-B2D4-FAE42D3DD6B1}"

#define WindowsCameraClsid1080 "{{C0D3C4FE-26D3-480B-9A96-F88E26E5D4F0}"

[Setup]
AppId={{60DE2C47-5D1C-47BC-8B76-19F65B744621}
AppName=Portrait Camera
AppVersion={#AppVersion}
AppPublisher=Portrait Camera
DefaultDirName={autopf}\Portrait Camera
UsePreviousAppDir=no
DefaultGroupName=Portrait Camera
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\dist
OutputBaseFilename=PortraitCamera-Setup-{#AppVersion}-win-x64
SetupIconFile=..\assets\portrait.ico
UninstallDisplayIcon={app}\PortraitCamera.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=PortraitCamera.exe,PortraitCamera.MfProbe.exe
RestartApplications=no
AppMutex=Local\PortraitCamera.App.89084f72
UninstallDisplayName=Portrait Camera

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "PortraitCamera.MediaSource.dll,PortraitCamera.Native.dll,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\PortraitCamera.MediaSource.dll"; DestDir: "{app}"; DestName: "{#CameraSourceFile}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\Portrait Camera"; Filename: "{app}\PortraitCamera.exe"

Name: "{commondesktop}\Portrait Camera"; Filename: "{app}\PortraitCamera.exe"

[Registry]
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid}"; ValueType: string; ValueData: "Portrait Camera Windows Source"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid}\InprocServer32"; ValueType: string; ValueData: "{app}\{#CameraSourceFile}"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid360}"; ValueType: string; ValueData: "Portrait Camera Windows Source"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid360}\InprocServer32"; ValueType: string; ValueData: "{app}\{#CameraSourceFile}"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid360}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid1080}"; ValueType: string; ValueData: "Portrait Camera Windows Source"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid1080}\InprocServer32"; ValueType: string; ValueData: "{app}\{#CameraSourceFile}"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#WindowsCameraClsid1080}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#CameraClsid}"; ValueType: string; ValueData: "Portrait Camera (Legacy)"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#CameraClsid}\InprocServer32"; ValueType: string; ValueData: "{app}\PortraitCamera.Native-{#AppVersion}.dll"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#CameraClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "CLSID"; ValueData: "{#CameraClsid}"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "FriendlyName"; ValueData: "Portrait Camera (Legacy)"
Root: HKLM64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: binary; ValueName: "FilterData"; ValueData: "{#CameraFilterData}"
Root: HKLM32; Subkey: "Software\Classes\CLSID\{#CameraClsid}"; ValueType: string; ValueData: "Portrait Camera (Legacy)"; Flags: uninsdeletekey
Root: HKLM32; Subkey: "Software\Classes\CLSID\{#CameraClsid}\InprocServer32"; ValueType: string; ValueData: "{app}\camera-x86\PortraitCamera.Native-{#AppVersion}.dll"
Root: HKLM32; Subkey: "Software\Classes\CLSID\{#CameraClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM32; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "CLSID"; ValueData: "{#CameraClsid}"; Flags: uninsdeletekey
Root: HKLM32; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "FriendlyName"; ValueData: "Portrait Camera (Legacy)"
Root: HKLM32; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: binary; ValueName: "FilterData"; ValueData: "{#CameraFilterData}"

[Run]
Filename: "{app}\PortraitCamera.Registration.exe"; Parameters: "--refresh"; Flags: runhidden waituntilterminated
Filename: "{app}\PortraitCamera.exe"; Parameters: "--configure-startup"; Flags: runasoriginaluser runhidden waituntilterminated
Filename: "{app}\PortraitCamera.exe"; Description: "Open Portrait Camera"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  OldDir: String;
  ExitCode: Integer;
begin
  Result := '';
  { Only migrate the former installer at its fixed, owned location.
    Run it as the original user, never elevate a user-writable executable. }
  OldDir := ExpandConstant('{localappdata}\Programs\Portrait Camera');
  if FileExists(OldDir + '\unins000.exe') then begin
    if FileExists(OldDir + '\PortraitCamera.exe') then
      ExecAsOriginalUser(OldDir + '\PortraitCamera.exe', '--quit', OldDir,
        SW_HIDE, ewWaitUntilTerminated, ExitCode);
    Sleep(1000);
    if not ExecAsOriginalUser(OldDir + '\unins000.exe',
      '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', OldDir,
      SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'Please uninstall the previous Portrait Camera version, then run this installer again.'
    else if ExitCode <> 0 then
      Result := 'The previous Portrait Camera version could not be removed. Please close Portrait Camera and try again.';
  end;
end;



procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Users: TArrayOfString;
  I: Integer;
  RunKey, Command, Expected: String;
begin
  if CurUninstallStep <> usUninstall then Exit;
  { Elevation may use a different account from the camera user. Clean only
    loaded profiles whose startup value points to this exact installation.
    Do not mount offline hives or remove another installation's startup entry. }
  Expected := '"' + ExpandConstant('{app}\PortraitCamera.exe') + '" --background';
  if RegGetSubkeyNames(HKEY_USERS, '', Users) then
    for I := 0 to GetArrayLength(Users) - 1 do begin
      RunKey := Users[I] + '\Software\Microsoft\Windows\CurrentVersion\Run';
      if RegQueryStringValue(HKEY_USERS, RunKey, 'PortraitCamera', Command) then
        if CompareText(Command, Expected) = 0 then
          if not RegDeleteValue(HKEY_USERS, RunKey, 'PortraitCamera') then
            Log('Could not remove Portrait Camera startup entry: ' + RunKey);
    end;
end;
