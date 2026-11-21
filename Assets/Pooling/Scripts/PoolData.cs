using UnityEngine;

namespace AngryKoala.Pooling
{
    public readonly struct PoolData
    {
        public GameObject Prefab { get; }
        public int InUseCount { get; }
        public int WaitingCount { get; }
        public int MaximumSize { get; }
        public PoolExhaustionRule ExhaustionRule { get; }

        internal PoolData(GameObject prefab, int inUseCount, int waitingCount, int maximumSize,
            PoolExhaustionRule exhaustionRule)
        {
            Prefab = prefab;
            InUseCount = inUseCount;
            WaitingCount = waitingCount;
            MaximumSize = maximumSize;
            ExhaustionRule = exhaustionRule;
        }
    }
}
