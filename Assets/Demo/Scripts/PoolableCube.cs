using AngryKoala.Pooling;
using UnityEngine;

public class PoolableCube : MonoBehaviour, IPoolableMono
{
    [SerializeField] private Rigidbody _rigidbody;

    private MonoPool _pool;

    public void OnRequestedFromPool()
    {
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.linearVelocity = Vector3.zero;

        _pool.Return(this, 10f);
    }

    public void OnReturnedToPool()
    {
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