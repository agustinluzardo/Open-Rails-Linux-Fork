"""Add coarse startup stages and aggregate asset timings without changing I/O."""

from pathlib import Path
from shutil import copyfile


def patch_startup_timings(source: Path, replace_once) -> None:
    copyfile(Path(__file__).with_name("route-editor") / "RielStartup.h",
             source / "src/tsre/RielStartup.h")

    def instrument(relative, old, new):
        path = source / relative
        replace_once(path, old, new)
        text = path.read_text(encoding="utf-8")
        if '#include <tsre/RielStartup.h>' not in text:
            path.write_text('#include <tsre/RielStartup.h>\n' + text, encoding="utf-8")

    instrument("src/main.cpp", 'int main(int argc, char *argv[]){',
               'int main(int argc, char *argv[]){\n    RielStartup::clock();')
    instrument("src/tsre/world/Route.cpp", 'void Route::load(){',
               'void Route::load(){\n    RielStartup::Stage rielStage("route-load");')
    route = source / "src/tsre/world/Route.cpp"
    text = route.read_text(encoding="utf-8")
    start, end = text.index('void Route::load(){'), text.index('void Route::load(QString name){')
    old = text[start:end]
    new = old
    for name, statement in (
        ("trk", "trk->load();"),
        ("tsection", "this->tsection = new TSectionDAT();"),
        ("tdb", "this->trackDB->loadTdb();"),
        ("rdb", "this->roadDB->loadTdb();"),
        ("ref", "loadAddons();"),
        ("markers", "loadMkrList();"),
        ("services", "loadServices();"),
        ("traffic", "loadTraffic();"),
        ("paths", "loadPaths();"),
        ("activities", "loadActivities();"),
    ):
        if new.count(statement) != 1:
            raise SystemExit(f"Route startup stage {name}: unexpected source")
        new = new.replace(statement, '{ RielStartup::Stage stage("' + name + '"); ' + statement + ' }')
    replace_once(route, old, new)
    instrument("src/routeEditor/RouteEditorGLWidget.cpp", 'void RouteEditorGLWidget::initializeGL() {',
               'void RouteEditorGLWidget::initializeGL() {\n    RielStartup::Stage rielStage("opengl-init");')
    instrument("src/routeEditor/RouteEditorGLWidget.cpp", 'bool RouteEditorGLWidget::initRoute(){',
               'bool RouteEditorGLWidget::initRoute(){\n    RielStartup::Stage rielStage("route-init");')
    instrument("src/routeEditor/RouteEditorWindow.cpp", '    setProperty("rielMainShown", true);',
               '    setProperty("rielMainShown", true);\n    RielStartup::milestone("main-shown");')
    window = source / "src/routeEditor/RouteEditorWindow.cpp"
    replace_once(window, '        glWidget->setProperty("rielFirstFramePresented", true);',
                 '        glWidget->setProperty("rielFirstFramePresented", true);\n'
                 '        RielStartup::milestone("first-frame");\n'
                 '        RielStartup::summary("first-frame");\n'
                 '        QTimer::singleShot(5000, glWidget, [] { RielStartup::summary("after-5s"); });')
    for method in ("loadAll(QString gameRoot, bool gui)", "loadSimpleList(QString gameRoot, bool reload)"):
        instrument("src/tsre/trains/ConLib.cpp", f'int ConLib::{method}' + '{',
                   f'int ConLib::{method}' + '{\n    RielStartup::Stage rielStage("consists");')
    for relative, signature, work, active in (
        ("src/tsre/world/Tile.cpp", "void Tile::load() {", "WorldParse", "true"),
        ("src/tsre/shape/SFile.cpp", "void SFile::load() {", "ShapeParse", "true"),
        ("src/tsre/shape/SFileLegacy.cpp", "bool SFileLegacy::loadData() {", "ShapeParse", "loaded != 1"),
        ("src/tsre/shape/SFileComplex.cpp", "bool SFileComplex::loadData() {", "ShapeParse", "!d->loaded"),
        ("src/tsre/texture/TexLib.cpp", "bool decodeDetachedTexture(RielTextureLoadKind kind, Texture &texture, int quality) {", "TextureDecode", "true"),
        ("src/tsre/texture/Texture.cpp", "bool Texture::GLTextures(bool mipmaps) {", "GpuUpload", "loaded && !glLoaded"),
    ):
        instrument(relative, signature, signature + '\n    RielStartup::Measure rielMeasure(RielStartup::Work::'
                   + work + ', ' + active + ');')
