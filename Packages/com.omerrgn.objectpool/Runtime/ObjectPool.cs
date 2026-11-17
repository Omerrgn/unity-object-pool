using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Omerrgn.ObjectPooling
{
    /// <summary>Keeps instances of one prefab around so they can be reused instead of destroyed.</summary>
    // Runs early, so that other scripts can already call Get in their own Awake
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

        // Used as a stack. A List, so that an instance destroyed while waiting can be taken out of the middle
        private readonly List<GameObject> _inactive = new List<GameObject>();
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, float> _releaseTimes = new Dictionary<GameObject, float>();
        private readonly List<GameObject> _dueReleases = new List<GameObject>();
        private readonly Dictionary<GameObject, IPoolable[]> _poolables = new Dictionary<GameObject, IPoolable[]>();
        private GameObject _poolContainer;

        public int CountActive => _active.Count;
        public int CountInactive => _inactive.Count;
        public int CountAll => _active.Count + _inactive.Count;

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError($"Pool '{name}' has no prefab assigned, so it cannot create instances.", this);
                return;
            }

            // Kept at the scene root: as a child of the pool, the instances would move with it
            _poolContainer = new GameObject($"Pool - {prefab.name}");
            KeepContainerInPoolScene();

            CreatePooler();
        }

        private void Start()
        {
            // The pool may have been marked DontDestroyOnLoad after its own Awake
            KeepContainerInPoolScene();
        }

        // New objects go into the active scene, which is not always the scene the pool is in
        private void KeepContainerInPoolScene()
        {
            if (_poolContainer == null || _poolContainer.scene == gameObject.scene)
            {
                return;
            }

            if (gameObject.scene.name == "DontDestroyOnLoad")
            {
                DontDestroyOnLoad(_poolContainer);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(_poolContainer, gameObject.scene);
            }
        }

        private void OnValidate()
        {
            if (maxSize > 0 && maxSize < initialSize)
            {
                maxSize = initialSize;
            }
        }

        private void OnDestroy()
        {
            if (_poolContainer != null)
            {
                Destroy(_poolContainer);
            }
        }

        private void CreatePooler()
        {
            for (int i = 0; i < initialSize; i++)
            {
                _inactive.Add(CreateInstance());
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

            if (!newInstance.TryGetComponent(out PooledObject pooledObject))
            {
                pooledObject = newInstance.AddComponent<PooledObject>();
            }
            pooledObject.Pool = this;

            return newInstance;
        }

        // For the tests: sets up a pool from code. Call it before the pool's GameObject is activated
        internal void Configure(GameObject prefab, int initialSize, int maxSize)
        {
            this.prefab = prefab;
            this.initialSize = initialSize;
            this.maxSize = maxSize;
        }

        // Called by PooledObject when an instance is destroyed instead of released
        internal void Forget(GameObject instance)
        {
            CancelPendingRelease(instance);
            _active.Remove(instance);
            _inactive.Remove(instance);
            _poolables.Remove(instance);
        }

        /// <summary>Returns an active instance, left at the position it had before.</summary>
        public GameObject Get()
        {
            GameObject instance = TakeInstance();
            Activate(instance);
            return instance;
        }

        /// <summary>Returns an active instance, placed before it is activated.</summary>
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

            // Instances destroyed while they waited in the pool are skipped
            while (instance == null && _inactive.Count > 0)
            {
                int last = _inactive.Count - 1;
                instance = _inactive[last];
                _inactive.RemoveAt(last);
            }

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
            CallPoolables(instance, poolable => poolable.OnTakenFromPool());
        }

        // An exception in one component must not leave the instance half taken or half returned
        private void CallPoolables(GameObject instance, System.Action<IPoolable> call)
        {
            if (!_poolables.TryGetValue(instance, out IPoolable[] poolables))
            {
                return;
            }

            for (int i = 0; i < poolables.Length; i++)
            {
                try
                {
                    call(poolables[i]);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, poolables[i] as Object);
                }
            }
        }

        /// <summary>Puts an instance back so that Get can hand it out again.</summary>
        public void Release(GameObject instance)
        {
            if (!CanRelease(instance))
            {
                return;
            }

            CancelPendingRelease(instance);
            _active.Remove(instance);
            CallPoolables(instance, poolable => poolable.OnReturnedToPool());

            // The pool is full, so this extra instance is not kept
            if (maxSize > 0 && _inactive.Count >= maxSize)
            {
                _poolables.Remove(instance);
                Destroy(instance);
                return;
            }

            instance.SetActive(false);

            if (instance.transform.parent != _poolContainer.transform)
            {
                instance.transform.SetParent(_poolContainer.transform, false);
            }

            _inactive.Add(instance);
        }

        /// <summary>Puts an instance back after a delay in seconds.</summary>
        public void Release(GameObject instance, float delay)
        {
            if (!CanRelease(instance))
            {
                return;
            }

            _releaseTimes[instance] = Time.time + delay;
        }

        // The pool runs before other scripts, so an instance due back this frame is in the pool
        // before their Update asks for one
        private void Update()
        {
            if (_releaseTimes.Count == 0)
            {
                return;
            }

            float now = Time.time;
            foreach (KeyValuePair<GameObject, float> pending in _releaseTimes)
            {
                if (pending.Value <= now)
                {
                    _dueReleases.Add(pending.Key);
                }
            }

            for (int i = 0; i < _dueReleases.Count; i++)
            {
                GameObject instance = _dueReleases[i];
                _releaseTimes.Remove(instance);

                if (instance != null)
                {
                    Release(instance);
                }
                else
                {
                    _active.Remove(instance);
                }
            }

            _dueReleases.Clear();
        }

        // A timer left over from an earlier use must not release the instance during its next use
        private void CancelPendingRelease(GameObject instance)
        {
            _releaseTimes.Remove(instance);
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

            string reason = _inactive.Contains(instance) ? "it is already in the pool" : "it does not belong to this pool";
            Debug.LogWarning($"Pool '{name}': '{instance.name}' was not released because {reason}.", instance);
            return false;
        }
    }
}
