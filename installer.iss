; Inno Setup Script for Pad-Avan Force
; Install Inno Setup from: https://jrsoftware.org/isdl.php

[Setup]
AppId={{A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D}}
AppName=Pad-Avan Force
AppVersion=1.3
AppPublisher=Wayne Tech Enterprises
AppPublisherURL=https://github.com/Ghost3379/Pad-Avan
; Install to user's AppData\Local\Programs by default (no admin needed)
; If /ALLUSERS is passed, installs to Program Files (requires admin)
DefaultDirName={code:GetInstallDir}
DefaultGroupName=Pad-Avan Force
OutputDir=installer
OutputBaseFilename=PadAvanForce_x64_v1.3-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Wizard images - custom logo on installation pages
; Large image on left side (164x314 pixels recommended)
WizardImageFile=Pad-Avan Force\Assets\wayne_tech_logo_L.bmp
; Small image on top right (55x55 pixels recommended)
WizardSmallImageFile=Pad-Avan Force\Assets\wayne_tech_logo_S.bmp
LicenseFile=
InfoBeforeFile=
InfoAfterFile=
SetupIconFile=Pad-Avan Force\Assets\Pad-Avan Force logo.ico
UninstallDisplayIcon={app}\Pad-Avan Force.exe
; Install for current user by default (no admin rights needed)
; Use /ALLUSERS command line flag to install for all users (requires admin)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
; Allow updates - if same AppId is detected, it will update instead of installing fresh
AllowNoIcons=yes
DisableProgramGroupPage=no
DisableReadyPage=no
VersionInfoVersion=1.3.0.0
VersionInfoCompany=Wayne Tech Enterprises
VersionInfoDescription=Pad-Avan Force - Macro Pad Configuration Tool
VersionInfoCopyright=Copyright (C) 2025
; Minimize Windows SmartScreen warnings (full removal requires code signing certificate)
; To fully remove warnings, you need to code-sign the installer with a certificate
; Uncomment and configure SignTool below if you have a code signing certificate:
; SignTool=signtool sign /f "path\to\certificate.pfx" /p "password" /t http://timestamp.digicert.com $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "quicklaunchicon"; Description: "Create a quick launch icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Include all files from the published output directory
; NOTE: This ONLY includes the Avalonia app files from dotnet publish
; The FeatherS3 scripts folder and other repository files are NOT included
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Pad-Avan Force"; Filename: "{app}\Pad-Avan Force.exe"
Name: "{group}\Uninstall Pad-Avan Force"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Pad-Avan Force"; Filename: "{app}\Pad-Avan Force.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Pad-Avan Force"; Filename: "{app}\Pad-Avan Force.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\Pad-Avan Force.exe"; Description: "Launch Pad-Avan Force"; Flags: nowait postinstall skipifsilent

[Code]
var
  InstallTypePage: TInputOptionWizardPage;
  InstallForAllUsers: Boolean;

function InitializeSetup(): Boolean;
begin
  Result := True;
  // Check if .NET 8.0 Runtime is installed (optional check)
  // If you publish as self-contained, this check is not needed
  
  // If /ALLUSERS is passed via command line or already running as admin, set flag
  InstallForAllUsers := IsAdminInstallMode or (ExpandConstant('{param:ALLUSERS}') <> '');
end;

procedure InitializeWizard();
begin
  // Create page to let user choose installation type
  InstallTypePage := CreateInputOptionPage(wpWelcome,
    'Installation Type', 'Who should this application be installed for?',
    'Please select whether you wish to install Pad-Avan Force for all users or just yourself.',
    True, False);
  
  InstallTypePage.Add('Install for current user only (no admin rights needed)');
  InstallTypePage.Add('Install for all users (requires administrator rights)');
  
  // If /ALLUSERS flag is set or already running as admin, select "all users"
  if IsAdminInstallMode or (ExpandConstant('{param:ALLUSERS}') <> '') then
  begin
    InstallTypePage.SelectedValueIndex := 1;
    InstallForAllUsers := True;
  end
  else
  begin
    // Default to current user only
    InstallTypePage.SelectedValueIndex := 0;
    InstallForAllUsers := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // If user is on the installation type page and selected "All Users"
  if (CurPageID = InstallTypePage.ID) and (InstallTypePage.SelectedValueIndex = 1) then
  begin
    // User wants to install for all users - need admin rights
    if not IsAdmin then
    begin
      // Not running as admin - restart with elevation
      if MsgBox('Installing for all users requires administrator rights.' + #13#10#13#10 +
                'The installer will now restart and request administrator privileges.' + #13#10#13#10 +
                'Do you want to continue?',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        // Restart installer with /ALLUSERS flag and request elevation via UAC
        // ShellExec('runas') will show the UAC prompt to the user
        if ShellExec('runas', ExpandConstant('{srcexe}'), '/ALLUSERS', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then
        begin
          Result := False; // Cancel this instance
          WizardForm.Close;
        end
        else
        begin
          // If ShellExec failed, show error and go back to selection
          // Error code 1223 = ERROR_CANCELLED (user cancelled UAC prompt)
          if ErrorCode = 1223 then
            MsgBox('Administrator privileges were not granted. The installation will continue for the current user only.' + #13#10#13#10 +
                   'If you want to install for all users, please run the installer as administrator manually.',
                   mbInformation, MB_OK)
          else
            MsgBox('Failed to restart installer with administrator rights. Error code: ' + IntToStr(ErrorCode) + #13#10#13#10 +
                   'Please run the installer as administrator manually, or choose "Install for current user only".',
                   mbError, MB_OK);
          Result := False; // Go back to selection
        end;
      end
      else
      begin
        // User cancelled - go back to selection
        Result := False;
      end;
    end
    else
    begin
      // Already running as admin - set flag
      InstallForAllUsers := True;
    end;
  end
  else if (CurPageID = InstallTypePage.ID) and (InstallTypePage.SelectedValueIndex = 0) then
  begin
    // User selected current user only
    InstallForAllUsers := False;
  end;
end;

function GetInstallDir(Param: String): String;
begin
  // Check if installing for all users (via /ALLUSERS flag or user selection)
  if IsAdminInstallMode or InstallForAllUsers then
    Result := ExpandConstant('{autopf}\Pad-Avan Force')
  else
    Result := ExpandConstant('{localappdata}\Programs\Pad-Avan Force');
end;

