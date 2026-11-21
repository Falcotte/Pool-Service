# Angry Koala Pooling

A pooling service for Unity. Pools are keyed by prefab. `Get` and `Return` reuse inactive instances instead of instantiating and destroying them. The service can prewarm pools, return an instance after a delay, and decide what happens when a pool reaches its maximum size. The Pool Inspector shows each pool live in Play Mode. The module depends on the Services module to find the service.

**Use it for**

- Objects that are created and destroyed often during play, for example bullets, enemies, effects, floating texts and pooled audio sources.

**Do not use it for**

- One-off objects. Instantiate them normally.
- Plain C# objects. Use Unity's `UnityEngine.Pool.ObjectPool<T>`. This module no longer has its own generic pool.

## Install

Add the package to the project's `Packages/manifest.json`. The Services module must be listed too:

```json
"com.angrykoala.pooling": "https://github.com/Falcotte/Pool-Service.git?path=/Assets/Pooling#v2.0.0",
"com.angrykoala.services": "https://github.com/Falcotte/Service-Locator.git?path=/Assets/Services#v2.0.0"
```

Unity does not resolve git dependencies between packages. Each AngryKoala module must be listed in the project's own `manifest.json`.

Minimum Unity version: 6000.3 (from `package.json`). Developed on Unity 6000.6.4f1.

## Public interface

```csharp
public interface IPoolService : IService
{
    // Takes an instance of the prefab from its pool, or creates one. Placed at the prefab's own position and rotation, with no parent.
    T Get<T>(T prefab) where T : Component;

    // Takes an instance, parents it, places it, then activates it. The parent is optional.
    T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component;

    // Returns an instance now. The instance may be the component itself or any child component of it.
    void Return(Component instance);

    // Returns an instance after the delay, in scaled seconds. A delay of zero or less returns it now.
    void Return(Component instance, float delaySeconds);

    // Returns every instance of one prefab.
    void ReturnAll(Component prefab);

    // Returns every instance of every pool.
    void ReturnAll();

    // Creates instances until the pool holds count instances in total (in use plus waiting), capped by its maximum.
    void Prewarm(Component prefab, int count);

    // A coroutine that prewarms every entry of the settings, a batch per frame. onProgress reports 0 to 1.
    IEnumerator PrewarmAll(Action<float> onProgress = null);

    // Clears the list and fills it with one PoolData per pool. Meant for tools.
    void GetData(List<PoolData> results);
}

public interface IPoolable
{
    // Called on each instance taken from the pool, after it is placed and activated.
    void OnRequestedFromPool();

    // Called when the instance goes back to the pool.
    void OnReturnedToPool();
}

public enum PoolExhaustionRule
{
    // Creates instances past the maximum.
    Grow,

    // Get returns null and logs a warning once per pool.
    ReturnNull,

    // Returns the instance that has been in use the longest, and hands it out again.
    RecycleOldest
}

public readonly struct PoolData
{
    // The prefab the pool is for.
    public GameObject Prefab { get; }

    // The number of instances currently taken.
    public int InUseCount { get; }

    // The number of instances waiting in the pool.
    public int WaitingCount { get; }

    // The maximum size, or 0 for no maximum.
    public int MaximumSize { get; }

    // The rule used when the pool is full.
    public PoolExhaustionRule ExhaustionRule { get; }
}

// PoolService is the implementation: a MonoBehaviour that extends BaseService<IPoolService>.
// It has two Inspector fields: Settings (a list of PoolSettings) and Prewarm Per Frame (default 8).

[CreateAssetMenu(menuName = "Angry Koala/Pooling/Pool Settings")]
public sealed class PoolSettings : ScriptableObject
{
    // The entries, one per prefab.
    public IReadOnlyList<PoolSettingsEntry> Entries { get; }
}

[Serializable]
public struct PoolSettingsEntry
{
    // The prefab this entry configures.
    public GameObject Prefab { get; }

    // The number of instances that PrewarmAll creates.
    public int PrewarmCount { get; }

    // The maximum size, or 0 for no maximum.
    public int MaximumSize { get; }

    // The rule used when the pool is full.
    public PoolExhaustionRule ExhaustionRule { get; }
}
```

