@echo off
setlocal

set "ROOT=%~dp0"
set "OUTPUT=%ROOT%artifacts\Alacrity-Launcher"
set "PRESERVED_DATA=%TEMP%\AlacrityLauncherData-%RANDOM%%RANDOM%"

if exist "%OUTPUT%\data\versions.json" (
  if not exist "%PRESERVED_DATA%" mkdir "%PRESERVED_DATA%"
  copy /y "%OUTPUT%\data\versions.json" "%PRESERVED_DATA%\versions.json" >nul
)
if exist "%OUTPUT%\data\launcher-settings.json" (
  if not exist "%PRESERVED_DATA%" mkdir "%PRESERVED_DATA%"
  copy /y "%OUTPUT%\data\launcher-settings.json" "%PRESERVED_DATA%\launcher-settings.json" >nul
)
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"
if exist "%OUTPUT%" goto :publish_failed

dotnet publish "%ROOT%src\Alacrity.Launcher\Alacrity.Launcher.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "%OUTPUT%"
if errorlevel 1 goto :publish_failed

if exist "%PRESERVED_DATA%" (
  if exist "%PRESERVED_DATA%\versions.json" copy /y "%PRESERVED_DATA%\versions.json" "%OUTPUT%\data\versions.json" >nul
  if exist "%PRESERVED_DATA%\launcher-settings.json" copy /y "%PRESERVED_DATA%\launcher-settings.json" "%OUTPUT%\data\launcher-settings.json" >nul
  rmdir /s /q "%PRESERVED_DATA%"
)

echo.
echo Alacrity Launcher published to:
echo %OUTPUT%
exit /b 0

:publish_failed
set "ERRORLEVEL_TO_RETURN=%ERRORLEVEL%"
if "%ERRORLEVEL_TO_RETURN%"=="0" set "ERRORLEVEL_TO_RETURN=1"
if exist "%PRESERVED_DATA%" (
  if not exist "%OUTPUT%" mkdir "%OUTPUT%"
  if not exist "%OUTPUT%\data" mkdir "%OUTPUT%\data"
  if exist "%PRESERVED_DATA%\versions.json" copy /y "%PRESERVED_DATA%\versions.json" "%OUTPUT%\data\versions.json" >nul
  if exist "%PRESERVED_DATA%\launcher-settings.json" copy /y "%PRESERVED_DATA%\launcher-settings.json" "%OUTPUT%\data\launcher-settings.json" >nul
  rmdir /s /q "%PRESERVED_DATA%"
)
exit /b %ERRORLEVEL_TO_RETURN%
