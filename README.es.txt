Muelle
======

Gabriel Castiglia · 11 de noviembre de 2026 · versión 2.1.0 · GPL-2.0-only

English (README.txt) · Español

Manual en PDF: docs/Muelle-manual-es.pdf

  Solo para Windows 10/11 en x64 (Intel o AMD de 64 bits). No hay versión para procesadores ARM.

Muelle es un dock de aplicaciones para Windows 10 y 11, escrito en C# con .NET 10 y WPF. Puede ser
un dock flotante que amplía los iconos al pasar el puntero, al estilo de macOS, o un panel fijo de
borde a borde, al estilo de Ubuntu. La interfaz está en inglés y en castellano: arranca en el idioma
elegido al instalar (en inglés si corre sin instalación) y se cambia cuando se quiera. El nombre es
la palabra castellana para el lugar donde amarran los barcos, que en inglés se dice «dock». Puede
ser claro, oscuro o seguir a Windows, en uno de doce colores.

Requisitos
----------

- Windows 11, o Windows 10 versión 1803 o posterior, en un procesador x64 (Intel o AMD de 64 bits).

No hace falta instalar nada más. Muelle lleva su propio .NET, y la ventana de instalación usa el
.NET Framework que ya trae Windows.

Instalación
-----------

Los archivos se bajan de la
última versión publicada <https://github.com/Gabriel-Castiglia/muelle/releases/latest>. Hay tres
formas de tener Muelle, y scripts\build.ps1 deja las tres en la carpeta dist:

  Archivo            Qué es
  ------------------ ----------------------------------------------------------------------------------------------------------------
  muelle-setup.exe   El instalador, en inglés o en castellano. Es la forma habitual.
  muelle.msi         La misma instalación como paquete de Windows Installer, para instalar sin ventanas: msiexec /i muelle.msi /qn.
  Muelle.exe         El programa solo; anda sin instalar, desde cualquier carpeta.

Con el archivo SHA256SUMS.txt de cada versión se puede comprobar una descarga antes, en PowerShell:
Get-FileHash muelle-setup.exe.

El instalador abre una ventana con un menú de idioma arriba a la derecha, y cada página cambia de
idioma en el acto. Instala Muelle para todos los usuarios en C:\Program Files\Muelle, lo que pide
permisos de administrador (Windows los pide al empezar), y lo agrega al menú Inicio. El idioma
elegido en esa ventana es el que usa el dock la primera vez que arranca.

Correr un muelle-setup.exe más nuevo actualiza el Muelle instalado y conserva la configuración de
cada usuario. Si Muelle está abierto, el instalador lo cierra antes de reemplazar sus archivos. Una
versión vieja no se puede instalar encima de una más nueva.

Para que Muelle arranque con Windows, marcar Configuración → Comportamiento → «Iniciar Muelle al
encender Windows».

Desinstalación
--------------

En la Configuración de Windows, Aplicaciones → Aplicaciones instaladas, buscar Muelle y elegir
Desinstalar. Se abre la misma ventana del instalador, con su menú de idioma, en el idioma que se
eligió al instalar. Cierra Muelle si está abierto, quita el programa y su acceso del menú Inicio, y
borra la entrada de «Iniciar con Windows» del usuario que desinstala.

La configuración de cada usuario, en %APPDATA%\Muelle, se conserva, así una instalación posterior la
encuentra. Para borrarla también, eliminar esa carpeta.

Para desinstalar sin ventanas: muelle-setup.exe /uninstall /quiet.

Uso del dock
------------

- Un clic en un icono abre la aplicación o, si ya está abierta, trae su ventana al frente; con
  varias ventanas, cada clic trae la siguiente. Un clic sobre la única ventana que ya está al frente
  la minimiza. Mayúsculas+clic o el botón del medio abren una ventana nueva.
- El icono rebota mientras la aplicación arranca, y un punto debajo marca las aplicaciones abiertas.
- Las aplicaciones abiertas que no están en el dock aparecen al final y se van al cerrarse.
  «Mantener en el dock» en su menú, o arrastrarlas entre las demás, las deja fijas.
- Las aplicaciones se agregan soltando un acceso directo o un .exe sobre el dock, con «Agregar
  aplicación…» en el menú del dock o desde Configuración. También se pueden agregar apps de la
  Tienda (Teams, Terminal…) y objetos del sistema como el Panel de control.
- Los iconos se reordenan arrastrándolos a lo largo del dock y se quitan arrastrándolos afuera.
  «Bloquear iconos» impide las dos cosas, para evitar cambios por accidente.
- Cuando los iconos no entran en una pantalla, se achican solo en esa pantalla.
- Con una aplicación a pantalla completa (un video, un juego) el dock pasa por detrás.

El clic derecho sobre un icono ofrece Mostrar (o Abrir), Abrir otra ventana, Ejecutar como
administrador, Abrir ubicación del archivo, Finalizar tarea (en las aplicaciones con procesos
propios) y Mantener en el dock o Quitar del dock. El clic derecho sobre el dock ofrece Agregar
aplicación…, el cambio entre dock flotante y panel fijo, Bloquear iconos, los menús de idioma y de
apariencia, Configuración…, Acerca de Muelle… y Salir de Muelle.

Configuración
-------------

