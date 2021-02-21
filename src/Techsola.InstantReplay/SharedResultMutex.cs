using System;
using System.Threading;

namespace Techsola.InstantReplay
{
    internal sealed class SharedResultMutex<T>
    {
        private readonly Func<T> resultFactory;
        private volatile BasicCompletionSource<T>? resultSource;

        /// <summary>
        /// The result factory is never invoked concurrently by the same <see cref="SharedResultMutex{T}"/> instance.
        /// </summary>
        public SharedResultMutex(Func<T> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        /// <summary>
        /// <para>
        /// If no other thread is currently getting the result, invokes the result factory and returns the result.
        /// Otherwise, blocks until the other thread is finished getting the result and then returns the same result as
        /// the other thread.
        /// </para>
        /// <para>
        /// The result factory is never invoked concurrently by the same <see cref="SharedResultMutex{T}"/> instance.
        /// </para>
        /// </summary>
        public T GetResult()
        {
            var (resultSource, didCreateSource) = GetResultSource();

            if (!didCreateSource) return resultSource.GetResult();

            try
            {
                try
                {
                    var result = resultFactory.Invoke();
                    resultSource.SetResult(result);
                    return result;
                }
                catch (Exception ex) when (resultSource.SetExceptionAndReturnFalse(ex)) // Use exception filter so that the stack information isn't lost on net35
                {
                    throw; // Never hit because of the exception filter, but the compiler doesn't know that.
                }
            }
            finally
            {
                this.resultSource = null; // Volatile write
            }
        }

        private (BasicCompletionSource<T> Source, bool DidCreateSource) GetResultSource()
        {
            var resultSource = this.resultSource; // Volatile read
            if (resultSource is null)
            {
                var newInvocationResult = new BasicCompletionSource<T>();

                resultSource = Interlocked.CompareExchange(ref this.resultSource, newInvocationResult, null);
                if (resultSource is null)
                    return (newInvocationResult, DidCreateSource: true);

                newInvocationResult.Dispose();
            }

            return (resultSource, DidCreateSource: false);
        }
    }
}
