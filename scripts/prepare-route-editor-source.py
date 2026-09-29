#!/usr/bin/env python3
"""Apply Riel integration patches to a pinned TSRE5vc checkout.

The editor remains GPL-3.0-or-later software by Piotr Gadecki/GokuMK. This
script changes product integration/branding only; original copyright/license
headers stay intact.
"""

from __future__ import annotations

import argparse
from pathlib import Path


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one occurrence of {old!r}, found {count}")
    path.write_text(text.replace(old, new), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    source = args.source.resolve()

    game = source / "src" / "tsre" / "Game.cpp"
    game_header = source / "src" / "tsre" / "Game.h"
    replace_once(game, 'QString Game::AppName = "TSRE5";', 'QString Game::AppName = "Riel";')
    replace_once(game, 'QString Game::AppVersion = "v" TSRE5_VERSION;', 'QString Game::AppVersion = TSRE5_VERSION;')
    replace_once(game, 'QString Game::root = "C:/tsdata/Train Simulator/";', 'QString Game::root = "";')
    replace_once(game, 'QString Game::route = "bbb1";', 'QString Game::route = "";')
    replace_once(
        game_header,
        '    static void InitAssets();',
        '    static QString AssetsPath(const QString &name);\n'
        '    static bool DownloadAsset(const QString &name);\n'
        '    static void InitAssets();',
    )
    replace_once(game, '#include <QDir>', '#include <QDir>\n#include <QStandardPaths>')
    replace_once(
        game,
        'void Game::InitAssets() {',
        'QString Game::AssetsPath(const QString &name) {\n'
        '    if (!qEnvironmentVariableIsSet("RIEL_EDITOR_BUNDLE_DIR"))\n'
        '        return QDir::current().filePath("assets/" + name);\n'
        '    const QString dataRoot = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation);\n'
        '    return QDir(dataRoot).filePath("Riel/RouteEditor/assets/" + name);\n'
        '}\n\n'
        'bool Game::DownloadAsset(const QString &name) {\n'
        '    return downloadResourceDirectory(QFileInfo(AssetsPath(name)).absolutePath(), name);\n'
        '}\n\n'
        'void Game::InitAssets() {',
    )
    replace_once(
        game,
        'void Game::InitAssets() {\n'
        '    QString path;',
        'void Game::InitAssets() {\n'
        '    // The Riel package supplies appdata and editor commands. Avoid writing\n'
        '    // launchers into a root-owned installation or downloading optional\n'
        '    // TSRE resources on every start of an installed editor.\n'
        '    if (qEnvironmentVariableIsSet("RIEL_EDITOR_BUNDLE_DIR")) {\n'
        '        if (!QDir("appdata/" + AppDataVersion).exists())\n'
        '            qCritical() << "Riel editor appdata is missing";\n'
        '        return;\n'
        '    }\n'
        '    QString path;',
    )

    settings = source / "src" / "settings" / "SettingsProfile.cpp"
    replace_once(
        settings,
        'return QDir(base).filePath("TSRE");',
        'return QDir(base).filePath("Riel/RouteEditor");',
    )

    # Keep user-generated editor assets outside the read-only /opt or Arch
    # installation. New routes can then fetch their template on demand, and
    # geographic presets can be extracted next to the user's own catalogues.
    creator = source / "src" / "tsre" / "world" / "RouteCreator.cpp"
    replace_once(
        creator,
        'return QDir::current().absoluteFilePath(\n'
        '            QStringLiteral("assets/templateRoute_0.6"));',
        'return Game::AssetsPath(QStringLiteral("templateRoute_0.6"));',
    )
    load_window = source / "src" / "routeEditor" / "LoadWindow.cpp"
    replace_once(
        load_window,
        '    const QString path = "./assets/templateRoute_0.6";',
        '    const QString path = Game::AssetsPath("templateRoute_0.6");',
    )
    replace_once(
        load_window,
        'void LoadWindow::downloadTemplateRoute(QString path){',
        'void LoadWindow::downloadTemplateRoute(QString path){\n'
        '    if (qEnvironmentVariableIsSet("RIEL_EDITOR_BUNDLE_DIR")) {\n'
        '        Q_UNUSED(path);\n'
        '        if (!Game::DownloadAsset("templateRoute_0.6"))\n'
        '            qWarning() << "Riel route template download failed";\n'
        '        return;\n'
        '    }',
    )
    geo_presets = source / "src" / "tsre" / "geo" / "GeoPresetData.cpp"
    replace_once(
        geo_presets,
        '    return QStringLiteral("assets/geo/geo_cities_presets.txt");',
        '    return Game::AssetsPath(QStringLiteral("geo/geo_cities_presets.txt"));',
    )
    for relative, filename in (
        ("src/tsre/geo/ElevationSource.cpp", "elevation-datasets.json"),
        ("src/tsre/geo/ImagerySource.cpp", "imagery-datasets.json"),
    ):
        catalogue = source / relative
        replace_once(catalogue, '#include <QDir>', '#include <QDir>\n#include <tsre/Game.h>')
        replace_once(
            catalogue,
            f'    return QStringLiteral("assets/geo/{filename}");',
            f'    return Game::AssetsPath(QStringLiteral("geo/{filename}"));',
        )

    main = source / "src" / "main.cpp"
    # Upstream always switches to the ELF's directory. Riel keeps the ELF in
    # bin/ and ships appdata/, the splash/icon and startup-args.txt beside bin/.
    # Without this, shaders disappear and the editor silently tries to fetch
    # appdata over the network even though it was included in the package.
    replace_once(
        main,
        '        QDir::setCurrent(executable.absoluteDir().absolutePath());',
        '        const QDir configuredBundle(qEnvironmentVariable("RIEL_EDITOR_BUNDLE_DIR"));\n'
        '        QDir executableDir = executable.absoluteDir();\n'
        '        const QDir bundleDir(executableDir.filePath(".."));\n'
        '        if (qEnvironmentVariableIsSet("RIEL_EDITOR_BUNDLE_DIR")\n'
        '                && configuredBundle.exists("appdata/" + Game::AppDataVersion))\n'
        '            QDir::setCurrent(configuredBundle.absolutePath());\n'
        '        else if (executableDir.dirName() == "bin"\n'
        '                && bundleDir.exists("appdata/" + Game::AppDataVersion))\n'
        '            QDir::setCurrent(bundleDir.absolutePath());\n'
        '        else\n'
        '            QDir::setCurrent(executableDir.absolutePath());',
    )
    replace_once(
        main,
        '    workingDir.replace("/build", "");',
        '    if (workingDir.endsWith("/build"))\n'
        '        workingDir.chop(6);',
    )
    replace_once(
        main,
        'const QCommandLineOption AppDataProfileOption("appdata-profile", "Use the TSRE profile stored in user application data.");',
        'const QCommandLineOption AppDataProfileOption("appdata-profile", "Use the Riel editor profile stored in user application data.");',
    )
    replace_once(
        main,
        '#include <QApplication>',
        '#include <QApplication>\n#include <QOpenGLWidget>\n#include <QOpenGLFunctions>',
    )
    replace_once(
        main,
        '    const QCommandLineOption TestOption("test", "Run TSRE test runner and exit.");',
        '    const QCommandLineOption GraphicsCheckOption("graphics-check", "Verify the native editor OpenGL widget and exit.");\n'
        '    parser.addOption(GraphicsCheckOption);\n'
        '    const QCommandLineOption TestOption("test", "Run TSRE test runner and exit.");',
    )
    replace_once(
        main,
        '    const QCommandLineOption TestOption("test", "Run TSRE test runner and exit.");',
        '    const QCommandLineOption RouteActionCheckOption("route-action-check", "Check route database warning buttons.");\n'
        '    parser.addOption(RouteActionCheckOption);\n'
        '    const QCommandLineOption RouteSessionCheckOption("route-session-check", "Open and render a route briefly, then exit.");\n'
        '    parser.addOption(RouteSessionCheckOption);\n'
        '    const QCommandLineOption AcePreviewCheckOption("ace-preview-check", "Check fitted ACE preview and Ctrl+wheel zoom.");\n'
        '    parser.addOption(AcePreviewCheckOption);\n'
        '    const QCommandLineOption ConsistSelectionCheckOption("consist-selection-check", "Check selecting and clearing a rolling-stock entry.");\n'
        '    parser.addOption(ConsistSelectionCheckOption);\n'
        '    const QCommandLineOption TestOption("test", "Run TSRE test runner and exit.");',
    )
    replace_once(
        main,
        '    if (parser.isSet(TestVerboseOption)) {\n'
        '        consoleArgs["TEST_VERBOSE"] = "TRUE";\n'
        '    }',
        '    if (parser.isSet(TestVerboseOption)) {\n'
        '        consoleArgs["TEST_VERBOSE"] = "TRUE";\n'
        '    }\n'
        '    if (parser.isSet(GraphicsCheckOption))\n'
        '        consoleArgs["GRAPHICS_CHECK"] = "TRUE";',
    )
    replace_once(
        main,
        '    if (parser.isSet(GraphicsCheckOption))\n'
        '        consoleArgs["GRAPHICS_CHECK"] = "TRUE";',
        '    if (parser.isSet(GraphicsCheckOption))\n'
        '        consoleArgs["GRAPHICS_CHECK"] = "TRUE";\n'
        '    if (parser.isSet(RouteActionCheckOption))\n'
        '        consoleArgs["ROUTE_ACTION_CHECK"] = "TRUE";\n'
        '    if (parser.isSet(RouteSessionCheckOption))\n'
        '        consoleArgs["ROUTE_SESSION_CHECK"] = "TRUE";\n'
        '    if (parser.isSet(AcePreviewCheckOption))\n'
        '        consoleArgs["ACE_PREVIEW_CHECK"] = "TRUE";\n'
        '    if (parser.isSet(ConsistSelectionCheckOption))\n'
        '        consoleArgs["CONSIST_SELECTION_CHECK"] = "TRUE";',
    )
    replace_once(
        main,
        '#include <QOpenGLFunctions>',
        '#include <QOpenGLFunctions>\n#include <QGraphicsView>\n#include <QWheelEvent>\n'
        '#include <QPushButton>\n#include <QListWidget>\n#include <QLineEdit>\n#include <QTemporaryDir>',
    )
    replace_once(main, '#include <conEditor/CELoadWindow.h>',
                 '#include <conEditor/CELoadWindow.h>\n'
                 '#include <conEditor/ConEditorWindow.h>\n'
                 '#include <conEditor/EngListWidget.h>')
    replace_once(main, '#include <tsre/Game.h>',
                 '#include <tsre/Game.h>\n#include <tsre/gui/ActionChooseDialog.h>\n'
                 '#include <tsre/world/Route.h>\n#include <routeEditor/RouteEditorGLWidget.h>')
    replace_once(
        main,
        '    LoadRouteEditor();\n\n    //MapWindow aaa;',
        '''    LoadRouteEditor();
    if (consoleArgs["ROUTE_SESSION_CHECK"] == "TRUE") {
        QTimer::singleShot(3000, &app, [&app] {
            RouteEditorWindow *window = nullptr;
            for (QWidget *widget : QApplication::topLevelWidgets()) {
                window = qobject_cast<RouteEditorWindow *>(widget);
                if (window) break;
            }
            auto *view = window ? window->findChild<RouteEditorGLWidget *>() : nullptr;
            if (!window || !window->isVisible() || !view || !view->isValid()
                    || !view->currentRoute() || !view->currentRoute()->loaded) {
                qCritical() << "RIEL_ROUTE_SESSION_FAILED: route did not render";
                app.exit(1);
                return;
            }
            view->grabFramebuffer(); // Force a real GL paint, not just a shown window.
            printf("RIEL_ROUTE_SESSION_OK\\n");
            app.quit();
        });
    }

    //MapWindow aaa;''',
    )
    replace_once(
        main,
        '    // Test runner (headless) - runs and exits without starting the GUI.',
        '''    if (consoleArgs["ROUTE_ACTION_CHECK"] == "TRUE") {
        const QStringList actionIds = {"FIX", "VIEW", "IGNORE", "EXIT"};
        for (const QString &expected : actionIds) {
            ActionChooseDialog dialog(4);
            for (const QString &id : actionIds)
                dialog.pushAction(id, id);
            QTimer::singleShot(0, &dialog, [&dialog, expected] {
                for (QPushButton *button : dialog.findChildren<QPushButton *>()) {
                    if (button->text() == expected) {
                        button->click();
                        return;
                    }
                }
                dialog.reject();
            });
            QTimer::singleShot(2000, &dialog, &QDialog::reject);
            if (dialog.exec() != QDialog::Accepted || dialog.actionChoosen != expected) {
                qCritical() << "RIEL_ROUTE_ACTION_FAILED:" << expected << dialog.actionChoosen;
                return 1;
            }
        }
        ActionChooseDialog cancelled(1);
        cancelled.pushAction("FIX", "FIX");
        QTimer::singleShot(0, &cancelled, &QDialog::reject);
        if (cancelled.exec() != QDialog::Rejected || !cancelled.actionChoosen.isEmpty())
            return 1;
        printf("RIEL_ROUTE_ACTION_OK\\n");
        return 0;
    }

    // Test runner (headless) - runs and exits without starting the GUI.''',
    )
    replace_once(
        main,
        '    // Test runner (headless) - runs and exits without starting the GUI.',
        '''    if (consoleArgs["ACE_PREVIEW_CHECK"] == "TRUE") {
        QTemporaryDir directory;
        QImage image(2048, 1024, QImage::Format_RGB32);
        image.fill(Qt::red);
        if (!directory.isValid() || !image.save(directory.filePath("large.png"))) return 1;
        AceConverterWindow window;
        window.show();
        QGraphicsView *view = window.findChild<QGraphicsView *>("texturePreview");
        QLineEdit *file = window.findChild<QLineEdit *>("sourceFile");
        if (!view || !file) return 1;
        window.loadFile(directory.filePath("large.png"));
        QElapsedTimer timer;
        timer.start();
        while (timer.elapsed() < 10000 &&
               (window.isBusy() || view->transform().m11() >= 1))
            app.processEvents(QEventLoop::AllEvents, 20);
        if (window.isBusy() || file->text().isEmpty() ||
                view->transform().m11() >= 1 || view->transform().m11() <= 0) {
            qCritical() << "RIEL_ACE_PREVIEW_FAILED: image did not fit";
            return 1;
        }
        const qreal before = view->transform().m11();
        const QPointF center = view->viewport()->rect().center();
        QWheelEvent wheel(center, view->viewport()->mapToGlobal(center.toPoint()),
                          QPoint(), QPoint(0, 120), Qt::NoButton,
                          Qt::ControlModifier, Qt::NoScrollPhase, false);
        app.sendEvent(view->viewport(), &wheel);
        if (view->transform().m11() <= before) {
            qCritical() << "RIEL_ACE_PREVIEW_FAILED: Ctrl+wheel did not zoom";
            return 1;
        }
        QPushButton *fit = window.findChild<QPushButton *>("fitPreview");
        if (!fit) return 1;
        fit->click();
        if (view->transform().m11() >= 1) return 1;
        printf("RIEL_ACE_PREVIEW_OK\\n");
        return 0;
    }
    if (consoleArgs["CONSIST_SELECTION_CHECK"] == "TRUE") {
        if (Game::root.isEmpty()) return 1;
        Game::InitAssets();
        fprintf(stderr, "RIEL_CONSIST_STEP create window\\n");
        // Match production's top-level window lifetime so this smoke check
        // isolates loading and selection from window teardown.
        auto *window = new ConEditorWindow();
        fprintf(stderr, "RIEL_CONSIST_STEP show window\\n");
        window->show();
        EngListWidget *engList = window->findChild<EngListWidget *>();
        QListWidget *items = engList ? engList->findChild<QListWidget *>() : nullptr;
        if (!items || items->count() == 0) {
            qCritical() << "RIEL_CONSIST_SELECTION_FAILED: no rolling stock";
            return 1;
        }
        fprintf(stderr, "RIEL_CONSIST_STEP first selection\\n");
        items->setCurrentRow(0);
        app.processEvents();
        fprintf(stderr, "RIEL_CONSIST_STEP filter and reselect\\n");
        engList->fillEngList(); // clearing a selected list used to dereference null
        app.processEvents();
        if (items->count() == 0) return 1;
        QMetaObject::invokeMethod(engList, "itemsSelected"); // no current item
        items->setCurrentRow(0);
        app.processEvents();
        fprintf(stderr, "RIEL_CONSIST_STEP stale ID\\n");
        window->engListSelected(-1); // a stale ID should not create a null entry
        printf("RIEL_CONSIST_SELECTION_OK\\n");
        return 0;
    }

    // Test runner (headless) - runs and exits without starting the GUI.''',
    )
    replace_once(
        main,
        '    // Test runner (headless) - runs and exits without starting the GUI.',
        '    // Exercise the same QOpenGLWidget path as the route, consist and shape editors.\n'
        '    // This needs a display (Xvfb in CI); --version does not test graphics.\n'
        '    if (consoleArgs["GRAPHICS_CHECK"] == "TRUE") {\n'
        '        QOpenGLWidget widget;\n'
        '        widget.resize(64, 64);\n'
        '        widget.show();\n'
        '        app.processEvents();\n'
        '        QOpenGLContext *context = widget.context();\n'
        '        if (!widget.isValid() || !context || !context->isValid()) {\n'
        '            qCritical() << "RIEL_EDITOR_GL_FAILED: QOpenGLWidget has no valid context";\n'
        '            return 1;\n'
        '        }\n'
        '        widget.makeCurrent();\n'
        '        const GLubyte *rawVersion = context->functions()->glGetString(GL_VERSION);\n'
        '        const QByteArray version = rawVersion ? QByteArray(reinterpret_cast<const char *>(rawVersion)) : QByteArray();\n'
        '        widget.doneCurrent();\n'
        '        if (version.isEmpty()) {\n'
        '            qCritical() << "RIEL_EDITOR_GL_FAILED: no GL_VERSION";\n'
        '            return 1;\n'
        '        }\n'
        '        printf("RIEL_EDITOR_GL_OK %s\\n", version.constData());\n'
        '        return 0;\n'
        '    }\n\n'
        '    // Test runner (headless) - runs and exits without starting the GUI.',
    )
    replace_once(
        main,
        "    QApplication app(argc, argv);\n    TranslationManager translationManager;",
        '    QApplication app(argc, argv);\n'
        '    QGuiApplication::setDesktopFileName("riel-route-editor");\n'
        '    app.setWindowIcon(QIcon(QDir::current().filePath("riel-route-editor.png")));\n'
        "    TranslationManager translationManager;",
    )

    # Qt 6 on Linux can otherwise negotiate a compatibility context while TSRE's
    # renderer uses a mixture of modern VAOs/VBOs and legacy GLSL 1.40 shaders.
    # Request a deterministic 3.3 core context so every editor window uses the
    # same shader ABI and the native driver cannot silently choose a legacy path.
    replace_once(
        main,
        "    QSurfaceFormat format;\n"
        "//#ifdef __APPLE__\n"
        "//    format.setVersion(3, 3);\n"
        "//    format.setProfile(QSurfaceFormat::CoreProfile);\n"
        "//#endif",
        "    QSurfaceFormat format;\n"
        "#if defined(Q_OS_LINUX) || defined(__APPLE__)\n"
        "    format.setVersion(3, 3);\n"
        "    format.setProfile(QSurfaceFormat::CoreProfile);\n"
        "#endif",
    )

    # ACE previews start fitted and Ctrl+wheel zooms around the pointer. Keep
    # ordinary wheel scrolling and the 100% button's explicit scale intact.
    ace_window = source / "src" / "aceConverter" / "AceConverterWindow.cpp"
    ace_header = source / "src" / "aceConverter" / "AceConverterWindow.h"
    replace_once(
        ace_header,
        'class QGraphicsView;',
        'class AcePreviewView;',
    )
    replace_once(ace_header, '    QGraphicsView *preview = nullptr;',
                 '    AcePreviewView *preview = nullptr;')
    replace_once(
        ace_window,
        '#include <QGraphicsView>',
        '#include <QGraphicsView>\n#include <QWheelEvent>\n#include <QTimer>\n#include <cmath>',
    )
    replace_once(
        ace_window,
        'AceConverterWindow::AceConverterWindow(const QColor &mainLabelColor, QWidget *parent) : QMainWindow(parent) {',
        '''class AcePreviewView final : public QGraphicsView {
public:
    using QGraphicsView::QGraphicsView;

    void fitImage() {
        if (!scene() || scene()->sceneRect().isEmpty()) return;
        fitInView(scene()->sceneRect(), Qt::KeepAspectRatio);
    }

protected:
    void wheelEvent(QWheelEvent *event) override {
        if (!(event->modifiers() & Qt::ControlModifier)) {
            QGraphicsView::wheelEvent(event);
            return;
        }
        const int steps = event->angleDelta().y();
        if (steps == 0) {
            event->ignore();
            return;
        }
        const qreal current = transform().m11();
        const qreal target = qBound(qreal(0.02),
                                    current * std::pow(1.2, steps / 120.0), qreal(32.0));
        if (target != current) scale(target / current, target / current);
        event->accept();
    }
};

AceConverterWindow::AceConverterWindow(const QColor &mainLabelColor, QWidget *parent) : QMainWindow(parent) {''',
    )
    replace_once(ace_window, '    preview = new QGraphicsView(scene, central);',
                 '    preview = new AcePreviewView(scene, central);')
    replace_once(ace_window, '    preview->setDragMode(QGraphicsView::ScrollHandDrag);',
                 '    preview->setDragMode(QGraphicsView::ScrollHandDrag);\n'
                 '    preview->setTransformationAnchor(QGraphicsView::AnchorUnderMouse);')
    replace_once(ace_window,
                 '    connect(fit, &QPushButton::clicked, this, [this] {\n'
                 '        if (!scene->sceneRect().isEmpty()) preview->fitInView(scene->sceneRect(), Qt::KeepAspectRatio);\n'
                 '    });',
                 '    connect(fit, &QPushButton::clicked, this, [this] { preview->fitImage(); });')
    replace_once(ace_window, '        preview->resetTransform();\n        mipmaps->setChecked(false);',
                 '        preview->resetTransform();\n'
                 '        // Let Qt lay out the viewport before fitting a newly loaded image.\n'
                 '        QTimer::singleShot(0, preview, [view = preview] { view->fitImage(); });\n'
                 '        mipmaps->setChecked(false);')

    # A cleared list emits itemSelectionChanged with no current item. A stale
    # engine/consist ID can likewise disappear when the catalogue is reloaded.
    eng_list = source / "src" / "conEditor" / "EngListWidget.cpp"
    con_window = source / "src" / "conEditor" / "ConEditorWindow.cpp"
    replace_once(eng_list,
                 '    QListWidgetItem * item = items.currentItem();\n'
                 '    //qDebug() << item->type() << " " << item->text();\n'
                 '    emit engListSelected(item->type());',
                 '    QListWidgetItem * item = items.currentItem();\n'
                 '    if (item == nullptr) return;\n'
                 '    emit engListSelected(item->type());')
    replace_once(con_window,
                 '    if(currentCon == NULL) return;\n'
                 '    currentCon->select(uid);\n'
                 '    setCurrentEng(currentCon->engItems[uid].eng);',
                 '    if(currentCon == NULL || uid < 0 || uid >= currentCon->engItems.size()) return;\n'
                 '    currentCon->select(uid);\n'
                 '    setCurrentEng(currentCon->engItems[uid].eng);')
    replace_once(con_window,
                 '    currentEng = englib->eng[id];\n'
                 '    qDebug() << currentEng->engName;',
                 '    auto selected = englib->eng.find(id);\n'
                 '    if (selected == englib->eng.end() || !selected->second || selected->second->loaded != 1) {\n'
                 '        qWarning() << "Consist editor: selected rolling stock is unavailable:" << id;\n'
                 '        return;\n'
                 '    }\n'
                 '    currentEng = selected->second;\n'
                 '    qDebug() << currentEng->engName;')
    replace_once(con_window,
                 '            engSetsList.addItem(ConLib::con[engSets[i]]->showName, i);',
                 '            auto found = ConLib::con.find(engSets[i]);\n'
                 '            if (found != ConLib::con.end() && found->second)\n'
                 '                engSetsList.addItem(found->second->showName, i);')
    replace_once(con_window,
                 '    if(engSetId >= 0 ){\n'
                 '        pos = -ConLib::con[engSets[engSetId]]->conLength - 1;',
                 '    if (engSetId >= 0) {\n'
                 '        if (engSetId >= engSets.size()) engSetId = -1;\n'
                 '        else {\n'
                 '            auto found = ConLib::con.find(engSets[engSetId]);\n'
                 '            if (found == ConLib::con.end() || !found->second) engSetId = -1;\n'
                 '        }\n'
                 '    }\n'
                 '    if(engSetId >= 0 ){\n'
                 '        pos = -ConLib::con.at(engSets[engSetId])->conLength - 1;')
    replace_once(con_window,
                 '    //currentEng = englib->eng[id];\n'
                 '    qDebug() << currentEng->engName;',
                 '    // setCurrentEng validates IDs before using the selected engine.')

    # Qt 6 emits mappedInt(int), while the legacy mapped(int) connection
    # silently fails at runtime and leaves every button in this modal inert.
    # Connect the buttons directly and treat closing the warning as cancel.
    action_dialog = source / "src" / "tsre" / "gui" / "ActionChooseDialog.cpp"
    action_header = source / "src" / "tsre" / "gui" / "ActionChooseDialog.h"
    replace_once(action_dialog,
                 '        mapper.setMapping(bok[i], i);\n'
                 '        connect(bok[i], SIGNAL(clicked()), &mapper, SLOT(map()));',
                 '        connect(bok[i], &QPushButton::clicked, this, [this, i] { action(i); });')
    replace_once(action_dialog,
                 '    connect(&mapper, SIGNAL(mapped(int)), this, SLOT(action(int)));\n',
                 '')
    replace_once(action_dialog,
                 'void ActionChooseDialog::action(int i){\n'
                 '    actionChoosen = actions[i];\n'
                 '    this->close();',
                 'void ActionChooseDialog::action(int i){\n'
                 '    if (i < 0 || i >= count) return;\n'
                 '    actionChoosen = actions.value(i);\n'
                 '    accept();')
    replace_once(action_header, '    QSignalMapper mapper;\n', '')
    route = source / "src" / "tsre" / "world" / "Route.cpp"
    replace_once(route,
                 '    if(dialog.actionChoosen == "EXIT"){',
                 '    if(dialog.actionChoosen == "EXIT" || dialog.actionChoosen.isEmpty()){')
    route_gl = source / "src" / "routeEditor" / "RouteEditorGLWidget.cpp"
    replace_once(route_gl,
                 '    float spos[3];\n'
                 '    if (Game::start == 2) {\n'
                 '        camera->setPozT(Game::startTileX, -Game::startTileY);\n'
                 '    } else {\n'
                 '        camera->setPozT(route->getStartTileX(), -route->getStartTileZ());\n'
                 '        spos[0] = route->getStartpX();\n'
                 '        spos[2] = -route->getStartpZ();\n'
                 '    }\n'
                 '    if (Game::terrainLib->load(route->getStartTileX(), -route->getStartTileZ())) {\n'
                 '        spos[1] = 20 + Game::terrainLib->getHeight(route->getStartTileX(), -route->getStartTileZ(), route->getStartpX(), -route->getStartpZ());',
                 '    const bool explicitTile = Game::start == 2;\n'
                 '    const int tileX = explicitTile ? Game::startTileX : route->getStartTileX();\n'
                 '    const int tileZ = explicitTile ? Game::startTileY : route->getStartTileZ();\n'
                 '    // An explicit starting tile begins at its local origin. Initialize\n'
                 '    // both coordinates before passing them into the camera.\n'
                 '    float spos[3] = {explicitTile ? 0.0f : route->getStartpX(), 0.0f,\n'
                 '                     explicitTile ? 0.0f : -route->getStartpZ()};\n'
                 '    camera->setPozT(tileX, -tileZ);\n'
                 '    if (Game::terrainLib->load(tileX, -tileZ)) {\n'
                 '        spos[1] = 20 + Game::terrainLib->getHeight(tileX, -tileZ, spos[0], spos[2]);')

    # Use the GLSL 3.30 shader set whenever the negotiated context is modern.
    # Previously Linux always loaded the 1.40 shaders; on Qt 6/NVIDIA that can
    # produce valid geometry with corrupted red/white/black material output.
    gluu = source / "src" / "tsre" / "ogl" / "GLUU.cpp"
    replace_once(
        gluu,
        '#ifdef __APPLE__\n'
        '    QFile* shaderData = new QFile(QString("appdata/")+Game::AppDataVersion+"/shaders330/"+shaderScript+"."+type);\n'
        '#else\n'
        '    QFile* shaderData = new QFile(QString("appdata/")+Game::AppDataVersion+"/shaders/"+shaderScript+"."+type);\n'
        '#endif',
        '    QString shaderDirectory = "shaders";\n'
        '    if (QOpenGLContext *context = QOpenGLContext::currentContext()) {\n'
        '        const QSurfaceFormat format = context->format();\n'
        '        if (format.profile() == QSurfaceFormat::CoreProfile\n'
        '                || format.majorVersion() > 3\n'
        '                || (format.majorVersion() == 3 && format.minorVersion() >= 3))\n'
        '            shaderDirectory = "shaders330";\n'
        '    }\n'
        '    QFile* shaderData = new QFile(QString("appdata/")+Game::AppDataVersion+"/"+shaderDirectory+"/"+shaderScript+"."+type);',
    )
    replace_once(
        gluu,
        '            qDebug() << "Loading shader .vs file failed.";',
        '            qCritical() << "Vertex shader compile failed:" << definition.name << shaders[definition.name]->log();',
    )
    replace_once(
        gluu,
        '            qDebug() << "Loading shader .fs file failed.";',
        '            qCritical() << "Fragment shader compile failed:" << definition.name << shaders[definition.name]->log();',
    )
    replace_once(
        gluu,
        '            qDebug() << "Shader link failed.";',
        '            qCritical() << "Shader link failed:" << definition.name << currentShader->log();',
    )
    replace_once(
        gluu,
        '    //currentShader = shaders["StandardFog"];\n'
        '    currentShader = shaders["StandardBloom"];',
        '    // Keep the initial shader deterministic across all editor widgets.\n'
        '    currentShader = shaders["StandardFog"];',
    )
    replace_once(
        gluu,
        'void GLUU::initShader() {\n'
        '    QOpenGLContext *context = QOpenGLContext::currentContext();\n'
        '    QOpenGLExtraFunctions *extra = context->extraFunctions();',
        'void GLUU::initShader() {\n'
        '    QOpenGLContext *context = QOpenGLContext::currentContext();\n'
        '    QOpenGLExtraFunctions *extra = context->extraFunctions();\n'
        '    if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0) {\n'
        '        const QSurfaceFormat fmt = context->format();\n'
        '        QOpenGLFunctions *f = context->functions();\n'
        '        qInfo().noquote() << "RIEL_RENDER_GL"\n'
        '            << "vendor=" << reinterpret_cast<const char *>(f->glGetString(GL_VENDOR))\n'
        '            << "renderer=" << reinterpret_cast<const char *>(f->glGetString(GL_RENDERER))\n'
        '            << "version=" << reinterpret_cast<const char *>(f->glGetString(GL_VERSION))\n'
        '            << "context=" << QString("%1.%2").arg(fmt.majorVersion()).arg(fmt.minorVersion())\n'
        '            << "profile=" << int(fmt.profile())\n'
        '            << "rgba=" << QString("%1/%2/%3/%4").arg(fmt.redBufferSize()).arg(fmt.greenBufferSize()).arg(fmt.blueBufferSize()).arg(fmt.alphaBufferSize())\n'
        '            << "samples=" << fmt.samples();\n'
        '    }',
    )

    # MSTS content was authored for a case-insensitive filesystem. Riel runs
    # natively on Linux, so resolve a differently-cased path only when every
    # component has exactly one case-insensitive match. Exact spelling always
    # wins; ambiguous siblings such as foo.ace/Foo.ace are never guessed.
    content_path = source / "src" / "tsre" / "fileFunctions" / "ContentPath.h"
    replace_once(
        content_path,
        'inline QString join(const QString &base,const QString &name) {\n'
        '    return normalize(base+"/"+name);\n'
        '}',
        'inline QString join(const QString &base,const QString &name) {\n'
        '    return normalize(base+"/"+name);\n'
        '}\n'
        'inline QString resolveExistingCaseInsensitive(const QString &path) {\n'
        '    if (synthetic(path)) return path;\n'
        '    const QString normalized = normalize(path);\n'
        '    if (QFileInfo(normalized).exists()) return normalized;\n'
        '#ifdef Q_OS_LINUX\n'
        '    const QString absolute = QFileInfo(normalized).absoluteFilePath();\n'
        '    const QStringList parts = QDir::cleanPath(absolute).split(\'/\', Qt::SkipEmptyParts);\n'
        '    QString current = absolute.startsWith(\'/\') ? QString("/") : QString();\n'
        '    for (const QString &part : parts) {\n'
        '        QDir dir(current.isEmpty() ? QDir::currentPath() : current);\n'
        '        QString exact;\n'
        '        QStringList foldedMatches;\n'
        '        const QStringList entries = dir.entryList(QDir::AllEntries | QDir::NoDotAndDotDot | QDir::Hidden | QDir::System);\n'
        '        for (const QString &entry : entries) {\n'
        '            if (entry == part) { exact = entry; break; }\n'
        '            if (entry.compare(part, Qt::CaseInsensitive) == 0)\n'
        '                foldedMatches.push_back(entry);\n'
        '        }\n'
        '        QString selected;\n'
        '        if (!exact.isEmpty()) {\n'
        '            selected = exact;\n'
        '        } else if (foldedMatches.size() == 1) {\n'
        '            selected = foldedMatches.front();\n'
        '        } else if (foldedMatches.size() > 1) {\n'
        '            qWarning() << "Riel ambiguous case-insensitive content path:"\n'
        '                       << normalized << "component" << part\n'
        '                       << "matches" << foldedMatches;\n'
        '            return normalized;\n'
        '        } else {\n'
        '            return normalized;\n'
        '        }\n'
        '        current = QDir(current.isEmpty() ? QDir::currentPath() : current).filePath(selected);\n'
        '    }\n'
        '    if (QFileInfo(current).exists()) {\n'
        '        if (current != normalized)\n'
        '            qDebug() << "Riel content case fallback:" << normalized << "->" << current;\n'
        '        return current;\n'
        '    }\n'
        '#endif\n'
        '    return normalized;\n'
        '}',
    )
    replace_once(
        content_path,
        'inline bool readable(const QString &path) {\n'
        '    const QFileInfo file(normalize(path));return file.isFile() && file.isReadable();\n'
        '}',
        'inline bool readable(const QString &path) {\n'
        '    const QFileInfo file(resolveExistingCaseInsensitive(path));return file.isFile() && file.isReadable();\n'
        '}',
    )
    replace_once(
        content_path,
        'inline QString textureSource(const QString &path) {\n'
        '    const QString source=normalize(path);\n'
        '    if(source.endsWith(".ace",Qt::CaseInsensitive) && !QFileInfo(source).isFile())\n'
        '        return withExtension(source,"dds");\n'
        '    return source;\n'
        '}',
        'inline QString textureSource(const QString &path) {\n'
        '    const QString source=resolveExistingCaseInsensitive(path);\n'
        '    if(source.endsWith(".ace",Qt::CaseInsensitive) && !QFileInfo(source).isFile())\n'
        '        return resolveExistingCaseInsensitive(withExtension(source,"dds"));\n'
        '    return source;\n'
        '}',
    )

    texlib = source / "src" / "tsre" / "texture" / "TexLib.cpp"
    replace_once(
        texlib,
        'int TexLib::addTex(QString pathid, bool reload) {\n'
        '    pathid = ContentPath::normalize(pathid);',
        'int TexLib::addTex(QString pathid, bool reload) {\n'
        '    pathid = ContentPath::resolveExistingCaseInsensitive(pathid);\n'
        '    if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0) {\n'
        '        static quint64 requestCount = 0;\n'
        '        ++requestCount;\n'
        '        qInfo().noquote() << "RIEL_RENDER_REQUEST" << requestCount\n'
        '            << "path=" << pathid << "exists=" << QFileInfo(pathid).isFile() << "reload=" << reload;\n'
        '        if ((requestCount % 250) == 0)\n'
        '            dumpStats(QString("render diagnostic #%1").arg(requestCount));\n'
        '    }',
    )


    # Large MSTS routes can start hundreds of ACE/DDS QThreads at once. Those workers
    # publish into Texture instances which the Qt/OpenGL widgets consume, and the
    # upstream ACE/DDS/Image branches also leak their QThread wrapper objects.
    # Riel favors deterministic/stable editor loading: serialize disk texture decode
    # by default, while RIEL_EDITOR_ASYNC_TEXTURES=1 remains an opt-in for testing.
    ace_worker = source / "src" / "tsre" / "texture" / "AceLib.cpp"
    dds_worker = source / "src" / "tsre" / "texture" / "DdsLib.cpp"
    replace_once(
        ace_worker,
        'bool AceLib::IsThread = true;',
        'bool AceLib::IsThread = qEnvironmentVariableIntValue("RIEL_EDITOR_ASYNC_TEXTURES") != 0;',
    )
    replace_once(
        dds_worker,
        'bool DdsLib::IsThread = true;',
        'bool DdsLib::IsThread = qEnvironmentVariableIntValue("RIEL_EDITOR_ASYNC_TEXTURES") != 0;',
    )
    replace_once(
        texlib,
        '''    if(tType == "ace"){
        AceLib* t = new AceLib();
        t->texture = newFile;
        if(AceLib::IsThread && !reload)
            t->start();
        else
            t->run();
    } else if(tType == "dds"){
        DdsLib* t = new DdsLib();
        t->texture = newFile;
        if(DdsLib::IsThread && !reload)
            t->start();
        else
            t->run();
    } else if(tType == "png"||tType == "bmp"||tType == "jpg"/*||tType == "dds"*/||tType == "tga"){
        ImageLib* t = new ImageLib();
        t->texture = newFile;
        if(ImageLib::IsThread && !reload)
            t->start();
        else
            t->run();
    } else if(tType == ":painttex"){
        PaintTexLib* t = new PaintTexLib();
        t->texture = newFile;
        //t->start();
        t->run();
''',
        '''    if(tType == "ace"){
        AceLib* t = new AceLib();
        t->texture = newFile;
        if(AceLib::IsThread && !reload) {
            QObject::connect(t, &QThread::finished, t, &QObject::deleteLater);
            t->start();
        } else {
            t->run();
            delete t;
        }
    } else if(tType == "dds"){
        DdsLib* t = new DdsLib();
        t->texture = newFile;
        if(DdsLib::IsThread && !reload) {
            QObject::connect(t, &QThread::finished, t, &QObject::deleteLater);
            t->start();
        } else {
            t->run();
            delete t;
        }
    } else if(tType == "png"||tType == "bmp"||tType == "jpg"/*||tType == "dds"*/||tType == "tga"){
        ImageLib* t = new ImageLib();
        t->texture = newFile;
        if(ImageLib::IsThread && !reload) {
            QObject::connect(t, &QThread::finished, t, &QObject::deleteLater);
            t->start();
        } else {
            t->run();
            delete t;
        }
    } else if(tType == ":painttex"){
        PaintTexLib* t = new PaintTexLib();
        t->texture = newFile;
        t->run();
        delete t;
''',
    )


    ace_lib = source / "src" / "tsre" / "texture" / "AceLib.cpp"
    replace_once(
        ace_lib,
        '    if (!load(texture->pathid, *texture, options, error)) {\n'
        '        texture->error = true;\n'
        '        texture->missing = !QFileInfo::exists(texture->pathid);\n'
        '        texture->errorMessage = error;\n'
        '        texture->loaded = false;\n'
        '        qWarning().noquote() << "ACE:" << texture->pathid << error;\n'
        '    }\n'
        '}',
        '    if (!load(texture->pathid, *texture, options, error)) {\n'
        '        texture->error = true;\n'
        '        texture->missing = !QFileInfo::exists(texture->pathid);\n'
        '        texture->errorMessage = error;\n'
        '        texture->loaded = false;\n'
        '        qWarning().noquote() << "ACE:" << texture->pathid << error;\n'
        '        if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0)\n'
        '            qWarning().noquote() << "RIEL_RENDER_ACE failed path=" << texture->pathid\n'
        '                << "missing=" << texture->missing << "error=" << error;\n'
        '    } else if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0) {\n'
        '        qInfo().noquote() << "RIEL_RENDER_ACE loaded path=" << texture->pathid\n'
        '            << "size=" << QString("%1x%2").arg(texture->width).arg(texture->height)\n'
        '            << "bpp=" << texture->bytesPerPixel\n'
        '            << "compressedBytes=" << texture->compressedData.size();\n'
        '    }\n'
        '}',
    )

    texture_cpp = source / "src" / "tsre" / "texture" / "Texture.cpp"
    replace_once(
        texture_cpp,
        'bool Texture::GLTextures(bool mipmaps) {\n'
        '    auto *context = QOpenGLContext::currentContext();\n'
        '    if (!loaded || !context || width <= 0 || height <= 0 ||\n'
        '        (bytesPerPixel != 3 && bytesPerPixel != 4))\n'
        '        return false;',
        'bool Texture::GLTextures(bool mipmaps) {\n'
        '    auto *context = QOpenGLContext::currentContext();\n'
        '    if (!loaded || !context || width <= 0 || height <= 0 ||\n'
        '        (bytesPerPixel != 3 && bytesPerPixel != 4)) {\n'
        '        if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0)\n'
        '            qWarning().noquote() << "RIEL_RENDER_GPU rejected path=" << pathid\n'
        '                << "loaded=" << loaded << "context=" << (context != nullptr)\n'
        '                << "size=" << QString("%1x%2").arg(width).arg(height)\n'
        '                << "bpp=" << bytesPerPixel << "error=" << errorMessage;\n'
        '        return false;\n'
        '    }',
    )
    replace_once(
        texture_cpp,
        '    if (!direct && !decodeToCpu())\n'
        '        return false;',
        '    if (!direct && !decodeToCpu()) {\n'
        '        if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0)\n'
        '            qWarning().noquote() << "RIEL_RENDER_GPU cpu-decode-failed path=" << pathid\n'
        '                << "compressedBytes=" << compressedData.size() << "error=" << errorMessage;\n'
        '        return false;\n'
        '    }',
    )
    replace_once(
        texture_cpp,
        '    if (!pixelTransferSucceeded(*this))\n'
        '        return false; // Keep CPU data for retry/diagnostics.\n'
        '    delete[] imageData;',
        '    if (!pixelTransferSucceeded(*this)) {\n'
        '        if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0)\n'
        '            qWarning().noquote() << "RIEL_RENDER_GPU upload-failed path=" << pathid\n'
        '                << "directCompressed=" << direct << "internalFormat=" << Qt::hex << gpuInternalFormat\n'
        '                << "error=" << errorMessage;\n'
        '        return false; // Keep CPU data for retry/diagnostics.\n'
        '    }\n'
        '    if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0)\n'
        '        qInfo().noquote() << "RIEL_RENDER_GPU uploaded path=" << pathid\n'
        '            << "textureId=" << tex[0] << "directCompressed=" << direct\n'
        '            << "internalFormat=" << Qt::hex << gpuInternalFormat;\n'
        '    delete[] imageData;',
    )

    renderer_cpp = source / "src" / "tsre" / "renderer" / "OpenGL3Renderer.cpp"
    replace_once(
        renderer_cpp,
        '    if(item->texturesEnabled){\n'
        '        gluu->enableTextures();\n'
        '        gluu->bindTexture(f, item->texAddr);\n'
        '    } else {\n'
        '        gluu->disableTextures(item->colorX, item->colorY, item->colorZ, item->colorA);\n'
        '    }',
        '    if(item->texturesEnabled){\n'
        '        gluu->enableTextures();\n'
        '        gluu->bindTexture(f, item->texAddr);\n'
        '    } else {\n'
        '        if (qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0\n'
        '                && item->colorX > 0.95f && item->colorY < 0.05f && item->colorZ > 0.95f) {\n'
        '            static quint64 magentaFallbackDraws = 0;\n'
        '            ++magentaFallbackDraws;\n'
        '            if (magentaFallbackDraws <= 200 || (magentaFallbackDraws % 1000) == 0)\n'
        '                qWarning().noquote() << "RIEL_RENDER_FALLBACK magenta draw=" << magentaFallbackDraws\n'
        '                    << "texAddr=" << item->texAddr << "vertices=" << item->vertCount\n'
        '                    << "selection=" << item->selectionId;\n'
        '        }\n'
        '        gluu->disableTextures(item->colorX, item->colorY, item->colorZ, item->colorA);\n'
        '    }',
    )


    main_cpp = source / "src" / "main.cpp"
    replace_once(
        main_cpp,
        '    logFile.setFileName("log.txt");\n'
        '    if(logFile.open(QIODevice::WriteOnly)){\n'
        '        logFileOut.setDevice(&logFile);\n'
        '    } else {\n'
        '        qDebug() << "Cannot open log file for writing!";\n'
        '    }\n',
        '    QString stateRoot = qEnvironmentVariable("XDG_STATE_HOME");\n'
        '    if (stateRoot.isEmpty())\n'
        '        stateRoot = QDir::homePath() + "/.local/state";\n'
        '    const QString logDirectory = QDir(stateRoot).filePath("riel/Logs");\n'
        '    QDir().mkpath(logDirectory);\n'
        '    logFile.setFileName(QDir(logDirectory).filePath("Riel Route Editor Log.txt"));\n'
        '    if(logFile.open(QIODevice::WriteOnly | QIODevice::Truncate)){\n'
        '        logFileOut.setDevice(&logFile);\n'
        '    } else {\n'
        '        fprintf(stderr, "Cannot open Route Editor log for writing: %s\\n", qPrintable(logFile.fileName()));\n'
        '    }\n',
    )
    replace_once(
        main_cpp,
        '    if(Game::consoleOutput)\n'
        '        std::cout << output.toStdString() << "\\n";\n'
        '    logFileOut << output << "\\n";\n'
        '    logFileOut.flush();\n'
        '    logFile.flush(); ',
        '    const bool renderDiagnostics = qEnvironmentVariableIntValue("RIEL_EDITOR_RENDER_DIAGNOSTICS") != 0;\n'
        '    if(Game::consoleOutput || renderDiagnostics || !logFile.isOpen())\n'
        '        std::cerr << output.toStdString() << "\\n";\n'
        '    if (logFile.isOpen()) {\n'
        '        logFileOut << output << "\\n";\n'
        '        logFileOut.flush();\n'
        '        logFile.flush();\n'
        '    } ',
    )

    about = source / "src" / "routeEditor" / "AboutWindow.cpp"
    replace_once(
        about,
        '    QLabel* myLabel2 = new QLabel(\n',
        '    QLabel* rielLabel = new QLabel("<b>Riel Route Editor</b> — native Linux integration based on TSRE5vc. "'
        '"<a href=\\\"https://github.com/agustinluzardo/Riel-Linux\\\">Riel project</a>");\n'
        '    rielLabel->setOpenExternalLinks(true);\n'
        '    rielLabel->setContentsMargins(5,0,0,0);\n'
        '    QLabel* myLabel2 = new QLabel(\n',
    )
    replace_once(
        about,
        '    mainLayout->addWidget(myLabel);\n    mainLayout->addWidget(myLabel2);',
        '    mainLayout->addWidget(myLabel);\n    mainLayout->addWidget(rielLabel);\n    mainLayout->addWidget(myLabel2);',
    )

    splash = 'myImage->load(QString("appdata/")+Game::AppDataVersion+"/load.png");'
    for relative in (
        "src/routeEditor/LoadWindow.cpp",
        "src/routeEditor/AboutWindow.cpp",
        "src/conEditor/CELoadWindow.cpp",
    ):
        replace_once(source / relative, splash, 'myImage->load("riel-route-editor.png");')

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
