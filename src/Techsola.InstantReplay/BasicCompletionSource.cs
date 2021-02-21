using System;

#if NET35
using System.Threading;
#else
using System.Threading.Tasks;
#endif

namespace Techsola.InstantReplay
{
    /// <summary>
    /// Because <c>TaskCompletionSource</c> isn't available on .NET Framework 3.5.
    /// </summary>
    internal sealed class BasicCompletionSource<T> : IDisposable
    {
#if NET35
        private readonly ManualResetEvent completed = new(initialState: false);
        private Exception? exception;
        private T? result;
#else
        private readonly TaskCompletionSource<T> source = new();
#endif

        public void SetResult(T result)
        {
#if NET35
            if (completed.WaitOne(TimeSpan.Zero))
                throw new InvalidOperationException("The source is already completed.");

            this.result = result;
            completed.Set();
#else
            source.SetResult(result);
#endif
        }

        public void SetException(Exception exception)
        {
            if (exception is null) throw new ArgumentNullException(nameof(exception));

#if NET35
            if (completed.WaitOne(TimeSpan.Zero))
                throw new InvalidOperationException("The source is already completed.");

            this.exception = exception;
            completed.Set();
#else
            source.SetException(exception);
#endif
        }

        public bool SetExceptionAndReturnFalse(Exception exception)
        {
            SetException(exception);
            return false;
        }

        public T GetResult()
        {
#if NET35
            completed.WaitOne();

            if (exception is not null) throw exception;
            return result!;
#else
            return source.Task.GetAwaiter().GetResult();
#endif
        }

        public void Dispose()
        {
#if NET35
            completed.Close();
#endif
        }
    }
}
