using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Keeps instances of one prefab around so they can be reused instead of destroyed

public class ObjectPool : MonoBehaviour
{
    [FormerlySerializedAs("prefebs")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private int poolSize = 10;

    // Instances waiting to be handed out, and instances currently handed out
    private readonly Stack<GameObject> _inactive = new Stack<GameObject>();
    private readonly HashSet<GameObject> _active = new HashSet<GameObject>();

    private readonly Dictionary<GameObject, Coroutine> _pendingReleases = new Dictionary<GameObject, Coroutine>();
    private GameObject _poolContainer;

    public int CountActive => _active.Count;
    public int CountInactive => _inactive.Count;
    public int CountAll => _active.Count + _inactive.Count;

    private void Awake()
    {
        _poolContainer = new GameObject($"Pool - {prefab.name}");

        CreatePooler();
    }

    private void CreatePooler()
    {
        for (int i = 0; i < poolSize; i++)
        {
            _inactive.Push(CreateInstance());
        }
    }

    private GameObject CreateInstance()
    {
        GameObject newInstance = Instantiate(prefab);
        newInstance.transform.SetParent(_poolContainer.transform);
        newInstance.SetActive(false);
        return newInstance;
    }

    // Returns an active instance, left at the position it had before
    public GameObject Get()
    {
        GameObject instance = TakeInstance();
        instance.SetActive(true);
        return instance;
    }

    // Returns an active instance, placed before it is activated so its OnEnable sees the final position
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject instance = TakeInstance();
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
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

        // The pool is exhausted: grow it
        if (instance == null)
        {
            instance = CreateInstance();
        }

        _active.Add(instance);
        return instance;
    }

    // Puts an instance back so that Get can hand it out again
    public void Release(GameObject instance)
    {
        if (!CanRelease(instance))
        {
            return;
        }

        CancelPendingRelease(instance);
        _active.Remove(instance);

        instance.SetActive(false);

        // The caller may have moved the instance under another object while using it
        if (instance.transform.parent != _poolContainer.transform)
        {
            instance.transform.SetParent(_poolContainer.transform, false);
        }

        _inactive.Push(instance);
    }

    // Puts an instance back after a delay. The pool runs the timer itself, so the instance
    // still comes back if the object that asked for it is disabled in the meantime
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
