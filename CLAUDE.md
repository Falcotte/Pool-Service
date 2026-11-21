# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Workflow

This is a Unity project. Building, running, and testing all happen inside the Unity Editor (6000.6.4f1, from `ProjectSettings/ProjectVersion.txt`). Open the project in Unity, or drive the open editor with `unity command` (see the global editor automation rules). Open the solution in your IDE for code editing with IntelliSense.

Work happens on the `development` branch. `main` is the release branch.

To run the editor inspection tool: **Angry Koala > Pooling > Pool Inspector** (Play Mode only).

Tests live in `Assets/Pooling/Tests/Runtime` (Play Mode, `[UnityTest]` cases). Run them from the Test Runner, in Play Mode.

## Repository Layout

- `Assets/Pooling/` is the package. It holds `package.json` (`com.angrykoala.pooling`, version 2.0.0), the runtime code in `Scripts/`, the Editor code in `Scripts/Editor/`, and the tests in `Tests/Runtime/`. Consumers install it from git with `?path=/Assets/Pooling#<tag>`.
- `Assets/Demo/` is the demo scene, prefabs, a `PoolSettings` asset and demo scripts. It is outside the package and is not distributed with it.
- `Packages/manifest.json` lists `com.angrykoala.services` from git, plus the Unity packages the project uses.
- `README.md` at the root is the user guide. `Assets/Pooling/CHANGELOG.md` is the changelog.

## Distribution

The module is a UPM package whose root is `Assets/Pooling`. It depends on `com.angrykoala.services` 2.0.0, which the project must also list in its own `Packages/manifest.json`, because Unity does not resolve git dependencies between packages. The minimum Unity version is 6000.3 (`package.json`).

## Architecture

The package lives entirely under `Assets/Pooling/`. Its namespace is `AngryKoala.Pooling`. The Services module (`AngryKoala.Services`) is external and is not part of this repo.

### Assemblies

- `AngryKoala.Pooling`: runtime code in `Scripts/`. References `AngryKoala.Services`.
- `AngryKoala.Pooling.Editor`: `Scripts/Editor/`, Editor only.
- `AngryKoala.Pooling.Tests.Runtime`: `Tests/Runtime/`, Play Mode tests. Uses `UNITY_INCLUDE_TESTS` as a define constraint and `overrideReferences`.

`AssemblyInfo.cs` makes the internals visible to `AngryKoala.Pooling.Tests.Runtime`. Tests call `AddSettings`, `SetPrewarmPerFrame` and `TrackedInstanceCount` through that.

### Public API

**`IPoolService`** (extends `IService`): `Get` (two overloads), `Return` (with and without delay), `ReturnAll` (per prefab and all), `Prewarm`, `PrewarmAll` (a coroutine), and `GetData(List<PoolData>)`. Everything is keyed by a prefab `Component`.

**`IPoolable`**: `OnRequestedFromPool` and `OnReturnedToPool`. Optional, found on the instance's hierarchy.

**`PoolExhaustionRule`**: `Grow`, `ReturnNull`, `RecycleOldest`.

**`PoolData`** (`readonly struct`): `Prefab`, `InUseCount`, `WaitingCount`, `MaximumSize`, `ExhaustionRule`. Its constructor is internal.

**`PoolSettings`** (ScriptableObject, menu `Angry Koala/Pooling/Pool Settings`) with `PoolSettingsEntry` structs. The entries are read by the service through the `Entries` property.

**`PoolService`** (`sealed`, extends `BaseService<IPoolService>`): the implementation. It is a MonoBehaviour. It has no `DefaultExecutionOrder`.

### Structure

- `PoolService` owns a list of `PrefabPool` (`_pools`), a dictionary from the prefab GameObject to its pool (`_poolByPrefab`), and a registry from every instance's GameObject to its `PooledInstance` (`_instanceByGameObject`). It also keeps `_pendingReturns`, the delayed returns, and the serialized settings list and `_prewarmPerFrame`.
- `PrefabPool` (internal, one per prefab) holds the in-use instances in a `LinkedList` (oldest first, so `RecycleOldest` takes `First`) and the waiting instances in a `List`. It owns `Acquire`, `Release`, `Prewarm`, `PruneDestroyed` and the callback calls. Its container transform is named `<prefab name> Pool`, a child of the service.
- `PooledInstance` (internal, one per instance) holds the GameObject, the `IPoolable` array collected at creation, its linked-list node, a use id, the number of callbacks requested for the current use, and the pending-return fields.
- A `Staging` GameObject, inactive, is a child of the service. New instances are instantiated under it, so they never run `Awake` before they are placed.

