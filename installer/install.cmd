@echo off
setlocal
set "TARGET=%LOCALAPPDATA%\Programs\WowRunner"
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"

if not exist "%TARGET%" mkdir "%TARGET%"
robocopy "%~dp0" "%TARGET%" /E /XF appsettings.json /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 exit /b 1
if not exist "%TARGET%\appsettings.json" copy /Y "%~dp0appsettings.json" "%TARGET%\appsettings.json" >nul

if not exist "%START_MENU%" mkdir "%START_MENU%"
if not exist "%USERPROFILE%\Desktop" mkdir "%USERPROFILE%\Desktop"
cscript.exe //nologo "%TARGET%\create-shortcut.vbs" "%TARGET%\WowRunner.exe" "%START_MENU%\WowRunner.lnk" ""
cscript.exe //nologo "%TARGET%\create-shortcut.vbs" "%TARGET%\WowRunner.exe" "%USERPROFILE%\Desktop\WowRunner.lnk" ""

if /I "%WOWRUNNER_INSTALL_NO_START%"=="1" exit /b 0
start "" "%TARGET%\WowRunner.exe"
exit /b 0
