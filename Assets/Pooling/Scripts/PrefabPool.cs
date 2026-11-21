using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AngryKoala.Pooling
{
    internal sealed class PrefabPool
    {
        public GameObject Prefab { get; }
        public int PrewarmCount { get; }
        public int MaximumSize { get; }
        public PoolExhaustionRule ExhaustionRule { get; }

        private readonly Transform _container;
        private readonly Transform _staging;
        private readonly Dictionary<GameObject, PooledInstance> _registry;

        private readonly LinkedList<PooledInstance> _inUse = new();
        private readonly List<PooledInstance> _waiting = new();

        private Type _componentType;
        private bool _hasWarnedAboutMaximum;

        private int TotalCount => _inUse.Count + _waiting.Count;

        public PrefabPool(GameObject prefab, int prewarmCount, int maximumSize, PoolExhaustionRule exhaustionRule,
            Transform container, Transform staging, Dictionary<GameObject, PooledInstance> registry)
        {
            Prefab = prefab;
            PrewarmCount = prewarmCount;
            MaximumSize = maximumSize;
            ExhaustionRule = exhaustionRule;

            _container = container;
            _staging = staging;
            _registry = registry;
        }

        public void RememberComponentType(Component prefabComponent)
        {
            if (_componentType != null || prefabComponent == null)
            {
                return;
            }

            _componentType = prefabComponent.GetType();
        }

        public PooledInstance Acquire(Vector3 position, Quaternion rotation, Transform parent)
        {
            PooledInstance instance = PopWaiting();

            if (instance == null && IsAtMaximum())
            {
                switch (ExhaustionRule)
                {
                    case PoolExhaustionRule.ReturnNull:
                        WarnAboutMaximum();
                        return null;
                    case PoolExhaustionRule.RecycleOldest:
                        Release(_inUse.First.Value);
                        instance = PopWaiting();
                        break;
                }
            }

            if (instance == null)
            {
                GameObject gameObject = Object.Instantiate(Prefab, position, rotation, parent);
                instance = Register(gameObject);

                BeginUse(instance);
            }
            else
            {
                BeginUse(instance);

                Transform instanceTransform = instance.GameObject.transform;
                instanceTransform.SetParent(parent, false);
                instanceTransform.SetPositionAndRotation(position, rotation);
            }

            if (!instance.GameObject.activeSelf)
            {
                instance.GameObject.SetActive(true);
            }

            InvokeRequested(instance);

            return instance;
        }

        public bool Release(PooledInstance instance)
        {
            if (!instance.IsInUse)
            {
                return false;
            }

            instance.IsInUse = false;
            instance.HasPendingReturn = false;
            _inUse.Remove(instance.Node);

            if (instance.GameObject == null)
            {
                _registry.Remove(instance.GameObject);
                return true;
            }

            InvokeReturned(instance);

            if (instance.GameObject == null)
            {
                _registry.Remove(instance.GameObject);
                return true;
            }

            instance.GameObject.SetActive(false);
            instance.GameObject.transform.SetParent(_container, false);
            _waiting.Add(instance);

            return true;
        }

        public void ReturnAll()
        {
            if (_inUse.Count == 0)
            {
                return;
            }

            List<PooledInstance> snapshot = new(_inUse.Count);
            for (LinkedListNode<PooledInstance> node = _inUse.First; node != null; node = node.Next)
            {
                snapshot.Add(node.Value);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                Release(snapshot[i]);
            }
        }

        public int GetMissingCount(int targetCount)
        {
            PruneDestroyed();

            if (MaximumSize > 0)
            {
                targetCount = Mathf.Min(targetCount, MaximumSize);
            }

            return Mathf.Max(0, targetCount - TotalCount);
        }

        public void Prewarm(int count)
        {
            int missingCount = GetMissingCount(count);
            for (int i = 0; i < missingCount; i++)
            {
                CreateWaiting();
            }
        }

        public void CreateWaiting()
        {
            GameObject gameObject = Object.Instantiate(Prefab, _staging);
            gameObject.SetActive(false);

            PooledInstance instance = Register(gameObject);

            gameObject.transform.SetParent(_container, false);
            _waiting.Add(instance);
        }

        public PoolData CreateData()
        {
            PruneDestroyed();

            return new PoolData(Prefab, _inUse.Count, _waiting.Count, MaximumSize, ExhaustionRule);
        }

        private PooledInstance Register(GameObject gameObject)
        {
            Component component = _componentType != null ? gameObject.GetComponent(_componentType) : null;
            IPoolable[] poolables = gameObject.GetComponentsInChildren<IPoolable>(true);

            PooledInstance instance = new(this, gameObject, component, poolables);
            _registry[gameObject] = instance;

            return instance;
        }

        private PooledInstance PopWaiting()
        {
            while (_waiting.Count > 0)
            {
                int lastIndex = _waiting.Count - 1;
                PooledInstance instance = _waiting[lastIndex];
                _waiting.RemoveAt(lastIndex);

                if (instance.GameObject == null)
                {
                    _registry.Remove(instance.GameObject);
                    continue;
                }

                return instance;
            }

            return null;
        }

        private bool IsAtMaximum()
        {
            if (MaximumSize <= 0)
            {
                return false;
            }

            if (TotalCount < MaximumSize)
            {
                return false;
            }

            PruneDestroyed();

            return TotalCount >= MaximumSize;
        }

        private void BeginUse(PooledInstance instance)
        {
            instance.IsInUse = true;
            instance.UseId++;
            instance.HasPendingReturn = false;
            _inUse.AddLast(instance.Node);
        }

        private void PruneDestroyed()
        {
            LinkedListNode<PooledInstance> node = _inUse.First;
            while (node != null)
            {
                LinkedListNode<PooledInstance> next = node.Next;
                PooledInstance instance = node.Value;

                if (instance.GameObject == null)
                {
                    instance.IsInUse = false;
                    instance.HasPendingReturn = false;
                    _inUse.Remove(node);
                    _registry.Remove(instance.GameObject);
                }

                node = next;
            }

            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                PooledInstance instance = _waiting[i];

                if (instance.GameObject == null)
                {
                    _waiting.RemoveAt(i);
                    _registry.Remove(instance.GameObject);
                }
            }
        }

        private void WarnAboutMaximum()
        {
            if (_hasWarnedAboutMaximum)
            {
                return;
            }

            _hasWarnedAboutMaximum = true;
            Debug.LogWarning($"The pool for {Prefab.name} reached its maximum size of {MaximumSize}. Returning null.",
                Prefab);
        }

        private static void InvokeRequested(PooledInstance instance)
        {
            int useId = instance.UseId;

            for (int i = 0; i < instance.Poolables.Length; i++)
            {
                if (!instance.IsInUse || instance.UseId != useId)
                {
                    return;
                }

                try
                {
                    instance.Poolables[i].OnRequestedFromPool();
                }
                catch (Exception exception)
                {
                    LogCallbackFailure(nameof(IPoolable.OnRequestedFromPool), instance, exception);
                }
            }
        }

        private static void InvokeReturned(PooledInstance instance)
        {
            for (int i = 0; i < instance.Poolables.Length; i++)
            {
                try
                {
                    instance.Poolables[i].OnReturnedToPool();
                }
                catch (Exception exception)
                {
                    LogCallbackFailure(nameof(IPoolable.OnReturnedToPool), instance, exception);
                }
            }
        }

        private static void LogCallbackFailure(string callbackName, PooledInstance instance, Exception exception)
        {
            Debug.LogError($"{callbackName} threw an exception on a pooled instance of {instance.Pool.Prefab.name}.",
                instance.GameObject);
            Debug.LogException(exception, instance.GameObject);
        }
    }
}
