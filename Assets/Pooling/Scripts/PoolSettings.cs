using System.Collections.Generic;
using UnityEngine;

namespace AngryKoala.Pooling
{
    [CreateAssetMenu(menuName = "Angry Koala/Pooling/Pool Settings")]
    public sealed class PoolSettings : ScriptableObject
    {
        [SerializeField] private List<PoolSettingsEntry> _entries = new();
        public IReadOnlyList<PoolSettingsEntry> Entries => _entries;

        internal void AddEntry(GameObject prefab, int prewarmCount, int maximumSize, PoolExhaustionRule exhaustionRule)
        {
            _entries.Add(new PoolSettingsEntry(prefab, prewarmCount, maximumSize, exhaustionRule));
        }
    }
}
