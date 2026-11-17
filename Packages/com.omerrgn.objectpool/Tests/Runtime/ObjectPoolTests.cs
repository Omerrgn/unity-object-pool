using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Omerrgn.ObjectPooling.Tests
{
    public class ObjectPoolTests
    {
        private GameObject _template;
        private ObjectPool _pool;

        [SetUp]
        public void SetUp()
        {
            _template = new GameObject("Template");
            _template.AddComponent<CallCounter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_pool != null)
            {
                Object.Destroy(_pool.gameObject);
                _pool = null;
            }

            Object.Destroy(_template);
        }

        private ObjectPool CreatePool(int initialSize, int maxSize = 0)
        {
            var poolObject = new GameObject("Pool");
            poolObject.SetActive(false);

            _pool = poolObject.AddComponent<ObjectPool>();
            _pool.Configure(_template, initialSize, maxSize);

            poolObject.SetActive(true);
            return _pool;
        }

        [Test]
        public void Get_ReturnsActiveInstance()
        {
            ObjectPool pool = CreatePool(1);

            GameObject instance = pool.Get();

            Assert.IsTrue(instance.activeSelf);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void Get_WithPosition_PlacesInstanceBeforeActivatingIt()
        {
            ObjectPool pool = CreatePool(1);
            var position = new Vector3(1f, 2f, 3f);

            GameObject instance = pool.Get(position, Quaternion.identity);

            Assert.AreEqual(position, instance.GetComponent<CallCounter>().PositionOnEnable);
        }

        [Test]
        public void Release_InstanceIsHandedOutAgain()
        {
            ObjectPool pool = CreatePool(1);
            GameObject first = pool.Get();

            pool.Release(first);
            GameObject second = pool.Get();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void Get_WhenPoolIsEmpty_CreatesInstanceAndKeepsIt()
        {
            ObjectPool pool = CreatePool(1);
            GameObject first = pool.Get();
            GameObject second = pool.Get();
            pool.Release(first);
            pool.Release(second);

            GameObject third = pool.Get();
            GameObject fourth = pool.Get();

            CollectionAssert.AreEquivalent(new[] { first, second }, new[] { third, fourth });
            Assert.AreEqual(2, pool.CountAll);
        }

        [Test]
        public void Release_Twice_IsRejected()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();
            pool.Release(instance);

            LogAssert.Expect(LogType.Warning, new Regex("already in the pool"));
            pool.Release(instance);

            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void Release_ObjectFromOutsideThePool_IsRejected()
        {
            ObjectPool pool = CreatePool(1);
            var stranger = new GameObject("Stranger");

            LogAssert.Expect(LogType.Warning, new Regex("does not belong to this pool"));
            pool.Release(stranger);

            Assert.IsTrue(stranger.activeSelf);
            Assert.AreEqual(1, pool.CountAll);
            Object.Destroy(stranger);
        }

        [UnityTest]
        public IEnumerator Release_WhenPoolIsFull_DestroysExtraInstance()
        {
            ObjectPool pool = CreatePool(1, maxSize: 1);
            GameObject kept = pool.Get();
            GameObject extra = pool.Get();

            pool.Release(kept);
            pool.Release(extra);
            yield return null;

            Assert.IsTrue(extra == null);
            Assert.AreEqual(1, pool.CountAll);
        }

        [UnityTest]
        public IEnumerator ReleaseWithDelay_ReleasesAfterDelay()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();

            pool.Release(instance, 0.1f);

            Assert.IsTrue(instance.activeSelf);
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [UnityTest]
        public IEnumerator ReleaseWithDelay_IsCancelledWhenReleasedEarlier()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();
            pool.Release(instance, 0.1f);
            pool.Release(instance);

            GameObject reused = pool.Get();
            yield return new WaitForSeconds(0.2f);

            Assert.AreSame(instance, reused);
            Assert.IsTrue(reused.activeSelf, "The timer from the first use released the instance during its second use");
        }

        [Test]
        public void IPoolable_IsCalledOnGetAndRelease_WhileInstanceIsActive()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();
            var counter = instance.GetComponent<CallCounter>();

            Assert.AreEqual(1, counter.TakenCount);

            pool.Release(instance);

            Assert.AreEqual(1, counter.ReturnedCount);
            Assert.IsTrue(counter.WasActiveWhenReturned);
        }

        [Test]
        public void PooledObject_ReleasesInstanceToItsPool()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();

            instance.GetComponent<PooledObject>().Release();

            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [UnityTest]
        public IEnumerator DestroyedInstance_IsNoLongerCountedAsActive()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();

            Object.Destroy(instance);
            yield return null;

            Assert.AreEqual(0, pool.CountActive);
        }
    }
}
