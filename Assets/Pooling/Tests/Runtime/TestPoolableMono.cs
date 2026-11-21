using UnityEngine;

namespace AngryKoala.Pooling.Tests
{
    public sealed class TestPoolableMono : MonoBehaviour, IPoolableMono
    {
        public Vector3 PositionWhenRequested { get; private set; }

        public int RequestedCount { get; private set; }
        public int ReturnedCount { get; private set; }

        private MonoPool _pool;

        public void OnRequestedFromPool()
        {
            RequestedCount++;
            PositionWhenRequested = transform.position;
        }

        public void OnReturnedToPool()
        {
            ReturnedCount++;
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void SetPool(MonoPool pool)
        {
            _pool = pool;
        }

        public MonoPool GetPool()
        {
            return _pool;
        }
    }
}
