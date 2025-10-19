using UnityEngine;

namespace AngryKoala.Pooling
{
    public interface IPoolableMono : IPoolable
    {
        GameObject GetGameObject();
        
        void SetPool(MonoPool pool);
        
        MonoPool GetPool();
    }
}