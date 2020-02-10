using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace csharp_io
{
    public static partial class IO
    {
        public static TaskAwaiter<T> GetAwaiter<T>(this IO<T> io)
        {
            return io.UnsafePerformIO().GetAwaiter();
            // var a = new IOAwaiter<T>(io);

            // return a;
        }
    }

    enum BuilderState: byte {
        Running,
        Exception,
        Done
    }

    public class IOTaskMethodBuilder<T>
    {
        public static IOTaskMethodBuilder<T> Create() => new IOTaskMethodBuilder<T>();

        IAsyncStateMachine stateMachinePrototype;

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            this.stateMachinePrototype = stateMachine;
            // do nothing, this is called when the IO is created
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine) =>
            throw new NotImplementedException();
        public void SetException(Exception exception) => SetExceptionImpl(exception);
        protected virtual void SetExceptionImpl(Exception exception)
        {
            throw new Exception("nono, wrong");
        }

        public void SetResult(T result) => SetResultImpl(result);
        protected virtual void SetResultImpl(T result)
        {
            throw new Exception("nono, wrong");
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            IAsyncStateMachine machine = stateMachine;
            awaiter.OnCompleted(machine.MoveNext);
        }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            AwaitOnCompleted(ref awaiter, ref stateMachine);
        }

        public IO<T> Task => new BuiltIO(this);

        private class BuiltIO : IO<T>
        {
            private IOTaskMethodBuilder<T> builder;

            public BuiltIO(IOTaskMethodBuilder<T> iOTaskMethodBuilder)
            {
                this.builder = iOTaskMethodBuilder;
            }

            public Task<T> UnsafePerformIO()
            {
                // here we start actually running the state machine
                var (machine, tcs) = CreateMachine();
                machine.MoveNext();
                return tcs.Task;
            }

            private (IAsyncStateMachine, TaskCompletionSource<T>) CreateMachine()
            {
                var type = builder.stateMachinePrototype.GetType();
                var machine = (IAsyncStateMachine)Activator.CreateInstance(type);
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    f.SetValue(machine, f.GetValue(builder.stateMachinePrototype));
                var tcs = new TaskCompletionSource<T>();
                var betterBuilder = new TcsBuilder(tcs);
                type.GetField("<>t__builder", BindingFlags.Public | BindingFlags.Instance)
                    .SetValue(machine, betterBuilder);
                return (machine, tcs);
            }
        }

        class TcsBuilder: IOTaskMethodBuilder<T>
        {
            TaskCompletionSource<T> tcs;

            public TcsBuilder(TaskCompletionSource<T> tcs)
            {
                this.tcs = tcs;
            }

            protected override void SetExceptionImpl(Exception exception) =>
                tcs.SetException(exception);
            protected override void SetResultImpl(T result) =>
                tcs.SetResult(result);
        }
    }


    public struct IOAwaiter<T>: INotifyCompletion
    {
        readonly IO<T> io;

        bool done;
        T result;
        Action onCompleted;

        public IOAwaiter(IO<T> io)
        {
            this.io = io;
            this.done = false;
            this.result = default;
            this.onCompleted = null;
        }

        public bool IsCompleted => false;
        [Obsolete("Just don't use this manually")]
        public T GetResult() =>
            done ? result :
            throw new Exception("IO action not completed. Just don't call GetResult manually!");
        public void OnCompleted(Action completion)
        {
            if (done) completion();
            else onCompleted += completion;
        }

        internal void Complete(T result)
        {
            this.done = true;
            this.result = result;
            this.onCompleted?.Invoke();
        }
    }
}
