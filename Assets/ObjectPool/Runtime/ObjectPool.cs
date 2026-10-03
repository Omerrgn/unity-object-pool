using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Omerrgn.ObjectPooling
{
    /// <summary>
    /// Keeps instances of one prefab around so they can be reused instead of destroyed.
    /// </summary>
    /// <remarks>
    /// The pool creates <c>Initial Size</c> instances when it starts. When all of them are in use
    /// and another one is requested, the pool creates it on the spot and keeps it. The pool
    /// therefore grows to the largest number of instances that were ever in use at the same
    /// time, and never shrinks. <c>Max Size</c> limits how many instances are kept: instances
    /// released while the pool is full are destroyed.
    /// </remarks>
    // Runs before other scripts, so that they can already call Get in their own Awake
    [DefaultExecutionOrder(-100)]
    public class ObjectPool : MonoBehaviour
    {
        [Tooltip("The prefab this pool creates instances of.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("How many instances are created when the pool starts.\n\n" +
                 "Too high: the extra instances are never used and only cost memory.\n" +
                 "Too low: the pool creates the missing instances during play and keeps them, " +
                 "so it grows to what is really needed and never shrinks.\n\n" +
                 "To find a good value, play the game and read CountAll afterwards.")]
        [Min(0)]
        [SerializeField] private int initialSize = 10;

        [Tooltip("The largest number of instances the pool keeps. 0 means no limit.\n\n" +
                 "Get never fails: if every instance is in use, the pool creates another one even " +
                 "beyond this number. When such an extra instance is released and the pool is " +
                 "already full, it is destroyed instead of kept.")]
        [Min(0)]
        [SerializeField] private int maxSize = 0;

        // Instances waiting to be handed out, and instances currently handed out
        private readonly Stack<GameObject> _inactive = new Stack<GameObject>();
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();

        private readonly Dictionary<GameObject, Coroutine> _pendingReleases = new Dictionary<GameObject, Coroutine>();

        // The IPoolable components of each instance, looked up once when the instance is created
        private readonly Dictionary<GameObject, IPoolable[]> _poolables = new Dictionary<GameObject, IPoolable[]>();
        private GameObject _poolContainer;

        /// <summary>Number of instances that are handed out right now.</summary>
        public int CountActive => _active.Count;

        /// <summary>Number of instances waiting in the pool.</summary>
        public int CountInactive => _inactive.Count;

        /// <summary>
        /// Number of instances the pool has created. After a play session this is the
        /// largest number that was in use at once, or Initial Size if that is higher.
        /// </summary>
        public int CountAll => _active.Count + _inactive.Count;

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError($"Pool '{name}' has no prefab assigned, so it cannot create instances.", this);
                return;
            }

            // The container stays at the scene root instead of under this object. Instances
            // are its children, so they would otherwise move and scale with the pool
            _poolContainer = new GameObject($"Pool - {prefab.name}");

            CreatePooler();
        }

        private void OnValidate()
        {
            // A limit below the initial size would destroy pre-created instances on their first release
            if (maxSize > 0 && maxSize < initialSize)
            {
                maxSize = initialSize;
            }
        }

        private void OnDestroy()
        {
            // Nothing else removes the container and the instances along with the pool
            if (_poolContainer != null)
            {
                Destroy(_poolContainer);
            }
        }

        private void CreatePooler()
        {
            for (int i = 0; i < initialSize; i++)
            {
                _inactive.Push(CreateInstance());
            }
        }

        private GameObject CreateInstance()
        {
            GameObject newInstance = Instantiate(prefab);
            newInstance.transform.SetParent(_poolContainer.transform);
            newInstance.SetActive(false);

            IPoolable[] poolables = newInstance.GetComponentsInChildren<IPoolable>(true);
            if (poolables.Length > 0)
            {
                _poolables[newInstance] = poolables;
            }

            // Added here rather than on the prefab, so that using a prefab with the pool needs no setup
            if (!newInstance.TryGetComponent(out PooledObject pooledObject))
            {
                pooledObject = newInstance.AddComponent<PooledObject>();
            }
            pooledObject.Pool = this;

            return newInstance;
        }

        // Called by PooledObject when an instance is destroyed instead of released. An instance
        // that was waiting in the pool stays on the stack and is skipped by TakeInstance
        internal void Forget(GameObject instance)
        {
            CancelPendingRelease(instance);
            _active.Remove(instance);
            _poolables.Remove(instance);
        }

        /// <summary>
        /// Returns an active instance, left at the position it had before.
        /// Creates a new instance if none is waiting in the pool.
        /// </summary>
        public GameObject Get()
        {
            GameObject instance = TakeInstance();
            Activate(instance);
            return instance;
        }

        /// <summary>
        /// Returns an active instance at the given position and rotation. The instance is placed
        /// before it is activated, so its OnEnable already sees the final position.
        /// Creates a new instance if none is waiting in the pool.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance = TakeInstance();
            instance.transform.SetPositionAndRotation(position, rotation);
            Activate(instance);
            return instance;
        }

        private GameObject TakeInstance()
        {
            GameObject instance = null;

            // Skip instances that were destroyed while they waited in the pool
            while (instance == null && _inactive.Count > 0)
            {
                instance = _inactive.Pop();
            }

            // Nothing is waiting: grow the pool. Release keeps this instance like any other
            if (instance == null)
            {
                instance = CreateInstance();
            }

            _active.Add(instance);
            return instance;
        }

        private void Activate(GameObject instance)
        {
            instance.SetActive(true);

            if (_poolables.TryGetValue(instance, out IPoolable[] poolables))
            {
                for (int i = 0; i < poolables.Length; i++)
                {
                    poolables[i].OnTakenFromPool();
                }
            }
        }

        /// <summary>
        /// Puts an instance back so that Get can hand it out again. Logs a warning and does
        /// nothing if the object is not an instance of this pool that is currently in use.
        /// </summary>
        public void Release(GameObject instance)
        {
            if (!CanRelease(instance))
            {
                return;
            }

            CancelPendingRelease(instance);
            _active.Remove(instance);

            // Told while the instance is still active, so it can stop effects or coroutines
            if (_poolables.TryGetValue(instance, out IPoolable[] poolables))
            {
                for (int i = 0; i < poolables.Length; i++)
                {
                    poolables[i].OnReturnedToPool();
                }
            }

            // The pool already holds as many instances as it may keep, so this one was
            // created to get through a peak and is not needed any more
            if (maxSize > 0 && _inactive.Count >= maxSize)
            {
                _poolables.Remove(instance);
                Destroy(instance);
                return;
            }

            instance.SetActive(false);

            // The caller may have moved the instance under another object while using it
            if (instance.transform.parent != _poolContainer.transform)
            {
                instance.transform.SetParent(_poolContainer.transform, false);
            }

            _inactive.Push(instance);
        }

        /// <summary>
        /// Puts an instance back after a delay in seconds. The pool runs the timer itself, so the
        /// instance still comes back if the object that asked for it is disabled in the meantime.
        /// Releasing the instance earlier, or scheduling it again, cancels the previous timer.
        /// </summary>
        public void Release(GameObject instance, float delay)
        {
            if (!CanRelease(instance))
            {
                return;
            }

            CancelPendingRelease(instance);
            _pendingReleases[instance] = StartCoroutine(ReleaseAfterDelay(instance, delay));
        }

        private IEnumerator ReleaseAfterDelay(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);

            _pendingReleases.Remove(instance);

            if (instance != null)
            {
                Release(instance);
            }
            else
            {
                // The instance was destroyed while it was in use: forget it
                _active.Remove(instance);
            }
        }

        // Without this, a timer started for an earlier use would release the instance
        // in the middle of its next use
        private void CancelPendingRelease(GameObject instance)
        {
            if (_pendingReleases.TryGetValue(instance, out Coroutine pending))
            {
                StopCoroutine(pending);
                _pendingReleases.Remove(instance);
            }
        }

        private bool CanRelease(GameObject instance)
        {
            if (instance == null)
            {
                Debug.LogWarning($"Pool '{name}': cannot release an object that is null or destroyed.", this);
                return false;
            }

            if (_active.Contains(instance))
            {
                return true;
            }

            // Stack.Contains walks the whole stack, which is fine because this is the error path
            string reason = _inactive.Contains(instance) ? "it is already in the pool" : "it does not belong to this pool";
            Debug.LogWarning($"Pool '{name}': '{instance.name}' was not released because {reason}.", instance);
            return false;
        }
    }
}
