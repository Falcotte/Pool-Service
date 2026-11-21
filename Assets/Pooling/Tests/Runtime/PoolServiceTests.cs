using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AngryKoala.Coroutines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AngryKoala.Pooling.Tests
{
    public sealed class PoolServiceTests
    {
        private readonly List<GameObject> _gameObjects = new();

        private PoolService _service;
        private GameObject _prefab;

        private const string _poolKey = "TestPool";

        private const float _returnDelaySeconds = 0.2f;
        private const float _waitPastDelaySeconds = 0.4f;

        private static readonly Vector3 _spawnPosition = new(3f, 4f, 5f);

        [SetUp]
        public void SetUp()
        {
            Create("CoroutineService").AddComponent<CoroutineService>();
            _service = Create("PoolService").AddComponent<PoolService>();

            _prefab = Create("TestPoolable");
            _prefab.AddComponent<TestPoolableMono>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in _gameObjects)
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }

            _gameObjects.Clear();

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_PlacesInstanceBeforeRequestCallback()
        {
            CreatePool(initialSize: 1, maxSize: 1);

            TestPoolableMono instance = _service.Get<TestPoolableMono>(_poolKey);
            instance.transform.position = _spawnPosition;

            Assert.That(instance.PositionWhenRequested, Is.EqualTo(_spawnPosition));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnDelayed_AfterEarlierReturn_DoesNotReturnTheNextUse()
        {
            CreatePool(initialSize: 1, maxSize: 1);

            TestPoolableMono instance = _service.Get<TestPoolableMono>(_poolKey);
            _service.Return(instance, _returnDelaySeconds);
            _service.Return(instance);

            TestPoolableMono nextUse = _service.Get<TestPoolableMono>(_poolKey);
            Assert.That(nextUse, Is.SameAs(instance));

            yield return new WaitForSeconds(_waitPastDelaySeconds);

            Assert.That(nextUse.gameObject.activeSelf, Is.True);
            Assert.That(nextUse.ReturnedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Get_AfterWaitingInstanceWasDestroyed_ReturnsLiveInstance()
        {
            MonoPool pool = CreatePool(initialSize: 1, maxSize: 2);

            Object.Destroy(pool.transform.GetChild(0).gameObject);

            yield return null;

            TestPoolableMono instance = _service.Get<TestPoolableMono>(_poolKey);

            Assert.That(instance != null, Is.True);
            Assert.That(instance.gameObject.activeSelf, Is.True);
        }

        private MonoPool CreatePool(int initialSize, int maxSize)
        {
            GameObject poolObject = Create("MonoPool");
            poolObject.SetActive(false);

            MonoPool pool = poolObject.AddComponent<MonoPool>();
            SetField(pool, "_poolKey", _poolKey);
            SetField(pool, "_prefab", _prefab);
            SetField(pool, "_initialSize", initialSize);
            SetField(pool, "_maxSize", maxSize);

            poolObject.SetActive(true);

            return pool;
        }

        private GameObject Create(string name)
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);

            return gameObject;
        }

        private static void SetField(MonoPool pool, string fieldName, object value)
        {
            FieldInfo field = typeof(MonoPool).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(pool, value);
        }
    }
}
