using System;
using System.Collections.Generic;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    [DefaultExecutionOrder(-1000)]
    public class PoolService : BaseService<IPoolService>, IPoolService
    {
        private readonly Dictionary<string, MonoPool> _monoPools = new(StringComparer.Ordinal);

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

        #endregion
    }
}