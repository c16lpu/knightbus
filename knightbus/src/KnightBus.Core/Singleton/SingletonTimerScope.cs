using System;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Singleton
{
    public class SingletonTimerScope : IDisposable
    {
        private readonly ILog _log;
        private readonly ISingletonLockHandle _lockHandle;
        private readonly bool _autoRelease; //clock drift makes triggers unstable for singleton use if the function is fast
        private readonly CancellationTokenSource _cts;
        private Task _runningTask;

        public SingletonTimerScope(ILog log, ISingletonLockHandle lockHandle, bool autoRelease, CancellationTokenSource  cancellationTokenSource)
        {
            _log = log;
            _lockHandle = lockHandle;
            _autoRelease = autoRelease;
            _cts = cancellationTokenSource;

            _runningTask = Task.Run(async () => await TimerLoop(_cts.Token), _cts.Token);
        }

        private async Task TimerLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RenewLock(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(19), cancellationToken);
                }
                catch (Exception)
                {
                    //Stop execution
                    break;
                }
            }
        }

        private async Task RenewLock(CancellationToken cancellationToken)
        {
            var delay = 0;
            var retries = 3;
            while (true)
            {
                if (retries == 0) return;
                var success = await _lockHandle.RenewAsync(_log, cancellationToken).ConfigureAwait(false);
                if (success) return;

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay += 1000;
                retries -= 1;
            }
        }
        

        public void Dispose()
        {
            _cts.Cancel();
            if (_lockHandle != null && _autoRelease)
            {
                _log.Information("Releasing lock {LockHandle}", _lockHandle);
                _lockHandle.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}