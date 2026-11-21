using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AngryKoala.Pooling.Tests
{
    public sealed class PoolServiceTests
    {
        private readonly List<Object> _objects = new();
        private readonly List<PoolData> _poolData = new();

        private PoolService _service;
        private TestPoolable _template;

        private const float _returnDelaySeconds = 0.2f;
        private const float _waitPastDelaySeconds = 0.5f;
        private const float _longDelaySeconds = 30f;

        private const int _prewarmCount = 3;
        private const int _largeCount = 20;
        private const int _prewarmAllFirstCount = 20;
        private const int _prewarmAllSecondCount = 5;
        private const int _defaultPrewarmPerFrame = 8;

        private static readonly Vector3 _templatePosition = new(1f, 2f, 3f);
        private static readonly Vector3 _spawnPosition = new(3f, 4f, 5f);
        private static readonly Vector3 _parentPosition = new(10f, 0f, 0f);

        [SetUp]
        public void SetUp()
        {
            _service = Create("PoolService").AddComponent<PoolService>();
            _template = CreateTemplate("Template");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object unityObject in _objects)
            {
                if (unityObject != null)
                {
                    Object.Destroy(unityObject);
                }
            }

            _objects.Clear();

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_FirstRequest_CreatesPoolAndReturnsPrefabComponentType()
        {
            TestPoolable instance = Track(_service.Get(_template));

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.Not.SameAs(_template));
            Assert.That(instance, Is.TypeOf<TestPoolable>());
            Assert.That(instance.gameObject.activeSelf, Is.True);

            PoolData data = GetSinglePoolData();
            Assert.That(data.Prefab, Is.SameAs(_template.gameObject));
            Assert.That(data.InUseCount, Is.EqualTo(1));
            Assert.That(data.WaitingCount, Is.EqualTo(0));
            Assert.That(data.MaximumSize, Is.EqualTo(0));
            Assert.That(data.ExhaustionRule, Is.EqualTo(PoolExhaustionRule.Grow));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_PrefabWithoutPoolable_Works()
        {
            BoxCollider prefab = CreateTemplate("Plain").GetComponent<BoxCollider>();

            BoxCollider first = Track(_service.Get(prefab));
            _service.Return(first);
            BoxCollider second = Track(_service.Get(prefab));

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.gameObject.activeSelf, Is.True);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_OtherComponentTypeOfSamePrefab_ReturnsThatComponent()
        {
            TestPoolable instance = Track(_service.Get(_template));
            Transform instanceTransform = Track(_service.Get(_template.transform));

            Assert.That(instanceTransform, Is.Not.Null);
            Assert.That(instanceTransform, Is.Not.SameAs(instance.transform));
            Assert.That(GetSinglePoolData().InUseCount, Is.EqualTo(2));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_NullPrefab_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _service.Get<TestPoolable>(null));
            Assert.Throws<ArgumentNullException>(() => _service.Prewarm(null, 1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_FreshInstance_IsPlacedBeforeEnableAndRequestCallback()
        {
            Transform parent = CreateParent();

            TestPoolable instance = Track(_service.Get(_template, _spawnPosition, Quaternion.identity, parent));

            Assert.That(instance.PositionOnEnable, Is.EqualTo(_spawnPosition));
            Assert.That(instance.PositionWhenRequested, Is.EqualTo(_spawnPosition));
            Assert.That(instance.ParentOnEnable, Is.SameAs(parent));
            Assert.That(instance.ParentWhenRequested, Is.SameAs(parent));
            Assert.That(instance.transform.parent, Is.SameAs(parent));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_ReusedInstance_IsPlacedBeforeEnableAndRequestCallback()
        {
            Transform parent = CreateParent();

            TestPoolable first = Track(_service.Get(_template));
            _service.Return(first);

            TestPoolable second = Track(_service.Get(_template, _spawnPosition, Quaternion.identity, parent));

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.EnableCount, Is.EqualTo(2));
            Assert.That(second.PositionOnEnable, Is.EqualTo(_spawnPosition));
            Assert.That(second.PositionWhenRequested, Is.EqualTo(_spawnPosition));
            Assert.That(second.ParentOnEnable, Is.SameAs(parent));
            Assert.That(second.ParentWhenRequested, Is.SameAs(parent));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Get_WithoutPosition_UsesPrefabPositionAndNoParent()
        {
            TestPoolable fresh = Track(_service.Get(_template));

            Assert.That(fresh.PositionOnEnable, Is.EqualTo(_templatePosition));
            Assert.That(fresh.PositionWhenRequested, Is.EqualTo(_templatePosition));
            Assert.That(fresh.transform.parent, Is.Null);

            Transform parent = CreateParent();
            _service.Return(fresh);
            TestPoolable moved = Track(_service.Get(_template, _spawnPosition, Quaternion.identity, parent));
            _service.Return(moved);

            TestPoolable reused = Track(_service.Get(_template));

            Assert.That(reused, Is.SameAs(fresh));
            Assert.That(reused.PositionOnEnable, Is.EqualTo(_templatePosition));
            Assert.That(reused.PositionWhenRequested, Is.EqualTo(_templatePosition));
            Assert.That(reused.transform.parent, Is.Null);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Return_DeactivatesAndReparentsUnderService()
        {
            Transform parent = CreateParent();
            TestPoolable instance = Track(_service.Get(_template, _spawnPosition, Quaternion.identity, parent));

            _service.Return(instance);

            Assert.That(instance.gameObject.activeSelf, Is.False);
            Assert.That(instance.transform.IsChildOf(_service.transform), Is.True);
            Assert.That(instance.RequestedCount, Is.EqualTo(1));
            Assert.That(instance.ReturnedCount, Is.EqualTo(1));

            PoolData data = GetSinglePoolData();
            Assert.That(data.InUseCount, Is.EqualTo(0));
            Assert.That(data.WaitingCount, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Return_CallsPoolableOnChild_AndAcceptsChildComponent()
        {
            var childObject = new GameObject("Child");
            childObject.transform.SetParent(_template.transform, false);
            TestPoolable childTemplate = childObject.AddComponent<TestPoolable>();

            TestPoolable instance = Track(_service.Get(_template));
            TestPoolable child = instance.transform.GetChild(0).GetComponent<TestPoolable>();

            Assert.That(child, Is.Not.SameAs(childTemplate));
            Assert.That(child.RequestedCount, Is.EqualTo(1));

            _service.Return(child);

            Assert.That(instance.gameObject.activeSelf, Is.False);
            Assert.That(child.ReturnedCount, Is.EqualTo(1));
            Assert.That(instance.ReturnedCount, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Return_Twice_Warns()
        {
            TestPoolable instance = Track(_service.Get(_template));
            _service.Return(instance);

            LogAssert.Expect(LogType.Warning, new Regex("not in use"));
            _service.Return(instance);

            Assert.That(instance.ReturnedCount, Is.EqualTo(1));
            Assert.That(instance, Is.Not.Null);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Return_ObjectNotFromPool_WarnsAndDoesNotDestroyIt()
        {
            GameObject stranger = Create("Stranger");

            LogAssert.Expect(LogType.Warning, new Regex("not created by a pool"));
            _service.Return(stranger.transform);

            yield return null;

            Assert.That(stranger != null, Is.True);
        }

        [UnityTest]
        public IEnumerator Return_Null_IsSilent()
        {
            _service.Return(null);
            _service.Return(null, 1f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnDelayed_ReturnsAfterDelay()
        {
            TestPoolable instance = Track(_service.Get(_template));

            _service.Return(instance, _returnDelaySeconds);

            Assert.That(instance.gameObject.activeSelf, Is.True);

            yield return new WaitForSeconds(_waitPastDelaySeconds);

            Assert.That(instance.gameObject.activeSelf, Is.False);
            Assert.That(instance.ReturnedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReturnDelayed_WithNonPositiveDelay_ReturnsAtOnce()
        {
            TestPoolable instance = Track(_service.Get(_template));

            _service.Return(instance, 0f);

            Assert.That(instance.gameObject.activeSelf, Is.False);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnDelayed_AfterEarlierReturn_DoesNotReturnTheNextUse()
        {
            TestPoolable instance = Track(_service.Get(_template));

            _service.Return(instance, _returnDelaySeconds);
            _service.Return(instance);

            TestPoolable nextUse = Track(_service.Get(_template));
            Assert.That(nextUse, Is.SameAs(instance));

            yield return new WaitForSeconds(_waitPastDelaySeconds);

            Assert.That(nextUse.gameObject.activeSelf, Is.True);
            Assert.That(nextUse.ReturnedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReturnDelayed_SecondDelayReplacesTheFirst()
        {
            TestPoolable instance = Track(_service.Get(_template));

            _service.Return(instance, _returnDelaySeconds);
            _service.Return(instance, _longDelaySeconds);

            yield return new WaitForSeconds(_waitPastDelaySeconds);

            Assert.That(instance.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator ReturnDelayed_WhenServiceIsDisabled_DropsPendingReturns()
        {
            TestPoolable instance = Track(_service.Get(_template));

            _service.Return(instance, _returnDelaySeconds);
            _service.enabled = false;
            _service.enabled = true;

            yield return new WaitForSeconds(_waitPastDelaySeconds);

            Assert.That(instance.gameObject.activeSelf, Is.True);
            Assert.That(instance.ReturnedCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Get_AfterWaitingInstanceWasDestroyed_ReturnsLiveInstance()
        {
            TestPoolable first = Track(_service.Get(_template));
            GameObject firstObject = first.gameObject;
            _service.Return(first);

            Object.Destroy(firstObject);

            yield return null;

            TestPoolable second = Track(_service.Get(_template));

            Assert.That(second != null, Is.True);
            Assert.That(second.gameObject.activeSelf, Is.True);
            Assert.That(ReferenceEquals(second.gameObject, firstObject), Is.False);
        }

        [UnityTest]
        public IEnumerator GetData_AfterInUseInstanceWasDestroyed_DropsIt()
        {
            TestPoolable first = Track(_service.Get(_template));
            TestPoolable second = Track(_service.Get(_template));

            Object.Destroy(first.gameObject);

            yield return null;

            PoolData data = GetSinglePoolData();
            Assert.That(data.InUseCount, Is.EqualTo(1));
            Assert.That(data.WaitingCount, Is.EqualTo(0));

            _service.Return(second);

            Assert.That(GetSinglePoolData().WaitingCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GetData_NullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _service.GetData(null));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ExhaustionRule_Grow_CreatesBeyondMaximum()
        {
            AddSettings(_template.gameObject, 0, 1, PoolExhaustionRule.Grow);

            TestPoolable first = Track(_service.Get(_template));
            TestPoolable second = Track(_service.Get(_template));

            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(GetSinglePoolData().InUseCount, Is.EqualTo(2));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ExhaustionRule_ReturnNull_ReturnsNullAndWarnsOnce()
        {
            AddSettings(_template.gameObject, 0, 1, PoolExhaustionRule.ReturnNull);

            TestPoolable first = Track(_service.Get(_template));

            LogAssert.Expect(LogType.Warning, new Regex("maximum size"));
            TestPoolable second = _service.Get(_template);
            TestPoolable third = _service.Get(_template);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
            Assert.That(third, Is.Null);

            _service.Return(first);
            TestPoolable fourth = Track(_service.Get(_template));
            Assert.That(fourth, Is.SameAs(first));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ExhaustionRule_RecycleOldest_ReusesLongestInUseInstance()
        {
            AddSettings(_template.gameObject, 0, 2, PoolExhaustionRule.RecycleOldest);

            TestPoolable first = Track(_service.Get(_template));
            TestPoolable second = Track(_service.Get(_template));
            _service.Return(first, _returnDelaySeconds);

            TestPoolable third = Track(_service.Get(_template));

            Assert.That(third, Is.SameAs(first));
            Assert.That(first.ReturnedCount, Is.EqualTo(1));
            Assert.That(first.RequestedCount, Is.EqualTo(2));
            Assert.That(second.gameObject.activeSelf, Is.True);

            PoolData data = GetSinglePoolData();
            Assert.That(data.InUseCount, Is.EqualTo(2));
            Assert.That(data.WaitingCount, Is.EqualTo(0));

            yield return new WaitForSeconds(_waitPastDelaySeconds);

            Assert.That(third.gameObject.activeSelf, Is.True);
            Assert.That(third.ReturnedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExhaustionRule_Default_GrowsWithoutLimit()
        {
            for (int i = 0; i < _largeCount; i++)
            {
                Assert.That(Track(_service.Get(_template)), Is.Not.Null);
            }

            Assert.That(GetSinglePoolData().InUseCount, Is.EqualTo(_largeCount));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Callbacks_ThatThrow_AreLoggedAndDoNotBreakGetOrReturn()
        {
            _template.Configure(throwOnRequested: true, throwOnReturned: true, returnSelfOnRequested: false);

            ExpectCallbackFailure("OnRequestedFromPool");
            TestPoolable instance = Track(_service.Get(_template));

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.gameObject.activeSelf, Is.True);

            ExpectCallbackFailure("OnReturnedToPool");
            _service.Return(instance);

            Assert.That(instance.gameObject.activeSelf, Is.False);
            Assert.That(GetSinglePoolData().WaitingCount, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Callback_ThatReturnsItsOwnInstance_Works()
        {
            _template.Configure(throwOnRequested: false, throwOnReturned: false, returnSelfOnRequested: true);

            TestPoolable instance = Track(_service.Get(_template));

            Assert.That(instance.RequestedCount, Is.EqualTo(1));
            Assert.That(instance.ReturnedCount, Is.EqualTo(1));
            Assert.That(instance.gameObject.activeSelf, Is.False);

            PoolData data = GetSinglePoolData();
            Assert.That(data.InUseCount, Is.EqualTo(0));
            Assert.That(data.WaitingCount, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Prewarm_CreatesInactiveInstancesWithoutRunningAwake()
        {
            _service.Prewarm(_template, _prewarmCount);

            TestPoolable[] waiting = _service.GetComponentsInChildren<TestPoolable>(true);

            Assert.That(waiting.Length, Is.EqualTo(_prewarmCount));

            foreach (TestPoolable instance in waiting)
            {
                Assert.That(instance.gameObject.activeSelf, Is.False);
                Assert.That(instance.AwakeCount, Is.EqualTo(0));
                Assert.That(instance.EnableCount, Is.EqualTo(0));
            }

            PoolData data = GetSinglePoolData();
            Assert.That(data.WaitingCount, Is.EqualTo(_prewarmCount));
            Assert.That(data.InUseCount, Is.EqualTo(0));

            TestPoolable requested = Track(_service.Get(_template));

            Assert.That(requested.AwakeCount, Is.EqualTo(1));
            Assert.That(requested.EnableCount, Is.EqualTo(1));
            Assert.That(GetSinglePoolData().WaitingCount, Is.EqualTo(_prewarmCount - 1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Prewarm_CreatesOnlyWhatIsMissing_AndRespectsMaximum()
        {
            AddSettings(_template.gameObject, 0, 2, PoolExhaustionRule.Grow);

            _service.Prewarm(_template, _largeCount);

            Assert.That(GetSinglePoolData().WaitingCount, Is.EqualTo(2));

            _service.Prewarm(_template, 1);

            Assert.That(GetSinglePoolData().WaitingCount, Is.EqualTo(2));

            yield return null;
        }

        [UnityTest]
        public IEnumerator PrewarmAll_SpreadsCreationOverFramesAndEndsAtOne()
        {
            TestPoolable secondTemplate = CreateTemplate("SecondTemplate");
            AddSettings(_template.gameObject, _prewarmAllFirstCount, 0, PoolExhaustionRule.Grow);
            AddSettings(secondTemplate.gameObject, _prewarmAllSecondCount, 0, PoolExhaustionRule.Grow);

            var progressValues = new List<float>();
            var progressFrames = new List<int>();

            yield return _service.StartCoroutine(_service.PrewarmAll(progress =>
            {
                progressValues.Add(progress);
                progressFrames.Add(Time.frameCount);
            }));

            int totalCount = _prewarmAllFirstCount + _prewarmAllSecondCount;
            int expectedBatchCount = Mathf.CeilToInt((float)totalCount / _defaultPrewarmPerFrame);

            Assert.That(progressValues[0], Is.EqualTo(0f));
            Assert.That(progressValues[^1], Is.EqualTo(1f));
            Assert.That(progressValues, Is.Ordered);
            Assert.That(progressValues.Count, Is.EqualTo(expectedBatchCount + 1));
            Assert.That(progressFrames[^1] - progressFrames[0], Is.EqualTo(expectedBatchCount - 1));

            _service.GetData(_poolData);

            Assert.That(_poolData.Count, Is.EqualTo(2));
            Assert.That(_poolData[0].WaitingCount, Is.EqualTo(_prewarmAllFirstCount));
            Assert.That(_poolData[1].WaitingCount, Is.EqualTo(_prewarmAllSecondCount));
        }

        [UnityTest]
        public IEnumerator PrewarmAll_WithNothingToDo_ReportsOneOnly()
        {
            var progressValues = new List<float>();

            yield return _service.StartCoroutine(_service.PrewarmAll(progress => progressValues.Add(progress)));

            Assert.That(progressValues, Is.EqualTo(new List<float> { 1f }));
        }

        [UnityTest]
        public IEnumerator ReturnAll_Prefab_ReturnsOnlyThatPrefabsInstances()
        {
            TestPoolable secondTemplate = CreateTemplate("SecondTemplate");

            TestPoolable first = Track(_service.Get(_template));
            TestPoolable second = Track(_service.Get(_template));
            TestPoolable other = Track(_service.Get(secondTemplate));

            _service.ReturnAll(_template);

            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(second.gameObject.activeSelf, Is.False);
            Assert.That(other.gameObject.activeSelf, Is.True);

            _service.ReturnAll(null);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnAll_ReturnsEveryPoolsInstances()
        {
            TestPoolable secondTemplate = CreateTemplate("SecondTemplate");

            TestPoolable first = Track(_service.Get(_template));
            TestPoolable other = Track(_service.Get(secondTemplate));

            _service.ReturnAll();

            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(other.gameObject.activeSelf, Is.False);

            _service.GetData(_poolData);

            foreach (PoolData data in _poolData)
            {
                Assert.That(data.InUseCount, Is.EqualTo(0));
                Assert.That(data.WaitingCount, Is.EqualTo(1));
            }

            yield return null;
        }

        private TestPoolable CreateTemplate(string name)
        {
            GameObject templateObject = Create(name);
            templateObject.transform.position = _templatePosition;
            templateObject.AddComponent<BoxCollider>();

            return templateObject.AddComponent<TestPoolable>();
        }

        private Transform CreateParent()
        {
            GameObject parent = Create("Parent");
            parent.transform.position = _parentPosition;

            return parent.transform;
        }

        private GameObject Create(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);

            return gameObject;
        }

        private T Track<T>(T component) where T : Component
        {
            if (component != null)
            {
                _objects.Add(component.gameObject);
            }

            return component;
        }

        private void AddSettings(GameObject prefab, int prewarmCount, int maximumSize,
            PoolExhaustionRule exhaustionRule)
        {
            var settings = ScriptableObject.CreateInstance<PoolSettings>();
            settings.AddEntry(prefab, prewarmCount, maximumSize, exhaustionRule);

            _objects.Add(settings);
            _service.AddSettings(settings);
        }

        private PoolData GetSinglePoolData()
        {
            _service.GetData(_poolData);

            Assert.That(_poolData.Count, Is.EqualTo(1));

            return _poolData[0];
        }

        private static void ExpectCallbackFailure(string callbackName)
        {
            LogAssert.Expect(LogType.Error, new Regex(callbackName));
            LogAssert.Expect(LogType.Exception, new Regex(TestPoolable.ExceptionMessage));
        }
    }
}