## Example

A gun that fires bullets from a pool. The bullet prefab is set in the Inspector. The bullets are prewarmed by a loader at startup, using a `PoolSettings` asset that lists the bullet prefab.

```csharp
using AngryKoala.Pooling;
using AngryKoala.Services;
using UnityEngine;

public sealed class Gun : MonoBehaviour
{
    [SerializeField] private Bullet _bulletPrefab;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private float _bulletLifetimeSeconds = 1.5f;

    private IPoolService _poolService;

    private void Start()
    {
        _poolService = ServiceLocator.Get<IPoolService>();
    }

    public void Fire()
    {
        Bullet bullet = _poolService.Get(_bulletPrefab, _muzzle.position, _muzzle.rotation);

        _poolService.Return(bullet, _bulletLifetimeSeconds);
    }
}

public sealed class Bullet : MonoBehaviour, IPoolable
{
    [SerializeField] private Rigidbody _rigidbody;

    public void OnRequestedFromPool()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    public void OnReturnedToPool()
    {
    }
}

public sealed class PoolLoader : MonoBehaviour
{
    private IPoolService _poolService;

    private void Start()
    {
        _poolService = ServiceLocator.Get<IPoolService>();

        StartCoroutine(_poolService.PrewarmAll(progress => Debug.Log($"Prewarmed {progress:P0}")));
    }
}
```

The `PoolService` object is in the scene once. Its Settings list holds the `PoolSettings` asset. The asset has one entry for the bullet prefab, with a Prewarm Count, a Maximum Size and an Exhaustion Rule.

## Rules

- A pool is keyed by the prefab's GameObject. `Get` with any component of the prefab, or with the prefab's transform, uses the same pool. A pool is created on the first `Get`, `Prewarm` or `PrewarmAll` for that prefab.
- A pool's settings come from the first `PoolSettings` entry for its prefab, searched in the Settings list order. The entry is read when the pool is created. Editing the asset later does not change a pool that already exists.
- A prefab without an entry has no prewarm, no maximum and the `Grow` rule.
- `Get` parents the instance, places it, then activates it. `OnEnable` sees the new position and parent, and `OnRequestedFromPool` runs after activation.
- `Get(prefab)` without a position uses the prefab's own position and rotation, with no parent.
- An instance taken without a parent sits at the root of the service's scene, not the active scene. With a service that persists across scene loads, such an instance persists too until it is returned.
- A reused instance keeps the local scale it had. The pool does not reset it.
- A prewarmed instance is created inactive, and its `Awake` does not run until the first `Get` takes it.
- `IPoolable` is optional. Each `IPoolable` in the instance's hierarchy, including inactive children, is called. The list is found when the instance is created.
- `OnReturnedToPool` is called only on the `IPoolable` components that received `OnRequestedFromPool` for that use.
- A destroyed `IPoolable` component is skipped.
- An exception in a callback is logged with the prefab, and does not stop `Get` or `Return`.
- `Grow` creates instances past the maximum. The maximum still limits prewarming.
- `ReturnNull` makes `Get` return null, and logs a warning once per pool.
- `RecycleOldest` returns the instance that has been in use the longest, calls its `OnReturnedToPool`, and hands it out.
- Under `RecycleOldest`, do not request the same prefab from its own `OnRequestedFromPool`. On a full pool the instance recycles itself and the calls never end.
- `Prewarm(prefab, count)` brings the total (in use plus waiting) up to `count`, capped by the maximum. It does not add `count` more.
- `PrewarmAll` creates the instances in batches of Prewarm Per Frame (default 8), one batch per frame. Progress is reported as 0 at the start and 1 at the end. When nothing needs creating, it reports 1 only.
- Two `PrewarmAll` calls at once end at the target count, not twice the target.
- A delayed return is in scaled time, belongs to one use, and is cancelled by an earlier return or by a recycle.
- A second delayed return on the same use replaces the first delay.
- A delay of zero or less returns the instance at once.
- A delayed return is dropped if the service is disabled before it fires. The instance stays in use. A delayed return asked for while the service is disabled is kept and counts down once the service is enabled again.
- `Return(null)` and `Return(null, delay)` do nothing and log nothing.
- Returning an instance that is not in use logs a warning and does nothing.
- Returning an object that the pool did not create logs a warning and does not destroy it.
- An instance destroyed from outside is dropped the next time its pool looks at its instances (`Get`, `Prewarm`, `GetData`, or a prune on a large pool).
- `Get<T>` with a component type the instance no longer has logs an error naming the type, releases the instance, and returns null.
- Waiting instances are inactive children of the pool's container, named `<prefab name> Pool`, under the service. They are destroyed with the service.
- In-use instances are parented where the caller put them, and are not destroyed with the service.
- A pooled instance parented under another pooled instance is not returned with its parent. It goes inactive with the parent and stays in use until it is returned itself.
- A pool is never removed. If its prefab is destroyed, the pool and its waiting instances stay until the service is destroyed.
- After the service is destroyed, `Get` returns null with a warning. The other calls do nothing and log nothing.
- `GetData` throws `ArgumentNullException` on a null list. It fills the list you pass, and is meant for tools.
- Start order: the service registers itself in `Awake` through `BaseService`. Resolve it in `Start` or later, with `ServiceLocator.Get<IPoolService>()`.

