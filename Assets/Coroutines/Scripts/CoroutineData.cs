using System;
using UnityEngine;

namespace AngryKoala.Coroutines
{
    [Serializable]
    public sealed class CoroutineData
    {
        public Coroutine Coroutine;
        
        public MonoBehaviour Owner;
        public string Tag;

        public string RoutineTypeName;

        public float StartedTime;
        public float StartedRealtime;

        public float ElapsedTime;
        public float ElapsedRealtime;

#if UNITY_EDITOR
        public float TotalPauseDuration;
#endif
    }
}