# Historial de versiones

Todos los cambios importantes de Muelle. Las versiones siguen el [versionado semántico](https://semver.org/lang/es/):
mayor.menor.parche.

[English](CHANGELOG.md) · Español · [日本語](CHANGELOG.ja.md)

## 2.5.1 - 2026-11-26

### Corregido

- Con «Bloquear dock» marcado, un clic en el lugar «+» de un dock vacío igual abría el diálogo de archivos y
  agregaba apps; ahora no hace nada, como las otras formas de agregar.
- Un `.ico` elegido como icono de una pila se lee directo del archivo, como cualquier otro icono: un archivo que
  cambió mostraba la imagen vieja que guarda la caché de iconos de Windows.
- Una ventana de configuración abierta al arrancar (`Muelle.exe --settings`) podía mostrar «Iniciar Muelle al
  encender Windows» como estaba guardado la última vez, y no como dicen la tarea de inicio o la clave Run; ahora se
  actualiza en cuanto Muelle lo averigua.
- La ventana de actualización cambia el icono de su encabezado cuando se cambia el color, como la ventana Acerca de.
- Los README y los manuales decían que el dock pasa por detrás de una aplicación a pantalla completa (desde la 2.3.1
  se oculta del todo) y que «Buscar actualizaciones» y la apariencia están en el menú del dock (desde la 2.4.0 están
  en Acerca de Muelle… y en Configuración).

### Cambiado

- Los manuales en PDF usan los colores claros de Muelle y su acento azul, con un icono más grande en la portada.
- El historial de versiones también está en castellano y en japonés (`CHANGELOG.es.md`, `CHANGELOG.ja.md`).
- Los README mencionan `Muelle.exe --settings` y todo lo que revisa la autoprueba; los textos en castellano escriben
  «icono» en todos lados y usan el voseo en la indicación del dock vacío, como el resto del programa.
- Limpieza del código: el diálogo para agregar apps, los botones y textos atenuados de las ventanas, abrir un archivo
  o una página web, mostrar un archivo en el Explorador, leer un icono elegido y los nombres de las pantallas se
  escriben una sola vez y se comparten; se quita el código que no se usaba, incluido `scripts/make-icon.ps1`, que
  dibujaba el icono de antes de la 2.1.0 y habría pisado el actual.
- La autoprueba también revisa el icono de una pila y el diálogo para agregar apps.

## 2.5.0 - 2026-11-26

### Agregado

- «Iniciar Muelle al encender Windows» reconoce una tarea del Programador de tareas armada a mano que arranca Muelle
  (`\Muelle (inicio de sesion)` o `\Muelle`): la casilla la muestra, desmarcarla deshabilita la tarea y marcarla la
  vuelve a habilitar, y no se agrega además la clave Run, así Muelle nunca arranca dos veces. La tarea se busca fuera
  del hilo de la interfaz, así el dock no arranca más tarde.
- README y manual en PDF en japonés (`README.ja.md`, `docs/Muelle-manual-ja.pdf`), con imágenes de las ventanas en
  japonés; el instalador pone `README.ja.txt` junto a los otros README. Al armar los manuales se comprueba también la
  versión que figura bajo el título del README en japonés.
- La autoprueba comprueba que la tarea de inicio se puede buscar.

### Cambiado

- Dibujo más liviano: la sombra de los iconos ya no se aplica al área del puntero, que ocupa todo el largo de la
  pantalla, sino solo a lo que se dibuja; la etiqueta con el nombre se arma una vez por nombre en lugar de en cada
  cuadro, y los fondos de los iconos sin imagen se hacen una sola vez.
- Las imágenes de las ventanas en los manuales se vuelven a tomar, así Configuración muestra «Bloquear dock».

## 2.4.2 - 2026-11-25

### Cambiado

- **Actualizaciones firmadas por el autor**: el `SHA256SUMS.txt` de una versión ahora tiene que llevar la firma del
  autor (`release.sig`, ECDSA P-256, comprobada con dos claves que trae Muelle) antes de que se baje el instalador.
  Antes, el instalador solo se comparaba con hashes que venían de la misma página, así que una versión puesta ahí
  por otra persona se habría instalado sola. Una versión sin firma válida se ofrece como página, para bajarla a mano.
  Cualquiera puede seguir bajando, instalando y compartiendo Muelle como antes. Muelle 2.4.1 y anteriores instalan
  esta como siempre.

### Corregido

- Configuración decía «Bloquear los iconos» mientras el menú del clic derecho dice «Bloquear dock», y la opción
  bloquea más que el orden de los iconos: ahora es «Bloquear dock» en los dos lugares, y su texto nombra lo que
  impide (agregar, quitar, reordenar, fijar y renombrar iconos).
- Los manuales (PDF) y el `README.txt` que se instala junto a `Muelle.exe` describían la versión 2.3.1 desde la
  2.3.2: se vuelven a armar a partir de los README de esta versión. Ahora el armado de los manuales se detiene cuando
  la versión escrita bajo el título de un README no es la del programa.

## 2.4.1 - 2026-11-25

### Corregido

- Un icono que no se puede leer ya no vacía el dock. Antes, un solo icono dañado en `config.json` (un campo de otro
  tipo después de editarlo a mano, o al volver a una versión anterior) descartaba la lista entera, y los guardados
  siguientes dejaban vacío también `config.previous.json`. Ahora se descarta solo ese icono (o ese acceso de una
  pila); lo descartado se guarda en `config.dropped-<fecha>.json` y el archivo completo en
  `config.partial-<fecha>.json`, y `config.previous.json` conserva la versión que todavía lo tenía.
- La configuración se escribe hasta el disco antes de reemplazar el archivo: un corte de luz justo después de
  guardar deja el archivo nuevo o el viejo.
- La búsqueda diaria de actualizaciones cuenta solo cuando llegó a GitHub. Una notebook que arrancaba sin red
  perdía la búsqueda de ese día; ahora vuelve a probar una hora después.
- Aceptar «Renombrar…» sin cambiar el nombre ya no lo deja fijo: el nombre de una app predeterminada sigue el idioma
  de la interfaz.
- Una pila que se quitó (o que quedó en una lista que ya no se muestra) mientras su editor estaba abierto ya no
  vuelve al apretar «Guardar»: el editor lo avisa y no cambia nada.
- Un dock que se cierra (al conectar o desconectar pantallas, o con un dock por pantalla) ya no vuelve a reservar
  su franja de pantalla un momento después.
- Una app instalada con ClickOnce (`.appref-ms`) conserva su punto de «abierta» después de actualizarse sola.
- El instalador descargado en `%TEMP%\MuelleUpdate` se borra después de que se instala una actualización.

### Cambiado

- El dock ya no recorre los accesos directos del menú Inicio, el escritorio y la barra de tareas cada 15 segundos:
  vigila esas carpetas y las vuelve a leer solo cuando algo cambia (y cada 5 minutos, por las dudas). Las fechas de
  los archivos de los iconos se leen fuera del hilo de la ventana, así un icono en una carpeta de red que no
  responde ya no congela el dock.
- La lista de pantallas se guarda hasta que Windows avisa que cambiaron, en lugar de preguntarla cada segundo.
- El menú del clic derecho de una app de la Tienda (Teams, Terminal…) abre más rápido: ya no abre todos los procesos
  del sistema para saber si corresponde «Finalizar tarea».

## 2.4.0 - 2026-11-24

### Agregado

- Japonés. La interfaz, la ventana del instalador y los nombres de los colores y de las apps predeterminadas están en
  inglés, castellano y japonés, en ese orden en Configuración y en la ventana del instalador.
- «Renombrar…» en el menú de un icono fijo. Un nombre vacío vuelve al nombre del programa.

### Cambiado

- El menú del clic derecho de un icono es corto: Abrir otra ventana (o Abrir, si la aplicación está cerrada),
  Renombrar…, Fijar al dock o Liberar del dock, y Más (Ejecutar como administrador, Abrir ubicación del archivo, Mover
  a la pila); después Bloquear dock, Configuración… y Salir de Muelle. Finalizar tarea va al final y aparte, solo
  mientras la aplicación está abierta. El clic derecho en una parte vacía del dock ofrece Agregar aplicación…, Nueva
  pila…, Bloquear dock, Configuración… y Salir de Muelle. El estilo, el idioma, la apariencia, las actualizaciones y
  Acerca de están solo en Configuración.
- «Bloquear iconos» ahora es «Bloquear dock», y además desactiva fijar, liberar, renombrar y agregar desde el menú.
- La ventana del instalador arranca en inglés, salvo que Muelle ya esté instalado en otro idioma; antes tomaba el
  idioma de Windows cuando era castellano.

### Corregido

- Un icono sigue más cambios sin reiniciar el dock: apps de la Tienda que se actualizan solas (Teams, Terminal…), un
  acceso directo cuyo icono se cambia en sus propiedades, y un icono elegido en la configuración cuyo archivo cambia.
  Antes solo se notaba un cambio del archivo del programa o de su icono de tema.

## 2.3.2 - 2026-11-24

### Corregido

- Un acceso directo con un icono elegido a mano lo vuelve a mostrar. Desde la 2.3.0 el dock tomaba el icono del
  programa que abre el acceso, de los accesos del propio programa, y dejaba de lado el elegido para ese acceso: un
  acceso que abre un documento con un icono rojo mostraba el icono del programa. Ahora gana el icono elegido para un
  acceso (en sus propiedades, desde fuera de la carpeta del programa), y el icono propio del programa se sigue solo
  cuando no hay ninguno.
- Un icono elegido en la configuración (`Icon` de un elemento en `config.json`) ahora vale para cualquier icono, no
  solo para las pilas, y gana sobre todos los demás.

### Cambiado

- La configuración nunca vuelve a los valores predeterminados. Un ajuste que una versión no puede leer (un valor
  agregado por una versión más nueva, una entrada dañada) se descarta solo y se conservan todos los demás; antes, un
  valor así hacía que Muelle arrancara con todo en sus valores predeterminados, y el guardado siguiente los escribía
  encima de los del usuario. Un archivo que no se puede leer en absoluto se guarda aparte como
  `config.unreadable-<fecha>.json` y se lee en su lugar el archivo anterior, `config.previous.json` (escrito antes de
  cada guardado, nunca a partir de un archivo dañado).
- Los ajustes escritos por una versión más nueva se conservan y se vuelven a escribir tal como estaban, así volver a
  una versión anterior y después avanzar otra vez no pierde nada.
- Cuando una versión distinta de Muelle lee la configuración por primera vez (después de una actualización), primero
  guarda una copia como `config.before-<versión>.json`.

## 2.3.1 - 2026-11-23

### Corregido

- El dock ya no aparece encima de un juego o un video a pantalla completa. Windows solo le avisa a una barra de
  aplicaciones que hay una app a pantalla completa al frente, y el dock no lo es cuando se oculta solo o no reserva
  su franja: quedaba encima de todo, y un juego que desplaza la vista cuando el puntero llega al borde lo hacía
  aparecer una y otra vez. Ahora el dock se fija por su cuenta cada segundo y cada vez que otra ventana pasa al
  frente: si esa ventana cubre toda su pantalla sin barra de título, el dock se oculta del todo, también para el
  puntero en el borde, y vuelve cuando la app sale de pantalla completa u otra ventana pasa al frente. Una ventana
  maximizada con barra de título no cuenta.

## 2.3.0 - 2026-11-21

### Agregado

- Los iconos siguen a sus apps. Cada 15 segundos el dock compara las fechas de los archivos de los que leyó sus
  iconos, y vuelve a leer el icono de una app que se actualizó o que cambió su propio icono, sin reiniciarse. Antes,
  un icono nuevo aparecía solo después de reiniciar Muelle.
- Para un programa, el icono que nombran sus propios accesos directos (menú Inicio, escritorio, barra de tareas),
  cuando ese `.ico` está en la carpeta del programa. Vigía escribe ahí el icono del color que se le eligió y apunta
  sus accesos a él; el dock, que apunta a `Vigia.exe`, mostraba el icono que trae el programa. El archivo se lee
  directo, porque la caché de iconos de Windows guarda la imagen vieja de una ruta cuyo archivo cambió. Un icono
  elegido a mano desde otro lado no se toca.

## 2.2.0 - 2026-11-20

### Agregado

- Actualizaciones que se instalan solas. Una vez por día (si la opción está marcada) y con «Buscar actualizaciones»
  (menú del dock y Acerca de Muelle…), Muelle le pregunta a GitHub por su última versión publicada y, si es más
  nueva, hace una sola pregunta: Actualizar ahora, Ver novedades o Más tarde. Después todo pasa solo: se baja el
  instalador de esa versión y se compara con el `SHA256SUMS.txt` de la versión (un archivo que no coincide se borra
  y nunca se ejecuta), Muelle se cierra, el instalador instala en silencio conservando el idioma elegido al instalar,
  y Muelle se vuelve a abrir con los mismos argumentos. Si algo falla, Muelle sigue o vuelve y dice qué pasó. Una
  copia que corre sin instalación solo ofrece la página de la versión.
- El instalador acepta `/muellewaitpid=<pid>` (espera a que ese Muelle se cierre antes de tocar cualquier archivo) y
  `/muellerelaunch=1` (vuelve a abrir Muelle al terminar, diciendo si la actualización entró), que las
  actualizaciones usan junto con `/quiet`.
- Pilas: un icono del dock que despliega varios accesos a su lado, como las pilas del Dock de la Mac. Se crean con
  «Nueva pila…» (menú del dock o Configuración), con un nombre, un icono propio o sus primeros accesos en
  cuadrícula, y accesos que se agregan, quitan y reordenan en el editor de la pila. «Mover a la pila», en el menú de
  un icono, lo pone en una pila.
- Cada pantalla con sus propios iconos (Configuración → Ubicación). Una pantalla nueva empieza con una copia de los
  iconos comunes; las pantallas se reconocen por el nombre de dispositivo del monitor y su conector (o por el nombre
  de pantalla y la resolución), y los iconos de una pantalla desconectada se guardan y vuelven con ella. Apagado de
  entrada: todas las pantallas muestran el mismo dock, como antes.
- `Muelle.exe --self-test <registro>`: revisa el archivo de configuración, las pilas, los iconos por pantalla y las
  funciones de actualización sobre una configuración descartable, sin ventanas; el código de salida es la cantidad
  de fallas. `--online` además compara el instalador de la última versión con sus hashes, sin ejecutarlo.
- `scripts\build.ps1` escribe `SHA256SUMS.txt` junto a los instaladores.

### Cambiado

- Las imágenes de los manuales muestran apps de ejemplo y una pila, tomadas con la configuración predeterminada
  (`--sample`), nunca las apps de quien las genera; hay una imagen nueva de una pila abierta.
- `scripts/manual.sh` también corre en Git Bash en Windows (Chrome se toma de `CHROME`).
- Los campos de texto siguen la apariencia clara u oscura.

## 2.1.0 - 2026-11-11

### Agregado

- Claro, oscuro o como Windows. El dock pasa a ser de vidrio claro esmerilado o de vidrio oscuro, y las ventanas de
  configuración, Acerca de y aviso de versión nueva lo siguen, barra de título incluida. «Como Windows» cambia con
  Windows mientras Muelle corre. Se elige en Configuración → Apariencia o desde el menú del dock. Un archivo de
  configuración de una versión anterior conserva el dock oscuro; una instalación nueva sigue a Windows.
- Doce colores. El elegido pinta el punto de las apps abiertas, los interruptores, los controles deslizantes y lo
  seleccionado, y el icono de las ventanas de Muelle.
- Aviso de versiones nuevas: una vez por día Muelle le pregunta a GitHub el número de la última versión y, si es más
  nueva, ofrece abrir la página de descarga. Se puede apagar en Configuración → Comportamiento; Acerca de Muelle…
  pregunta cuando se le pide.
- `SHA256SUMS.txt` y los manuales en PDF en cada versión publicada.

### Cambiado

- Un icono nuevo: el barco de tres palos armado del grabado de Frans Huys según Pieter Bruegel el Viejo (Museo
  Metropolitano de Arte, dominio público), dibujado por `scripts/make_icon.py` en los doce colores.
- La ventana de configuración: tarjetas redondeadas, interruptores en lugar de casillas, y sus propias listas
  desplegables, controles deslizantes y barras de desplazamiento. El menú del dock tiene el mismo aspecto
  redondeado.
- El README y los manuales dicen que es solo para x64, y enlazan a la última versión publicada.

### Corregido

- La ventana del instalador mostraba un icono borroso: tomaba el primer cuadro del archivo del icono en lugar del
  más grande.
- Un error mientras Muelle arranca ahora se escribe en `muelle.log`; antes solo llegaba al registro de eventos de
  Windows.

## 2.0.2 - 2026-11-09

### Corregido

- Los datos de depuración incluidos en la ventana del instalador nombraban la carpeta donde se compiló; ahora los
  archivos de código se nombran desde la raíz del repositorio.

## 2.0.1 - 2026-11-08

### Agregado

- `README.txt` y `README.es.txt`, copias en texto plano de los README para leerlas sin un visor de Markdown.
  `scripts/manual.sh` las arma junto con los manuales en PDF, y el instalador las pone junto a `Muelle.exe`.

### Corregido

- Las propiedades de `muelle-setup.exe` decían «Todos los derechos reservados»; ahora dan la licencia
  GPL-2.0-only, como el resto de Muelle.

## 2.0.0 - 2026-11-08

### Agregado

- Interfaz en inglés y castellano. El idioma se elige desde el menú del dock (Idioma / Language) o en
  Configuración, y queda guardado; el primer arranque usa el idioma elegido al instalar, o inglés.
- Instalador con ventana propia en inglés y castellano, elegible desde un menú en cualquier momento. La misma ventana
  se abre para desinstalar desde «Aplicaciones instaladas» de Windows, y ofrece reparar cuando Muelle ya está
  instalado. Cierra un Muelle abierto antes de reemplazar o quitar sus archivos.
- El desinstalador borra la entrada de «Iniciar con Windows» del usuario que desinstala.
- README y manual en PDF en inglés y castellano, con imágenes de cada ventana.
- `scripts\screenshots.ps1`, que genera imágenes de las ventanas para los manuales sin mostrarlas.

### Cambiado

- Licencia: GNU GPL solo versión 2 (GPL-2.0-only); la versión 1.0 era GPL-3.0-or-later.
- Las apps de la Tienda muestran su icono más nítido: el dock lee el paquete de la app y elige el logo más grande o
  el icono de su ejecutable, en lugar del chico que Windows le da a la barra de tareas.
- Corre sobre .NET 10 (la versión 1.0 usaba .NET 8, cuyo soporte termina el 10 de noviembre de 2026).
- Los nombres de las aplicaciones que se agregan en el primer arranque siguen el idioma de la interfaz, y se agrega
  Windows Terminal cuando está (si no, Windows PowerShell).
- Un solo recorrido de las ventanas abiertas por segundo, compartido por todos los monitores, en lugar de uno por
  ventana de dock.
- La configuración se escribe en el disco poco después del último cambio, no en cada paso de un control deslizante.
- La ventana de configuración adapta sus filas a su ancho y ya no corta etiquetas ni valores.
- Los límites del tamaño de los iconos en el archivo de configuración coinciden con la ventana de configuración (de
  24 a 96 px).
- El archivo de registro se mantiene por debajo de 512 KB.
- La versión se fija solo en `src/Muelle/Muelle.csproj`; los instaladores y la ventana Acerca de la leen de ahí.
- Nueva organización del repositorio (`src`, `installer`, `scripts`, `docs`), archivos UTF-8 con finales de línea LF
  y un solo formato de encabezado de licencia.

### Corregido

- Arrastrar fuera del dock una aplicación abierta que no está fijada mostraba la animación de quitar, pero el icono
  se quedaba.
- Cerrar una ventana del dock desde afuera (el instalador, Alt+F4) dejaba el programa corriendo sin ventanas, así
  su ejecutable seguía en uso durante una actualización.
- Desinstalar dejaba la entrada de «Iniciar con Windows» apuntando a un archivo que ya no existía.
- Los ejecutables se abren solo como archivos de recursos cuando se leen sus iconos.

## 1.0.0 - 2026-11-06

Primera versión: dock flotante (estilo Mac) y panel fijo (estilo Ubuntu) en cualquier borde de la pantalla; un dock
por monitor o un monitor elegido; indicadores de apps abiertas y apps abiertas no fijadas; apps de la Tienda y
objetos del sistema; arrastrar y soltar para agregar, reordenar y quitar; bloqueo de iconos; Finalizar tarea;
ocultar automáticamente; iniciar con Windows; ventanas de configuración y Acerca de; paquete MSI e instalador
hechos con WiX Toolset 5. Interfaz solo en castellano.
