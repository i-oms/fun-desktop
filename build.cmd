@echo off
rem Builds IdleWalker.exe with the C# compiler that ships with Windows.
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ ^
  /out:"%~dp0IdleWalker.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0IdleWalker.cs"
