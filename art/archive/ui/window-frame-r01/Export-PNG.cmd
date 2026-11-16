@echo off
setlocal
set "CHRONO_ASEPRITE=D:\02_Games\01_Steam\Library\steamapps\common\Aseprite\Aseprite.exe"
if not exist "%CHRONO_ASEPRITE%" set "CHRONO_ASEPRITE=D:\02_Games\01_Steam\SteamClient\steamapps\common\Aseprite\Aseprite.exe"
if not exist "%CHRONO_ASEPRITE%" (
  echo Aseprite.exe was not found. Update CHRONO_ASEPRITE in this file.
  pause
  exit /b 1
)
"%CHRONO_ASEPRITE%" --batch --script-param "input=%~dp0window-frame.aseprite" --script-param "output=%~dp0." --script "%~dp0export.lua"
if errorlevel 1 pause
