using System;
using System.Collections.Generic;
using AngryKoala.Coroutines;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    [DefaultExecutionOrder(-999)]
    public sealed class MonoPool : MonoBehaviour
    {
        [SerializeField] private string _poolKey;
        public string PoolKey => _poolKey;

        [SerializeField] private GameObject _prefab;

        [SerializeField] private int _initialSize;
        [SerializeField] private int _maxSize;

        [SerializeField] private Transform _container;

        private readonly Queue<IPoolableMono> _availablePoolables = new();
        private readonly HashSet<IPoolableMono> _activePoolables = new();

        private int _totalCreatedCount;

        private IPoolService _poolService;
        private ICoroutineService _coroutineService;

        private void Awake()
        {
            if (_container == null)
            {
                _container = transform;
            }

            WarmPool();
            TryRegisterWithServices();
        }

        private void OnDestroy()
        {
            TryDeregisterFromService();
        }

        private void WarmPool()
        {
            if (_prefab == null)
            {
                Debug.LogWarning($"MonoPool {_poolKey} has no prefab assigned.");
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
                _availablePoolables.Enqueue(instance);
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
                    Debug.LogError($"Prefab for pool {_poolKey} does not implement IPoolableMono.");
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

        private void TryRegisterWithServices()
        {
            _poolService ??= ServiceLocator.Get<IPoolService>();
            _coroutineService ??= ServiceLocator.Get<ICoroutineService>();

            if (_poolService != null)
            {
                _poolService.RegisterMonoPool(this);
            }
            else
            {
                Debug.LogWarning(
                    $"MonoPool {_poolKey} could not find IPoolService. Ensure a PoolService is present in the scene.");
            }

            if (_coroutineService == null)
            {
                Debug.LogWarning(
                    $"MonoPool {_poolKey} could not find ICoroutineService. Ensure a CoroutineService is present in the scene.");
            }
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
            if (_availablePoolables.Count > 0)
            {
                IPoolableMono instance = _availablePoolables.Dequeue();

                _activePoolables.Add(instance);
                instance.GetGameObject().SetActive(true);
                instance.OnRequestedFromPool();

                return instance;
            }

            if (_totalCreatedCount < _maxSize)
            {
                IPoolableMono newInstance = CreateNew();

                if (newInstance != null)
                {
                    _activePoolables.Add(newInstance);
                    newInstance.GetGameObject().SetActive(true);
                    newInstance.OnRequestedFromPool();
                }

                return newInstance;
            }

            Debug.LogWarning($"Pool {_poolKey} is exhausted (max {_maxSize}). Consider increasing its size.");
            return null;
        }

        public void Return(IPoolableMono instance)
        {
            ReturnInternal(instance);
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

            if (_coroutineService != null)
            {
                _coroutineService.RunDelayed(this, () => Return(instance), delaySeconds);
                return;
            }

            Return(instance);
        }

        public void ReturnAll()
        {
            List<IPoolableMono> snapshot = new List<IPoolableMono>(_activePoolables.Count);

            foreach (IPoolableMono item in _activePoolables)
            {
                snapshot.Add(item);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                ReturnInternal(snapshot[i]);
            }
        }
        
        public void ReturnAll(float delaySeconds)
        {
            if (delaySeconds <= 0f)
            {
                ReturnAll();
                return;
            }

            if (_coroutineService == null)
            {
                ReturnAll();
                return;
            }

            List<IPoolableMono> snapshot = new List<IPoolableMono>(_activePoolables.Count);
            foreach (IPoolableMono item in _activePoolables)
            {
                snapshot.Add(item);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                IPoolableMono instance = snapshot[i];
                _coroutineService.RunDelayed(this, () => Return(instance), delaySeconds);
            }
        }

        private void ReturnInternal(IPoolableMono instance)
        {
            if (instance == null)
            {
                return;
            }

            GameObject go = instance.GetGameObject();
            if (go == null)
            {
                _activePoolables.Remove(instance);
                return;
            }

            if (!_activePoolables.Remove(instance))
            {
                return;
            }

            instance.OnReturnedToPool();
            instance.GetGameObject().SetActive(false);
            instance.GetGameObject().transform.SetParent(_container, false);
            
            _availablePoolables.Enqueue(instance);
        }
    }
}