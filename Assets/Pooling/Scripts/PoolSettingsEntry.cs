using System;
using UnityEngine;

namespace AngryKoala.Pooling
{
    [Serializable]
    public struct PoolSettingsEntry
    {
        [SerializeField] private GameObject _prefab;
        public GameObject Prefab => _prefab;

        [SerializeField] private int _prewarmCount;
        public int PrewarmCount => _prewarmCount;

        [Tooltip("0 means no maximum.")]
        [SerializeField] private int _maximumSize;
        public int MaximumSize => _maximumSize;

        [SerializeField] private PoolExhaustionRule _exhaustionRule;
        public PoolExhaustionRule ExhaustionRule => _exhaustionRule;

        internal PoolSettingsEntry(GameObject prefab, int prewarmCount, int maximumSize,
            PoolExhaustionRule exhaustionRule)
        {
            _prefab = prefab;
            _prewarmCount = prewarmCount;
            _maximumSize = maximumSize;
            _exhaustionRule = exhaustionRule;
        }
    }
}
