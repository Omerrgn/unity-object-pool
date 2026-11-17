using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Omerrgn.ObjectPooling.Tests
{
    public class ObjectPoolTests
    {
        private readonly List<GameObject> _createdByTest = new List<GameObject>();
        private GameObject _template;
        private ObjectPool _pool;
        private Scene _otherScene;

        [SetUp]
        public void SetUp()
        {
            _template = new GameObject("Template");
            _template.AddComponent<CallCounter>();
        }

        // Runs even when an assertion fails, so nothing a test created can disturb the next one
        [TearDown]
        public void TearDown()
        {
            if (_pool != null)
            {
                Object.Destroy(_pool.gameObject);
                _pool = null;
            }

            foreach (GameObject created in _createdByTest)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _createdByTest.Clear();
            Object.Destroy(_template);

            if (_otherScene.IsValid() && _otherScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(_otherScene);
            }
        }

        private GameObject CreateObject(string name)
        {
            var created = new GameObject(name);
            _createdByTest.Add(created);
            return created;
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
            GameObject stranger = CreateObject("Stranger");

            LogAssert.Expect(LogType.Warning, new Regex("does not belong to this pool"));
            pool.Release(stranger);

            Assert.IsTrue(stranger.activeSelf);
            Assert.AreEqual(1, pool.CountAll);
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

        [UnityTest]
        public IEnumerator ReleaseWithDelay_InstanceDueThisFrameIsBackBeforeOtherScriptsGet()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();
            pool.Release(instance, 0.1f);

            var getter = CreateObject("Getter").AddComponent<GetWhenDue>();
            getter.Pool = pool;
            getter.DueTime = Time.time + 0.1f;
            yield return new WaitUntil(() => getter.Received != null);

            Assert.AreSame(instance, getter.Received, "The pool created a new instance instead of reusing the one due back");
            Assert.AreEqual(1, pool.CountAll);
        }

        [UnityTest]
        public IEnumerator ReleaseWithDelay_WaitsWhilePoolIsInactive()
        {
            ObjectPool pool = CreatePool(1);
            GameObject instance = pool.Get();
            pool.Release(instance, 0.1f);

            pool.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(instance.activeSelf, "The instance went back while the pool was inactive");

            pool.gameObject.SetActive(true);
            yield return null;
            Assert.IsFalse(instance.activeSelf, "The overdue instance did not go back once the pool was active again");
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
        public void IPoolable_ThatThrows_DoesNotStopGetOrRelease()
        {
            _template.AddComponent<ThrowingPoolable>();
            _template.AddComponent<CallCounter>();
            ObjectPool pool = CreatePool(1);

            LogAssert.Expect(LogType.Exception, new Regex("Thrown from OnTakenFromPool"));
            GameObject instance = pool.Get();

            Assert.AreEqual(1, instance.GetComponents<CallCounter>()[1].TakenCount,
                "A component after the one that threw was not called");

            LogAssert.Expect(LogType.Exception, new Regex("Thrown from OnReturnedToPool"));
            pool.Release(instance);

            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(1, pool.CountInactive);
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

        [UnityTest]
        public IEnumerator DestroyedWaitingInstance_IsNoLongerCountedAsInactive()
        {
            ObjectPool pool = CreatePool(2);
            GameObject instance = pool.Get();
            pool.Release(instance);

            Object.Destroy(instance);
            yield return null;

            Assert.AreEqual(1, pool.CountInactive);
            Assert.IsFalse(pool.Get() == null, "The pool handed out the destroyed instance");
        }

        [UnityTest]
        public IEnumerator PoolMarkedDontDestroyOnLoad_KeepsInstancesInItsScene()
        {
            ObjectPool pool = CreatePool(1);
            Object.DontDestroyOnLoad(pool.gameObject);
            yield return null;

            GameObject instance = pool.Get();

            Assert.AreEqual(pool.gameObject.scene, instance.scene);
        }

        [UnityTest]
        public IEnumerator PoolInAnotherScene_KeepsInstancesInThatScene()
        {
            _otherScene = SceneManager.CreateScene("Pool Test Scene");
            var poolObject = new GameObject("Pool");
            poolObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(poolObject, _otherScene);

            _pool = poolObject.AddComponent<ObjectPool>();
            _pool.Configure(_template, 1, 0);
            poolObject.SetActive(true);
            GameObject instance = _pool.Get();

            Assert.AreEqual(_otherScene, instance.scene);
            yield return SceneManager.UnloadSceneAsync(_otherScene);
        }
    }
}
