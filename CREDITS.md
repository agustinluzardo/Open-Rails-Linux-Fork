# Créditos de Riel Linux

Riel reúne trabajo propio de integración Linux con un motor y herramientas desarrollados por
otros proyectos. Los cambios de nombre y presentación conservan la autoría de ese trabajo.

## TSRE5 y TSRE5vc — Piotr Gadecki (GokuMK)

**Piotr Gadecki, conocido como GokuMK, es el creador de TSRE5 y TSRE5vc.** Sus herramientas son
la base de Riel Route Editor, Riel Consist Editor, Riel Shape Viewer y Riel ACE Converter.
Agradecemos a Piotr y a los colaboradores de TSRE por esos editores.

- [TSRE5vc: código y colaboradores](https://github.com/GokuMK/TSRE5vc).
- [TSRE5: proyecto original](https://github.com/GokuMK/TSRE5).
- [Web, manual y descargas](http://koniec.org/tsre5/).
- Commit upstream fijado por Riel:
  [`15a5d148a15ad305e14a9e602961210ad18ada74`](https://github.com/GokuMK/TSRE5vc/tree/15a5d148a15ad305e14a9e602961210ad18ada74).

TSRE5vc conserva el copyright de Piotr Gadecki y su licencia GNU GPL 3.0 o posterior.
Riel conserva las cabeceras originales, aplica la integración mediante
[`scripts/prepare-route-editor-source.py`](scripts/prepare-route-editor-source.py) y los helpers
de `scripts/route-editor/`, y adjunta el código exacto utilizado en cada release como
`riel-route-editor-source.tar.gz`. Ver [docs/route-editor.md](docs/route-editor.md).

## Free Train Simulator y Open Rails

El motor de Riel procede de [Free Train Simulator](https://github.com/perpetualKid/FreeTrainSimulator),
mantenido por **perpetualKid y sus colaboradores**, que a su vez deriva de
[Open Rails](https://github.com/openrails/openrails), desarrollado por su equipo y comunidad.

Se reconoce su trabajo en simulación, física, formatos MSTS, señales, tráfico AI, horarios,
material rodante, gráficos, sonidos y herramientas. Se mantienen los avisos de copyright del
código. El [README histórico](docs/UPSTREAM-README.md) se conserva como referencia; las
funciones de Riel se describen en su [README](README.md).

## Integración y mantenimiento de Riel

**Agustín Luzardo y los colaboradores de Riel** mantienen este repositorio, la integración
Linux, el launcher, la CLI, la adaptación de contenido, las correcciones, las pruebas y los paquetes.

Los autores de cambios individuales se registran en el historial Git y en los avisos de los
archivos. Ver [CONTRIBUTING.md](CONTRIBUTING.md).

## Bibliotecas y herramientas

Riel agradece también a los autores de estos componentes principales:

| Proyecto | Uso en Riel |
| --- | --- |
| [.NET](https://github.com/dotnet/runtime) | Runtime, bibliotecas y compilación. |
| [MonoGame](https://github.com/MonoGame/MonoGame) | Framework de juego y renderizado DesktopGL. |
| [SDL](https://github.com/libsdl-org/SDL) | Ventanas, eventos y sesión gráfica. |
| [OpenAL Soft](https://github.com/kcat/openal-soft) | Audio del simulador y los editores. |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Interfaz del launcher Linux. |
| [SkiaSharp / Skia](https://github.com/mono/SkiaSharp) | Dibujo y rasterizado de texto. |
| [Qt](https://www.qt.io/) | Interfaz y widgets OpenGL de TSRE5vc. |
| [Mesa / Zink](https://www.mesa3d.org/) | Controladores OpenGL y ejecución experimental sobre Vulkan. |

Cada dependencia conserva su propia licencia. Este reconocimiento no reemplaza sus avisos
ni las licencias distribuidas con ella.

## Autores del contenido

Las rutas, trenes, cabinas, señales, sonidos y texturas MSTS/OR pertenecen a sus respectivos
autores. Riel carga el contenido aportado por el usuario; el programa no incluye una colección
de rutas y trenes de terceros. El repositorio contiene contenido sintético de pruebas.

## Licencia

Riel y la integración de TSRE5vc se distribuyen bajo **GNU GPL 3.0 o posterior**. Ver
[LICENSE](LICENSE). Los avisos originales y las licencias de dependencias y contenido
permanecen vigentes.
