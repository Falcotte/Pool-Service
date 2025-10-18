using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    public interface IPoolService : IService
    {
        T Get<T>(string poolKey) where T : Component, IPoolableMono;
        IPoolableMono Get(string poolKey);
        
        void Return(IPoolableMono instance);
        
        void RegisterMonoPool(MonoPool monoPool);
        void DeregisterMonoPool(MonoPool monoPool);
    }
}