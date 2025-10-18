using System;
using System.Collections.Generic;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    [DefaultExecutionOrder(-999)]
    public sealed class MonoPool : MonoBehaviour
    {
        public string PoolKey => _poolKey;

        [SerializeField] private string _poolKey;
        [SerializeField] private GameObject _prefab;

        [SerializeField] private int _initialSize;
        [SerializeField] private int _maxSize;

        [SerializeField] private Transform _container;

        private readonly Queue<IPoolableMono> _availableQueue = new();
        private int _totalCreatedCount;
        
        private IPoolService _poolService;

        private void Awake()
        {
            if (_container == null)
            {
                _container = transform;
            }

            WarmPool();
            TryRegisterWithService();
        }

        private void OnDestroy()
        {
            TryDeregisterFromService();
        }
        
        private void WarmPool()
        {
            if (_prefab == null)
            {
                Debug.LogWarning($"MonoPool '{_poolKey}' has no prefab assigned.");
                return;
            }

            int warmCount = Mathf.Min(_initialSize, _maxSize);
            for (int i = 0; i < warmCount; i++)
            {
                IPoolableMono instance = CreateNew();
                if (instance == null)
                {
                    continue;
                }

                instance.GetGameObject().SetActive(false);
                _availableQueue.Enqueue(instance);
            }
        }
        
        private IPoolableMono CreateNew()
        {
            try
            {
                GameObject go = Instantiate(_prefab, _container);
                IPoolableMono poolable = go.GetComponent<IPoolableMono>();
                if (poolable == null)
                {
                    Debug.LogError($"Prefab for pool '{_poolKey}' does not implement IPoolableMono.");
                    Destroy(go);
                    return null;
                }

                poolable.SetPool(this);
                _totalCreatedCount++;
                return poolable;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }
        
        private void TryRegisterWithService()
        {
            _poolService ??= ServiceLocator.Get<IPoolService>();

            if (_poolService != null)
            {
                _poolService.RegisterMonoPool(this);
                return;
            }

            Debug.LogWarning(
                $"MonoPool '{_poolKey}' could not find IPoolService. Ensure a PoolService is present in the scene.");
        }

        private void TryDeregisterFromService()
        {
            _poolService ??= ServiceLocator.Get<IPoolService>();

            _poolService?.DeregisterMonoPool(this);
        }

        public T Get<T>() where T : Component, IPoolableMono
        {
            IPoolableMono instance = GetInternal();
            
            return instance?.GetGameObject().GetComponent<T>();
        }

        public IPoolableMono Get()
        {
            return GetInternal();
        }
        
        private IPoolableMono GetInternal()
        {
            if (_availableQueue.Count > 0)
            {
                IPoolableMono instance = _availableQueue.Dequeue();
                instance.GetGameObject().SetActive(true);
                instance.OnRequestedFromPool();
                return instance;
            }

            if (_totalCreatedCount < _maxSize)
            {
                IPoolableMono newInstance = CreateNew();
                if (newInstance != null)
                {
                    newInstance.GetGameObject().SetActive(true);
                    newInstance.OnRequestedFromPool();
                }

                return newInstance;
            }

            Debug.LogWarning($"Pool '{_poolKey}' is exhausted (max {_maxSize}). Consider increasing its size.");
            return null;
        }

        public void Return(IPoolableMono instance)
        {
            ReturnInternal(instance);
        }

        private void ReturnInternal(IPoolableMono instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.OnReturnedToPool();
            instance.GetGameObject().SetActive(false);
            instance.GetGameObject().transform.SetParent(_container, false);
            _availableQueue.Enqueue(instance);
        }
    }
}