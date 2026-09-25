// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Riel is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Riel.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Processes
{
    /// <summary>
    /// Runs, on the graphics thread, the OpenGL work other threads have handed to MonoGame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the thread that holds the OpenGL context may create or fill a texture or a buffer, so
    /// MonoGame's OpenGL backend queues such a call from any other thread and makes the caller wait
    /// until the graphics thread runs the queue - which MonoGame itself does once per frame, after
    /// drawing. The engine was written for Direct3D, where any thread may do this directly, and
    /// both its other busy threads rely on it:
    /// </para>
    /// <list type="bullet">
    /// <item>The loader creates every texture and buffer of a route. At one of them per frame, and
    /// the loading screen's ten frames a second, a route would take many minutes to load.</item>
    /// <item>The updater fills the particle buffers of smoke, steam and rain while it prepares a
    /// frame - and the graphics thread waits for it to finish that frame before drawing the next.
    /// With the queue only run after the wait, the second buffer it fills in a frame waits for a
    /// graphics thread that is waiting for it: the game freezes as soon as a locomotive smokes.</item>
    /// </list>
    /// <para>
    /// Running the queue whenever the graphics thread would otherwise wait - for the updater, and
    /// for the next frame of the loading screen - removes both. The queue belongs to MonoGame and is
    /// internal, so it is reached by reflection; if a MonoGame update moves it, this degrades to
    /// plain waiting and says so in the log.
    /// </para>
    /// </remarks>
    internal static class GraphicsQueue
    {
        private static readonly Action run;
        private static readonly IList queued;

        static GraphicsQueue()
        {
            Type threading = typeof(Game).Assembly.GetType("Microsoft.Xna.Framework.Threading", throwOnError: false);
            MethodInfo method = threading?.GetMethod("Run", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
            queued = threading?.GetField("_queuedActions", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as IList;
            if (method != null && queued != null)
                run = method.CreateDelegate<Action>();
            else
                Trace.TraceWarning("MonoGame's graphics work queue was not found; work queued by other threads runs once per frame only.");
        }

        /// <summary>
        /// Whether another thread is waiting for the graphics thread. Read without MonoGame's lock:
        /// a queue caught mid-change is simply seen on the next look.
        /// </summary>
        private static bool Pending => queued?.Count > 0;

        /// <summary>Runs whatever is queued. Graphics thread only.</summary>
        internal static void RunPending()
        {
            if (Pending)
                run();
        }

        /// <summary>
        /// Waits for <paramref name="process"/> to finish its frame, running the work it and the
        /// other threads queue for the graphics thread meanwhile.
        /// </summary>
        internal static void WaitFor(ProcessBase process)
        {
            ArgumentNullException.ThrowIfNull(process);

            if (run == null)
            {
                process.WaitForComplection();
                return;
            }
            while (!process.WaitForComplection(1))
                RunPending();
        }

        /// <summary>
        /// Gives an already-active background upload burst a bounded slice of graphics-thread time.
        /// </summary>
        /// <remarks>
        /// During normal gameplay the loader creates OpenGL resources from its worker thread and
        /// MonoGame marshals those operations back to this thread. With VSync enabled, presentation
        /// can leave the worker advancing at roughly one resource per displayed frame. Dense MSTS
        /// tiles can therefore outrun streaming even though the loader itself is not CPU-bound.
        ///
        /// Unlike <see cref="Pump"/>, this method returns immediately when no upload is pending at
        /// entry, so it has effectively zero steady-state cost. Once a burst has started it keeps a
        /// very small window open for the loader to enqueue the next dependent texture/buffer.
        /// </remarks>
        internal static void PumpPending(TimeSpan budget)
        {
            if (run == null || !Pending || budget <= TimeSpan.Zero)
                return;

            long deadline = Stopwatch.GetTimestamp() + (long)(budget.TotalSeconds * Stopwatch.Frequency);
            // Do not spend the entire upload budget spinning when the loader has no
            // follow-up work: on a 60 Hz display that can burn ~18% of a CPU core.
            // A short idle grace still lets consecutive resource uploads share a frame.
            long idleGrace = Math.Max(1, Stopwatch.Frequency / 4000); // 0.25 ms
            long idleSince = 0;

            do
            {
                if (Pending)
                {
                    run();
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

        /// <summary>
        /// Keeps running queued work for <paramref name="budget"/>, including work queued after
        /// this starts: while a route loads, the loader queues the next texture a moment after the
        /// last one is done.
        /// </summary>
        internal static void Pump(TimeSpan budget)
        {
            if (run == null)
                return;

            long deadline = Stopwatch.GetTimestamp() + (long)(budget.TotalSeconds * Stopwatch.Frequency);
            while (Stopwatch.GetTimestamp() < deadline)
            {
                if (Pending)
                    run();
                else
                    Thread.Sleep(1);
            }
        }
    }
}
