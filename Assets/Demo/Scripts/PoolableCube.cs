using UnityEngine;

namespace AngryKoala.Pooling.Demo
{
    public sealed class PoolableCube : MonoBehaviour, IPoolable
    {
        [SerializeField] private Rigidbody _rigidbody;

        public void OnRequestedFromPool()
        {
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.linearVelocity = Vector3.zero;
        }

        public void OnReturnedToPool()
        {
        }
    }
}
