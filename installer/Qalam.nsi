Unicode true
!include "MUI2.nsh"

!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!ifndef PACKAGE_ZIP
  !error "PACKAGE_ZIP is required"
!endif
!ifndef BOOTSTRAP
  !error "BOOTSTRAP is required"
!endif
!ifndef OUTPUT
  !define OUTPUT "Qalam-Setup-${VERSION}.exe"
!endif
!ifndef ICON
  !define ICON "assets\qalam.ico"
!endif

Name "Qalam"
Caption "Qalam ${VERSION} Setup"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Qalam"
InstallDirRegKey HKCU "Software\Qalam" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 64
SetOverwrite on
BrandingText "Qalam"
Icon "${ICON}"
AutoCloseWindow true
ShowInstDetails nevershow
ShowUninstDetails nevershow

VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Qalam"
VIAddVersionKey /LANG=1033 "FileDescription" "Qalam installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" ""

!define MUI_ABORTWARNING
!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Section "Install"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=Qalam-Windows.zip "${PACKAGE_ZIP}"
  File /oname=setup-bootstrap.ps1 "${BOOTSTRAP}"
  DetailPrint "Installing Qalam..."
  ExecWait '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "$PLUGINSDIR\setup-bootstrap.ps1" -Zip "$PLUGINSDIR\Qalam-Windows.zip"' $0
  IntCmp $0 0 install_ok
    MessageBox MB_ICONSTOP|MB_OK "Qalam could not finish installing. See $LOCALAPPDATA\Qalam\logs\install.log." /SD IDOK
    SetErrorLevel 1
    Abort

  install_ok:
  WriteUninstaller "$INSTDIR\Uninstall Qalam.exe"
  CreateShortCut "$DESKTOP\Qalam.lnk" "$INSTDIR\Qalam.exe"

  WriteRegStr HKCU "Software\Qalam" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "DisplayName" "Qalam"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "Publisher" "Qalam"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "DisplayIcon" "$INSTDIR\Qalam.exe,0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "UninstallString" '"$INSTDIR\Uninstall Qalam.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "QuietUninstallString" '"$INSTDIR\Uninstall Qalam.exe" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam" "NoRepair" 1

  IfSilent install_done
  Exec '"$INSTDIR\Qalam.exe"'
  install_done:
SectionEnd

Section "Uninstall"
  ExecWait '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "$INSTDIR\uninstall.ps1"' $0
  Delete "$DESKTOP\Qalam.lnk"
  Delete "$INSTDIR\Uninstall Qalam.exe"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Qalam"
  DeleteRegKey HKCU "Software\Qalam"
  RMDir /r "$INSTDIR"
SectionEnd
