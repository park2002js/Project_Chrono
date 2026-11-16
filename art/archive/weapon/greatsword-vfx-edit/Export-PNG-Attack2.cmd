@echo off
setlocal
set "CHRONO_ASEPRITE=D:\02_Games\01_Steam\Library\steamapps\common\Aseprite\Aseprite.exe"
if not exist "%CHRONO_ASEPRITE%" set "CHRONO_ASEPRITE=D:\02_Games\01_Steam\SteamClient\steamapps\common\Aseprite\Aseprite.exe"
if not exist "%CHRONO_ASEPRITE%" (
  echo Aseprite.exe was not found. Update CHRONO_ASEPRITE in this file.
  pause
  exit /b 1
)
"%CHRONO_ASEPRITE%" --batch --script-param "input=%~dp0aseprite\greatsword-swing-attack2.aseprite" --script-param "output=%~dp0png-attack2" --script-param "variant=attack2" --script "%~dp0export.lua"
if errorlevel 1 (
  echo Export failed. Check the message above.
  pause
  exit /b 1
)
echo Saved attack2 PNG files to %~dp0png-attack2
endlocal
