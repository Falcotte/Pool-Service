using System;
using System.Collections.Generic;

namespace AngryKoala.Pooling
{
    public sealed class ObjectPool<T> : IObjectPool where T : class, IPoolable
    {
        private readonly Queue<T> _available = new();
        
        private readonly Func<T> _factory;
        
        private readonly int _maxSize;
        private int _totalCreated;
        
        public int TotalCreated => _totalCreated;
        public int MaxSize => _maxSize;
        
        public int AvailableCount => _available.Count;

        public ObjectPool(Func<T> factory, int initialSize, int maxSize)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (initialSize < 0)
            {
                initialSize = 0;
            }

            if (maxSize > 0 && initialSize > maxSize)
            {
                initialSize = maxSize;
            }

            _factory = factory;
            _maxSize = maxSize > 0 ? maxSize : int.MaxValue;

            WarmPool(initialSize);
        }
        
        private void WarmPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_totalCreated >= _maxSize)
                {
                    break;
                }

                T created = _factory();
                if (created == null)
                {
                    continue;
                }

                _totalCreated++;
                _available.Enqueue(created);
            }
        }
        
        public IPoolable Get()
        {
            return GetTyped();
        }

        private T GetTyped()
        {
            if (_available.Count > 0)
            {
                T instance = _available.Dequeue();
                if (instance != null)
                {
                    instance.OnRequestedFromPool();
                    return instance;
                }
            }

            if (_totalCreated < _maxSize)
            {
                T created = _factory();
                _totalCreated++;
                if (created != null)
                {
                    created.OnRequestedFromPool();
                }

                return created;
            }

            return null;
        }
        
        public void Return(IPoolable instance)
        {
            ReturnTyped(instance as T);
        }

        private void ReturnTyped(T instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.OnReturnedToPool();
            _available.Enqueue(instance);
        }
    }
}