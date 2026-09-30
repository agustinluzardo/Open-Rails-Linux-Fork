#include <tsre/fileFunctions/ContentPath.h>
#include <QCoreApplication>
#include <QTemporaryDir>
#include <thread>
#include <atomic>

int main(int argc, char **argv) {
    QCoreApplication app(argc, argv);
    QTemporaryDir temp;
    if (!temp.isValid()) return 1;
    const QString root = temp.path() + "/MixedRoot";
    int failures = 0;
    auto check = [&](bool success, const char *name) {
        if (!success) { ++failures; qCritical() << "FAILED:" << name; }
    };
    auto put = [&](const QString &path) {
        QDir().mkpath(QFileInfo(path).absolutePath());
        QFile file(path);
        check(file.open(QIODevice::WriteOnly) && file.write("fixture") == 7, "write fixture");
    };
    auto resolve = [](const QString &path) { return ContentPath::resolveExistingCaseInsensitive(path); };
    const QString requested = root + "/GLOBAL/SHAPES/";
    const QString xtrack = root + "/Global/Shapes/XTrackOnly.S";
    const QString ytrack = root + "/global/shapes/YTrackOnly.s";
    put(requested + "Base.S"); put(xtrack); put(ytrack);
    put(root + "/Global/Textures/Rail.ACE");
    put(root + "/Global/TSection.DAT");
    check(resolve(requested + "XTrackOnly.S") == xtrack, "XTracks in split GLOBAL tree");
    check(resolve(requested + "ytrackonly.S") == ytrack, "YTracks and filename case fallback");
    check(resolve(root + "/GLOBAL/TEXTURES/rail.ace") == root + "/Global/Textures/Rail.ACE", "split textures");
    check(resolve(root + "/GLOBAL/tsection.dat") == root + "/Global/TSection.DAT", "split tsection catalog");
    check(resolve(requested + "Base.S") == requested + "Base.S", "exact source spelling wins");
    const QString routeA = root + "/ROUTES/ALPHA";
    const QString routeB = root + "/ROUTES/BETA";
    put(routeA + "/SHAPES/Base.S");
    put(routeA + "/Shapes/RouteOnly.S");
    put(routeA + "/Textures/Snow/Soil.ACE");
    put(routeB + "/Textures/Snow/Soil.ACE");
    check(resolve(routeA + "/SHAPES/routeonly.s") == routeA + "/Shapes/RouteOnly.S", "route shape overlay");
    check(resolve(routeA + "/TEXTURES/SNOW/soil.ace") == routeA + "/Textures/Snow/Soil.ACE", "season texture overlay");
    check(resolve(routeB + "/TEXTURES/SNOW/soil.ace") == routeB + "/Textures/Snow/Soil.ACE", "route texture isolation");
    check(resolve("gltfimg:Exact/Key") == "gltfimg:Exact/Key", "synthetic IDs are untouched");
    // Case-only duplicate files may only be selected through an exact physical path.
    put(root + "/Global/Shapes/Foo.S"); put(root + "/Global/Shapes/foo.s");
    check(resolve(requested + "FOO.S") == requested + "FOO.S", "ambiguous filename refused");
    check(resolve(root + "/Global/Shapes/Foo.S") == root + "/Global/Shapes/Foo.S", "exact ambiguous source accepted");
    put(requested + "Shared.S"); put(root + "/Global/Shapes/Shared.S");
    check(resolve(root + "/GLOBAL/Shapes/shared.s") == root + "/GLOBAL/Shapes/shared.s", "ambiguous split sources refused");
    // Directory mtime/inode generations invalidate both hits and misses without
    // a directory scan for each lookup; Linux nanosecond mtimes catch fast edits.
    check(resolve(requested + "Late.S") == requested + "Late.S", "initial miss");
    put(root + "/Global/Shapes/LATE.s");
    check(resolve(requested + "Late.S") == root + "/Global/Shapes/LATE.s", "new alias discovered after miss");
    QFile::remove(xtrack);
    check(resolve(requested + "XTrackOnly.S") == requested + "XTrackOnly.S", "deleted file invalidates hit");
    put(xtrack);
    check(resolve(requested + "XTrackOnly.S") == xtrack, "recreated source found");
    // Different spellings of an ordinary ancestor are resolved only once per
    // directory view and never merged into an arbitrary alternate installation.
    check(resolve((requested + "YTrackOnly.s").toUpper()) == ytrack, "mixed-case absolute ancestors");
    resolve(requested + "ytrackonly.s");
    const quint64 before = RielContentIndex::enumerationCount();
    QElapsedTimer timer; timer.start();
    for (int i = 0; i < 10000; ++i) {
        check(resolve(requested + "ytrackonly.s") == ytrack, "indexed repeat");
        check(resolve(requested + "missing.s") == requested + "missing.s", "indexed missing repeat");
    }
    check(RielContentIndex::enumerationCount() == before, "20000 lookups perform no directory enumeration");
    qInfo() << "RIEL_CONTENT_INDEX_LOOKUPS count=20000 ms=" << timer.elapsed()
            << "extra-directory-scans=" << RielContentIndex::enumerationCount() - before;
    std::atomic<bool> threadsOk{true};
    std::thread workers[4];
    for (auto &worker : workers)
        worker = std::thread([&] {
            for (int i = 0; i < 1000; ++i)
                if (resolve(requested + "ytrackonly.s") != ytrack) threadsOk = false;
        });
    for (auto &worker : workers) worker.join();
    check(threadsOk, "concurrent read-only index requests");
    check(RielContentIndex::enumerationCount() == before, "workers reuse the same directory view");
    qInfo() << "RIEL_CONTENT_INDEX_TEST failures=" << failures;
    return failures ? 1 : 0;
}
