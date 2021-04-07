using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace csharp_io
{
    [AsyncMethodBuilder(typeof(IOTaskMethodBuilder<>))]
    public interface IO<T>
    {
        /// <summary>
        /// Actually performs the IO action. The result is returned as a task.
        /// </summary>
        /// <param name="yield"> If true, the execution of this method finishes in constant time </param>
        Task<T> UnsafePerformIO(bool yield = false);

        async IO<U> Select<U>(Func<T, U> f) => f(await this);
        async IO<U> SelectMany<U>(Func<T, IO<U>> f) => await f(await this);
    }

    public static partial class IO
    {
        public static IO<T> Pure<T>(T result) => new PureIO<T>(result);
        public static IO<T> Error<T>(Exception result) => new ErrorIO<T>(result);
        public static IO<T> Do<T>(Func<IO<T>> func) => func();
        public static async IO<T> DoTask<T>(Func<Task<T>> func) => await func();
        public static async IO<(T1, T2)> All<T1, T2>(IO<T1> a, IO<T2> b)
        {
            var a_ = a.UnsafePerformIO(yield: true);
            var b_ = b.UnsafePerformIO();
            return (await a_, await b_);
        }
        public static async IO<(T1, T2, T3)> All<T1, T2, T3>(IO<T1> a, IO<T2> b, IO<T3> c)
        {
            var a_ = a.UnsafePerformIO(yield: true);
            var b_ = b.UnsafePerformIO(yield: true);
            var c_ = c.UnsafePerformIO();
            return (await a_, await b_, await c_);
        }
        public static async IO<T[]> All<T>(IEnumerable<IO<T>> a)
        {
            var a_ = a.Select(a => a.UnsafePerformIO(yield: true)).ToArray();
            return await Task.WhenAll(a_);
        }
        public static async IO<T[]> Sequence<T>(IEnumerable<IO<T>> a)
        {
            var r = new List<T>();
            foreach (var x in a)
                r.Add(await x);
            return r.ToArray();
        }

        [DebuggerDisplay("pure({result,nq})")]
        class PureIO<T>: IO<T>
        {
            private readonly T result;

            public PureIO(T result)
            {
                this.result = result;
            }

            public PureIO<U> Select<U>(Func<T, U> f) => new PureIO<U>(f(this.result));
            IO<U> IO<T>.Select<U>(Func<T, U> f) => new PureIO<U>(f(this.result));
            public IO<U> SelectMany<U>(Func<T, IO<U>> f) => f(this.result);

            public Task<T> UnsafePerformIO(bool yield = false)
            {
                return Task.FromResult(result);
            }
        }

        [DebuggerDisplay("error({error,nq})")]
        class ErrorIO<T>: IO<T>
        {
            private readonly Exception error;

            public ErrorIO<U> Select<U>(Func<T, U> f) => new ErrorIO<U>(error);
            IO<U> IO<T>.Select<U>(Func<T, U> f) => Select(f);
            public IO<U> SelectMany<U>(Func<T, IO<U>> f) => new ErrorIO<U>(error);

            public ErrorIO(Exception error)
            {
                this.error = error;
            }

            public Task<T> UnsafePerformIO(bool yield = false)
            {
                return Task.FromException<T>(error);
            }
        }
    }
}
