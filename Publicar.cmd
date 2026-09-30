@echo off
rem ---------------------------------------------------------------------------------------------
rem  Publica Ferroli_time_Terminal en bin\publish para desplegar en IIS.
rem  Requiere en el servidor el "ASP.NET Core 8 Hosting Bundle" (el mismo que usa carta-portes-cs).
rem  Copiar la carpeta COMPLETA al sitio de IIS (conservando el appsettings.json del servidor).
rem ---------------------------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

echo.
echo === Publicando Ferroli_time_Terminal ===
if exist "bin\publish" rd /s /q "bin\publish"
dotnet publish "FerroliTime.Terminal.csproj" -c Release -o "bin\publish"
if errorlevel 1 goto error

echo.
echo Carpeta generada: %CD%\bin\publish
if "%~1"=="" explorer "bin\publish"
if "%~1"=="" pause
exit /b 0

:error
echo.
echo *** ERROR al publicar Ferroli_time_Terminal ***
if "%~1"=="" pause
exit /b 1
