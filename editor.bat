@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%bin\Release\net10.0-windows\Desktop Clock.exe"
if not exist "%EXE%" (
  dotnet build "%ROOT%DesktopClock.csproj" -c Release >nul
  if errorlevel 1 exit /b 1
)
start "" "%EXE%" --editor --config "%ROOT%desktop-image-clock.json"
endlocal
