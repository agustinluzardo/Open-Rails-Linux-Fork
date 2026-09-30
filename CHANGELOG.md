# Cambios de Riel Linux

Las versiones stable y sus archivos están en
[Releases](https://github.com/agustinluzardo/Riel-Linux/releases). Las builds `main-<commit>`
son prereleases que superaron las pruebas y el empaquetado automático.

## 0.1.3

### Suite de edición

- Integra Route Editor, Consist Editor, Shape Viewer y ACE Converter nativos, basados en
  TSRE5vc de **Piotr Gadecki (GokuMK)**, con runtime, iconos y código fuente correspondiente.
- Abre la ruta y carpeta de contenido seleccionadas en el launcher y utiliza un perfil propio.
- Corrige la corrupción de memoria causada por escrituras concurrentes al log del editor.
- Valida dimensiones y datos DDS antes de decodificar; los archivos truncados se rechazan.
- Resuelve referencias con mayúsculas mezcladas, `GLOBAL`/`Global` separados, X/Y Tracks,
  modelos y texturas de la ruta.
- Limita caché de directorios y trabajadores de texturas, preservando la identidad de modelos
  y su contexto de texturas.
- Corrige arranque OpenGL, recursos, selección de formaciones y zoom de previsualización ACE.
- Prueba ventanas principales reales, modelos y texturas subidas a GPU; verifica decoders y
  logging concurrente con sanitizers.

### Simulador y launcher

- Activa el Vulkan experimental de Configuración en el entorno nativo antes de crear el
  contexto; el renderizador OpenGL se ejecuta mediante Mesa Zink.
- F3 informa el renderizador activo y temperaturas disponibles de CPU/GPU.
- Amplía ajustes y diagnósticos e incorpora accesos a la suite de edición.
- Reescanea contenido al iniciar el launcher y evita repetir el escaneo al volver de una partida.
- Ajusta señales y límites del monitor de vía al rango visible.

### Estado conocido

- Zink sigue siendo experimental y no cambia el renderizador de los editores.
- Multiplayer no tiene un flujo validado desde el launcher Linux.
- Los pantallazos negros durante la partida siguen en investigación.
- En FCGR, la señal entre Quilmes y Ezpeleta puede mostrarse verde aunque se trate como roja.
- Los add-ons compilados contra `FreeTrainSimulator.*` requieren adaptación a `Riel.*`.
  La compatibilidad de guardados antiguos no está completamente verificada.

## 0.1.2

- Corrige el índice circular de emisores y restaura el stride de 80 bytes de los vértices.
- Recupera buffers dinámicos de partículas invalidados por DesktopGL.
- Corrige el shader billboard de DesktopGL para que humo, vapor y otros emisores no colapsen
  a polígonos de área cero.
- Agrega diagnósticos de partículas y escape diésel en Configuración.

## 0.1.1

- Aplica una corrección intermedia al layout de vértices de partículas para OpenGL.
- La corrección completa del billboard llega en 0.1.2.

## 0.1.0

- Primera stable Linux de Riel, con runtime .NET incluido.
- Renombra las bibliotecas públicas a `Riel.*` y adapta launcher y actualizaciones.
- Endurece las pruebas de actividades para registrar fallos y continuar el lote.
- Mejora la carga visual de trenes AI cercanos y agrega diagnósticos.
- Incorpora información del sistema con F3 y lectura de temperaturas en Linux.
