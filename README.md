# C# `IO<T>`

Turns out, the async/await syntax is quite powerful and extensible. With few tricks, you can implement custom awaitable and asyncable (or how should I call it?) types with quite different behavior.

### Why?

C# devs nowadays basically use `Task<T>` to mark function which have side effects, which is quite nice (unless all functions are marked like that :D). Returning `Task<T>` however only marks the function as non-pure, but does not actually make them pure. And you can still call the `Task` function from a non-task function and not `await` it, hiding the effect. It's a shame, since it is actually very easy fix.

The problem is that `Task<T>`-returning function starts performing the side effects in the moment you call it and the `Task<T>` is not referentially transparent. For example, if the function `A(x)` and `B(y, z)` behave purely, `y = A(x); B(y, y)` should be equivalent to `B(A(x), A(x))`. This is clearly not the case for expressions `Task.WhenAll(SendHttpRequest(x), SendHttpRequest(x))` and `y = SendHttpRequest(x); Task.WhenAll(y, y)`. May seem like a small thing, but thinking in terms of results of already running operations is simply more complicated than thinking about the self-contained operations.

### The `IO<T>` type

`IO<T>` is a drop in replacement for `Task<T>` with the difference that it is evaluated at the point it's awaited, not when it's created. This means, you can for example read `n` lines using this one-liner

```csharp
var lines = await IO.Sequence(Enumerable.Repeat(n, ReadLine));
```

It is also compatible with `Task<T>` in both directions - you can `await` Tasks in IO methods and `IO` may be awaited in `Task` methods. Also, there is a tiny helper for "do notation". For example, ReadLine may defined as


```csharp
IO<string> ReadLine = IO.Do(async () => await Console.In.ReadLineAsync())
```

You can call if from `Task<T>` method:

```csharp

public async Task Main() {
    IO<int> readInt = ReadLine.Select(l => int.Parse);
    var (a, b) = (await readInt, await readInt); // each await runs the action again

    await Console.Out.WriteLineAsync((a + b).ToString());
}

```

### Proof of concept

This thing is a proof concept, I just wanted to see if it's possible to use C#'s custom `AsyncMethodBuilder` for this purpose. Turns out it is and it is quite simple - have a look into the source, it's just ~200 lines of quite simple C# :) Maybe I'll try to explore if it may be used for other monads (like lists, Result or Reader/Writer/State), but that's quite certainly not going to be that intuitive.

This code should work reliably, if you find issues, feel free to discuss them in the issues. The performance is probably going to be very suboptimal, but this could be solved with a bit more work&code.
