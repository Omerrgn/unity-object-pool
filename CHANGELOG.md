# Changelog

## 1.1.0 (unreleased)

- The pool is now a Unity package, `com.omerrgn.objectpool`. Install it from the Package Manager with the git URL in the README instead of copying files.
- The sample scene is imported from the package's Samples tab in the Package Manager.
- The scripts compile into their own assembly, `Omerrgn.ObjectPooling`.
- Requires Unity 6 (6000.0) or newer.
- Play mode tests cover reuse, growing, Max Size, rejected releases, delayed releases, `IPoolable` and `PooledObject`.
- The instances now stay in the pool's scene. A pool marked `DontDestroyOnLoad` used to lose them on the next scene load, and a pool in an additively loaded scene put them into the active scene.
- An exception thrown by an `IPoolable` method is logged and no longer leaves the instance stuck between the pool and the game.

## 1.0.0

First release.

- `ObjectPool` component: one pool per prefab, set up in the Inspector with an initial size and an optional maximum size.
- `Get()` and `Get(position, rotation)` return an active instance; `Release(instance)` and `Release(instance, delay)` put it back.
- The pool grows when it runs out of instances and keeps the new ones.
- `IPoolable` lets components reset themselves when their instance is taken from or returned to the pool.
- `PooledObject` lets an instance release itself without a reference to its pool.
- `CountActive`, `CountInactive` and `CountAll` report what the pool is doing.
- Sample scene with a launcher that fires pooled cubes.
