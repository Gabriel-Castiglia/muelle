Muelle
======

Gabriel Castiglia · 27 de noviembre de 2026 · versión 2.6.0 · GPL-2.0-only

English (README.txt) · Español · 日本語 (README.ja.txt)

Manual en PDF: docs/Muelle-manual-es.pdf

  Solo para Windows 10/11 en x64 (Intel o AMD de 64 bits). No hay versión para procesadores ARM.

Muelle es un dock de aplicaciones para Windows 10 y 11, escrito en C# con .NET 10 y WPF. Puede ser
un dock flotante que amplía los iconos al pasar el puntero, al estilo de macOS, o un panel fijo de
borde a borde, al estilo de Ubuntu. La interfaz está en inglés, castellano y japonés: arranca en el
idioma elegido al instalar (en inglés si se ejecuta sin instalación) y puedes cambiarlo cuando
quieras. El nombre es la palabra castellana para el lugar donde amarran los barcos, que en inglés se
dice «dock». Puede ser claro, oscuro o seguir a Windows, en uno de doce colores. Varios accesos
pueden compartir un icono como pila, cada pantalla puede tener sus propios iconos, y las versiones
nuevas se instalan solas.

Requisitos
----------

- Windows 11, o Windows 10 versión 1803 o posterior, en un procesador x64 (Intel o AMD de 64 bits).

No hace falta instalar nada más. Muelle lleva su propio .NET, y la ventana de instalación usa el
.NET Framework que ya trae Windows.

Instalación
-----------

Descarga los archivos de la
última versión publicada <https://github.com/Gabriel-Castiglia/muelle/releases/latest>. Hay tres
formas de tener Muelle, y scripts\build.ps1 deja las tres en la carpeta dist:

  Archivo            Qué es
  ------------------ ----------------------------------------------------------------------------------------------------------------
  muelle-setup.exe   El instalador, en inglés, castellano o japonés. Es la forma habitual.
  muelle.msi         La misma instalación como paquete de Windows Installer, para instalar sin ventanas: msiexec /i muelle.msi /qn.
  Muelle.exe         El programa solo; funciona sin instalar, desde cualquier carpeta.

Cada versión publicada trae además este manual en PDF, en inglés, castellano y japonés
(Muelle-manual-en.pdf, Muelle-manual-es.pdf, Muelle-manual-ja.pdf). Con el archivo SHA256SUMS.txt de
cada versión puedes comprobar una descarga antes de usarla, en PowerShell:
Get-FileHash muelle-setup.exe.

El instalador abre una ventana con un menú de idioma arriba a la derecha (primero inglés, después
castellano y japonés), y cada página cambia de idioma en el acto. Instala Muelle para todos los
usuarios en C:\Program Files\Muelle, lo que pide permisos de administrador (Windows los pide al
empezar), y lo agrega al menú Inicio. El idioma elegido en esa ventana es el que usa el dock la
primera vez que arranca.

