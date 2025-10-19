using System;
using System.Collections.Generic;
using AngryKoala.Coroutines;
using AngryKoala.Services;
using Unity.VisualScripting;
using UnityEngine;

namespace AngryKoala.Pooling
{
    [DefaultExecutionOrder(-1000)]
    public class PoolService : BaseService<IPoolService>, IPoolService
    {
        private readonly Dictionary<string, MonoPool> _monoPools = new(StringComparer.Ordinal);

        private readonly Dictionary<string, IObjectPool> _objectPools = new(StringComparer.Ordinal);
        private readonly Dictionary<IPoolable, IObjectPool> _objectOwners = new(ReferenceEqualityComparer.Instance);

        private ICoroutineService _coroutineService;

        protected override void Awake()
        {
            base.Awake();

            _coroutineService = ServiceLocator.Get<ICoroutineService>();
        }

        #region MonoPool

        public void RegisterMonoPool(MonoPool monoPool)
        {
            if (monoPool == null)
            {
                return;
            }

            string key = monoPool.PoolKey;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("Attempted to register a MonoPool with a null or empty pool key.");
                return;
            }

            if (_monoPools.TryGetValue(key, out MonoPool existing))
            {
                if (existing == monoPool)
                {
                    return;
                }

                Debug.LogWarning(
                    $"A different MonoPool is already registered for key '{key}'. Ignoring duplicate.");
                return;
            }

            _monoPools.Add(key, monoPool);
        }

        public void DeregisterMonoPool(MonoPool monoPool)
        {
            if (monoPool == null)
            {
                return;
            }

            string key = monoPool.PoolKey;
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_monoPools.TryGetValue(key, out MonoPool existing) && existing == monoPool)
            {
                _monoPools.Remove(key);
            }
        }

        public T Get<T>(string poolKey) where T : Component, IPoolableMono
        {
            MonoPool pool = GetMonoPool(poolKey);
            if (pool == null)
            {
                return null;
            }

            return pool.Get<T>();
        }

        public IPoolableMono Get(string poolKey)
        {
            MonoPool pool = GetMonoPool(poolKey);
            if (pool == null)
            {
                return null;
            }

            return pool.Get();
        }

        public void Return(IPoolableMono instance)
        {
            if (instance == null)
            {
                return;
            }

            MonoPool pool = instance.GetPool();
            if (pool != null)
            {
                pool.Return(instance);
                return;
            }

            Debug.LogWarning("Returning an instance without a valid pool. Destroying it.");
            Destroy(instance.GetGameObject());
        }

        public void Return(IPoolableMono instance, float delaySeconds)
        {
            if (instance == null)
            {
                return;
            }

            if (delaySeconds <= 0f)
            {
                Return(instance);
                return;
            }

            if (_coroutineService == null)
            {
                _coroutineService = ServiceLocator.Get<ICoroutineService>();
            }

            MonoPool owner = instance.GetPool();

            if (_coroutineService != null)
            {
                if (owner != null)
                {
                    _coroutineService.RunDelayed(owner, () => Return(instance), delaySeconds);
                    return;
                }

                _coroutineService.RunDelayed(() => Return(instance), delaySeconds);
                return;
            }

            Debug.LogWarning("CoroutineService was not found. Returning immediately.");
            Return(instance);
        }

        #endregion
        
        #region ObjectPool

        public void RegisterObjectPool<T>(string poolKey, ObjectPool<T> pool) where T : class, IPoolable
        {
            if (string.IsNullOrEmpty(poolKey) || pool == null)
            {
                return;
            }

            if (!_objectPools.TryAdd(poolKey, pool))
            {
                Debug.LogWarning($"ObjectPool with key '{poolKey}' already exists.");
                return;
            }
        }

        public void RegisterObjectPool<T>(string poolKey, Func<T> factory, int initialSize, int maxSize) where T : class, IPoolable
        {
            if (string.IsNullOrEmpty(poolKey))
            {
                Debug.LogWarning("Attempted to register an object pool with a null or empty pool key.");
                return;
            }

            if (factory == null)
            {
                Debug.LogWarning($"Object pool '{poolKey}' factory is null.");
                return;
            }

            if (_objectPools.ContainsKey(poolKey))
            {
                Debug.LogWarning($"Object pool '{poolKey}' is already registered.");
                return;
            }

            ObjectPool<T> pool = new ObjectPool<T>(factory, initialSize, maxSize);
            _objectPools.Add(poolKey, pool);
        }
        
        public void DeregisterObjectPool(string poolKey)
        {
            if (string.IsNullOrEmpty(poolKey))
            {
                return;
            }

            if (_objectPools.Remove(poolKey, out IObjectPool pool))
            {
                List<IPoolable> toRemove = new List<IPoolable>();
                foreach (KeyValuePair<IPoolable, IObjectPool> keyValuePair in _objectOwners)
                {
                    if (ReferenceEquals(keyValuePair.Value, pool))
                    {
                        toRemove.Add(keyValuePair.Key);
                    }
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    _objectOwners.Remove(toRemove[i]);
                }
            }
        }
        
        public T GetObject<T>(string poolKey) where T : class, IPoolable
        {
            IObjectPool pool = GetObjectPool(poolKey);
            if (pool == null)
            {
                return null;
            }

            IPoolable item = pool.Get();
            if (item == null)
            {
                return null;
            }

            _objectOwners[item] = pool;
            return item as T;
        }
        
        public void ReturnObject(IPoolable instance)
        {
            if (instance == null)
            {
                return;
            }

            if (_objectOwners.TryGetValue(instance, out IObjectPool pool))
            {
                pool.Return(instance);
                _objectOwners.Remove(instance);
                return;
            }

            Debug.LogWarning("ReturnObject was called for an instance with no recorded owner pool.");
        }

        public void ReturnObject<T>(string poolKey, T instance) where T : class, IPoolable
        {
            if (instance == null)
            {
                return;
            }

            IObjectPool pool = GetObjectPool(poolKey);
            if (pool == null)
            {
                return;
            }

            pool.Return(instance);
            _objectOwners.Remove(instance);
        }
        
        #endregion

        #region Utility

        private MonoPool GetMonoPool(string poolKey)
        {
            if (string.IsNullOrEmpty(poolKey))
            {
                Debug.LogWarning("Requested MonoPool with a null or empty key.");
                return null;
            }

            if (_monoPools.TryGetValue(poolKey, out MonoPool pool))
            {
                return pool;
            }

            Debug.LogWarning($"No MonoPool found for key '{poolKey}'.");
            return null;
        }

        private IObjectPool GetObjectPool(string poolKey)
        {
            if (string.IsNullOrEmpty(poolKey))
            {
                Debug.LogWarning("Requested ObjectPool with a null or empty key.");
                return null;
            }

            if (_objectPools.TryGetValue(poolKey, out IObjectPool pool))
            {
                return pool;
            }

            Debug.LogWarning($"No ObjectPool found for key '{poolKey}'.");
            return null;
        }

        #endregion
    }
}