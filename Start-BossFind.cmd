@echo off
setlocal EnableExtensions

rem Always run from the repository root, even when launched by double-click.
cd /d "%~dp0"

set "APP_EXE=src\BossFind.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\BossFind.App.exe"
set "DOTNET="

echo Building the current BossFind source before launch...

if exist "%~dp0.dotnet\dotnet.exe" set "DOTNET=%~dp0.dotnet\dotnet.exe"
if exist "%DOTNET%" goto restore
for /f "delims=" %%D in ('where dotnet 2^>nul') do if not defined DOTNET set "DOTNET=%%D"
if defined DOTNET goto restore

echo.
echo .NET SDK was not found. Install .NET SDK 10.0.400 or place the SDK in .dotnet.
goto failed

:restore
echo Using SDK: %DOTNET%
"%DOTNET%" restore BossFind.sln --locked-mode
if errorlevel 1 goto failed

"%DOTNET%" build src\BossFind.App\BossFind.App.csproj -c Debug -p:Platform=x64 --no-restore -m:1
if errorlevel 1 goto failed
if not exist "%APP_EXE%" (
    echo.
    echo Build completed, but the application was not found: %APP_EXE%
    goto failed
)

:launch
echo Starting BossFind...
start "" "%APP_EXE%"
if errorlevel 1 goto failed
goto done

:failed
echo.
echo BossFind failed to start. Review the error messages above.
pause
exit /b 1

:done
endlocal
exit /b 0
