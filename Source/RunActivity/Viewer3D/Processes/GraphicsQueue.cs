// COPYRIGHT 2026 by the Riel project.
//
// GPL-3.0-or-later. DesktopGL requires GPU resource creation to run on the
// thread that owns the OpenGL context.

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;

namespace Orts.Viewer3D.Processes
{
    internal static class GraphicsQueue
    {
        private static readonly Action RunQueue;
        private static readonly IList QueuedActions;

        static GraphicsQueue()
        {
            Type threading = typeof(Game).Assembly.GetType("Microsoft.Xna.Framework.Threading", throwOnError: false);
            MethodInfo method = threading?.GetMethod(
                "Run",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            QueuedActions = threading?.GetField("_queuedActions", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as IList;

            if (method != null && QueuedActions != null)
                RunQueue = method.CreateDelegate<Action>();
            else
                Trace.TraceWarning("MonoGame graphics work queue was not found; queued DesktopGL work will run once per frame.");
        }

        private static bool Pending => QueuedActions?.Count > 0;

        internal static void RunPending()
        {
            if (Pending)
                RunQueue();
        }

        internal static void WaitFor(UpdaterProcess process)
        {
            ArgumentNullException.ThrowIfNull(process);

            if (RunQueue == null)
            {
                process.WaitTillFinished();
                return;
            }

            while (!process.WaitTillFinished(1))
                RunPending();
        }

        internal static void Pump(TimeSpan budget)
        {
            if (RunQueue == null || budget <= TimeSpan.Zero)
                return;

            long deadline = Stopwatch.GetTimestamp() + (long)(budget.TotalSeconds * Stopwatch.Frequency);
            while (Stopwatch.GetTimestamp() < deadline)
            {
                if (Pending)
                    RunQueue();
                else
                    Thread.Sleep(1);
            }
        }

        internal static void PumpPending(TimeSpan budget)
        {
            if (RunQueue == null || !Pending || budget <= TimeSpan.Zero)
                return;

            long deadline = Stopwatch.GetTimestamp() + (long)(budget.TotalSeconds * Stopwatch.Frequency);
            long idleGrace = Math.Max(1, Stopwatch.Frequency / 4000); // 0.25 ms
            long idleSince = 0;

            do
            {
                if (Pending)
                {
                    RunQueue();
                    idleSince = 0;
                }
                else
                {
                    long now = Stopwatch.GetTimestamp();
                    if (idleSince == 0)
                        idleSince = now;
                    else if (now - idleSince >= idleGrace)
                        break;
                    Thread.Yield();
                }
            }
            while (Stopwatch.GetTimestamp() < deadline);
        }
    }
}
