using System;
using System.Threading;

namespace RowanBridge;

internal sealed class DisposableAction : IDisposable
{
    private Action? _onDispose;

    public DisposableAction(Action onDispose)
    {
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
