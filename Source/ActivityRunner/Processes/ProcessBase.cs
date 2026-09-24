using System;
using System.Diagnostics;
using System.Threading;

using FreeTrainSimulator.Common.Diagnostics;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Processes
{
    internal abstract class ProcessBase : IDisposable
    {
        private protected readonly Thread thread;
        private protected readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool disposedValue;
        private protected readonly GameHost gameHost;
        private readonly bool timerBased;
        private readonly int timerPeriod;
        private GameTime gameTime;

        private protected ProcessState processState;
        private protected Profiler profiler;

        protected ProcessBase(GameHost gameHost, string name, int timerPeriod = 0)
        {
            this.gameHost = gameHost;
            processState = new ProcessState(name);
            profiler = new Profiler(name);
            thread = new Thread(ThreadMethod) { Name = name };
            if (timerPeriod > 0)
            {
                timerBased = true;
                this.timerPeriod = timerPeriod;
            }
        }

        internal virtual void Start()
        {
            thread.Start();
        }

        internal virtual void Stop()
        {
            processState.SignalTerminate();
            cancellationTokenSource.Cancel();
        }

        internal bool WaitForExit(int millisecondsTimeout) =>
            !thread.IsAlive || (thread != Thread.CurrentThread && thread.Join(millisecondsTimeout));

        internal virtual void TriggerUpdate(GameTime gameTime)
        {
            this.gameTime = gameTime;
            processState.SignalStart();
        }

        internal void WaitForComplection()
        {
            processState.WaitTillFinished();
        }

        /// <summary>
        /// Waits at most <paramref name="millisecondsTimeout"/> for the current update to finish;
        /// true when it has.
        /// </summary>
        internal bool WaitForComplection(int millisecondsTimeout)
        {
            return processState.WaitTillFinished(millisecondsTimeout);
        }

        protected abstract void Update(GameTime gameTime);

        protected virtual void Initialize()
        { }

        protected void ThreadMethod()
        {
            profiler.SetThread();
            try
            {
                Initialize();
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (timerBased)
                        cancellationTokenSource.Token.WaitHandle.WaitOne(timerPeriod);
                    else
                        processState.WaitTillStarted();
                    if (cancellationTokenSource.IsCancellationRequested)
                        break;
                    try
                    {
                        profiler.Start();
                        Update(gameTime);
                    }
                    finally
                    {
                        profiler.Stop();
                        processState.SignalFinish();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
            {
                // Normal shutdown, including a cancelled loader.
            }
            catch (Exception error) when (!Debugger.IsAttached)
            {
                gameHost.ProcessReportError(error);
            }
            finally
            {
                processState.SignalTerminate();
                Trace.TraceInformation("{0} process exited.", processState.ProcessName);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                    processState.SignalTerminate();
                    processState.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
