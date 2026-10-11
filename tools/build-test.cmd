@echo off
rem Builds + runs the v2 headless selftest (Domain only, no windows).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /optimize+ ^
  /out:"%~dp0SelfTest.exe" /r:System.Drawing.dll ^
  "%~dp0..\src\Domain\*.cs" "%~dp0..\src\Settings.cs" "%~dp0..\src\Platform\BrainStore.cs" "%~dp0SelfTest.cs"
@if errorlevel 1 exit /b 1
"%~dp0SelfTest.exe"
