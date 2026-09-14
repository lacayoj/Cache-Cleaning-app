# SS Clan Cleaner Pro

Limpiador de caché y archivos temporales para Windows, escrito en C# / WPF. Libera espacio en disco eliminando caché de navegadores, temporales del sistema, shaders de GPU, caché de apps de mensajería/streaming y herramientas de desarrollador — con múltiples capas de seguridad para evitar borrar algo que no debería tocarse.

## Características

- **Perfiles de limpieza**: Rápida, Profunda, Modo Gamer/GPU, Modo Desarrollador y Personalizada.
- **Categorías cubiertas**: temporales de usuario y del sistema, Prefetch, caché de Windows Update, miniaturas, informes de error/WER, volcados de memoria (BSOD), caché de optimización de entrega, DNS, Papelera de Reciclaje, caché de navegadores (Chrome/Edge/Brave/Firefox), shaders de GPU (DirectX/NVIDIA/AMD), caché de Discord/Spotify/Telegram, y caché de herramientas de desarrollador (npm, Yarn, pip, NuGet HTTP cache, VS Code).
- **Capas de seguridad**:
  - Lista blanca/negra de rutas: nunca permite operar sobre raíces de disco, `C:\Windows`, `Program Files`, el perfil de usuario, Escritorio/Documentos/Imágenes/Música/Vídeos, ni datos personales de WhatsApp.
  - Bloqueo de archivos críticos del sistema (`.sys`, `.msc`, `.vhd(x)`, `.iso`, `SAM`, `SYSTEM`, `BCD`, `bootmgr`, etc.).
  - Protección contra *reparse points*/symlinks/junctions para que una carpeta temporal no redirija hacia una ruta real.
  - Nunca fuerza el cierre de procesos críticos del sistema (shell de Windows, `svchost`, `lsass`, etc.), solo cierra apps normales (navegador, Discord, Spotify...) que puedan tener archivos bloqueados.
  - Filtro opcional de "proteger archivos de las últimas 24 h" para no tocar instaladores/descargas recién creados.
  - Confirmación explícita antes de borrar, con aviso reforzado si se incluyen categorías irreversibles (Papelera, volcados de memoria).
  - No requiere permisos de administrador salvo para las categorías que realmente los necesitan (se indica con la etiqueta `ADMIN` en cada categoría).

## Requisitos

- Windows 10/11.
- El compilador de C# de .NET Framework (`csc.exe`), que ya viene instalado en la mayoría de los Windows en:
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
  Si no está presente, instala el **.NET Framework Developer Pack** (4.8 o superior) desde [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet-framework).
- No se necesita Visual Studio ni `dotnet` CLI — la compilación se hace directamente con `csc.exe` contra los ensamblados de WPF del Framework.

## Cómo compilar

Clona el repositorio y ejecuta uno de los dos scripts incluidos desde la carpeta del proyecto:

**PowerShell (recomendado):**

```powershell
.\build.ps1
```

**CMD:**

```bat
build.bat
```

Ambos scripts:
1. Localizan `csc.exe` (rutas de 64 o 32 bits).
2. Compilan `CleanerModel.cs`, `DiskHelper.cs`, `CleanerEngine.cs`, `MainWindow.cs` y `Program.cs` referenciando WPF (`PresentationFramework`, `PresentationCore`, `WindowsBase`), `System.Xaml` y `System.Drawing`.
3. Embeben el ícono de la app (`AppIcon.ico`) en el ejecutable con `/win32icon`.
4. Generan `SSClanCleanerPro.exe` y una copia como `WinCleanPro.exe`.

## Cómo ejecutar

Tras compilar, simplemente abre el ejecutable generado:

```powershell
.\SSClanCleanerPro.exe
```

Algunas categorías (Temporales del Sistema, Prefetch, Caché de Windows Update, Volcados de Memoria, Optimización de Entrega) requieren permisos de administrador para limpiarse por completo; la app ofrece un botón para reiniciarse como administrador cuando detecta que falta.

## Estructura del proyecto

| Archivo | Contenido |
|---|---|
| `Program.cs` | Punto de entrada de la aplicación WPF. |
| `MainWindow.cs` | Interfaz de usuario (ventana principal, tarjetas de categoría, animaciones, diálogos). |
| `CleanerEngine.cs` | Lógica de limpieza: definición de categorías, escaneo, borrado seguro y todas las validaciones de seguridad. |
| `CleanerModel.cs` | Modelos de datos y enums (categorías, perfiles, opciones, resultados). |
| `DiskHelper.cs` | Utilidades de disco/procesos (formateo de tamaños, vaciar Papelera, flush de DNS, detección/cierre de procesos en conflicto, reinicio como admin). |
| `AppIcon.ico` | Ícono de la aplicación (multi-resolución, 16–256 px). |
| `build.bat` / `build.ps1` | Scripts de compilación. |

## Aviso

Esta herramienta borra archivos de forma **permanente** (no pasa por la Papelera de Reciclaje, salvo la categoría que la vacía explícitamente). Aunque cuenta con múltiples capas de protección, revisa las categorías seleccionadas antes de limpiar, especialmente si usas el perfil "Personalizada".
