@echo off
rem Builds IdleWalker.exe from every .cs file under src\ using the C# compiler
rem that ships with Windows (.NET Framework 4, C# 5 syntax).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ ^
  /out:"%~dp0IdleWalker.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /recurse:"%~dp0src\*.cs"