## What it needs

- Unity 6000.3 or later.
- The Services module, `com.angrykoala.services` 2.0.0, listed in the project's `manifest.json`.
- Tests need `com.unity.test-framework`.

## Pool Inspector

Open it from **Angry Koala > Pooling > Pool Inspector**. It works in Play Mode only. Outside Play Mode it asks you to enter Play Mode. If there is no `PoolService` in the scene, it says so.

The window lists one row per pool, not one row per instance. Each row shows the prefab, In Use, Waiting, Total, Maximum (None when there is no maximum), the exhaustion rule, and a **Return All** button for that pool. It has:

- A search box that filters by prefab name.
- Sorting by any column. Click a header to sort by it, and click again to reverse the order.
- A click on a prefab name pings the prefab in the Project window.
- **Refresh** and **Return All Pools** in the toolbar.
- An Auto-refresh toggle. Auto-refresh is on by default. The search, sort and toggle settings are remembered in EditorPrefs.
- A footer with the number of pools and the totals of In Use and Waiting.

## Tests

The module has Play Mode tests in `Assets/Pooling/Tests/Runtime`. They are `[UnityTest]` cases, 46 in total. Run them from the Test Runner, in Play Mode.

## Demo

The demo is in `Assets/Demo`, outside the package. Open `Assets/Demo/Scenes/DemoScene.unity`. `CubeSpawner` prewarms the pools at start, then spawns one cube at a time from three prefabs, and returns each cube after 10 seconds. `PoolableCube` implements `IPoolable` and clears its velocity when it is taken.

## Upgrading from 1.x

| 1.x | 2.0.0 |
|---|---|
| `MonoPool` object with a string key | A prefab reference, passed to `Get` and `Return`. The pool is created on first use. |
| `_poolService.Get("Bullets")` | `_poolService.Get(_bulletPrefab)` |
| `IPoolableMono` with `GetGameObject`, `SetPool` and `GetPool` | `IPoolable` with `OnRequestedFromPool` and `OnReturnedToPool` |
| `ObjectPool<T>` | Unity's `UnityEngine.Pool.ObjectPool<T>` |
| `PoolKeys` constants | Gone. Use the prefab reference. |
| Set the position after `Get` | Pass it to `Get(prefab, position, rotation, parent)` |
| `Return(bullet, delaySeconds: 1.5f)` | `Return(bullet, 1.5f)` |

Also remove the `Assets/Services` copy from the project, and list `com.angrykoala.services` 2.0.0 in `manifest.json`. Resolve the service in `Start` or later, not in `Awake`.
