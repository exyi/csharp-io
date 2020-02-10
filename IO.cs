using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace csharp_io
{
    [AsyncMethodBuilder(typeof(IOTaskMethodBuilder<>))]
    public interface IO<T>
    {
        Task<T> UnsafePerformIO();

    }

    public static partial class IO
    {
        public static IO<T> Pure<T>(T result) => new PureIO<T>(result);
        public static IO<T> Error<T>(Exception result) => new ErrorIO<T>(result);
        public static IO<T> Do<T>(Func<IO<T>> func) => func();
        public static async IO<(T1, T2)> All<T1, T2>(IO<T1> a, IO<T2> b)
        {
            var a_ = a.UnsafePerformIO();
            var b_ = b.UnsafePerformIO();
            return (await a_, await b_);
        }
        public static async IO<(T1, T2, T3)> All<T1, T2, T3>(IO<T1> a, IO<T2> b, IO<T3> c)
        {
            var a_ = a.UnsafePerformIO();
            var b_ = b.UnsafePerformIO();
            var c_ = c.UnsafePerformIO();
            return (await a_, await b_, await c_);
        }
        class PureIO<T>: IO<T>
        {
            private readonly T result;

            public PureIO(T result)
            {
                this.result = result;
            }

            public Task<T> UnsafePerformIO()
            {
                return Task.FromResult(result);
            }
        }

        class ErrorIO<T>: IO<T>
        {
            private readonly Exception error;

            public ErrorIO(Exception error)
            {
                this.error = error;
            }

            public Task<T> UnsafePerformIO()
            {
                return Task.FromException<T>(error);
            }
        }
    }
}