Clic derecho sobre el dock → Configuración…. Cada cambio se aplica en el momento.

  Grupo                   Opciones
  ----------------------- ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  Estilo                  Dock flotante (estilo Mac) o panel fijo (estilo Ubuntu); opacidad del panel.
  Ubicación               Borde de la pantalla; la pantalla principal, todas las pantallas (una copia en cada una) o una pantalla concreta.
  Comportamiento          Reservar la franja de pantalla, ocultar automáticamente, mostrar las apps abiertas que no están fijadas, bloquear los iconos, iniciar con Windows, avisar cuando haya una versión nueva.
  Apariencia              Como Windows, claro u oscuro; uno de doce colores.
  Iconos                  Tamaño (de 24 a 96 px), ampliación al pasar el puntero (hasta el doble), fondo del dock flotante.
  Idioma                  Inglés o castellano.
  Aplicaciones del dock   La lista de aplicaciones fijas, con Agregar, Quitar, Subir y Bajar.

Apariencia
----------

Muelle puede ser claro, oscuro o seguir lo que Windows tiene elegido para las aplicaciones
(Configuración → Personalización → Colores), y cambia en el momento cuando Windows cambia. El dock
pasa a ser de vidrio claro esmerilado o de vidrio oscuro, y las ventanas de configuración, Acerca de
y aviso de versión nueva lo siguen, barra de título incluida. La apariencia se elige en
Configuración → Apariencia o desde el menú del dock.

Hay doce colores. El elegido pinta el punto de las aplicaciones abiertas, los interruptores, los
controles deslizantes y lo seleccionado, y el ícono de las ventanas de Muelle: el barco navega sobre
un fondo de ese color. El ícono del menú Inicio sigue azul, porque Muelle se instala para todos los
usuarios y ese acceso es de todos.

Un archivo de configuración de una versión anterior conserva el dock oscuro que tenía; una
instalación nueva sigue a Windows.

Versiones nuevas
----------------

Una vez por día, Muelle le pregunta a GitHub el número de la última versión. Si es más nueva, una
ventana chica lo avisa y ofrece abrir la página de descarga; Muelle nunca baja ni instala nada por
su cuenta. La consulta no lleva más que el nombre del programa y su versión. Se apaga en
Configuración → Comportamiento → «Avisar cuando haya una versión nueva»; Acerca de Muelle… → «Buscar
versión nueva» pregunta cuando se quiera.

Idioma
------

La interfaz está en inglés y en castellano. El idioma se cambia desde el menú del dock (Idioma /
Language, rotulado en los dos idiomas para encontrarlo desde cualquiera) o en Configuración, y queda
guardado. Los nombres de las aplicaciones que Muelle agrega en su primer arranque (Explorador de
archivos, Bloc de notas, Calculadora…) siguen el idioma de la interfaz.

Archivos
--------

  Qué                                  Dónde
  ------------------------------------ ---------------------------------------------------------------------------------------------
  Programa instalado                   C:\Program Files\Muelle, con LICENSE.txt, README.txt y README.es.txt
  Configuración                        %APPDATA%\Muelle\config.json (se puede borrar para empezar de cero)
  Registro de errores, si algo falla   %APPDATA%\Muelle\muelle.log, de menos de 512 KB (la parte anterior queda en muelle.old.log)

Limitaciones conocidas
----------------------

- Cuando Muelle reserva su franja de pantalla, Windows reacomoda los iconos del escritorio que
  quedaban debajo y puede pasarlos a otro monitor. Así trata Windows a cualquier barra de
  aplicaciones. Si molesta, desmarcar «Reservar su franja de pantalla»; las ventanas maximizadas
  pasan entonces por debajo del dock.

Compilar desde el código
------------------------

Con el SDK de .NET 10 <https://dotnet.microsoft.com/download> instalado:

    dotnet build src\Muelle\Muelle.csproj -c Release
    powershell -ExecutionPolicy Bypass -File scripts\build.ps1

El primer comando compila el programa. El segundo arma todo en dist: el Muelle.exe suelto,
muelle.msi y muelle-setup.exe; el WiX Toolset 5 se baja solo como paquete NuGet. La versión se fija
en un solo lugar, src\Muelle\Muelle.csproj, y los instaladores y la ventana Acerca de la leen de
ahí.

scripts\screenshots.ps1 genera las imágenes de docs\images (desde una sesión de escritorio: por SSH
la imagen del dock sale vacía), y scripts/manual.sh (en macOS, con pandoc y Google Chrome) arma los
manuales en PDF y README.txt y README.es.txt a partir de README.md y de este archivo.
scripts/make_icon.py dibuja el ícono en sus doce colores a partir del grabado de assets/ (Python 3
con numpy, opencv-python y Pillow).

  Carpeta             Contenido
  ------------------- -----------------------------------------------------------------------------------
  src/Muelle          El programa.
  installer/Msi       El paquete MSI (WiX).
  installer/SetupUI   La ventana del instalador, en inglés y en castellano (.NET Framework 4.7.2, WPF).
  installer/Setup     El instalador que lleva esa ventana y el MSI (bundle de WiX).
  scripts             Compilación, imágenes, icono y manuales.
  assets              El grabado del que sale el ícono.
  docs                Manuales en PDF, imágenes y la hoja de estilo del manual.

Historial de versiones
----------------------

Ver CHANGELOG.md (en inglés).

Licencia
--------

Copyright (C) 2026 Gabriel Castiglia.

Muelle es software libre: se puede redistribuir y modificar según los términos de la Licencia
Pública General GNU, solo la versión 2 (GPL-2.0-only), publicada por la Free Software Foundation. Se
distribuye SIN NINGUNA GARANTÍA. El texto completo está en LICENSE.

El ícono es el barco de tres palos del grabado de Frans Huys según Pieter Bruegel el Viejo (Los
barcos de vela, hacia 1561-1565), Museo Metropolitano de Arte, dominio público (CC0).
