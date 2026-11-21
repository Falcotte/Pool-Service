using System;
using System.Collections;
using System.Collections.Generic;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    public interface IPoolService : IService
    {
        T Get<T>(T prefab) where T : Component;
        T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component;

        void Return(Component instance);
        void Return(Component instance, float delaySeconds);
        void ReturnAll(Component prefab);
        void ReturnAll();

        void Prewarm(Component prefab, int count);
        IEnumerator PrewarmAll(Action<float> onProgress = null);

        void GetData(List<PoolData> results);
    }
}
