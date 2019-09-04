using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace XsgTwitterBot.Services.Impl
{
    public abstract class BotEngineBase
    {
        protected CancellationTokenSource CancellationTokenSource;
        private readonly object _syncObject = new object();
        private Task _runningTask;

        public void Start()
        {
            CancellationToken cancellationToken;

            lock (_syncObject)
            {
                if (CancellationTokenSource != null)
                    throw new InvalidOperationException($"{GetType().Name} already started");

                CancellationTokenSource = new CancellationTokenSource();
                cancellationToken = CancellationTokenSource.Token;
            }

            _runningTask = Task.Factory.StartNew(RunLoop,
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Stop()
        {
            lock (_syncObject)
            {
                Log.Logger.Information($"Waiting {GetType().Name} to stop.");
                CancellationTokenSource?.Cancel();
                while (_runningTask.Status == TaskStatus.Running)
                {
                    Task.Delay(1000).Wait();
                    Log.Logger.Information($"Waiting {GetType().Name} to stop.");
                }
            }
        }

        protected abstract void RunLoop();
    }
}