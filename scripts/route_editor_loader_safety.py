"""Protect the shared editor logger and reject out-of-bounds DDS payloads."""

from pathlib import Path
from shutil import copyfile


def patch_loader_safety(source: Path, replace_once) -> None:
    assets = Path(__file__).with_name("route-editor")
    copyfile(assets / "RielEditorLogging.h", source / "src/tsre/RielEditorLogging.h")
    tests = source / "tests/riel"
    tests.mkdir(parents=True, exist_ok=True)
    copyfile(assets / "RielLoaderSafetyTests.cpp", tests / "RielLoaderSafetyTests.cpp")
    main = source / "src/main.cpp"
    replace_once(main, '#include <QApplication>',
                 '#include <QApplication>\n#include <tsre/RielEditorLogging.h>')
    replace_once(main, '''    if(Game::consoleOutput || renderDiagnostics || !logFile.isOpen())
        std::cerr << output.toStdString() << "\\n";
    if (logFile.isOpen()) {
        logFileOut << output << "\\n";
        logFileOut.flush();
        logFile.flush();
    } ''', '''    RielEditorLogging::write(logFile, logFileOut, output,
                             Game::consoleOutput || renderDiagnostics);''')

    dds = source / "src/tsre/texture/DdsLib.cpp"
    replace_once(dds, '''void DdsLib::run()
{
    QFile file(texture->pathid);''', '''void DdsLib::run()
{
    if (!texture) return;
    const auto invalid = [this](const QString &reason) {
        texture->loaded = false;
        texture->error = true;
        texture->errorMessage = "DDS: " + reason;
    };
    QFile file(texture->pathid);''')
    replace_once(dds, '''    if (width == 0 || height == 0) {''', '''    // Keep dimensions and allocation arithmetic bounded before reading pixels.
    if (width == 0 || height == 0 || quint64(width) * height > 64 * 1024 * 1024) {
        invalid("invalid or excessive image dimensions");''')
    replace_once(dds, '''    if (dataSize <= 0) {''', '''    if (dataSize <= 0 || dataSize > 512 * 1024 * 1024) {
        invalid("empty or excessive pixel payload");''')
    replace_once(dds, '''        const int expectedSize = blocksWide * blocksHigh * blockBytes;''',
                 '''        const qint64 expectedSize = qint64(blocksWide) * blocksHigh * blockBytes;''')
    replace_once(dds, '''        if (expectedSize <= 0 || expectedSize > data.size()) {''',
                 '''        if (expectedSize <= 0 || expectedSize > data.size()) {
            invalid("truncated compressed pixel payload");''')
    replace_once(dds, '''            srcRowPitch = expectedTightPitch;
        }
        if(texture->imageData != nullptr)''', '''            srcRowPitch = expectedTightPitch;
        }
        // Falling back to tight rows does not make a truncated file complete.
        if (quint64(srcRowPitch) * height > quint64(data.size())) {
            invalid("truncated uncompressed pixel payload");
            return;
        }
        if(texture->imageData != nullptr)''')
    # Both compressed and ordinary imports expose consistent CPU size metadata.
    text = dds.read_text(encoding="utf-8")
    old = '        texture->loaded = true;'
    if text.count(old) != 2:
        raise SystemExit(f"{dds}: expected two DDS success paths")
    dds.write_text(text.replace(old, '''        texture->bpp = texture->bytesPerPixel * 8;
        texture->imageSize = texture->width * texture->height * texture->bytesPerPixel;
        texture->error = texture->missing = false;
        texture->errorMessage.clear();
''' + old), encoding="utf-8")

    replace_once(source / "CMakeLists.txt",
                 'qt_add_executable(TSRE5vc ${TSRE5vc_SRC})',
                 '''qt_add_executable(TSRE5vc ${TSRE5vc_SRC})
add_executable(riel_loader_safety_tests
    tests/riel/RielLoaderSafetyTests.cpp
    src/tsre/texture/DdsLib.cpp src/tsre/texture/DdsLib.h)
target_include_directories(riel_loader_safety_tests PRIVATE src)
target_link_libraries(riel_loader_safety_tests PRIVATE Qt6::Core Qt6::Gui Qt6::OpenGL Threads::Threads)
if(CMAKE_SYSTEM_NAME STREQUAL "Linux" AND CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(riel_loader_safety_tests PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
    target_link_options(riel_loader_safety_tests PRIVATE -fsanitize=address,undefined)
endif()''')
