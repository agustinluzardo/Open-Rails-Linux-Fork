<p align="center"><img src="docs/linux/riel-linux-social.png" width="960" alt="Riel Linux — simulación ferroviaria nativa para Linux"></p>

# Riel Linux

**Simulador ferroviario y suite de edición para usar contenido de Microsoft Train Simulator en Linux.**

Riel permite conducir rutas, jugar actividades, explorar con el tren que elijas y operar servicios
por horario. Incluye un launcher gráfico, comandos de terminal, mapa del despachador y editores
nativos de rutas, formaciones, modelos y texturas. El simulador y los editores funcionan sin Wine.

El motor deriva de [Free Train Simulator](https://github.com/perpetualKid/FreeTrainSimulator) y
[Open Rails](https://github.com/openrails/openrails). La suite de edición deriva de
[TSRE5vc](https://github.com/GokuMK/TSRE5vc), creado por **Piotr Gadecki (GokuMK)**. Su trabajo y
el de todos los colaboradores de esos proyectos hacen posible Riel; ver [créditos](CREDITS.md).
Riel mantiene su integración Linux, interfaz, correcciones y releases en este repositorio.

[Descargar stable](https://github.com/agustinluzardo/Riel-Linux/releases/latest) ·
[Todas las releases](https://github.com/agustinluzardo/Riel-Linux/releases) ·
[Instalación](docs/linux/INSTALL.md) · [Cambios](CHANGELOG.md) ·
[Reportar un problema](https://github.com/agustinluzardo/Riel-Linux/issues)

## Descargar y empezar

La stable **0.1.3** reúne la suite de edición nativa, las correcciones de carga del editor y la
activación de Vulkan mediante Zink. Descargá `riel-linux-x64.zip` de la
[release stable](https://github.com/agustinluzardo/Riel-Linux/releases/latest), extraelo completo
y ejecutá:

```sh
cd riel-linux-x64
./riel gui
```

El ZIP incluye el runtime de .NET y el runtime de los editores. Para jugar no hace falta instalar
el SDK de .NET ni descargar paquetes NuGet. Se necesita Linux x86-64, un controlador OpenGL 3.3,
SDL 2, OpenAL, fontconfig, zlib y las bibliotecas del escritorio; los detalles por distribución
están en [INSTALL.md](docs/linux/INSTALL.md). En Wayland se usa XWayland para el launcher.

Agregá la carpeta de contenido que contiene `ROUTES` y `GLOBAL`, elegí una ruta y una actividad,
o pasá a **Explorar** para seleccionar recorrido y formación. Riel puede leer contenido de
otro disco y de carpetas de Wine o Proton; el contenido se usa desde esa ubicación.
Las rutas y los trenes se obtienen por separado y conservan las licencias de sus autores.

![Ilustración del launcher de Riel con ruta, actividad y detalles del tren](docs/linux/launcher-preview.svg)

La ilustración usa la ruta sintética de pruebas del repositorio. Al agregar tu contenido aparecen
tus rutas, actividades y trenes.

## Qué hace Riel

### Simulación y conducción

| Función | Qué permite hacer |
| --- | --- |
| Actividades | Cargar actividades MSTS, conducir el tren del jugador, atender paradas y compartir la ruta con tráfico AI. |
| Exploración | Elegir ruta, recorrido, locomotora y formación, con hora, estación del año y clima configurables. |
| Horarios | Seleccionar un conjunto de horarios, un servicio y el día de operación desde el launcher. |
| Material rodante | Usar locomotoras diésel, eléctricas y de vapor, vagones y coches definidos por el contenido. |
| Física | Simular tracción, adherencia, patinaje, frenos, acoples, resistencia, pendientes y curvas según el vehículo y los ajustes. |
| Operaciones | Acoplar y desacoplar vehículos, operar desvíos, revisar coches, controlar puertas y usar potencia distribuida cuando el tren la admite. |
| Señalización | Interpretar bases de vía, señales y scripts, límites de velocidad, autorizaciones y rutas de los trenes AI. |
| Cabinas y cámaras | Utilizar cabinas 2D y 3D disponibles en el material, vistas exteriores, cámaras de seguimiento y cámara libre. |
| Ambiente | Representar día y noche, cielo, niebla, lluvia, nieve, agua, vegetación y montañas lejanas según la ruta y la configuración. |
| Efectos | Mostrar humo, vapor, escape diésel, luces y otros emisores de partículas definidos por los vehículos. |
| Sonido | Reproducir sonidos de cabina, motor, vía y ambiente mediante OpenAL, con volumen y detalle configurables. |
| Guardado | Guardar partidas, reanudarlas desde el launcher y usar las funciones de replay del motor. La compatibilidad de partidas antiguas depende de la versión. |
| Evaluación | Registrar velocidad y paradas para evaluar el viaje, y exportar registros de física, rendimiento y conducción. |
| Controles | Configurar teclas y combinaciones con Ctrl, Shift y Alt; usar un escritorio RailDriver mediante hidraw en Linux. |
| Ayudas y scripts | Configurar alerter, control de velocidad, piloto automático y scripts TCS según la locomotora; cambiar de cabina y gestionar trenes en modo horario. |
| Servidor web | Habilitar el servidor web del motor y configurar su puerto desde los ajustes avanzados. |

La compatibilidad depende de los archivos de cada ruta y vehículo. Las prestaciones que
requieren equipo o scripts específicos sólo aparecen cuando ese contenido los ofrece.

### Launcher y gestión del contenido

- Listado y búsqueda de rutas, actividades, recorridos, locomotoras y formaciones.
- Pestañas de **Actividad**, **Explorar** y **Horario**, con información del recorrido y del tren.
- Horas sugeridas a partir de las salidas de actividades de la ruta.
- Varias carpetas de contenido, detección de instalaciones MSTS y lectura de ubicaciones
  configuradas en Wine o Proton.
- Escaneo del contenido al iniciar el launcher, caché de índices y reapertura tras una partida
  sin repetir un escaneo completo. También se puede forzar un reescaneo.
- Resolución de referencias sin distinguir mayúsculas y minúsculas, para usar contenido
  procedente de Windows en sistemas de archivos Linux.
- Lista de archivos o rutas que no pudieron leerse, con el motivo; el resto del contenido
  sigue disponible.
- Selección de partidas guardadas, prueba de actividades y acceso al manual y a contenido.
- Ajustes de conducción, audio, video, física, teclado, registro de datos, evaluación y opciones
  avanzadas, guardados en el perfil.
- Interfaz traducida al español, idioma según el sistema y tema claro u oscuro.
- Comprobación del equipo, mensajes de error con detalles y acceso a logs e informes de crash.
- Comprobación e instalación de actualizaciones de `main` que hayan superado el empaquetado y
  las pruebas automáticas. La descarga **stable** se publica por separado en Releases.

### Mapa y paneles durante la partida

El mapa del despachador muestra vías, trenes, estaciones, andenes, señales y desvíos. Permite
mover y ampliar la vista, centrar el mapa o seguir al tren del jugador. Al seleccionar un tren
se muestran velocidad, dirección, modo de control, formación y próxima parada; las señales
muestran su estado y las estaciones sus andenes.

En una partida local se pueden solicitar cambios de señal y desvío. El simulador rechaza cambios
de desvíos ocupados o reservados; los controles locales del despachador están deshabilitados
en multiplayer.

Los paneles incluyen ayuda, actividad, monitor de vía, conducción, operaciones del tren y de
coches, próxima estación, brújula, fuerzas, potencia distribuida y diagnóstico de señales.
Su disponibilidad depende de la partida y del vehículo.

| Tecla predeterminada | Acción |
| --- | --- |
| F1 | Ayuda y controles. |
| F2 | Guardar la partida. |
| F3 | Información del sistema y renderizador activo. |
| F4 | Monitor de vía. |
| F5 | Panel de conducción. |
| F9 | Operaciones de coches. |
| Ctrl + Alt + F9 | Operaciones del tren. |
| Ctrl + 9 | Mapa del despachador. |
| Escape | Menú de pausa. |
| Alt + F4 | Salir. |

Los atajos se pueden cambiar en **Configuración → Teclado**.

### Suite de edición nativa: TSRE5vc

Los cuatro programas están integrados en **Herramientas** y utilizan **TSRE5vc de Piotr Gadecki
(GokuMK)**, la evolución de su proyecto [TSRE5](https://github.com/GokuMK/TSRE5). Riel aporta
integración con el launcher, empaquetado Linux, perfil de ajustes, adaptación de rutas de archivos
y correcciones de estabilidad. Conserva los créditos y las cabeceras originales de copyright
y licencia.

| Herramienta | Función |
| --- | --- |
| Riel Route Editor | Crear y editar rutas MSTS/OR, terreno, vías y objetos con las herramientas de TSRE5vc. Abre la ruta seleccionada en el launcher. |
| Riel Consist Editor | Crear, inspeccionar y modificar formaciones usando el material de la carpeta seleccionada. |
| Riel Shape Viewer | Inspeccionar modelos `.s`, materiales y texturas. |
| Riel ACE Converter | Previsualizar y convertir texturas ACE con la herramienta de TSRE5vc; puede abrirse sin elegir una ruta. |

Los editores usan OpenGL 3.3 y un perfil propio. La integración contempla referencias con
mayúsculas mezcladas, carpetas `GLOBAL`/`Global` separadas, X/Y Tracks y texturas ACE/DDS.
La carga de texturas usa un número limitado de trabajadores; se sincroniza el log y se rechazan
los DDS truncados antes de decodificarlos. Los editores escriben el contenido cuando guardás cambios.

Cada release adjunta `riel-route-editor-source.tar.gz` con el código TSRE5vc exacto, incluidos
los cambios aplicados para construir esos binarios. Ver [route-editor.md](docs/route-editor.md).

### Gráficos, rendimiento y diagnóstico

- Renderizador MonoGame DesktopGL: monitor y resolución configurables, ventana, pantalla
  completa y ventana sin bordes.
- VSync, antialiasing compatible con el equipo, distancia de visión, detalle de modelos,
  instancing, brillo ambiente y sombras dinámicas con cascadas, resolución y suavizado ajustables.
- Cargas gráficas en el hilo propietario de OpenGL, servicio de la cola de recursos y
  priorización visual de trenes AI cercanos.
- **Vulkan experimental mediante Mesa Zink**, activable en Configuración y con reinicio del
  simulador. El motor emite OpenGL y Zink ejecuta esas órdenes sobre Vulkan.
- F3 identifica el renderizador real: **OpenGL over Vulkan (Zink)** cuando está activo.
  Solicitar Vulkan no garantiza que el controlador lo haya seleccionado.
- Información de sistema, rendimiento y temperaturas disponibles de CPU/GPU; sensores no
  disponibles o ambiguos aparecen como `n/a`.
- `riel doctor` comprueba binarios, shaders, bibliotecas de sonido y ventana, sesión gráfica y
  contenido; RailDriver se informa como hardware opcional.
- Logs, etapas de inicio e informes de crashes nativos, accesibles desde el launcher.
- Trazas de trenes AI, señales, pasos a nivel, resolución de rutas AI, sonido, luces, carga y
  partículas; diagnóstico de renderizado del editor.
- Pruebas automáticas del motor y de Riel, verificación de shaders, ventanas reales de ambos
  editores y comprobaciones de memoria del decoder DDS y del log concurrente.

El Vulkan experimental corresponde al simulador. Los editores conservan su OpenGL.
Ver [ARCHITECTURE.md](docs/linux/ARCHITECTURE.md) y [QA.md](docs/linux/QA.md).

## Comandos de terminal

En la descarga portable reemplazá `riel` por `./riel`. Los nombres de rutas, actividades,
recorridos y formaciones aceptan un prefijo único sin distinguir mayúsculas; usá comillas
si contienen espacios.

```sh
riel gui
riel content add "MSTS" "/mnt/datos/games/Train Simulator"
riel routes
riel activities "Marias Pass"
riel play "Marias Pass" "Coal Train"
riel paths "Marias Pass"
riel consists
riel explore "Marias Pass" "Shelby-Essex" "Freight" --time 08:30 --season autumn --weather rain
```

| Comando | Función |
| --- | --- |
| `riel content` | Listar carpetas de contenido. |
| `riel content add <nombre> <ruta>` | Agregar y escanear una carpeta. |
| `riel content remove <nombre>` | Quitar una carpeta de la configuración. |
| `riel content refresh` | Reescanear todas las carpetas. |
| `riel routes [carpeta]` | Listar rutas. |
| `riel activities <ruta>` | Listar actividades. |
| `riel paths <ruta>` | Listar recorridos del jugador. |
| `riel consists [carpeta]` | Listar formaciones. |
| `riel play <ruta> <actividad>` | Iniciar una actividad. |
| `riel explore <ruta> <recorrido> <formación>` | Explorar; admite hora, estación y clima. |
| `riel start` | Repetir la última selección. |
| `riel resume` | Reanudar la última partida guardada. |
| `riel run -- <argumentos>` | Pasar argumentos directamente al simulador. |
| `riel route-editor` | Abrir el editor de rutas; admite `--game-root` y `--route`. |
| `riel consist-editor` | Abrir el editor de formaciones. |
| `riel shape-viewer` | Abrir el visor de modelos. |
| `riel ace-converter` | Abrir el conversor ACE. |
| `riel doctor` | Comprobar equipo y contenido. |
| `riel update --check` | Consultar la última build probada de `main`. |
| `riel update` | Instalar esa build en una instalación actualizable. |
| `riel version` / `riel help` | Ver versión / ayuda. |

El modo por horario se selecciona en el launcher o mediante argumentos del motor;
la CLI no incorpora un comando abreviado `timetable`.

## Instalar en Arch o compilar

```sh
git clone https://github.com/agustinluzardo/Riel-Linux.git
cd Riel-Linux/packaging/arch
makepkg -si
```

El PKGBUILD compila e instala simulador, launcher y suite TSRE5vc, accesos del escritorio,
iconos, manual y regla udev de RailDriver. Sigue `main`; para una versión fija usá la descarga
stable. Ver dependencias y compilación manual en [INSTALL.md](docs/linux/INSTALL.md).

## Archivos y diagnóstico

| Ubicación predeterminada | Contenido |
| --- | --- |
| `~/.config/riel` | Ajustes y perfiles. |
| `~/.local/share/riel` | Guardados y datos del usuario. |
| `~/.local/state/riel/Logs` | Logs, `Startup.log` e informes en `Crashes`. |
| `~/.cache/riel` | Índices de contenido. |
| `~/.local/share/Riel/RouteEditor` | Ajustes y assets del editor. |
| `~/.local/state/riel/Logs/Riel Route Editor Log.txt` | Log de la suite de edición. |

Las rutas respetan las variables XDG correspondientes. Para informar un fallo, adjuntá
`riel doctor`, versión, log y ruta/actividad afectada en
[Issues](https://github.com/agustinluzardo/Riel-Linux/issues). Para problemas visuales, indicá
el renderizador que muestra F3 y si afecta toda la pantalla, el paisaje o el mapa del despachador.

## Estado y compatibilidad

- La distribución apunta a **Linux x86-64**. El portable incluye .NET; el paquete de Arch
  usa el runtime instalado por la distribución.
- WPF Toolbox y TrackViewer permanecen como herramientas Windows en el código histórico y
  no forman parte del paquete Linux. La suite TSRE5vc ofrece los editores distribuidos aquí.
- El servidor multiplayer se compila y se incluye, pero el flujo multiplayer no está validado
  en Linux ni expuesto como modo de juego en el launcher.
- Algunas rutas mantienen diferencias de tráfico AI, señales y pasos a nivel frente a Open Rails.
  La señal entre Quilmes y Ezpeleta en FCGR puede verse verde mientras la ruta la trata como roja.
- Los pantallazos negros comunicados durante la partida están en investigación; la 0.1.3 no
  se presenta como una corrección confirmada de ese problema.
- Las bibliotecas públicas renombradas usan `Riel.*`. Los add-ons compilados contra
  `FreeTrainSimulator.*` deben recompilarse y los scripts que importen esos espacios de nombres
  deben adaptarse. Las partidas anteriores al cambio no están completamente verificadas.

## Créditos y licencia

**Piotr Gadecki (GokuMK)** creó TSRE5 y TSRE5vc, base del Route Editor, Consist Editor,
Shape Viewer y ACE Converter. [TSRE5vc](https://github.com/GokuMK/TSRE5vc) ·
[TSRE5 original](https://github.com/GokuMK/TSRE5) · [Web y manual de TSRE](http://koniec.org/tsre5/).

Gracias también a **perpetualKid y los colaboradores de Free Train Simulator**, al **equipo y
colaboradores de Open Rails**, y a los autores de rutas, trenes, sonidos y texturas de la comunidad
MSTS/OR. [CREDITS.md](CREDITS.md) detalla procedencia y bibliotecas.

Riel se distribuye bajo **GPL-3.0-or-later**. Conserva las cabeceras originales y sus avisos de
copyright; las dependencias y el contenido tienen sus licencias correspondientes.
Ver [LICENSE](LICENSE), [CONTRIBUTING.md](CONTRIBUTING.md) y el
[README histórico de Free Train Simulator](docs/UPSTREAM-README.md).
