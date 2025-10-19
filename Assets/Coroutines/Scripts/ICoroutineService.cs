using System;
using System.Collections;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Coroutines
{
    public interface ICoroutineService : IService
    {
        Coroutine Run(IEnumerator routine);
        Coroutine Run(IEnumerator routine, string tag);
        Coroutine Run(MonoBehaviour owner, IEnumerator routine);
        Coroutine Run(MonoBehaviour owner, IEnumerator routine, string tag);
        
        Coroutine RunDelayed(Action action, float delaySeconds);
        Coroutine RunDelayed(Action action, float delaySeconds, string tag);
        Coroutine RunDelayed(MonoBehaviour owner, Action action, float delaySeconds);
        Coroutine RunDelayed(MonoBehaviour owner, Action action, float delaySeconds, string tag);
        
        void Stop(Coroutine coroutine);
        
        void StopAll();
        void StopAll(string tag);
        void StopAll(MonoBehaviour owner);
    }
}