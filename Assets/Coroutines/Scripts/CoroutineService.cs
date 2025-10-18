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
        private readonly Dictionary<MonoBehaviour, List<Coroutine>> _owners = new();

        public Coroutine Run(IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            return StartCoroutine(routine);
        }

        public Coroutine Run(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            Coroutine coroutine = StartCoroutine(TrackRoutineOwner(owner, routine));
            if (owner != null)
            {
                if (!_owners.TryGetValue(owner, out List<Coroutine> list))
                {
                    list = new List<Coroutine>();
                    _owners.Add(owner, list);
                }

                list.Add(coroutine);
            }

            return coroutine;
        }

        public Coroutine RunDelayed(Action action, float delaySeconds)
        {
            return Run(DelayRoutine(action, delaySeconds));
        }

        public Coroutine RunDelayed(MonoBehaviour owner, Action action, float delaySeconds)
        {
            return Run(owner, DelayRoutine(action, delaySeconds));
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

            _owners.Clear();
        }

        public void StopAll(MonoBehaviour owner)
        {
            if (owner == null)
            {
                return;
            }

            if (_owners.TryGetValue(owner, out List<Coroutine> list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Coroutine coroutine = list[i];
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
                    }
                }

                _owners.Remove(owner);
            }
        }

        #region Utility

        private IEnumerator TrackRoutineOwner(MonoBehaviour owner, IEnumerator routine)
        {
            yield return routine;

            if (owner == null)
            {
                yield break;
            }

            if (_owners.TryGetValue(owner, out List<Coroutine> list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] == null)
                    {
                        list.RemoveAt(i);
                    }
                }
            }
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
            foreach (KeyValuePair<MonoBehaviour, List<Coroutine>> keyValuePair in _owners)
            {
                List<Coroutine> list = keyValuePair.Value;
                if (list.Remove(coroutine))
                {
                    break;
                }
            }
        }

        #endregion
    }
}