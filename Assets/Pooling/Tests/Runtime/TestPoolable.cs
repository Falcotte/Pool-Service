using System;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling.Tests
{
    public sealed class TestPoolable : MonoBehaviour, IPoolable
    {
        [SerializeField] private bool _throwOnRequested;
        [SerializeField] private bool _throwOnReturned;
        [SerializeField] private bool _returnSelfOnRequested;

        public int AwakeCount { get; private set; }
        public int EnableCount { get; private set; }
        public int RequestedCount { get; private set; }
        public int ReturnedCount { get; private set; }

        public Vector3 PositionOnEnable { get; private set; }
        public Transform ParentOnEnable { get; private set; }
        public Vector3 PositionWhenRequested { get; private set; }
        public Transform ParentWhenRequested { get; private set; }

        public const string ExceptionMessage = "test-callback-failure";

        private void Awake()
        {
            AwakeCount++;
        }

        private void OnEnable()
        {
            EnableCount++;
            PositionOnEnable = transform.position;
            ParentOnEnable = transform.parent;
        }

        public void Configure(bool throwOnRequested, bool throwOnReturned, bool returnSelfOnRequested)
        {
            _throwOnRequested = throwOnRequested;
            _throwOnReturned = throwOnReturned;
            _returnSelfOnRequested = returnSelfOnRequested;
        }

        public void OnRequestedFromPool()
        {
            RequestedCount++;
            PositionWhenRequested = transform.position;
            ParentWhenRequested = transform.parent;

            if (_throwOnRequested)
            {
                throw new InvalidOperationException(ExceptionMessage);
            }

            if (_returnSelfOnRequested)
            {
                ServiceLocator.Get<IPoolService>().Return(this);
            }
        }

        public void OnReturnedToPool()
        {
            ReturnedCount++;

            if (_throwOnReturned)
            {
                throw new InvalidOperationException(ExceptionMessage);
            }
        }
    }
}
