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

    private List<GameObject> _pool;
    private GameObject _poolContainer;

    private void Awake()
    {
        _pool = new List<GameObject>();
        _poolContainer = new GameObject($"Pool - {prefab.name}");

        CreatePooler();
    }

    private void CreatePooler()
    {
        for (int i = 0; i < poolSize; i++)
        {
            _pool.Add(CreateInstance());
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
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!_pool[i].activeInHierarchy)
            {
                return _pool[i];
            }
        }

        // The pool is exhausted: grow it, so the new instance is reused later
        GameObject newInstance = CreateInstance();
        _pool.Add(newInstance);
        return newInstance;
    }

    public static void ReturnToPool(GameObject instance)
    {
        instance.SetActive(false);
    }

    public static IEnumerator ReturnToPoolWithDelay(GameObject instance , float delay)
    {
        yield return new WaitForSeconds(delay);
        instance.SetActive(false);
    }
}
