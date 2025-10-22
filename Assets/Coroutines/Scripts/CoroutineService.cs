using System;
using System.Collections;
using System.Collections.Generic;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Coroutines
{
    [DefaultExecutionOrder(-1002)]
    public sealed class CoroutineService : BaseService<ICoroutineService>, ICoroutineService
    {
        private readonly Dictionary<MonoBehaviour, List<Coroutine>> _ownedCoroutines = new();
        private readonly Dictionary<string, List<Coroutine>> _taggedCoroutines = new();

        private readonly Dictionary<Coroutine, CoroutineData> _coroutineData = new();

#if UNITY_EDITOR
        private bool _isEditorPaused;

        private float _pauseStartRealtime;
        private float _totalPauseDuration;
        
        private void OnEnable()
        {
            _isEditorPaused = false;
            _totalPauseDuration = 0f;

            UnityEditor.EditorApplication.pauseStateChanged += OnEditorPauseStateChanged;
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPauseStateChanged;
        }
        
        private void OnEditorPauseStateChanged(UnityEditor.PauseState pauseState)
        {
            if (pauseState == UnityEditor.PauseState.Paused)
            {
                _isEditorPaused = true;
                
                _pauseStartRealtime = Time.realtimeSinceStartup;
                return;
            }

            if (pauseState == UnityEditor.PauseState.Unpaused && _isEditorPaused)
            {
                float now = Time.realtimeSinceStartup;
                
                _totalPauseDuration += Mathf.Max(0f, now - _pauseStartRealtime);
                _isEditorPaused = false;
            }
        }
#endif

        public Coroutine Run(IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            TrackedRoutine trackedRoutine = new TrackedRoutine(this, null, null, routine);
            Coroutine coroutine = StartCoroutine(trackedRoutine);
            trackedRoutine.Coroutine = coroutine;

            RegisterCoroutineData(coroutine, null, null, routine);

            return coroutine;
        }

        public Coroutine Run(IEnumerator routine, string tag)
        {
            if (routine == null)
            {
                return null;
            }

            TrackedRoutine trackedRoutine = new TrackedRoutine(this, null, tag, routine);
            Coroutine coroutine = StartCoroutine(trackedRoutine);
            trackedRoutine.Coroutine = coroutine;

            RegisterTag(tag, coroutine);
            RegisterCoroutineData(coroutine, null, tag, routine);

            return coroutine;
        }

        public Coroutine Run(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            TrackedRoutine trackedRoutine = new TrackedRoutine(this, owner, null, routine);
            Coroutine coroutine = StartCoroutine(trackedRoutine);
            trackedRoutine.Coroutine = coroutine;

            RegisterOwner(owner, coroutine);
            RegisterCoroutineData(coroutine, owner, null, routine);

            return coroutine;
        }

        public Coroutine Run(MonoBehaviour owner, IEnumerator routine, string tag)
        {
            if (routine == null)
            {
                return null;
            }

            TrackedRoutine trackedRoutine = new TrackedRoutine(this, owner, tag, routine);
            Coroutine coroutine = StartCoroutine(trackedRoutine);
            trackedRoutine.Coroutine = coroutine;

            RegisterOwner(owner, coroutine);
            RegisterTag(tag, coroutine);
            RegisterCoroutineData(coroutine, owner, tag, routine);

            return coroutine;
        }

        public Coroutine RunDelayed(Action action, float delaySeconds)
        {
            return Run(DelayRoutine(action, delaySeconds));
        }

        public Coroutine RunDelayed(Action action, float delaySeconds, string tag)
        {
            return Run(DelayRoutine(action, delaySeconds), tag);
        }

        public Coroutine RunDelayed(MonoBehaviour owner, Action action, float delaySeconds)
        {
            return Run(owner, DelayRoutine(action, delaySeconds));
        }

        public Coroutine RunDelayed(MonoBehaviour owner, Action action, float delaySeconds, string tag)
        {
            return Run(owner, DelayRoutine(action, delaySeconds), tag);
        }

        public void Stop(Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            try
            {
                StopCoroutine(coroutine);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (_coroutineData.TryGetValue(coroutine, out CoroutineData data))
            {
                if (data.Owner != null)
                {
                    RemoveFromOwners(data.Owner, coroutine);
                }

                if (!string.IsNullOrEmpty(data.Tag))
                {
                    RemoveFromTags(data.Tag, coroutine);
                }

                _coroutineData.Remove(coroutine);
            }
        }

        public void StopAll()
        {
            try
            {
                StopAllCoroutines();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            _ownedCoroutines.Clear();
            _taggedCoroutines.Clear();
            _coroutineData.Clear();
        }

        public void StopAll(MonoBehaviour owner)
        {
            if (owner == null)
            {
                return;
            }

            if (_ownedCoroutines.TryGetValue(owner, out List<Coroutine> coroutineList))
            {
                Coroutine[] coroutines = coroutineList.ToArray();

                for (int i = 0; i < coroutines.Length; i++)
                {
                    Coroutine coroutine = coroutines[i];
                    if (coroutine == null)
                    {
                        continue;
                    }

                    try
                    {
                        StopCoroutine(coroutine);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    if (_coroutineData.TryGetValue(coroutine, out CoroutineData data))
                    {
                        if (!string.IsNullOrEmpty(data.Tag))
                        {
                            RemoveFromTags(data.Tag, coroutine);
                        }

                        _coroutineData.Remove(coroutine);
                    }

                    RemoveFromOwners(owner, coroutine);
                }

                _ownedCoroutines.Remove(owner);
            }
        }

        public void StopAll(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            if (_taggedCoroutines.TryGetValue(tag, out List<Coroutine> coroutineList))
            {
                Coroutine[] snapshot = coroutineList.ToArray();

                for (int i = 0; i < snapshot.Length; i++)
                {
                    Coroutine coroutine = snapshot[i];
                    if (coroutine == null)
                    {
                        continue;
                    }

                    try
                    {
                        StopCoroutine(coroutine);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    if (_coroutineData.TryGetValue(coroutine, out CoroutineData data))
                    {
                        if (data.Owner != null)
                        {
                            RemoveFromOwners(data.Owner, coroutine);
                        }

                        _coroutineData.Remove(coroutine);
                    }

                    RemoveFromTags(tag, coroutine);
                }

                _taggedCoroutines.Remove(tag);
            }
        }

        public IReadOnlyList<CoroutineData> GetData()
        {
            List<CoroutineData> list = new List<CoroutineData>(_coroutineData.Count);
            
            float nowRealtime = Time.realtimeSinceStartup;
            float nowTime = Time.time;

            foreach (KeyValuePair<Coroutine, CoroutineData> keyValuePair in _coroutineData)
            {
                list.Add(GetCoroutineDataCopy(keyValuePair.Value, nowTime, nowRealtime));
            }

            return list;
        }

        public IReadOnlyList<CoroutineData> GetData(MonoBehaviour owner)
        {
            if (owner == null)
            {
                return Array.Empty<CoroutineData>();
            }

            List<CoroutineData> list = new List<CoroutineData>();
            
            float nowRealtime = Time.realtimeSinceStartup;
            float nowTime = Time.time;

            foreach (KeyValuePair<Coroutine, CoroutineData> keyValuePair in _coroutineData)
            {
                CoroutineData data = keyValuePair.Value;
                if (data.Owner == owner)
                {
                    list.Add(GetCoroutineDataCopy(data, nowTime, nowRealtime));
                }
            }

            return list;
        }

        public IReadOnlyList<CoroutineData> GetData(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return Array.Empty<CoroutineData>();
            }

            List<CoroutineData> list = new List<CoroutineData>();
           
            float nowRealtime = Time.realtimeSinceStartup;
            float nowTime = Time.time;

            foreach (KeyValuePair<Coroutine, CoroutineData> keyValuePair in _coroutineData)
            {
                CoroutineData data = keyValuePair.Value;
                if (data.Tag == tag)
                {
                    list.Add(GetCoroutineDataCopy(data, nowTime, nowRealtime));
                }
            }

            return list;
        }

        public int GetActiveCoroutineCount()
        {
            return _coroutineData.Count;
        }

        #region Utility

        private void RegisterOwner(MonoBehaviour owner, Coroutine coroutine)
        {
            if (owner == null || coroutine == null)
            {
                return;
            }

            if (!_ownedCoroutines.TryGetValue(owner, out List<Coroutine> coroutineList))
            {
                coroutineList = new List<Coroutine>();
                _ownedCoroutines.Add(owner, coroutineList);
            }

            coroutineList.Add(coroutine);
        }

        private void RegisterTag(string tag, Coroutine coroutine)
        {
            if (string.IsNullOrEmpty(tag) || coroutine == null)
            {
                return;
            }

            if (!_taggedCoroutines.TryGetValue(tag, out List<Coroutine> coroutineList))
            {
                coroutineList = new List<Coroutine>();
                _taggedCoroutines.Add(tag, coroutineList);
            }

            coroutineList.Add(coroutine);
        }

        private void RegisterCoroutineData(Coroutine coroutine, MonoBehaviour owner, string tag, IEnumerator routine)
        {
            if (coroutine == null)
            {
                return;
            }

            string routineTypeName = routine != null ? routine.GetType().Name : "IEnumerator";
            
            float nowRealtime = Time.realtimeSinceStartup;
            float nowTime = Time.time;
            
            CoroutineData data = new CoroutineData
            {
                Coroutine = coroutine,
                Owner = owner,
                Tag = tag,
                RoutineTypeName = routineTypeName,
                StartedTime = nowTime,
                StartedRealtime = nowRealtime,
                ElapsedTime = 0f,
                ElapsedRealtime = 0f,
#if UNITY_EDITOR
                TotalPauseDuration = GetTotalPauseDuration(nowRealtime)
#endif
            };

            _coroutineData[coroutine] = data;
        }

        private IEnumerator DelayRoutine(Action action, float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            if (action != null)
            {
                action();
            }
        }

        private void RemoveFromOwners(MonoBehaviour owner, Coroutine coroutine)
        {
            if (owner == null || coroutine == null)
            {
                return;
            }

            if (_ownedCoroutines.TryGetValue(owner, out List<Coroutine> list))
            {
                list.Remove(coroutine);
                if (list.Count == 0)
                {
                    _ownedCoroutines.Remove(owner);
                }
            }
        }

        private void RemoveFromTags(string tag, Coroutine coroutine)
        {
            if (string.IsNullOrEmpty(tag) || coroutine == null)
            {
                return;
            }

            if (_taggedCoroutines.TryGetValue(tag, out List<Coroutine> list))
            {
                list.Remove(coroutine);
                if (list.Count == 0)
                {
                    _taggedCoroutines.Remove(tag);
                }
            }
        }
        
        private CoroutineData GetCoroutineDataCopy(CoroutineData data, float nowTime, float nowRealtime)
        {
            float elapsedTime = Mathf.Max(0f, nowTime - data.StartedTime);
            float elapsedRealtime;

#if UNITY_EDITOR
            float currentTotalPauseDuration = GetTotalPauseDuration(nowRealtime);
            float pauseDurationSinceStarted = Mathf.Max(0f, currentTotalPauseDuration - data.TotalPauseDuration);
            elapsedRealtime = Mathf.Max(0f, (nowRealtime - data.StartedRealtime) - pauseDurationSinceStarted);
#else
            elapsedRealtime = Mathf.Max(0f, nowRealtime - data.StartedRealtime);
#endif

            return new CoroutineData
            {
                Coroutine = data.Coroutine,
                Owner = data.Owner,
                Tag = data.Tag,
                RoutineTypeName = data.RoutineTypeName,
                StartedTime = data.StartedTime,
                StartedRealtime = data.StartedRealtime,
                ElapsedTime = elapsedTime,
                ElapsedRealtime = elapsedRealtime,
#if UNITY_EDITOR
                TotalPauseDuration = data.TotalPauseDuration
#endif
            };
        }
        
#if UNITY_EDITOR
        private float GetTotalPauseDuration(float nowRealtime)
        {
            if (_isEditorPaused)
            {
                return _totalPauseDuration + Mathf.Max(0f, nowRealtime - _pauseStartRealtime);
            }

            return _totalPauseDuration;
        }
#endif

        private void OnTrackedRoutineCompleted(MonoBehaviour owner, string tag, Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            if (owner != null)
            {
                RemoveFromOwners(owner, coroutine);
            }

            if (!string.IsNullOrEmpty(tag))
            {
                RemoveFromTags(tag, coroutine);
            }

            _coroutineData.Remove(coroutine);
        }

        private sealed class TrackedRoutine : IEnumerator
        {
            public object Current => _inner.Current;

            private readonly CoroutineService _service;

            private readonly MonoBehaviour _owner;
            private readonly string _tag;

            private readonly IEnumerator _inner;

            public TrackedRoutine(CoroutineService service, MonoBehaviour owner, string tag, IEnumerator inner)
            {
                _service = service;
                _owner = owner;
                _tag = tag;
                _inner = inner;
            }

            public Coroutine Coroutine { private get; set; }

            public bool MoveNext()
            {
                bool hasNext = false;

                try
                {
                    hasNext = _inner.MoveNext();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                if (!hasNext)
                {
                    _service.OnTrackedRoutineCompleted(_owner, _tag, Coroutine);
                    return false;
                }

                return true;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }
        }

        #endregion
    }
}