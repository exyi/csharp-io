# C# `IO<T>`

Turns out, the async/await syntax is quite powerful and extensible. Not only can you make custom implementations of `Task<T>` that behave basically the same, with few tricks, you can also implement custom awaitable and asyncable (or how should I call it?) types with quite different behavior.

### Why?

C# devs nowadays basically use `Task<T>` to mark function which have side effects, which is quite useful. Returning `Task<T>` however only marks the function as non-pure, but does not actually make it pure. It's a shame, since it is actually very easy fix.

The problem is that `Task<T>`-returning function starts performing the side effects in the moment you call it and the `Task<T>` is not referentially transparent. For example, if the function `A(x)` and `B(y, z)` behave purely, `y = A(x); B(y, y)` should be equivalent to `B(A(x), A(x))`. This is clearly not the case for expressions `Task.WhenAll(SendHttpRequest(x), SendHttpRequest(x))` and `y = SendHttpRequest(x); Task.WhenAll(y, y)`. May seem like a small thing, but thinking in terms of results of already running operations is simply more complicated than thinking about the operations themselves.

### The `IO<T>` type

`IO<T>` is a drop in replacement for `Task<T>` with the difference that it is evaluated at the point it's awaited, not when it's created. This means, you can for example read `n` lines using this one-liner

```csharp
var lines = await IO.Sequence(Enumerable.Repeat(n, ReadLine));
```

It is also compatible with `Task<T>` in both directions - you can `await` Tasks in IO methods and vice versa. Also, there is a tiny helper for single-expression "do notation". For example, ReadLine may defined as


```csharp
IO<string> ReadLine = IO.Do(async () => await Console.In.ReadLineAsync())
```
