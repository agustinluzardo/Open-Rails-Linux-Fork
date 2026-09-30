#pragma once

#include <QDebug>
#include <QElapsedTimer>
#include <array>
#include <atomic>

namespace RielStartup {
inline bool enabled() {
    static const bool value = qEnvironmentVariable("RIEL_EDITOR_STARTUP_TIMINGS") != "0";
    return value;
}

inline const QElapsedTimer &clock() {
    static const QElapsedTimer value = [] { QElapsedTimer t; t.start(); return t; }();
    return value;
}

inline void milestone(const char *name) {
    if (enabled())
        qInfo().noquote() << "RIEL_STARTUP_MILESTONE" << name
                         << "total-ms=" << clock().elapsed();
}

class Stage {
public:
    explicit Stage(const char *name) : name_(name) {
        if (!enabled()) return;
        timer_.start();
        qInfo().noquote() << "RIEL_STARTUP_BEGIN" << name_
                         << "total-ms=" << clock().elapsed();
    }
    ~Stage() {
        if (timer_.isValid())
            qInfo().noquote() << "RIEL_STARTUP_STAGE" << name_
                             << "ms=" << timer_.elapsed()
                             << "total-ms=" << clock().elapsed();
    }
private:
    const char *name_;
    QElapsedTimer timer_;
};

enum class Work { WorldParse, ShapeParse, TextureDecode, GpuUpload, Count };
struct Counter {
    std::atomic<quint64> calls{0};
    std::atomic<quint64> nanoseconds{0};
};
inline std::array<Counter, size_t(Work::Count)> counters;

// Hot operations accumulate measurements instead of writing one log per asset.
// Atomic counters allow private texture workers to record time independently.
class Measure {
public:
    explicit Measure(Work work, bool active = true) : work_(work) {
        if (active && enabled()) timer_.start();
    }
    ~Measure() {
        if (!timer_.isValid()) return;
        auto &counter = counters[size_t(work_)];
        counter.nanoseconds.fetch_add(timer_.nsecsElapsed(), std::memory_order_relaxed);
        counter.calls.fetch_add(1, std::memory_order_relaxed);
    }
private:
    Work work_;
    QElapsedTimer timer_;
};

inline void summary(const char *point) {
    if (!enabled()) return;
    static const char *names[] = {"world-parse", "shape-parse", "texture-decode", "gpu-upload"};
    for (size_t i = 0; i < counters.size(); ++i)
        qInfo().noquote() << "RIEL_STARTUP_WORK" << names[i] << "at=" << point
                         << "calls=" << counters[i].calls.load(std::memory_order_relaxed)
                         << "inclusive-ms=" << counters[i].nanoseconds.load(std::memory_order_relaxed) / 1e6
                         << "total-ms=" << clock().elapsed();
}
}
