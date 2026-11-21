using NUnit.Framework;

namespace AngryKoala.Pooling.Tests
{
    public sealed class ObjectPoolTests
    {
        private const int _maxSize = 4;

        [Test]
        public void Return_SameInstanceTwice_IsNotHandedOutTwice()
        {
            var pool = new ObjectPool<TestPoolable>(() => new TestPoolable(), 0, _maxSize);

            TestPoolable instance = (TestPoolable)pool.Get();
            pool.Return(instance);
            pool.Return(instance);

            IPoolable first = pool.Get();
            IPoolable second = pool.Get();

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Get_AfterFactoryReturnedNull_StillCreatesUpToMaxSize()
        {
            int factoryCallCount = 0;
            var pool = new ObjectPool<TestPoolable>(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? null : new TestPoolable();
            }, 0, 1);

            Assert.That(pool.Get(), Is.Null);
            Assert.That(pool.Get(), Is.Not.Null);
        }

        private sealed class TestPoolable : IPoolable
        {
            public void OnRequestedFromPool()
            {
            }

            public void OnReturnedToPool()
            {
            }
        }
    }
}
