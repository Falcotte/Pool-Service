# Pool Service

A modular and efficient **pooling system** for Unity, designed to minimize object instantiation overhead and improve runtime performance.

It supports both **MonoBehaviour-based pooling** for scene objects and **C# object pooling** for non-Mono types, with a powerful **Editor Inspector Window** for live debugging and management.

---

## ✨ Features

* **Unified Pool Service**

  * Centralized management for all pools through `PoolService`

  * Works seamlessly with the **Angry Koala Service Locator**

* **MonoBehaviour Pooling**

  * Reuses Unity GameObjects efficiently with `MonoPool`

* **Object Pooling**

  * Generic pooling for lightweight C# objects via `ObjectPool<T>`

* **Editor Inspector Window**

  * Real-time visualization of all active pools

  * Expand/collapse, search, and refresh tools

  * One-click object selection and “Return All” buttons

---

## 🧩 Architecture Overview

### Core Components

#### `IPoolable` and `IPoolableMono`

Defines the contract for all poolable instances:

```
public interface IPoolable
{
    void OnRequestedFromPool();
    void OnReturnedToPool();
}
```

For Unity objects:

```
public interface IPoolableMono : IPoolable
{
    GameObject GetGameObject();
    void SetPool(MonoPool pool);
    MonoPool GetPool();
}
```

These methods are automatically called by their pools when objects are spawned or returned.

#### `MonoPool`

A dedicated MonoBehaviour that manages a specific prefab-based pool:

```
[SerializeField] private string _poolKey;
[SerializeField] private GameObject _prefab;
[SerializeField] private int _initialSize;
[SerializeField] private int _maxSize;
```

* Prewarms instances on startup (`_initialSize`)

* Supports delayed returns through a coroutine service

* Automatically registers to the global `PoolService`

* Efficiently tracks available vs. active instances

Example usage:

```
IPoolableMono bullet = _poolService.Get("Bullets");
_poolService.Return(bullet, delaySeconds: 1.5f);
```

#### `ObjectPool<T>`

Generic, type-safe pooling for non-Mono objects:

```
ObjectPool<MyProjectile> pool = new ObjectPool<MyProjectile>(
    factory: () => new MyProjectile(),
    initialSize: 10,
    maxSize: 100
);
```

* Uses `Func<T>` factory delegates for instantiation

* Automatically prewarms instances

* Tracks total created, available, and max capacity

#### `PoolService`

Implements `IPoolService` and extends `BaseService<IPoolService>`, providing global access to both Mono and object pools.

Supports:

* Registration and deregistration of all pool types

* Centralized `Get`, `Return`, and `ReturnAll` operations

* Safe integration with `ICoroutineService` for delayed actions

Example:

```
var enemy = _poolService.Get<Enemy>("Enemies");
_poolService.Return(enemy);
```

#### `PoolInspectorEditorWindow`

A full-featured Unity Editor window for runtime pool monitoring and management:

* Separate panels for **Mono Pools** and **Object Pools**

* Search and foldout persistence across sessions

* Real-time updates (auto-refresh)

* Buttons for “Select”, “Return All”, and “Deregister”

Open it via:

`Menu → Angry Koala → Pooling → Pool Inspector`

---

## 🧠 Usage Example

### 1. Create a Poolable Object

```
using AngryKoala.Pooling;
using UnityEngine;

public class Bullet : MonoBehaviour, IPoolableMono
{
    private MonoPool _pool;

    public GameObject GetGameObject() => gameObject;
    public void SetPool(MonoPool pool) => _pool = pool;
    public MonoPool GetPool() => _pool;

    public void OnRequestedFromPool()
    {
        // Reset state
        transform.position = Vector3.zero;
        gameObject.SetActive(true);
    }

    public void OnReturnedToPool()
    {
        // Cleanup or disable effects
        gameObject.SetActive(false);
    }
}
```

### 2. Create a MonoPool

Attach a `MonoPool` component in your scene:

| Field | Description |
|--------|-------------|
| **Pool Key** | Unique identifier (used to retrieve the pool). |
| **Prefab** | The object to pool (must implement `IPoolableMono`). |
| **Initial Size** | Number of instances prewarmed at start. |
| **Max Size** | Maximum number of instances allowed. |
| **Container** | Optional parent transform for pooled objects. |

### 3. Retrieve and Return Objects

```
IPoolService poolService = ServiceLocator.Get<IPoolService>();

Bullet bullet = poolService.Get<Bullet>("Bullets");
poolService.Return(bullet, delaySeconds: 2f);
```

### 4. Inspect in Editor

In Play Mode, open:

`Menu → Angry Koala → Pooling → Pool Inspector`

You’ll see a live list of all registered Mono and Object pools with their stats (available, active, created, etc.), plus quick buttons to:

* Select Pool, Container, or Prefab

* Return all active objects

* Deregister object pools on the fly

---

## 🧱 Extending the System

You can extend the framework easily by:

* Defining new pool key constants in `PoolKeys`

* Creating specialized pool variants (e.g., timed despawn, network pooling)

* Hooking into the PoolService for runtime analytics or profiling

---

## 🪲 Debugging Tips

* **Prefab Missing Interface:** Ensure your prefab implements `IPoolableMono`.

* **Null PoolService:** Confirm a `PoolService` exists in the scene.

* **Pool Exhausted Warning:** Increase `_maxSize` for high-demand objects.

* **ObjectPool reuse:** Always call `ReturnObject()` when done.