### Get and Return

`PoolService.GetInternal` gets or creates the pool, then calls `PrefabPool.Acquire`. `Acquire` takes a waiting instance, or applies the exhaustion rule, or creates a new instance. Then it calls `BeginUse`, which adds the instance to the in-use list and bumps its use id. Then it parents and places the instance, activates it, and calls `InvokeRequested`. `InvokeRequested` stops if the instance was returned during a callback, by checking the use id.

`PoolService.Return` finds the `PooledInstance` through `TryFindInstance`, which walks up the parent chain from the component. A non-pool object or a non-in-use instance logs a warning. `PrefabPool.Release` clears the use, calls `InvokeReturned` for the callbacks requested on this use, deactivates the instance, parents it to the container, and adds it to the waiting list.

### Delayed returns

`Return(instance, delay)` sets `RemainingDelaySeconds` and `HasPendingReturn` on the instance and adds it to `_pendingReturns`. `Update` subtracts `Time.deltaTime` from each pending entry, from the end of the list. An entry that runs out is removed, and released only if it still has a pending return. `Release` and `BeginUse` clear `HasPendingReturn`, which is how an earlier return or a recycle cancels a delay. `OnDisable` clears the whole pending list.

### Pruning and destroyed instances

Instances destroyed from outside are not removed when they die. `PrefabPool.PruneDestroyed` removes them from the in-use list, the waiting list and the registry. It runs from `GetMissingCount` (so `Prewarm` and `PrewarmAll` call it), `CreateData`, `IsAtMaximum` when the total changes, and `BeginUse` once the in-use count reaches the prune threshold. `PopWaiting` also skips destroyed waiting instances.

### Prewarm

`Prewarm` and `PrewarmAll` use `GetMissingCount`, which caps the target by the maximum. `PrewarmAll` collects the missing creations from every settings entry, then creates `_prewarmPerFrame` of them per frame (minimum 1), and reports progress. A prewarmed instance is created inactive and parented to the container, so its `Awake` runs on the first `Get`.

### Errors and callbacks

`InvokeRequested` and `InvokeReturned` wrap each callback in `try/catch`, and log through `LogCallbackFailure` with the prefab name and the GameObject. Destroyed `IPoolable` components are skipped. A missing component type on `Get` is logged by `GetInternal`, and the instance is released again.

### Pool Inspector

`PoolInspectorEditorWindow` (`Scripts/Editor/`) lists one row per `PoolData`, from `GetData`. It finds the service with `ServiceLocator.TryGet` and refreshes on `EditorApplication.update` every 0.05 seconds when auto-refresh is on. Its toolbar and row buttons call `ReturnAll()` and `ReturnAll(prefab.transform)`. Its settings live in EditorPrefs.

## Conventions and gotchas

- The pool key is the prefab's GameObject, and `Get` accepts any component of it. `Get(prefab)` and `Get(prefab.transform)` share one pool. The component type is remembered from the first call for that prefab, and a later `Get<T>` for a type the instance lacks logs an error.
- A pool's settings are read once, when the pool is created. A settings change does not reach an existing pool.
- Keep `Acquire` ordering: parent and place, then activate, then `OnRequestedFromPool`. The tests `Get_FreshInstance_IsPlacedBeforeEnableAndRequestCallback` and `Get_ReusedInstance_IsPlacedBeforeEnableAndRequestCallback` check it.
- A `PooledInstance.Node` belongs to the in-use list of its pool. Add and remove it only through `BeginUse` and `Release`, or by the prune code, which removes it from the list too.
- The `_pendingReturns` loop in `Update` runs from the end, so `RemoveAt` is safe. Keep it that way when you change it.
- `OnDestroy` on the service sets `_isDestroyed`, which makes the public calls silent or return null. Call `base.OnDestroy()` first, as the code does.
- Services 2.0 resolution: resolve `IPoolService` in `Start` or later. The service registers in `Awake`.
- The `[DefaultExecutionOrder]` attribute is gone on purpose. Do not add it back to fix a start-order problem.
- `Prewarm` counts the total (in use plus waiting). It does not add to what is already there.
- Documentation must match the code and the tests. If behaviour changes, update `README.md`, `CHANGELOG.md` and the tests together.
