# Changelog

## 1.0.0

First release.

- `ObjectPool` component: one pool per prefab, set up in the Inspector with an initial size and an optional maximum size.
- `Get()` and `Get(position, rotation)` return an active instance; `Release(instance)` and `Release(instance, delay)` put it back.
- The pool grows when it runs out of instances and keeps the new ones.
- `IPoolable` lets components reset themselves when their instance is taken from or returned to the pool.
- `PooledObject` lets an instance release itself without a reference to its pool.
- `CountActive`, `CountInactive` and `CountAll` report what the pool is doing.
- Sample scene with a launcher that fires pooled cubes.
