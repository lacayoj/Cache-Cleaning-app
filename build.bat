@echo off
echo ========================================================
echo        Compilando SS Clan Cleaner Pro para Windows...
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist %CSC% (
    echo Error: No se encontro el compilador de C# en el sistema.
    pause
    exit /b 1
)

set FRAMEWORKDIR="C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if not exist %FRAMEWORKDIR% (
    set FRAMEWORKDIR="C:\Windows\Microsoft.NET\Framework\v4.0.30319"
)

set REFS=/r:%FRAMEWORKDIR%\WPF\PresentationFramework.dll /r:%FRAMEWORKDIR%\WPF\PresentationCore.dll /r:%FRAMEWORKDIR%\WPF\WindowsBase.dll /r:%FRAMEWORKDIR%\System.Xaml.dll /r:%FRAMEWORKDIR%\System.dll /r:%FRAMEWORKDIR%\System.Core.dll /r:%FRAMEWORKDIR%\System.Drawing.dll

%CSC% /target:winexe /optimize+ /win32icon:AppIcon.ico /out:SSClanCleanerPro.exe %REFS% CleanerModel.cs DiskHelper.cs CleanerEngine.cs MainWindow.cs Program.cs

if %ERRORLEVEL% EQU 0 (
    copy /y SSClanCleanerPro.exe WinCleanPro.exe >nul
    echo.
    echo ========================================================
    echo  [OK] Compilacion exitosa: SSClanCleanerPro.exe generado.
    echo ========================================================
) else (
    echo.
    echo [ERROR] La compilacion ha fallado. Revisa los errores arriba.
)

pause