Ejecutar un muelle-setup.exe más nuevo actualiza el Muelle instalado y conserva la configuración de
cada usuario. Si Muelle está abierto, el instalador lo cierra antes de reemplazar sus archivos y lo
vuelve a abrir al terminar. Una versión anterior no se puede instalar encima de una más nueva. El
Muelle instalado también se puede actualizar solo: consulta Versiones nuevas (#versiones-nuevas).

Para instalar sin ventanas: muelle-setup.exe /quiet (Windows pide igualmente permiso de
administrador).

Para que Muelle arranque con Windows, activa Configuración → Comportamiento → «Iniciar Muelle al
encender Windows». Si creaste manualmente una tarea del Programador de tareas que arranca Muelle
(\Muelle (inicio de sesion) o \Muelle), la casilla sigue a esa tarea: al activarla se habilita, al
desactivarla se deshabilita, y no se usa además la clave Run del registro, así Muelle nunca arranca
dos veces. El cambio se hace en segundo plano, así que la ventana de configuración no se bloquea
mientras Windows lo aplica.

Desinstalación
--------------

En la Configuración de Windows, ve a Aplicaciones → Aplicaciones instaladas, busca Muelle y elige
Desinstalar. Se abre la misma ventana del instalador, con su menú de idioma, en el idioma que se
eligió al instalar. Cierra Muelle si está abierto, quita el programa y su acceso del menú Inicio, y
borra la entrada de «Iniciar con Windows» del usuario que desinstala.

La configuración de cada usuario, en %APPDATA%\Muelle, se conserva, así una instalación posterior la
encuentra. Para borrarla también, elimina esa carpeta.

Para desinstalar sin ventanas: muelle-setup.exe /uninstall /quiet.

Uso del dock
------------

- Un clic en un icono abre la aplicación o, si ya está abierta, trae su ventana al frente; con
  varias ventanas, cada clic trae la siguiente. Un clic sobre la única ventana que ya está al frente
  la minimiza. Mayúsculas+clic o el botón del medio abren una ventana nueva.
- El icono rebota mientras la aplicación arranca, y un punto debajo marca las aplicaciones abiertas.
- Cada icono sigue a su aplicación: cuando se actualiza (también las apps de la Tienda), cuando
  cambia el color de su propio icono (como hace Vigía con el color del tema), o cuando cambia su
  acceso directo o el archivo del icono elegido, el dock muestra el nuevo en menos de 15 segundos,
  sin reiniciarse. Para un programa, el dock toma el icono que nombran los accesos directos del
  propio programa cuando ese archivo está en la carpeta del programa; un icono elegido manualmente
  para un acceso, desde cualquier otro lugar, no se toca.
- Las aplicaciones abiertas que no están en el dock aparecen al final y se van al cerrarse. «Fijar
  al dock» en su menú, o arrastrarlas entre las demás, las deja fijas.
- Las aplicaciones se agregan soltando un acceso directo o un .exe sobre el dock, con «Agregar
  aplicación…» en el menú del dock o desde Configuración. También se pueden agregar apps de la
  Tienda (Teams, Terminal…) y objetos del sistema como el Panel de control.
- Los iconos se reordenan arrastrándolos a lo largo del dock y se quitan arrastrándolos afuera.
  «Bloquear dock» impide las dos cosas, y también fijar, liberar y renombrar desde el menú, para
  evitar cambios por accidente. En Configuración → Aplicaciones del dock, los botones que cambiarían
  los iconos (Agregar…, Nueva pila…, Editar pila…, Quitar, Subir y Bajar) también aparecen
  atenuados, y su información sobre herramientas explica el motivo; vuelven a estar disponibles en
  cuanto se desactiva el bloqueo, desde el menú del dock o desde Configuración, aunque la ventana
  esté abierta.
- Una pila es un icono que guarda varios accesos: un clic los despliega al lado. Consulta
  Pilas (#pilas).
- Un dock vacío muestra un lugar con «+» («Arrastra aquí tus apps»): suelta aplicaciones encima, o
  haz clic en él para elegirlas.
- Cuando los iconos no caben en una pantalla, se reducen solo en esa pantalla.
- Con una aplicación a pantalla completa al frente (un video, un juego) el dock se oculta del todo,
  también para el puntero en el borde, y vuelve cuando la aplicación sale de pantalla completa.

  Acción                                   Resultado
  ---------------------------------------- ------------------------------------------------------------------------------------------------------------------------
  Clic                                     Abre la aplicación, trae su ventana al frente o muestra la siguiente; minimiza la única ventana que ya está al frente.
  Mayúsculas+clic, o botón del medio       Abre una ventana nueva de la aplicación.
  Clic derecho                             El menú del icono, o el del dock sobre una parte vacía.
  Arrastrar a lo largo del dock / afuera   Reordena el icono / lo quita (no con «Bloquear dock»).
  Soltar un acceso directo o un .exe       Lo agrega en ese lugar.

El clic derecho sobre un icono abre su menú, encabezado por el nombre de la aplicación:

- Una aplicación fijada: Abrir otra ventana (o Abrir, si está cerrada), Renombrar…, Liberar del
  dock, y Más (Ejecutar como administrador, Abrir ubicación del archivo y, si el dock tiene pilas,
  Mover a la pila).
- Una aplicación abierta que no está fijada: Abrir otra ventana, Fijar al dock, y Más (Ejecutar como
  administrador, Abrir ubicación del archivo).
- Una pila: Abrir la pila, Editar pila… y Liberar del dock.

Después vienen Bloquear dock (con una marca de verificación mientras está activo), Configuración… y
Salir de Muelle. Finalizar tarea va al final y aparte, solo mientras la aplicación está abierta; la
cierra en el acto, sin preguntar, así que lo que no se haya guardado en ella se pierde. El clic
derecho en una parte vacía del dock ofrece Agregar aplicación…, Nueva pila…, Bloquear dock,
Configuración… y Salir de Muelle. Con «Bloquear dock» activo, las líneas que cambiarían los iconos
aparecen atenuadas, y su información sobre herramientas explica que el dock está bloqueado y que
para cambiar los iconos hay que desactivar «Bloquear dock». Todo lo demás (estilo, idioma,
apariencia, actualizaciones, Acerca de) está en Configuración.

Renombrar… pide el nombre que se muestra bajo el puntero; con el nombre vacío vuelve al nombre del
programa.

Pilas
-----

Una pila guarda varios accesos bajo un solo icono, como las pilas del Dock de la Mac. Un clic la
despliega al lado del icono, cada acceso con su icono; un clic en uno lo abre, o trae su ventana al
frente si ya está abierto. «Editar pila…», al final de la lista, abre el editor de la pila. Una pila
sin accesos dice «La pila está vacía».

- «Nueva pila…», en el menú del dock o en Configuración → Aplicaciones del dock, crea una.
- En el editor: el nombre de la pila; su icono, uno propio («Elegir icono…»: un .ico, un programa o
  una imagen) o «Icono automático», sus primeros cuatro accesos en cuadrícula; y sus accesos, con
  Agregar…, Quitar, Subir y Bajar. El dock no cambia hasta «Guardar». Si la pila salió del dock
  mientras se editaba (se quitó, o se cambió «Cada pantalla con sus propios iconos»), «Guardar» lo
  avisa y no cambia nada.
- Solo hay un editor abierto a la vez. Elegir «Editar pila…» para la pila que ya se está editando
  trae su editor al frente. Si el editor abierto tiene cambios sin guardar, editar otra pila
  pregunta antes si quieres descartarlos; nunca se pierde nada sin preguntar.

- «Mover a la pila», en el menú de un icono, pone ese icono al final de una pila.
- Una pila se mueve y se quita como cualquier otro icono. Una app abierta que está en una pila no
  vuelve a aparecer al final del dock.

Cada pantalla con sus propios iconos
------------------------------------

Con el dock en todas las pantallas, de forma predeterminada cada copia muestra los mismos iconos.
Configuración → Ubicación → «Cada pantalla con sus propios iconos» le da a cada pantalla su propia
lista. Una pantalla que aparece por primera vez empieza con una copia de los iconos comunes, y a
partir de ahí sus iconos se agregan, quitan y reordenan en su propio dock, o en Configuración →
Aplicaciones del dock, que entonces tiene una lista «Iconos de» para elegir la pantalla.

Muelle reconoce cada pantalla por el nombre que Windows le da al monitor físico y al conector donde
está enchufado, así que conserva sus iconos después de reiniciar o si se reordenan las pantallas; si
Windows no da ese nombre, por su nombre de pantalla y su resolución. Cuando una pantalla se
desconecta, sus iconos quedan guardados en la configuración y vuelven al conectarla otra vez. Al
desactivar la opción se vuelve a los iconos comunes, y los de cada pantalla quedan guardados para la
próxima vez.

Configuración
-------------

Clic derecho sobre el dock → Configuración…. Cada cambio se aplica en el momento.
Muelle.exe --settings arranca Muelle con la ventana de configuración abierta (útil para un acceso
directo).

  Grupo                      Opciones
  -------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  Estilo                     Dock flotante (estilo Mac), iconos centrados que se amplían al pasar el puntero, o panel fijo (estilo Ubuntu), una barra de borde a borde con los iconos al inicio; opacidad del panel (de 0 a 100 %, solo el panel fijo).
  Ubicación                  Borde de la pantalla (abajo, arriba, izquierda o derecha); mostrar el dock solo en la pantalla principal, en todas las pantallas (una copia en cada una) o en una pantalla concreta; cada pantalla con sus propios iconos.
  Comportamiento             Reservar su franja de pantalla (las ventanas maximizadas no lo tapan; no mientras se oculta automáticamente); ocultar automáticamente (aparece al llevar el puntero al borde); mostrar también las apps abiertas que no están fijadas; bloquear el dock; iniciar Muelle al encender Windows; buscar versiones nuevas una vez al día.
  Apariencia                 Como Windows, claro u oscuro; uno de doce colores (también con el teclado: Tab llega a los colores, las flechas pasan de uno a otro y la barra espaciadora elige uno).
  Iconos                     Tamaño (de 24 a 96 px); ampliación al pasar el puntero, desde ninguna hasta el doble; fondo del dock flotante, vidrio sutil o transparente (la ampliación y el fondo valen solo para el dock flotante).
  Language / Idioma / 言語   Idioma de la interfaz: English, Español o 日本語.
  Aplicaciones del dock      La lista de aplicaciones fijas y pilas, con Agregar…, Nueva pila…, Editar pila… (o doble clic), Quitar, Subir y Bajar; con iconos propios por pantalla, «Iconos de:» elige la pantalla cuyos iconos se listan. Con «Bloquear dock» activo, todos los botones aparecen atenuados y el doble clic no abre el editor de pilas.

Al pie de la ventana, «Abrir carpeta de configuración» muestra config.json en el Explorador de
archivos, «Acerca de…» abre Acerca de Muelle y «Cerrar» cierra la ventana (también Esc). Las
opciones que no corresponden al estilo elegido aparecen atenuadas.

Apariencia
----------

Muelle puede ser claro, oscuro o seguir lo que Windows tiene elegido para las aplicaciones
(Configuración → Personalización → Colores), y cambia en el momento cuando Windows cambia. El dock
pasa a ser de vidrio claro esmerilado o de vidrio oscuro, y las ventanas de configuración, Acerca de
y aviso de versión nueva lo siguen, barra de título incluida. La apariencia se elige en
Configuración → Apariencia.

Hay doce colores: Azul (el predeterminado), Dorado, Morado, Celeste, Naranja, Rojo, Amarillo, Verde,
Verde azulado, Rosa, Negro y Blanco. El elegido pinta el punto de las aplicaciones abiertas, los
interruptores, los controles deslizantes y lo seleccionado, y el icono de las ventanas de Muelle: el
barco navega sobre un fondo de ese color. El icono del menú Inicio sigue azul, porque Muelle se
instala para todos los usuarios y ese acceso es de todos.

Un archivo de configuración de una versión anterior conserva el dock oscuro que tenía; una
instalación nueva sigue a Windows.

Teclado y lectores de pantalla
------------------------------

Las ventanas de configuración, del editor de pilas, Acerca de y de actualización se pueden usar solo
con el teclado: Tab y Mayúsculas+Tab pasan de un control a otro, y el control que tiene el foco del
teclado lo muestra con un borde del color de acento o un anillo del color del texto. En
Configuración → Apariencia, Tab llega a los doce colores como un solo grupo, las flechas pasan de
uno a otro y la barra espaciadora elige uno; el color con el foco tiene un marco propio.

El dock nunca toma el foco del teclado, así que lo que escribes siempre va a la aplicación que está
al frente. Los lectores de pantalla como Narrador o NVDA lo ven, mediante UI Automation, como una
barra de herramientas llamada Muelle con un botón por icono: cada botón tiene el nombre de su
aplicación o pila, indica si la aplicación está abierta o cuántos accesos tiene la pila, y su acción
predeterminada es la misma que un clic (abre la aplicación, trae su ventana al frente o abre la
pila).

Versiones nuevas
----------------

Una vez por día, Muelle le pregunta a GitHub por su última versión publicada; «Buscar
actualizaciones», en Acerca de Muelle (Configuración → Acerca de…), pregunta en cualquier momento.
Si hay una versión más nueva, una ventana pequeña hace una sola pregunta: Actualizar ahora, Ver
novedades (la página de la versión) o Más tarde.

Después de «Actualizar ahora» todo pasa solo:

1.  El SHA256SUMS.txt de esa versión tiene que llevar la firma del autor (release.sig, que se
    comprueba con dos claves que trae Muelle); una versión sin firma válida se rechaza y solo se
    puede descargar manualmente desde su página. Después el instalador, muelle-setup.exe, se
    descarga a %TEMP%\MuelleUpdate y se compara con ese SHA256SUMS.txt. Un archivo que no coincide
    se borra y nunca se ejecuta. Desde esa comprobación hasta que el instalador arranca, Muelle
    mantiene el archivo abierto solo para lectura, así nada puede cambiarlo, reemplazarlo ni
    borrarlo entretanto.
2.  Muelle anota cómo se lo abrió y se cierra.
3.  El instalador instala en silencio, con el idioma elegido al instalar Muelle; Windows pide
    permiso de administrador, como en cualquier instalación. Espera a que Muelle se haya cerrado, y
    cierra cualquier otra copia que se ejecute desde la carpeta de instalación.
4.  Muelle se vuelve a abrir, con los mismos argumentos, y avisa que se actualizó.

Si algo falla, no se instala nada: Muelle sigue, o se vuelve a abrir, y dice qué pasó (por ejemplo,
si se niega el permiso de administrador). Una copia de Muelle.exe que se ejecuta sin instalación no
se puede reemplazar sola; para ella la ventana solo ofrece la página de la versión.

Las consultas no llevan más que el nombre del programa y su versión. La consulta diaria se desactiva
en Configuración → Comportamiento.

Idioma
------

La interfaz está en inglés, castellano y japonés. El idioma se cambia en Configuración (Language /
Idioma / 言語, rotulado en los tres idiomas para encontrarlo desde cualquiera), y queda guardado. La
ventana del instalador arranca en inglés, salvo que Muelle ya esté instalado en otro idioma. Los
nombres de las aplicaciones que Muelle agrega en su primer arranque (Explorador de archivos, Bloc de
notas, Calculadora…) siguen el idioma de la interfaz.

Archivos
--------

  Qué                                  Dónde
  ------------------------------------ -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  Programa instalado                   C:\Program Files\Muelle, con LICENSE.txt, README.txt, README.es.txt y README.ja.txt
  Configuración                        %APPDATA%\Muelle\config.json (se puede borrar para empezar de cero); la versión anterior al último guardado, en config.previous.json
  Configuración que no se pudo leer    Un icono o un ajuste que esta versión no puede leer se descarta solo, nunca la lista entera: lo descartado va a config.dropped-<fecha>.json y el archivo completo, tal como estaba, a config.partial-<fecha>.json
  Registro de errores, si algo falla   %APPDATA%\Muelle\muelle.log, de menos de 512 KB (la parte anterior queda en muelle.old.log)
  Actualización en curso               %TEMP%\MuelleUpdate (el instalador y cómo se abrió Muelle); se borra un minuto después de instalada

Solución de problemas
---------------------

  Problema                                Qué hacer
  --------------------------------------- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  El dock no se ve                        Una aplicación a pantalla completa al frente lo oculta; con «Ocultar automáticamente» aparece al llevar el puntero al borde de la pantalla; revisa Configuración → Ubicación → «Mostrar el dock en:». Abrir Muelle otra vez mientras se ejecuta no hace nada: hay un solo dock.
  Una aplicación no se abre               Muelle avisa «No se pudo abrir» con el motivo que da Windows. Si el programa se movió o se desinstaló, quita su icono y agrégalo de nuevo.
  Un icono sigue con la imagen anterior   El dock busca iconos nuevos cada 15 segundos; si no cambia, quita el icono y agrégalo de nuevo.
  La configuración no funciona bien       Sal de Muelle y borra %APPDATA%\Muelle\config.json para empezar de cero, o cambia el nombre de config.previous.json a config.json para volver un guardado atrás («Abrir carpeta de configuración», en Configuración, abre la carpeta).
  Una actualización falló                 Muelle lo avisa y sigue en su versión; el registro del instalador queda en %TEMP%. Siempre puedes descargar el instalador de la página de la versión <https://github.com/Gabriel-Castiglia/muelle/releases/latest> y ejecutarlo manualmente.
  Otro problema                           Los detalles quedan en %APPDATA%\Muelle\muelle.log.

Limitaciones conocidas
----------------------

- Cuando Muelle reserva su franja de pantalla, Windows reacomoda los iconos del escritorio que
  quedaban debajo y puede pasarlos a otro monitor. Así trata Windows a cualquier barra de
  aplicaciones. Si te molesta, desactiva «Reservar su franja de pantalla»; las ventanas maximizadas
  pasan entonces por debajo del dock.

Compilar desde el código
------------------------

Con el SDK de .NET 10 <https://dotnet.microsoft.com/download> instalado:

    dotnet build src\Muelle\Muelle.csproj -c Release
    powershell -ExecutionPolicy Bypass -File scripts\build.ps1

El primer comando compila el programa. El segundo genera todo en dist: el Muelle.exe suelto,
muelle.msi y muelle-setup.exe, y muestra sus hashes SHA-256 sin escribir ningún archivo de hashes
(las sumas y la firma de una versión se publican aparte); el WiX Toolset 5 se descarga
automáticamente como paquete NuGet. Cada versión publicada tiene que llevar muelle-setup.exe,
SHA256SUMS.txt y release.sig con esos nombres, porque las actualizaciones los buscan.

Muelle.exe --self-test <registro> revisa el archivo de configuración, las pilas, los iconos por
pantalla, el bloqueo en Configuración, lo que ven los lectores de pantalla del dock y de los
colores, los iconos que siguen a su aplicación, la detección de aplicaciones a pantalla completa y
las funciones de actualización (comparación de versiones, hashes, la firma, el instalador abierto
mientras arranca, la línea de comandos del instalador), y escribe una línea por prueba en el
registro; su código de salida es el número de fallos. Trabaja sobre una carpeta de configuración
descartable en %TEMP% que borra al final, no muestra ventanas y no toca un Muelle abierto. Con
--online además descarga el instalador de la última versión y lo compara con su SHA256SUMS.txt y su
firma, sin ejecutarlo. La versión se fija en un solo lugar, src\Muelle\Muelle.csproj, y los
instaladores y la ventana Acerca de la leen de ahí.

scripts\screenshots.ps1 genera las imágenes de docs\images con aplicaciones de ejemplo (desde una
sesión de escritorio: por SSH la imagen del dock sale vacía), y scripts/manual.sh (con pandoc y
Google Chrome, en macOS o en Git Bash en Windows) genera los manuales en PDF y README.txt,
README.es.txt y README.ja.txt a partir de README.md, de este archivo y de README.ja.md.
scripts/make_icon.py dibuja el icono en sus doce colores a partir del grabado de assets/ (Python 3
con numpy, opencv-python y Pillow).

  Carpeta             Contenido
  ------------------- -----------------------------------------------------------------------------------------
  src/Muelle          El programa.
  installer/Msi       El paquete MSI (WiX).
  installer/SetupUI   La ventana del instalador, en inglés, castellano y japonés (.NET Framework 4.7.2, WPF).
  installer/Setup     El instalador que lleva esa ventana y el MSI (bundle de WiX).
  scripts             Compilación, imágenes, icono y manuales.
  assets              El grabado del que sale el icono.
  docs                Manuales en PDF, imágenes y la hoja de estilo del manual.

Historial de versiones
----------------------

Consulta CHANGELOG.es.md.

Licencia
--------

Copyright (C) 2026 Gabriel Castiglia.

Muelle es software libre: puedes redistribuirlo y modificarlo según los términos de la Licencia
Pública General GNU, solo la versión 2 (GPL-2.0-only), publicada por la Free Software Foundation. Se
distribuye SIN NINGUNA GARANTÍA. El texto completo está en LICENSE.

El icono es el barco de tres palos del grabado de Frans Huys según Pieter Bruegel el Viejo (Los
barcos de vela, hacia 1561-1565), Museo Metropolitano de Arte, dominio público (CC0).
