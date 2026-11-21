# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [2.0.0] - 2026-11-21

### Added

- `IPoolService.Get<T>(T prefab)` and `Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)`.
- `IPoolService.Return(Component)` and `Return(Component, float delaySeconds)`.
- `IPoolService.ReturnAll(Component prefab)` and `ReturnAll()`.
- `IPoolService.Prewarm(Component prefab, int count)` and `PrewarmAll(Action<float> onProgress = null)`, which prewarms in batches over several frames.
- `IPoolService.GetData(List<PoolData>)` and the `PoolData` readonly struct.
- `PoolExhaustionRule` (`Grow`, `ReturnNull` and `RecycleOldest`).
- `PoolSettings` ScriptableObject (menu `Angry Koala/Pooling/Pool Settings`) and its `PoolSettingsEntry` struct.
- `PoolService` Inspector field `Prewarm Per Frame`, default 8.
- Play Mode tests in `Tests/Runtime`.
- Assembly definitions `AngryKoala.Pooling`, `AngryKoala.Pooling.Editor` and `AngryKoala.Pooling.Tests.Runtime`.
- `package.json` for distribution as a UPM package from `Assets/Pooling`.

### Changed

- Pools are keyed by prefab. `Get`, `Return`, `ReturnAll` and `Prewarm` take the prefab component, not a string key.
- `IPoolable` is optional. Its callbacks are called on every `IPoolable` component in the instance's hierarchy.
- Services are resolved through the Services package, not a copy under `Assets/Services`.
- The Pool Inspector lists one row per pool, with In Use, Waiting, Total, Maximum, Rule and a Return All button.
- Prewarm count and maximum size come from `PoolSettings` assets listed on `PoolService`, not from fields on a `MonoPool`.

### Removed

- `MonoPool` and its Inspector setup.
- `IPoolableMono`, with `GetGameObject`, `SetPool` and `GetPool`.
- `ObjectPool<T>` and `IObjectPool`. Use Unity's `UnityEngine.Pool.ObjectPool<T>` instead.
- `PoolKeys` and the string-key API on `IPoolService`.
- The dependency on the Coroutines module. Delayed returns are handled by the service itself.
- The Utilities module dependency.
- The copies of the Services and Coroutines modules under `Assets`.
- `[DefaultExecutionOrder]` on the service. Callers resolve the service in `Start` or later.
- The per-instance listing in the Pool Inspector.

### Fixed

- A delayed return could return a later use of the same instance. A delay now belongs to the use it was given for.
- A pooled instance destroyed from outside stayed in the pool, and `Get` then threw `MissingReferenceException`. Destroyed instances are now dropped.
- `Get` activated the instance before the caller could place it. The instance is now parented and placed first, then activated.

## Upgrading from 1.x

- Replace `MonoPool` keys with prefab references, and pass the prefab to `Get` and `Return`.
- Replace `IPoolableMono` with `IPoolable`. Remove `GetGameObject`, `SetPool` and `GetPool`.
- Replace `ObjectPool<T>` with `UnityEngine.Pool.ObjectPool<T>`.
- Replace `Get` followed by a position change with `Get(prefab, position, rotation, parent)`.
- Remove the `Assets/Services` copy from the project and list `com.angrykoala.services` 2.0.0 in `manifest.json`.
- Resolve the service in `Start` or later, not in `Awake`.
