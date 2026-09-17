using System;
using System.Threading.Tasks;

namespace EventFlux.Internal
{
    internal static class ParallelPublish
    {
        public static async Task WhenAllAsync(Task[] tasks)
        {
            var whenAll = Task.WhenAll(tasks);

            try
            {
                await whenAll.ConfigureAwait(false);
            }
            catch when (whenAll.Exception?.InnerExceptions.Count > 1)
            {
                throw new AggregateException(whenAll.Exception!.InnerExceptions);
            }
        }
    }
}
