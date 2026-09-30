#pragma once

#include <QApplication>
#include <QElapsedTimer>
#include <QImage>
#include <QTimer>
#include <routeEditor/NaviWindow.h>
#include <routeEditor/RouteEditorWindow.h>
#include <routeEditor/RouteEditorGLWidget.h>
#include <tsre/world/Route.h>
#include <tsre/Game.h>
#include <tsre/shape/ShapeLib.h>
#include <tsre/shape/ComplexShape.h>
#include <tsre/texture/TexLib.h>
#include <tsre/texture/Texture.h>

// Start before LoadRouteEditor(): loading can block before app.exec() begins.
// CI also applies a process timeout, because a Qt timer cannot interrupt a
// stalled GUI thread. A successful launch must present a frame and subsequently
// dispatch timers on at least three separate turns of the real event loop.
class RielRouteWindowCheck : public QObject {
public:
    explicit RielRouteWindowCheck(QApplication &app) : QObject(&app), app_(app) {
        elapsed_.start();
        poll_.setInterval(100);
        connect(&poll_, &QTimer::timeout, this, [this] { poll(); });
        poll_.start();
    }

    bool succeeded() const { return succeeded_; }

private:
    bool expectedShapesLoaded() const {
        // Optional CI fixture expectations; ordinary user routes only check
        // their windows, first frame and responsiveness.
        const QStringList expected = qEnvironmentVariable("RIEL_EDITOR_CHECK_SHAPES")
                .split(';', Qt::SkipEmptyParts);
        for (const QString &name : expected) {
            bool loaded = false;
            if (Game::currentShapeLib)
                for (const auto &entry : Game::currentShapeLib->shape)
                    if (entry.second && entry.second->isLoaded()
                            && entry.second->getPathId().endsWith("/" + name, Qt::CaseInsensitive)) {
                        loaded = true;
                        break;
                    }
            if (!loaded) return false;
        }
        const QStringList textures = qEnvironmentVariable("RIEL_EDITOR_CHECK_TEXTURES")
                .split(';', Qt::SkipEmptyParts);
        for (const QString &name : textures) {
            bool loaded = false;
            for (const auto &entry : TexLib::mtex)
                if (entry.second && entry.second->loaded && !entry.second->error
                        && entry.second->glLoaded
                        && entry.second->pathid.endsWith("/" + name, Qt::CaseInsensitive)) {
                    loaded = true;
                    break;
                }
            if (!loaded) return false;
        }
        return true;
    }

    void poll() {
        RouteEditorWindow *window = nullptr;
        for (QWidget *widget : QApplication::topLevelWidgets()) {
            window = qobject_cast<RouteEditorWindow *>(widget);
            if (window) break;
        }
        auto *navi = window ? window->findChild<NaviWindow *>() : nullptr;
        auto *view = window ? window->findChild<RouteEditorGLWidget *>() : nullptr;
        const bool ready = window && window->isVisible()
                && window->property("rielMainShown").toBool()
                && navi && navi->isVisible() && view && view->isVisible()
                && view->isValid() && view->currentRoute() && view->currentRoute()->loaded
                && view->property("rielFirstFramePresented").toBool()
                && expectedShapesLoaded();
        if (ready && ++responsiveTurns_ >= 3) {
            const QImage frame = view->grabFramebuffer();
            if (frame.isNull()) {
                qCritical() << "RIEL_ROUTE_FULL_WINDOW_FAILED: empty framebuffer";
                app_.exit(1);
                return;
            }
            succeeded_ = true;
            poll_.stop();
            qInfo().noquote() << "RIEL_EDITOR_WINDOW event-loop-responsive";
            printf("RIEL_ROUTE_FULL_WINDOW_OK\n");
            app_.quit();
        } else if (elapsed_.elapsed() >= 20000) {
            qCritical() << "RIEL_ROUTE_FULL_WINDOW_FAILED: main window/frame/event loop not ready"
                        << "main=" << bool(window && window->isVisible())
                        << "navi=" << bool(navi && navi->isVisible());
            app_.exit(1);
        } else if (!ready) {
            responsiveTurns_ = 0;
        }
    }

    QApplication &app_;
    QElapsedTimer elapsed_;
    QTimer poll_;
    int responsiveTurns_ = 0;
    bool succeeded_ = false;
};
