using System.Collections.Generic;
using UnityEngine;

namespace AngryKoala.Pooling
{
    internal sealed class PooledInstance
    {
        public PrefabPool Pool { get; }
        public GameObject GameObject { get; }
        public Component Component { get; }
        public IPoolable[] Poolables { get; }
        public LinkedListNode<PooledInstance> Node { get; }

        public bool IsInUse { get; set; }
        public int UseId { get; set; }

        public bool HasPendingReturn { get; set; }
        public bool IsInPendingList { get; set; }
        public float RemainingDelaySeconds { get; set; }

        public PooledInstance(PrefabPool pool, GameObject gameObject, Component component, IPoolable[] poolables)
        {
            Pool = pool;
            GameObject = gameObject;
            Component = component;
            Poolables = poolables;
            Node = new LinkedListNode<PooledInstance>(this);
        }
    }
}
