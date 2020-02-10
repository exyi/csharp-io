using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace csharp_io
{
    public class UnitTest1
    {
        [Fact]
        public void ParallelExecution()
        {
            int numRunning = 0;
            var op = IO.Do(async () => {
                await Task.Delay(10);
                int number = Interlocked.Increment(ref numRunning);
                await Task.Delay(100);
                return number;
            });


            var (a, b, c) = IO.All(op, op, op).UnsafePerformIO().Result;
            Assert.Equal(new [] { a, b, c }.OrderBy(a => a), new [] { 1, 2, 3 });
        }

        [Fact]
        public void DoublePerform()
        {
            var results = new List<string>();
            var op = IO.Do(async () => {
                results.Add("abcd");
                return 0;
            });

            op.UnsafePerformIO().Wait();
            op.UnsafePerformIO().Wait();
            op.UnsafePerformIO().Wait();
            op.UnsafePerformIO().Wait();

            Assert.Equal(Enumerable.Repeat("abcd", 4), results);
        }

        public static readonly IO<string> FetchString = IO.Do(async () => {
            await Task.Delay(10);
            return Guid.NewGuid().ToString();
        });

        [Fact]
        public async Task AwaitJustWorks()
        {
            await FetchString;
            // var a = IO.Do(async () => {
            //     await Task.Delay(10);
            //     return $"{await FetchString} -- ${await FetchString}";
            // });

            // Assert.NotEqual(await a, await a);
        }
    }
}
