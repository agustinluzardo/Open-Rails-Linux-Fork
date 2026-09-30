#pragma once

#include <QFile>
#include <QTextStream>
#include <QString>
#include <iostream>
#include <mutex>

namespace RielEditorLogging {

// Qt invokes its message handler on the emitting thread. QFile and QTextStream
// are reentrant, but sharing these particular instances requires serialization.
inline void write(QFile &file, QTextStream &stream, const QString &message, bool console) {
    static std::mutex mutex;
    const std::lock_guard<std::mutex> lock(mutex);
    if (console || !file.isOpen())
        std::cerr << message.toStdString() << '\n';
    if (file.isOpen()) {
        stream << message << '\n';
        stream.flush();
        file.flush();
    }
}

} // namespace RielEditorLogging
