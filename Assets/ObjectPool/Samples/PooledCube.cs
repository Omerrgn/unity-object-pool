using UnityEngine;

// Sample: a pooled object that cleans up after itself, so its next user gets it in a known state

[RequireComponent(typeof(Rigidbody))]
public class PooledCube : MonoBehaviour, IPoolable
{
    private Rigidbody _body;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
    }

    public void OnTakenFromPool()
    {
        // Nothing to prepare: the launcher gives the cube its speed
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
