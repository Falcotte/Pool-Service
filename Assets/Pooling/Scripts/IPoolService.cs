using System;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    public interface IPoolService : IService
    {
        T Get<T>(string poolKey) where T : Component, IPoolableMono;
        IPoolableMono Get(string poolKey);
        
        void Return(IPoolableMono instance);
        void Return(IPoolableMono instance, float delaySeconds);
        
        void RegisterMonoPool(MonoPool monoPool);
        void DeregisterMonoPool(MonoPool monoPool);
        
        void RegisterObjectPool<T>(string poolKey, ObjectPool<T> pool) where T : class, IPoolable;
        void RegisterObjectPool<T>(string poolKey, Func<T> factory, int initialSize, int maxSize) where T : class, IPoolable;
        void DeregisterObjectPool(string poolKey);
        
        T GetObject<T>(string poolKey) where T : class, IPoolable;
        
        void ReturnObject(IPoolable instance);
        void ReturnObject<T>(string poolKey, T instance) where T : class, IPoolable;
    }
}