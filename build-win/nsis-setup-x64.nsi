Unicode True
RequestExecutionLevel admin

!define APP_NAME "hapet"
!define COMP_NAME "hapet-lang"
!define WEB_SITE "https://hapetlang.com"
!ifndef VERSION
!define VERSION "0.0.0"
!endif
!define COPYRIGHT "[crackanddie, 2026]"
!define DESCRIPTION "Compiler for hapet programming language"
!define INSTALLER_NAME "hapet_x64.exe"
!define MAIN_APP_EXE "hapet.exe"
!define ICON "../build-resources/logo.ico"
!define BANNER "../build-resources/banner.bmp"
#!define LICENSE_TXT "[CHANGEME License Text Document]"

!define INSTALL_DIR "$PROGRAMFILES64\${APP_NAME}"
!define INSTALL_TYPE "SetShellVarContext all"
!define REG_ROOT "HKLM"
!define REG_APP_PATH "Software\Microsoft\Windows\CurrentVersion\App Paths\${MAIN_APP_EXE}"
!define UNINSTALL_PATH "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define REG_START_MENU "Start Menu Folder"

!addplugindir "./envar/Plugins"

var SM_Folder
Var AddToPathCheck

######################################################################

VIProductVersion  "${VERSION}"
VIAddVersionKey "ProductName"  "${APP_NAME}"
VIAddVersionKey "CompanyName"  "${COMP_NAME}"
VIAddVersionKey "LegalCopyright"  "${COPYRIGHT}"
VIAddVersionKey "FileDescription"  "${DESCRIPTION}"
VIAddVersionKey "FileVersion"  "${VERSION}"

######################################################################

SetCompressor /SOLID Lzma
Name "${APP_NAME}"
Caption "${APP_NAME}"
OutFile "${INSTALLER_NAME}"
BrandingText "${APP_NAME}"
InstallDirRegKey "${REG_ROOT}" "${REG_APP_PATH}" ""
InstallDir "${INSTALL_DIR}"

######################################################################

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${BANNER}"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${BANNER}"

######################################################################

!include "MUI2.nsh"
!include "Sections.nsh"

!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!insertmacro MUI_PAGE_WELCOME

!ifdef LICENSE_TXT
!insertmacro MUI_PAGE_LICENSE "${LICENSE_TXT}"
!endif

!insertmacro MUI_PAGE_DIRECTORY

!ifdef REG_START_MENU
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "${APP_NAME}"
!define MUI_STARTMENUPAGE_REGISTRY_ROOT "${REG_ROOT}"
!define MUI_STARTMENUPAGE_REGISTRY_KEY "${UNINSTALL_PATH}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "${REG_START_MENU}"
!insertmacro MUI_PAGE_STARTMENU Application $SM_Folder
!endif

!insertmacro MUI_PAGE_COMPONENTS

!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM

!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

######################################################################

Section "Main Program" SecMain
	SectionIn RO
	${INSTALL_TYPE}

	SetOverwrite ifnewer
	SetOutPath "$INSTDIR"
    SetDetailsPrint none
	File /r "staging_folder\\"
    SetDetailsPrint both

    ExecWait 'icacls "$INSTDIR" /grant *S-1-1-0:(OI)(CI)F /T'
SectionEnd

Section "Add to PATH" SecPath
    EnVar::SetTooltip "Adds the installation directory to the system PATH variable"
    EnVar::AddValue "Path" "$INSTDIR"
    Pop $0 
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "Core files for Hapet compiler."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecPath} "Add ${APP_NAME} to your system environment variables (PATH) to use it from CMD/PowerShell."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

######################################################################

Section -Icons_Reg
    SetOutPath "$INSTDIR"
    WriteUninstaller "$INSTDIR\uninstall.exe"

    !ifdef REG_START_MENU
    !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
    CreateDirectory "$SMPROGRAMS\$SM_Folder"
    CreateShortCut "$SMPROGRAMS\$SM_Folder\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
    CreateShortCut "$SMPROGRAMS\$SM_Folder\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

    !ifdef WEB_SITE
    WriteIniStr "$INSTDIR\${APP_NAME} website.url" "InternetShortcut" "URL" "${WEB_SITE}"
    CreateShortCut "$SMPROGRAMS\$SM_Folder\${APP_NAME} Website.lnk" "$INSTDIR\${APP_NAME} website.url"
    !endif
    !insertmacro MUI_STARTMENU_WRITE_END
    !endif

    !ifndef REG_START_MENU
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
    CreateShortCut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

    !ifdef WEB_SITE
    WriteIniStr "$INSTDIR\${APP_NAME} website.url" "InternetShortcut" "URL" "${WEB_SITE}"
    CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME} Website.lnk" "$INSTDIR\${APP_NAME} website.url"
    !endif
    !endif

    WriteRegStr ${REG_ROOT} "${REG_APP_PATH}" "" "$INSTDIR\${MAIN_APP_EXE}"
    WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "DisplayName" "${APP_NAME}"
    WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "UninstallString" "$INSTDIR\uninstall.exe"
    WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "DisplayIcon" "$INSTDIR\${MAIN_APP_EXE}"
    WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "DisplayVersion" "${VERSION}"
    WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "Publisher" "${COMP_NAME}"

    !ifdef WEB_SITE
    WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "URLInfoAbout" "${WEB_SITE}"
    !endif
SectionEnd

######################################################################

Section Uninstall
    ${INSTALL_TYPE}

    EnVar::DeleteValue "Path" "$INSTDIR"
    Pop $0

    RmDir /r "$INSTDIR"

    !ifdef REG_START_MENU
    !insertmacro MUI_STARTMENU_GETFOLDER "Application" $SM_Folder
    Delete "$SMPROGRAMS\$SM_Folder\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\$SM_Folder\Uninstall ${APP_NAME}.lnk"
    !ifdef WEB_SITE
    Delete "$SMPROGRAMS\$SM_Folder\${APP_NAME} Website.lnk"
    !endif
    Delete "$DESKTOP\${APP_NAME}.lnk"

    RmDir "$SMPROGRAMS\$SM_Folder"
    !endif

    !ifndef REG_START_MENU
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
    !ifdef WEB_SITE
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME} Website.lnk"
    !endif
    Delete "$DESKTOP\${APP_NAME}.lnk"

    RmDir "$SMPROGRAMS\${APP_NAME}"
    !endif

    DeleteRegKey ${REG_ROOT} "${REG_APP_PATH}"
    DeleteRegKey ${REG_ROOT} "${UNINSTALL_PATH}"
SectionEnd