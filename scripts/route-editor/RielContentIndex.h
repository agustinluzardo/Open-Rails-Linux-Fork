#pragma once

#include <QCache>
#include <QDateTime>
#include <QDebug>
#include <QDir>
#include <QElapsedTimer>
#include <QFile>
#include <QFileInfo>
#include <QHash>
#include <QSet>
#include <mutex>
#ifdef Q_OS_LINUX
#include <sys/stat.h>
#endif

// Read-only views of content directories. Only the known MSTS GLOBAL and
// ROUTES trees merge case-only siblings; ordinary paths retain exact-directory
// precedence. No asset lookup recursively searches the filesystem.
namespace RielContentIndex {
struct Stamp {
    quint64 device = 0, inode = 0;
    qint64 seconds = 0, nanos = 0;
    bool exists = false;
    bool operator==(const Stamp &other) const {
        return exists == other.exists && device == other.device && inode == other.inode
                && seconds == other.seconds && nanos == other.nanos;
    }
};
inline Stamp stamp(const QString &path) {
    Stamp result;
#ifdef Q_OS_LINUX
    struct stat data;
    const QByteArray bytes = QFile::encodeName(path);
    if (::stat(bytes.constData(), &data) == 0) {
        result.exists = true;
        result.device = data.st_dev;
        result.inode = data.st_ino;
        result.seconds = data.st_mtim.tv_sec;
        result.nanos = data.st_mtim.tv_nsec;
    }
#else
    const QFileInfo info(path);
    result.exists = info.exists();
    result.seconds = info.lastModified().toMSecsSinceEpoch();
#endif
    return result;
}

struct DirectoryView {
    QHash<QString, Stamp> dependencies;
    QHash<QString, QStringList> entries;
    QSet<QString> warned;
    bool tooManyAliases = false;
    void watch(const QString &path) { dependencies.insert(path, stamp(path)); }
    bool current() const {
        for (auto it = dependencies.cbegin(); it != dependencies.cend(); ++it)
            if (!(stamp(it.key()) == it.value())) return false;
        return true;
    }
};
inline std::mutex mutex;
inline QCache<QString, DirectoryView> views(128);
inline quint64 directoryEnumerations = 0;

inline QStringList list(const QString &path, QDir::Filters filters) {
    ++directoryEnumerations;
    return QDir(path).entryList(filters | QDir::NoDotAndDotDot | QDir::Hidden | QDir::System);
}

// This walk runs only when creating/rebuilding a directory view, never for
// each shape. Unrelated ambiguous ancestor names are deliberately not merged.
inline QString baseDirectory(const QString &absolute, DirectoryView &view) {
    if (QFileInfo(absolute).isDir()) return absolute;
    QString current = "/";
    for (const QString &part : absolute.split('/', Qt::SkipEmptyParts)) {
        view.watch(current);
        const QString exact = QDir(current).filePath(part);
        if (QFileInfo(exact).isDir()) {
            current = exact;
            continue;
        }
        QStringList matches;
        for (const QString &entry : list(current, QDir::Dirs))
            if (entry.compare(part, Qt::CaseInsensitive) == 0)
                matches.push_back(QDir(current).filePath(entry));
        if (matches.size() != 1) {
            if (matches.size() > 1) view.tooManyAliases = true;
            return {};
        }
        current = matches.front();
    }
    return current;
}

inline int mstsPivot(const QStringList &parts) {
    for (int i = parts.size() - 1; i >= 0; --i) {
        const QString name = parts[i].toCaseFolded();
        const int tail = parts.size() - i;
        if (name == "global" && (tail == 1 || (tail <= 3
                && (parts[i + 1].compare("SHAPES", Qt::CaseInsensitive) == 0
                    || parts[i + 1].compare("TEXTURES", Qt::CaseInsensitive) == 0))))
            return i;
        if (name == "routes" && tail >= 2 && tail <= 4
                && (tail == 2 || parts[i + 2].compare("SHAPES", Qt::CaseInsensitive) == 0
                    || parts[i + 2].compare("TEXTURES", Qt::CaseInsensitive) == 0
                    || parts[i + 2].compare("TERRTEX", Qt::CaseInsensitive) == 0
                    || parts[i + 2].compare("OPENRAILS", Qt::CaseInsensitive) == 0))
            return i;
    }
    return -1;
}

inline DirectoryView *build(const QString &directory) {
    auto *view = new DirectoryView;
    const QStringList parts = directory.split('/', Qt::SkipEmptyParts);
    const int pivot = mstsPivot(parts);
    QStringList roots;
    if (pivot < 0) {
        const QString physical = baseDirectory(directory, *view);
        if (!physical.isEmpty()) roots.push_back(physical);
    } else {
        const QString base = baseDirectory("/" + parts.mid(0, pivot).join('/'), *view);
        if (!base.isEmpty()) roots.push_back(base);
        for (int i = pivot; i < parts.size() && !roots.isEmpty(); ++i) {
            QStringList next;
            for (const QString &root : roots) {
                view->watch(root);
                for (const QString &entry : list(root, QDir::Dirs))
                    if (entry.compare(parts[i], Qt::CaseInsensitive) == 0)
                        next.push_back(QDir(root).filePath(entry));
            }
            // Refuse pathological/ambiguous overlay trees without exploring
            // arbitrarily many branches. Normal split GLOBAL/SHAPES needs 2-4.
            if (next.size() > 16) {
                view->tooManyAliases = true;
                roots.clear();
                break;
            }
            roots = next;
        }
    }
    QSet<QString> physicalRoots;
    for (const QString &root : roots) {
        view->watch(root);
        const QString canonical = QFileInfo(root).canonicalFilePath();
        if (physicalRoots.contains(canonical)) continue;
        physicalRoots.insert(canonical);
        for (const QString &entry : list(root, QDir::AllEntries))
            view->entries[entry.toCaseFolded()].push_back(QDir(root).filePath(entry));
    }
    return view;
}

inline QString resolveFile(const QString &normalized) {
    const QFileInfo file(normalized);
    const QString directory = QDir::cleanPath(file.absolutePath());
    const QString foldedName = file.fileName().toCaseFolded();
    std::lock_guard<std::mutex> guard(mutex);
    DirectoryView *view = views.object(directory);
    if (!view || !view->current()) {
        view = build(directory);
        views.insert(directory, view);
    }
    const QStringList matches = view->entries.value(foldedName);
    if (matches.size() > 1 || view->tooManyAliases) {
        if (!view->warned.contains(foldedName)) {
            view->warned.insert(foldedName);
            qWarning() << "Riel ambiguous case-insensitive content path:" << normalized
                       << "matches" << matches;
        }
        return normalized;
    }
    if (matches.size() == 1 && QFileInfo::exists(matches.front())) return matches.front();
    return normalized;
}

inline quint64 enumerationCount() {
    std::lock_guard<std::mutex> guard(mutex);
    return directoryEnumerations;
}
inline void clear() {
    std::lock_guard<std::mutex> guard(mutex);
    views.clear();
}
}
