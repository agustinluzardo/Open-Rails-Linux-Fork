"""Patch the real editor-window smoke check into the pinned native source."""

from pathlib import Path
from shutil import copyfile


def patch_window_check(source: Path, replace_once) -> None:
    assets = Path(__file__).with_name("route-editor")
    copyfile(assets / "RielRouteWindowCheck.h", source / "src/routeEditor/RielRouteWindowCheck.h")
    main = source / "src/main.cpp"
    replace_once(main, '#include <routeEditor/RouteEditorGLWidget.h>',
                 '#include <routeEditor/RouteEditorGLWidget.h>\n'
                 '#include <routeEditor/RielRouteWindowCheck.h>')
    replace_once(main, '    parser.addOption(RouteSessionCheckOption);',
                 '    parser.addOption(RouteSessionCheckOption);\n'
                 '    const QCommandLineOption RouteFullWindowCheckOption("route-full-window-check", '
                 '"Verify both route windows, a presented frame and event-loop responsiveness.");\n'
                 '    parser.addOption(RouteFullWindowCheckOption);')
    replace_once(main, '    if (parser.isSet(RouteSessionCheckOption))\n'
                 '        consoleArgs["ROUTE_SESSION_CHECK"] = "TRUE";',
                 '    if (parser.isSet(RouteSessionCheckOption))\n'
                 '        consoleArgs["ROUTE_SESSION_CHECK"] = "TRUE";\n'
                 '    if (parser.isSet(RouteFullWindowCheckOption))\n'
                 '        consoleArgs["ROUTE_FULL_WINDOW_CHECK"] = "TRUE";')
    replace_once(main, '    LoadRouteEditor();\n    if (consoleArgs["ROUTE_SESSION_CHECK"]',
                 '    std::unique_ptr<RielRouteWindowCheck> windowCheck;\n'
                 '    if (consoleArgs["ROUTE_FULL_WINDOW_CHECK"] == "TRUE")\n'
                 '        windowCheck = std::make_unique<RielRouteWindowCheck>(app);\n'
                 '    LoadRouteEditor();\n    if (consoleArgs["ROUTE_SESSION_CHECK"]')
    replace_once(main, '    //aaa.show();\n    return app.exec();',
                 '    //aaa.show();\n    const int result = app.exec();\n'
                 '    return windowCheck && !windowCheck->succeeded() ? 1 : result;')
    replace_once(main, '    RouteEditorWindow *window = new RouteEditorWindow();',
                 '    RouteEditorWindow *window = new RouteEditorWindow();\n'
                 '    qInfo().noquote() << "RIEL_EDITOR_WINDOW main-created";')
    window = source / "src/routeEditor/RouteEditorWindow.cpp"
    replace_once(window, '    naviWindow = new NaviWindow(this);',
                 '    naviWindow = new NaviWindow(this);\n'
                 '    qInfo().noquote() << "RIEL_EDITOR_WINDOW navi-created";\n'
                 '    connect(glWidget, &QOpenGLWidget::frameSwapped, glWidget, [this] {\n'
                 '        if (!glWidget->property("rielFirstFramePainted").toBool()\n'
                 '                || glWidget->property("rielFirstFramePresented").toBool()) return;\n'
                 '        glWidget->setProperty("rielFirstFramePresented", true);\n'
                 '        qInfo().noquote() << "RIEL_EDITOR_WINDOW first-frame";\n'
                 '    });')
    replace_once(window, '    QMainWindow::show();',
                 '    QMainWindow::show();\n'
                 '    setProperty("rielMainShown", true);\n'
                 '    qInfo().noquote() << "RIEL_EDITOR_WINDOW main-shown";')
    view = source / "src/routeEditor/RouteEditorGLWidget.cpp"
    replace_once(view, '        paintActiveRendererPipelinePass();\n    }\n}\n\n'
                 'void RouteEditorGLWidget::paintActiveRendererPipelinePass(){',
                 '        paintActiveRendererPipelinePass();\n    }\n'
                 '    setProperty("rielFirstFramePainted", true);\n}\n\n'
                 'void RouteEditorGLWidget::paintActiveRendererPipelinePass(){')
