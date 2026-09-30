"""Resolve split MSTS add-on directories with reusable read-only indexes."""

from pathlib import Path
from shutil import copyfile


def patch_content_index(source: Path, replace_once) -> None:
    copyfile(Path(__file__).with_name("route-editor") / "RielContentIndex.h",
             source / "src/tsre/fileFunctions/RielContentIndex.h")
    test_directory = source / "tests/riel"
    test_directory.mkdir(parents=True, exist_ok=True)
    copyfile(Path(__file__).with_name("route-editor") / "RielContentIndexTests.cpp",
             test_directory / "RielContentIndexTests.cpp")
    replace_once(source / "CMakeLists.txt", 'qt_add_executable(TSRE5vc ${TSRE5vc_SRC})',
                 'qt_add_executable(TSRE5vc ${TSRE5vc_SRC})\n'
                 'find_package(Threads REQUIRED)\n'
                 'add_executable(riel_content_index_tests tests/riel/RielContentIndexTests.cpp)\n'
                 'target_include_directories(riel_content_index_tests PRIVATE src)\n'
                 'target_link_libraries(riel_content_index_tests PRIVATE Qt6::Core Threads::Threads)')
    content = source / "src/tsre/fileFunctions/ContentPath.h"
    text = content.read_text(encoding="utf-8")
    start = text.index('inline QString resolveExistingCaseInsensitive(')
    end = text.index('inline QString parentDirectory(', start)
    replace_once(content, text[start:end], '''inline QString resolveExistingCaseInsensitive(const QString &path) {
    if (synthetic(path)) return path;
    const QString normalized = normalize(path);
    if (QFileInfo::exists(normalized)) return normalized;
#ifdef Q_OS_LINUX
    return RielContentIndex::resolveFile(normalized);
#else
    return normalized;
#endif
}
''')
    replace_once(content, '#include <QString>',
                 '#include <QString>\n#include <tsre/fileFunctions/RielContentIndex.h>')
    # Preserve ShapeLib identity/texture context. Resolve only actual file reads;
    # never enumerate texture-context directories in ShapeLib::addShape().
    for relative, old, new in (
        ("src/tsre/shape/SFile.cpp", "    QFile *file = new QFile(pathid);",
         "    QFile *file = new QFile(ContentPath::resolveExistingCaseInsensitive(pathid));"),
        ("src/tsre/shape/SFileLegacy.cpp", "    QFile source(pathid);",
         "    QFile source(ContentPath::resolveExistingCaseInsensitive(pathid));"),
        ("src/tsre/shape/SFileComplex.cpp", "d->document->read(d->path, d->options.firstLodOnly, d->options.compact)",
         "d->document->read(ContentPath::resolveExistingCaseInsensitive(d->path), d->options.firstLodOnly, d->options.compact)"),
        ("src/tsre/shape/SFileComplex.cpp", '        QString path = ContentPath::withExtension(d->path, "sd");',
         '        QString path = ContentPath::resolveExistingCaseInsensitive(ContentPath::withExtension(d->path, "sd"));'),
        ("src/tsre/shape/SFile.cpp", '    QFile file(ContentPath::withExtension(pathid, "sd"));',
         '    QFile file(ContentPath::resolveExistingCaseInsensitive(ContentPath::withExtension(pathid, "sd")));'),
        ("src/tsre/shape/SFileLegacy.cpp", '    QFile file(ContentPath::withExtension(pathid, "sd"));',
         '    QFile file(ContentPath::resolveExistingCaseInsensitive(ContentPath::withExtension(pathid, "sd")));'),
        ("src/tsre/tdb/TSectionDAT.cpp", '    path = ContentPath::normalize(path);\n    orpath = ContentPath::normalize(orpath);',
         '    path = ContentPath::resolveExistingCaseInsensitive(path);\n    orpath = ContentPath::resolveExistingCaseInsensitive(orpath);'),
        ("src/tsre/fileFunctions/FileBuffer.cpp", '    incPath = ContentPath::normalize(incPath);',
         '    incPath = ContentPath::resolveExistingCaseInsensitive(incPath);'),
        ("src/tsre/fileFunctions/FileBuffer.cpp", '    alternativePath = ContentPath::normalize(alternativePath);',
         '    alternativePath = ContentPath::resolveExistingCaseInsensitive(alternativePath);'),
    ):
        replace_once(source / relative, old, new)
