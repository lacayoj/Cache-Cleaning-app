Write-Host "========================================================" -ForegroundColor DarkCyan
Write-Host "       Compilando SS Clan Cleaner Pro para Windows..." -ForegroundColor White
Write-Host "========================================================" -ForegroundColor DarkCyan

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Host "[ERROR] No se encontró el compilador csc.exe en el sistema." -ForegroundColor Red
    exit 1
}

$frameworkDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path $frameworkDir)) {
    $frameworkDir = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
}

$references = @(
    "/r:$frameworkDir\WPF\PresentationFramework.dll",
    "/r:$frameworkDir\WPF\PresentationCore.dll",
    "/r:$frameworkDir\WPF\WindowsBase.dll",
    "/r:$frameworkDir\System.Xaml.dll",
    "/r:$frameworkDir\System.dll",
    "/r:$frameworkDir\System.Core.dll",
    "/r:$frameworkDir\System.Drawing.dll"
)
$sourceFiles = @(
    "CleanerModel.cs",
    "DiskHelper.cs",
    "CleanerEngine.cs",
    "MainWindow.cs",
    "Program.cs"
)

$compileArgs = @(
    "/target:winexe",
    "/optimize+",
    "/win32icon:AppIcon.ico",
    "/out:SSClanCleanerPro.exe"
) + $references + $sourceFiles

& $cscPath $compileArgs

if ($LASTEXITCODE -eq 0) {
    Copy-Item "SSClanCleanerPro.exe" -Destination "WinCleanPro.exe" -Force
    Write-Host "`n[OK] Compilacion completada con exito!" -ForegroundColor Green
    Write-Host "Ejecutable creado: SSClanCleanerPro.exe (y WinCleanPro.exe)" -ForegroundColor Green
} else {
    Write-Host "`n[ERROR] Fallo al compilar SS Clan Cleaner Pro." -ForegroundColor Red
    exit 1
}
