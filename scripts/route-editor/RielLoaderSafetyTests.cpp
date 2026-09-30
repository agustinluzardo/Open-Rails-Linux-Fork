#include <tsre/RielEditorLogging.h>
#include <tsre/texture/DdsLib.h>
#include <QCoreApplication>
#include <QTemporaryDir>
#include <QSet>
#include <QtEndian>
#include <atomic>
#include <cstring>
#include <thread>
#include <vector>

// The real DDS decoder is linked unchanged. Only Texture's unrelated rendering
// methods are omitted from this CPU-only test executable.
Texture::Texture() {}
Texture::~Texture() {}

namespace {
int failures = 0;
void check(bool ok, const char *name) {
    if (!ok) { ++failures; fprintf(stderr, "FAIL: %s\n", name); }
}

QByteArray dds(quint32 width, quint32 height, int components, quint32 pitch,
               const QByteArray &pixels, quint32 fourCC = 0) {
    quint32 header[32]{};
    header[0] = 0x20534444;
    header[1] = 124;
    header[2] = 0x100f;
    header[3] = height;
    header[4] = width;
    header[5] = pitch;
    header[19] = 32;
    header[20] = fourCC ? 4 : (components == 4 ? 0x41 : 0x40);
    header[21] = fourCC;
    header[22] = components * 8;
    header[23] = 0xff0000;
    header[24] = 0xff00;
    header[25] = 0xff;
    header[26] = components == 4 ? 0xff000000 : 0;
    header[27] = 0x1000;
    for (auto &value : header) value = qToLittleEndian(value);
    return QByteArray(reinterpret_cast<const char *>(header), sizeof(header)) + pixels;
}

void textureCase(const QString &path, const QByteArray &bytes, bool valid,
                 int width = 0, int height = 0, int components = 0,
                 const QByteArray &expected = {}) {
    QFile file(path);
    check(file.open(QIODevice::WriteOnly | QIODevice::Truncate), "write DDS fixture");
    check(file.write(bytes) == bytes.size(), "write complete DDS fixture");
    file.close();
    Texture texture;
    texture.pathid = path;
    DdsLib loader;
    loader.texture = &texture;
    loader.run();
    check(texture.loaded == valid, "DDS load result");
    if (valid) {
        check(!texture.error && !texture.missing, "valid DDS has no error");
        check(texture.width == width && texture.height == height &&
              texture.bytesPerPixel == components, "DDS dimensions and channels");
        check(texture.imageSize == width * height * components &&
              texture.bpp == components * 8, "DDS image size metadata");
        if (!expected.isEmpty())
            check(texture.imageData &&
                  std::memcmp(texture.imageData, expected.constData(), expected.size()) == 0,
                  "DDS channel conversion and row padding");
    } else {
        check(texture.imageData == nullptr && texture.compressedData.isEmpty(),
              "invalid DDS publishes no pixels");
    }
    delete[] texture.imageData;
}

void textures(const QString &path) {
    const QByteArray bgr = QByteArray::fromHex("112233445566778899");
    const QByteArray rgb = QByteArray::fromHex("332211665544998877");
    textureCase(path, dds(3, 2, 3, 12, bgr + "pad" + bgr + "pad"), true, 3, 2, 3,
                rgb + rgb);
    // Retain support for tightly packed files with an inaccurate pitch field.
    textureCase(path, dds(3, 2, 3, 12, bgr + bgr), true, 3, 2, 3, rgb + rgb);
    textureCase(path, dds(3, 2, 3, 2, bgr + bgr), true, 3, 2, 3, rgb + rgb);
    textureCase(path, dds(2, 1, 4, 8, QByteArray::fromHex("112233aa445566bb")), true,
                2, 1, 4, QByteArray::fromHex("332211aa665544bb"));
    textureCase(path, dds(4, 4, 3, 12, "x"), false);
    textureCase(path, dds(4, 4, 4, 16, QByteArray(63, 'x')), false);
    textureCase(path, dds(3, 2, 3, 12, QByteArray(17, 'x')), false);
    textureCase(path, dds(0xffffffff, 0xffffffff, 4, 0, "x"), false);
    textureCase(path, dds(65536, 65536, 4, 0, "x"), false);
    textureCase(path, dds(0, 4, 3, 0, "x"), false);
    for (quint32 fourCC : {0x31545844u, 0x33545844u, 0x35545844u}) {
        const int blockBytes = fourCC == 0x31545844u ? 8 : 16;
        textureCase(path, dds(5, 3, 4, 0, QByteArray(2 * blockBytes, 'x'), fourCC),
                    true, 5, 3, 4);
        textureCase(path, dds(5, 3, 4, 0, QByteArray(2 * blockBytes - 1, 'x'), fourCC),
                    false);
    }
    textureCase(path, "DDS ", false);
    DdsLib nullLoader;
    nullLoader.run();
}

void logging(const QString &path) {
    QFile file(path);
    check(file.open(QIODevice::WriteOnly | QIODevice::Truncate), "open concurrent log");
    QTextStream stream(&file);
    constexpr int workers = 4, messages = 5000;
    std::atomic<bool> go{false};
    std::vector<std::thread> threads;
    for (int worker = 0; worker < workers; ++worker)
        threads.emplace_back([&, worker] {
            while (!go.load()) std::this_thread::yield();
            const QString payload(1024, QChar('a' + worker));
            for (int message = 0; message < messages; ++message)
                RielEditorLogging::write(file, stream,
                    QString("[I] %1:%2 %3").arg(worker).arg(message).arg(payload), false);
        });
    go = true;
    for (auto &thread : threads) thread.join();
    file.close();
    check(file.open(QIODevice::ReadOnly), "read concurrent log");
    QSet<QByteArray> received;
    int lines = 0;
    while (!file.atEnd()) { received.insert(file.readLine()); ++lines; }
    check(lines == workers * messages && received.size() == lines,
          "concurrent log has every message exactly once");
    for (int worker = 0; worker < workers; ++worker) {
        const QString payload(1024, QChar('a' + worker));
        for (int message = 0; message < messages; ++message)
            check(received.contains(QString("[I] %1:%2 %3\n")
                      .arg(worker).arg(message).arg(payload).toUtf8()),
                  "concurrent log message is intact");
    }
    printf("RIEL_EDITOR_LOG_STRESS messages=%d workers=%d\n", lines, workers);
}
} // namespace

int main(int argc, char **argv) {
    QCoreApplication app(argc, argv);
    QTemporaryDir directory;
    if (!directory.isValid()) return 2;
    textures(directory.filePath(QString::fromUtf8("textura-ñ.dds")));
    logging(directory.filePath("editor.log"));
    printf("RIEL_LOADER_SAFETY failures=%d\n", failures);
    return failures ? 1 : 0;
}
