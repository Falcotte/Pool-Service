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

        public Coroutine Run(IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            TrackedRoutine trackedRoutine = new TrackedRoutine(this, null, null, routine);
            Coroutine coroutine = StartCoroutine(trackedRoutine);
            trackedRoutine.Coroutine = coroutine;

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

            RemoveFromOwners(coroutine);
            RemoveFromTags(coroutine);
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
        }
        
        public void StopAll(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            if (_taggedCoroutines.TryGetValue(tag, out List<Coroutine> coroutineList))
            {
                for (int i = 0; i < coroutineList.Count; i++)
                {
                    Coroutine coroutine = coroutineList[i];
                    if (coroutine != null)
                    {
                        try
                        {
                            StopCoroutine(coroutine);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                        }

                        RemoveFromOwners(coroutine);
                    }
                }

                _taggedCoroutines.Remove(tag);
            }
        }

        public void StopAll(MonoBehaviour owner)
        {
            if (owner == null)
            {
                return;
            }

            if (_ownedCoroutines.TryGetValue(owner, out List<Coroutine> coroutineList))
            {
                for (int i = 0; i < coroutineList.Count; i++)
                {
                    Coroutine coroutine = coroutineList[i];
                    if (coroutine != null)
                    {
                        try
                        {
                            StopCoroutine(coroutine);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                        }

                        RemoveFromTags(coroutine);
                    }
                }

                _ownedCoroutines.Remove(owner);
            }
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

        private void RemoveFromOwners(Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            foreach (KeyValuePair<MonoBehaviour, List<Coroutine>> keyValuePair in _ownedCoroutines)
            {
                List<Coroutine> coroutineList = keyValuePair.Value;
                if (coroutineList.Remove(coroutine))
                {
                    if (coroutineList.Count == 0)
                    {
                        _ownedCoroutines.Remove(keyValuePair.Key);
                    }
                    break;
                }
            }
        }

        private void RemoveFromTags(Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<Coroutine>> keyValuePair in _taggedCoroutines)
            {
                List<Coroutine> coroutineList = keyValuePair.Value;
                if (coroutineList.Remove(coroutine))
                {
                    if (coroutineList.Count == 0)
                    {
                        _taggedCoroutines.Remove(keyValuePair.Key);
                    }
                    break;
                }
            }
        }
        
        private void OnTrackedRoutineCompleted(MonoBehaviour owner, string tag, Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            if (owner != null)
            {
                RemoveFromOwners(coroutine);
            }

            if (!string.IsNullOrEmpty(tag))
            {
                RemoveFromTags(coroutine);
            }
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