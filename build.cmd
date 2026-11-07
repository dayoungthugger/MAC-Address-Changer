@echo off
rem Builds MacChanger.exe with the .NET Framework compiler that ships with Windows.
rem No SDK, no Visual Studio, no NuGet. Produced exe requires .NET Framework 4.x,
rem which every Windows 10 / 11 machine already has.

setlocal
cd /d "%~dp0"

set CSC_TARGET=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC_TARGET%" set CSC_TARGET=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC_TARGET%" (
    echo ERROR: .NET Framework 4 compiler not found.
    exit /b 1
)

set RES=/res:data\oui.txt,OuiData
if not exist data\oui.txt (
    echo WARNING: data\oui.txt is missing - using the built-in fallback vendor list.
    set RES=
)

"%CSC_TARGET%" ^
  /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:"MacChanger.exe" ^
  /win32manifest:"app.manifest" ^
  /codepage:65001 ^
  %RES% ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll ^
  src\Program.cs src\MainForm.cs src\AdapterService.cs src\AdapterInfo.cs src\VendorDb.cs src\MacUtil.cs

if errorlevel 1 (
    echo.
    echo Build FAILED.
    exit /b 1
)

echo.
echo Build OK: MacChanger.exe
echo Run it; Windows will ask for administrator rights (required for spoofing).
endlocal
