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
    replace_once(game, 'QString Game::AppName = "TSRE5";', 'QString Game::AppName = "Riel";')
    replace_once(game, 'QString Game::AppVersion = "v" TSRE5_VERSION;', 'QString Game::AppVersion = TSRE5_VERSION;')
    replace_once(game, 'QString Game::root = "C:/tsdata/Train Simulator/";', 'QString Game::root = "";')
    replace_once(game, 'QString Game::route = "bbb1";', 'QString Game::route = "";')

    settings = source / "src" / "settings" / "SettingsProfile.cpp"
    replace_once(
        settings,
        'return QDir(base).filePath("TSRE");',
        'return QDir(base).filePath("Riel/RouteEditor");',
    )

    main = source / "src" / "main.cpp"
    replace_once(
        main,
        'const QCommandLineOption AppDataProfileOption("appdata-profile", "Use the TSRE profile stored in user application data.");',
        'const QCommandLineOption AppDataProfileOption("appdata-profile", "Use the Riel editor profile stored in user application data.");',
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
