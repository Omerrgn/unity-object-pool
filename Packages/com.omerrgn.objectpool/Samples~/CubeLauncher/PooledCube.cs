using UnityEngine;

namespace Omerrgn.ObjectPooling.Samples
{
    // Sample: a pooled object that sends itself back to the pool and resets itself
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
            // The pool adds PooledObject only after Awake has run
            if (_pooledObject == null)
            {
                _pooledObject = GetComponent<PooledObject>();
            }

            _pooledObject.Release(lifetime);
        }

        public void OnReturnedToPool()
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = Vector3.zero;
#else
            _body.velocity = Vector3.zero;
#endif
            _body.angularVelocity = Vector3.zero;
        }
    }
}
