@echo off
setlocal
rem Builds IdleWalker.exe from every .cs file under src\ using the C# compiler
rem that ships with Windows (.NET Framework 4, C# 5 syntax).
rem
rem Optional Authenticode signing (removes "Unknown publisher", builds
rem SmartScreen reputation, reduces Defender/Chrome warnings):
rem
rem   build.cmd [nosign]
rem
rem   nosign                 build only, skip signing even if configured.
rem
rem Configure signing ONE of these ways (env vars, or a git-ignored
rem sign.local.cmd next to this file with "set VAR=..." lines):
rem
rem   set CERT_THUMBPRINT=ABC123...     cert in store (USB token / HSM /
rem                                     Azure Key Vault). Thumbprint with
rem                                     NO spaces, e.g. from:
rem                                       certutil -store My
rem   set CERT_FILE=C:\path\cert.pfx
rem   set CERT_PASSWORD=secret           optional; omit to let signtool prompt
rem   set CERT_SUBJECT=My Company Name   "/n" substring match, alternative
rem                                      to the two above.
rem   set TIMESTAMP_URL=http://timestamp.digicert.com
rem                                      default shown; override only if needed.
rem   set SIGNTOOL=C:\path\signtool.exe  override auto-detection.
rem   set SIGN_CMD=...                   full custom sign command, e.g. for
rem                                      Azure Artifact Signing
rem                                      (trusted-signing-cli). "%EXE%" is
rem                                      expanded. If set, it runs instead of
rem                                      signtool.
rem   set SKIP_SIGN=1                    same as "nosign".
rem
rem sign.local.cmd example (never commit this file or the .pfx):
rem   set CERT_THUMBPRINT=3A4B5C6D7E8F90123456789ABCDEF1234567890A
rem   rem set CERT_FILE=%~dp0cert.pfx
rem   rem set CERT_PASSWORD=change-me
rem
rem You need one of:
rem   - Windows SDK signtool.exe (https://developer.microsoft.com/windows/downloads/windows-sdk/),
rem     only the "Windows SDK Signing Tools" workload, or
rem   - Azure Artifact Signing (ex-Trusted Signing, ~$9.99/mo, no token):
rem     install the Trusted Signing CLI and set SIGN_CMD, see comments below.
rem A plain unsigned build keeps working when nothing is configured.

set "EXE=%~dp0IdleWalker.exe"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

"%CSC%" /nologo /target:winexe /optimize+ ^
  /out:"%EXE%" /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /recurse:"%~dp0src\*.cs"
if errorlevel 1 exit /b 1

rem ---- Optional signing -------------------------------------------------
if /i "%~1"=="nosign" goto :unsigned
if "%SKIP_SIGN%"=="1" goto :unsigned
if exist "%~dp0sign.local.cmd" call "%~dp0sign.local.cmd"

rem Custom hook (e.g. Azure Artifact Signing). Example:
rem   set SIGN_CMD=trusted-signing-cli sign -a "https://eus.codesigning.azure.net/" -f IdleWalker -e EUS -i "https://dnsvault.azure.net/" -s "cert-profile" --timestamp-rfc3161 http://timestamp.digicert.com --timestamp-digest SHA256 "%EXE%"
if defined SIGN_CMD (
  call %SIGN_CMD%
  if errorlevel 1 echo SIGN FAILED ^(custom SIGN_CMD^). & exit /b 1
  echo Signed OK: %EXE% ^(custom SIGN_CMD^).
  exit /b 0
)

if not defined CERT_THUMBPRINT if not defined CERT_FILE if not defined CERT_SUBJECT goto :unsigned

if not defined TIMESTAMP_URL set "TIMESTAMP_URL=http://timestamp.digicert.com"

rem Locate signtool.exe
set "SIGNTOOL_FOUND="
if defined SIGNTOOL if exist "%SIGNTOOL%" set "SIGNTOOL_FOUND=%SIGNTOOL%"
if not defined SIGNTOOL_FOUND for /f "delims=" %%S in ('where signtool 2^>nul') do set "SIGNTOOL_FOUND=%%S" & goto :havesigntool
if not defined SIGNTOOL_FOUND (
  for /f "delims=" %%S in ('dir /s /b "%ProgramFiles(x86)%\Windows Kits\10\bin\signtool.exe" 2^>nul') do set "SIGNTOOL_FOUND=%%S"
)
if not defined SIGNTOOL_FOUND (
  for /f "delims=" %%S in ('dir /s /b "%ProgramFiles%\Windows Kits\10\bin\signtool.exe" 2^>nul') do set "SIGNTOOL_FOUND=%%S"
)
:havesigntool
if not defined SIGNTOOL_FOUND (
  echo Build OK: unsigned. signtool.exe not found - install the Windows SDK
  echo ^("Windows SDK Signing Tools"^) or set SIGNTOOL to its path to sign.
  exit /b 0
)

rem Build the signtool selector: PFX file ^> thumbprint ^> subject ^> auto.
set "SELECT="
if defined CERT_FILE (
  set "SELECT=/f \"%CERT_FILE%\""
  if defined CERT_PASSWORD set "SELECT=%SELECT% /p \"%CERT_PASSWORD%\""
) else if defined CERT_THUMBPRINT (
  set "SELECT=/sha1 %CERT_THUMBPRINT%"
) else if defined CERT_SUBJECT (
  set "SELECT=/n \"%CERT_SUBJECT%\""
) else (
  set "SELECT=/a"
)

"%SIGNTOOL_FOUND%" sign /fd SHA256 /tr "%TIMESTAMP_URL%" /td SHA256 /v %SELECT% "%EXE%"
if errorlevel 1 echo SIGN FAILED - exe left unsigned. & exit /b 1

:verify
"%SIGNTOOL_FOUND%" verify /pa /v "%EXE%"
if errorlevel 1 echo WARNING: signature verification failed. & exit /b 1
echo Signed OK: %EXE%
exit /b 0

:unsigned
echo Build OK: unsigned ^(no CERT_THUMBPRINT / CERT_FILE / CERT_SUBJECT configured^).
exit /b 0
