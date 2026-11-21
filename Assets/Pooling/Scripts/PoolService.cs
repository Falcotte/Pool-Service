using System;
using System.Collections;
using System.Collections.Generic;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling
{
    public sealed class PoolService : BaseService<IPoolService>, IPoolService
    {
        [SerializeField] private List<PoolSettings> _settings = new();

        [SerializeField] private int _prewarmPerFrame = 8;

        internal int TrackedInstanceCount => _instanceByGameObject.Count;

        private readonly Dictionary<GameObject, PrefabPool> _poolByPrefab = new();
        private readonly List<PrefabPool> _pools = new();
        private readonly Dictionary<GameObject, PooledInstance> _instanceByGameObject = new();
        private readonly List<PooledInstance> _pendingReturns = new();

        private Transform _staging;
        private bool _isDestroyed;

        private const string _stagingName = "Staging";
        private const string _poolNameSuffix = " Pool";

        private void OnDisable()
        {
            for (int i = 0; i < _pendingReturns.Count; i++)
            {
                _pendingReturns[i].HasPendingReturn = false;
                _pendingReturns[i].IsInPendingList = false;
            }

            _pendingReturns.Clear();
        }

        public T Get<T>(T prefab) where T : Component
        {
            ValidatePrefab(prefab);

            Transform prefabTransform = prefab.transform;

            return GetInternal(prefab, prefabTransform.position, prefabTransform.rotation, null);
        }

        public T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            ValidatePrefab(prefab);

            return GetInternal(prefab, position, rotation, parent);
        }

        public void Return(Component instance)
        {
            if (_isDestroyed)
            {
                return;
            }

            if (!TryFindInUseInstance(instance, out PooledInstance pooledInstance))
            {
                return;
            }

            pooledInstance.Pool.Release(pooledInstance);
        }

        public void Return(Component instance, float delaySeconds)
        {
            if (_isDestroyed)
            {
                return;
            }

            if (delaySeconds <= 0f)
            {
                Return(instance);
                return;
            }

            if (!TryFindInUseInstance(instance, out PooledInstance pooledInstance))
            {
                return;
            }

            pooledInstance.RemainingDelaySeconds = delaySeconds;
            pooledInstance.HasPendingReturn = true;

            if (!pooledInstance.IsInPendingList)
            {
                pooledInstance.IsInPendingList = true;
                _pendingReturns.Add(pooledInstance);
            }
        }

        public void ReturnAll(Component prefab)
        {
            if (_isDestroyed || prefab == null)
            {
                return;
            }

            if (_poolByPrefab.TryGetValue(prefab.gameObject, out PrefabPool pool))
            {
                pool.ReturnAll();
            }
        }

        public void ReturnAll()
        {
            if (_isDestroyed)
            {
                return;
            }

            for (int i = 0; i < _pools.Count; i++)
            {
                _pools[i].ReturnAll();
            }
        }

        public void Prewarm(Component prefab, int count)
        {
            ValidatePrefab(prefab);

            if (_isDestroyed)
            {
                return;
            }

            GetOrCreatePool(prefab.gameObject, prefab).Prewarm(count);
        }

        public IEnumerator PrewarmAll(Action<float> onProgress = null)
        {
            if (_isDestroyed)
            {
                yield break;
            }

            List<PrefabPool> creations = CollectPrewarmCreations();

            if (creations.Count == 0)
            {
                onProgress?.Invoke(1f);
                yield break;
            }

            onProgress?.Invoke(0f);

            int batchSize = Mathf.Max(1, _prewarmPerFrame);
            int processedCount = 0;

            while (processedCount < creations.Count)
            {
                if (_isDestroyed)
                {
                    yield break;
                }

                int batchEnd = Mathf.Min(processedCount + batchSize, creations.Count);

                while (processedCount < batchEnd)
                {
                    PrefabPool pool = creations[processedCount];
                    pool.TryCreateWaiting(pool.PrewarmCount);
                    processedCount++;
                }

                onProgress?.Invoke((float)processedCount / creations.Count);

                if (processedCount < creations.Count)
                {
                    yield return null;
                }
            }
        }

        public void GetData(List<PoolData> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (_isDestroyed)
            {
                return;
            }

            for (int i = 0; i < _pools.Count; i++)
            {
                results.Add(_pools[i].CreateData());
            }
        }

        internal void AddSettings(PoolSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings.Add(settings);
        }

        internal void SetPrewarmPerFrame(int prewarmPerFrame)
        {
            _prewarmPerFrame = prewarmPerFrame;
        }

        #region Internal Methods

        private T GetInternal<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
            if (_isDestroyed)
            {
                Debug.LogWarning($"Cannot get {prefab.name}: the pool service was destroyed.");
                return null;
            }

            PrefabPool pool = GetOrCreatePool(prefab.gameObject, prefab);
            PooledInstance instance = pool.Acquire(position, rotation, parent);

            if (instance == null || instance.GameObject == null)
            {
                return null;
            }

            if (TryResolveComponent(instance, out T component))
            {
                return component;
            }

            pool.Release(instance);
            Debug.LogError($"Cannot get a {typeof(T).Name} from a pooled instance of {prefab.name}: " +
                           "the instance has no such component.", prefab);

            return null;
        }

        #endregion

        private static bool TryResolveComponent<T>(PooledInstance instance, out T component) where T : Component
        {
            if (instance.Component != null && instance.Component is T cachedComponent)
            {
                component = cachedComponent;
                return true;
            }

            return instance.GameObject.TryGetComponent(out component);
        }

        private static void ValidatePrefab(Component prefab)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }
        }

        private PrefabPool GetOrCreatePool(GameObject prefabObject, Component prefabComponent)
        {
            if (!_poolByPrefab.TryGetValue(prefabObject, out PrefabPool pool))
            {
                pool = CreatePool(prefabObject);
            }

            pool.RememberComponentType(prefabComponent);

            return pool;
        }

        private PrefabPool CreatePool(GameObject prefabObject)
        {
            if (_staging == null)
            {
                var stagingObject = new GameObject(_stagingName);
                stagingObject.SetActive(false);
                _staging = stagingObject.transform;
                _staging.SetParent(transform, false);
            }

            var containerObject = new GameObject(prefabObject.name + _poolNameSuffix);
            Transform container = containerObject.transform;
            container.SetParent(transform, false);

            int prewarmCount = 0;
            int maximumSize = 0;
            var exhaustionRule = PoolExhaustionRule.Grow;

            if (TryFindEntry(prefabObject, out PoolSettingsEntry entry))
            {
                prewarmCount = Mathf.Max(0, entry.PrewarmCount);
                maximumSize = Mathf.Max(0, entry.MaximumSize);
                exhaustionRule = entry.ExhaustionRule;
            }

            var pool = new PrefabPool(prefabObject, prewarmCount, maximumSize, exhaustionRule, container, _staging,
                _instanceByGameObject);

            _poolByPrefab.Add(prefabObject, pool);
            _pools.Add(pool);

            return pool;
        }

        private bool TryFindEntry(GameObject prefabObject, out PoolSettingsEntry entry)
        {
            for (int i = 0; i < _settings.Count; i++)
            {
                PoolSettings settings = _settings[i];

                if (settings == null)
                {
                    continue;
                }

                IReadOnlyList<PoolSettingsEntry> entries = settings.Entries;

                for (int j = 0; j < entries.Count; j++)
                {
                    if (entries[j].Prefab == prefabObject)
                    {
                        entry = entries[j];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        private List<PrefabPool> CollectPrewarmCreations()
        {
            var creations = new List<PrefabPool>();
            var visitedPools = new HashSet<PrefabPool>();

            for (int i = 0; i < _settings.Count; i++)
            {
                PoolSettings settings = _settings[i];

                if (settings == null)
                {
                    continue;
                }

                IReadOnlyList<PoolSettingsEntry> entries = settings.Entries;

                for (int j = 0; j < entries.Count; j++)
                {
                    GameObject prefabObject = entries[j].Prefab;

                    if (prefabObject == null)
                    {
                        continue;
                    }

                    PrefabPool pool = GetOrCreatePool(prefabObject, null);

                    if (!visitedPools.Add(pool))
                    {
                        continue;
                    }

                    int missingCount = pool.GetMissingCount(pool.PrewarmCount);
                    for (int k = 0; k < missingCount; k++)
                    {
                        creations.Add(pool);
                    }
                }
            }

            return creations;
        }

        private bool TryFindInUseInstance(Component instance, out PooledInstance pooledInstance)
        {
            pooledInstance = null;

            if (ReferenceEquals(instance, null) || instance == null)
            {
                return false;
            }

            if (!TryFindInstance(instance, out pooledInstance))
            {
                Debug.LogWarning($"Cannot return {instance.name}: it was not created by a pool.", instance);
                return false;
            }

            if (!pooledInstance.IsInUse)
            {
                Debug.LogWarning($"Cannot return {instance.name}: it is not in use.", instance);
                return false;
            }

            return true;
        }

        private bool TryFindInstance(Component component, out PooledInstance pooledInstance)
        {
            for (Transform current = component.transform; current != null; current = current.parent)
            {
                if (_instanceByGameObject.TryGetValue(current.gameObject, out pooledInstance))
                {
                    return true;
                }
            }

            pooledInstance = null;
            return false;
        }

        private void Update()
        {
            if (_pendingReturns.Count == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            for (int i = _pendingReturns.Count - 1; i >= 0; i--)
            {
                if (i >= _pendingReturns.Count)
                {
                    continue;
                }

                PooledInstance pooledInstance = _pendingReturns[i];

                if (pooledInstance.HasPendingReturn && pooledInstance.GameObject != null)
                {
                    pooledInstance.RemainingDelaySeconds -= deltaTime;

                    if (pooledInstance.RemainingDelaySeconds > 0f)
                    {
                        continue;
                    }
                }

                _pendingReturns.RemoveAt(i);
                pooledInstance.IsInPendingList = false;

                if (pooledInstance.HasPendingReturn && pooledInstance.GameObject != null)
                {
                    pooledInstance.Pool.Release(pooledInstance);
                }

                pooledInstance.HasPendingReturn = false;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _isDestroyed = true;

            _pendingReturns.Clear();
            _pools.Clear();
            _poolByPrefab.Clear();
            _instanceByGameObject.Clear();
        }
    }
}
