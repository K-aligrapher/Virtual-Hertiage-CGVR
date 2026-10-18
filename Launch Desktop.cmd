@echo off
if not exist "%~dp0Builds\Windows\VirtualHeritageKarnataka.exe" (
  echo Build the demo first using Heritage ^> Build Windows Demo in Unity.
  pause
  exit /b 1
)
start "Virtual Heritage Karnataka" "%~dp0Builds\Windows\VirtualHeritageKarnataka.exe" --desktop
