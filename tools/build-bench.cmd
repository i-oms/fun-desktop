@echo off
rem Builds the two headless performance harnesses against src\ and runs the benchmark.
rem   Bench.exe [seconds=20] [cast=full|minions5|minions10] [seed=7]
rem     steps the troupe at 60 Hz with everyone out and paints every changed frame:
rem     the true paint demand of the cast as a share of one core.
rem   Hash.exe prints a pixel hash per pose for all four figures; run it before and
rem     after a painter change and diff the outputs (empty diff = pixel-identical).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:exe /optimize+ /main:IdleWalker.Tools.Bench ^
  /out:"%~dp0Bench.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /recurse:"%~dp0..\src\*.cs" "%~dp0Bench.cs"
@if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:exe /optimize+ /main:IdleWalker.Tools.Hash ^
  /out:"%~dp0Hash.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /recurse:"%~dp0..\src\*.cs" "%~dp0Hash.cs"
@if errorlevel 1 exit /b 1
"%~dp0Bench.exe" %*
