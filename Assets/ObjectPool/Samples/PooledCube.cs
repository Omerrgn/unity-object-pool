using UnityEngine;

// Sample: a pooled object that sends itself back to the pool and cleans up after itself

[RequireComponent(typeof(Rigidbody))]
public class PooledCube : MonoBehaviour, IPoolable
{
    [SerializeField] private float lifetime = 2f;

    private Rigidbody _body;
    private PooledObject _pooledObject;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
    }

    public void OnTakenFromPool()
    {
        // The pool puts a PooledObject on every instance, but only after Awake has run
        if (_pooledObject == null)
        {
            _pooledObject = GetComponent<PooledObject>();
        }

        // The cube sends itself back, so whoever took it does not have to keep track of it
        _pooledObject.Release(lifetime);
    }

    public void OnReturnedToPool()
    {
        // Otherwise the cube would start its next flight with what is left of this one
#if UNITY_6000_0_OR_NEWER
        _body.linearVelocity = Vector3.zero;
#else
        _body.velocity = Vector3.zero;
#endif
        _body.angularVelocity = Vector3.zero;
    }
}
