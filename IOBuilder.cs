using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;

namespace csharp_io
{
    public static partial class IO
    {
        public static TaskAwaiter<T> GetAwaiter<T>(this IO<T> io) =>
            io.UnsafePerformIO().GetAwaiter();
    }

    public struct IOTaskMethodBuilder<T>
    {
        [ThreadStatic]
        private static TaskCompletionSource<T> tcsGlobalSignal;

        private TaskCompletionSource<T> tcs;

        public static IOTaskMethodBuilder<T> Create() => new IOTaskMethodBuilder<T>();

        IAsyncStateMachine preboxedMachine;

        Action boxedMoveNext;

        IO<T> resultIO;

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            this.resultIO = new BuiltIO<TStateMachine>(stateMachine);

            // do nothing, this is called when the IO is created
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            // this method is not used.
            // We just use to signal TaskCompletionSource into the the current copy of the builder
            tcs = tcsGlobalSignal ?? throw new Exception();
            tcsGlobalSignal = null;
            preboxedMachine = stateMachine;
        }
        public void SetException(Exception exception)
        {
            tcs.SetException(exception);
        }

        public void SetResult(T result)
        {
            tcs.SetResult(result);
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(this.boxedMoveNext ?? (this.boxedMoveNext = this.preboxedMachine.MoveNext));
        }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            AwaitOnCompleted(ref awaiter, ref stateMachine);
        }

        public IO<T> Task => resultIO;

        private class BuiltIO<TStateMachine>: IO<T>
             where TStateMachine: IAsyncStateMachine
        {
            private readonly TStateMachine stateMachinePrototype;

            public BuiltIO(TStateMachine stateMachinePrototype)
            {
                this.stateMachinePrototype = stateMachinePrototype;
            }

            public Task<T> UnsafePerformIO(bool yield = false)
            {
                // here we start actually running the state machine
                var (machine, tcs) = CreateMachine();
                if (yield)
                    YieldAndMove(machine);
                else
                    machine.MoveNext();
                return tcs.Task;
            }

            private static void YieldAndMove(IAsyncStateMachine machine)
            {
                // TODO: do we want to pass SynchronizationContext in this case?
                System.Threading.Tasks.Task.Yield().GetAwaiter().OnCompleted(machine.MoveNext);
            }

            private static CallSite<Func<CallSite, object, IOTaskMethodBuilder<T>, object>> callSiteCache;
            
            private void SetTCS(IAsyncStateMachine machine, TaskCompletionSource<T> tcs)
            {
                // Option 1: (works only in release mode)
                // TODO: measure if it is any faster 
                tcsGlobalSignal = tcs;
                machine.SetStateMachine(machine);
                if (tcsGlobalSignal is object)
                {
                    IOTaskMethodBuilder<T> newBuilder;
                    newBuilder.preboxedMachine = machine;
                    newBuilder.tcs = tcs;
                    newBuilder.boxedMoveNext = null;
                    newBuilder.resultIO = this;
                    // TODO: add preboxed machine

                    tcsGlobalSignal = null;

                    machine.GetType().GetField("<>t__builder").SetValue(machine, newBuilder);

                    // // Using DLR
                    // // https://sharplab.io/#v2:D4AQTAjAsAULBuBDATgAgCaoLyoHYFMB3DAFgAoBKAblgGEA6AWTPQBoN8AzRAVwBsALtVjp6ASVwBnAA74AxgMo04MSfkR98mcKVgBvWAEgQAZlQBLXANSSBiAfmWxUL1KdSMAngGUByHgqoAgBGPOZ86PjIyq6osAC+sLC2/oFevqnWBjCx7pbWAB7KiSo6tKjZsW5mIBAAbG4kHmQA8sEAVvLWiOzpfgGFFBXOVbFkLJ64iAC25nIUiBT0IWERUdioRSOuJfFAA==

                    // if (callSiteCache is null)
                    // {
                    //     Type context = typeof(BuiltIO);
                    //     var args = new CSharpArgumentInfo[] {
                    //         CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    //         CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null)
                    //     };

                    //     callSiteCache = CallSite<Func<CallSite, object, IOTaskMethodBuilder<T>, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.SetMember(CSharpBinderFlags.None, "<>t__builder", context, args));
                    // }
                    // callSiteCache.Target(callSiteCache, machine, newBuilder);
                }
            }

            private (IAsyncStateMachine, TaskCompletionSource<T>) CreateMachine()
            {
                // var type = builder.stateMachinePrototype.GetType();
                // var machine = (IAsyncStateMachine)Activator.CreateInstance(type);
                // // we have to clone the prototype machine so it can be run multiple times
                // foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                //     f.SetValue(machine, f.GetValue(builder.stateMachinePrototype));

                IAsyncStateMachine machine =
                    typeof(TStateMachine).IsValueType ?
                    this.stateMachinePrototype :
                    (IAsyncStateMachine)MagicCloner.Clone(this.stateMachinePrototype);

                var tcs = new TaskCompletionSource<T>();
                SetTCS(machine, tcs);
                
                // the builder is a field in the stateMachine, so it's cloned too
                // we then just set the task completion source

                // replace the builder, so we can observe when this specific machine ends it's execution
                // type.GetField("<>t__builder", BindingFlags.Public | BindingFlags.Instance)
                //     .SetValue(machine, betterBuilder);
                return (machine, tcs);
            }
        }
    }

    internal class MagicCloner {
        [StructLayout(LayoutKind.Explicit)]
        struct HackCast {
            [FieldOffset(0)]
            public object obj;
            [FieldOffset(0)]
            public MagicCloner t;
            
            public static MagicCloner Cast(object x) {
                HackCast cast = new HackCast();
                cast.obj = x;
                return cast.t;
            }
        }
        
        public static Object Clone(Object x) {
            return HackCast.Cast(x).MemberwiseClone();
        }
    }
}
