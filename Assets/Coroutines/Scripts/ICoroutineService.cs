using System;
using System.Collections;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Coroutines
{
    public interface ICoroutineService : IService
    {
        Coroutine Run(IEnumerator routine);
        Coroutine Run(MonoBehaviour owner, IEnumerator routine);
        
        Coroutine RunDelayed(Action action, float delaySeconds);
        Coroutine RunDelayed(MonoBehaviour owner, Action action, float delaySeconds);
        
        void Stop(Coroutine coroutine);
        
        void StopAll();
        void StopAll(MonoBehaviour owner);
    }
}