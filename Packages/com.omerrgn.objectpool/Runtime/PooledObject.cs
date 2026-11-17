using UnityEngine;

namespace Omerrgn.ObjectPooling
{
    /// <summary>Added by the pool to every instance, so the instance can release itself.</summary>
    [DisallowMultipleComponent]
    public class PooledObject : MonoBehaviour
    {
        public ObjectPool Pool { get; internal set; }

        public void Release()
        {
            if (HasPool())
            {
                Pool.Release(gameObject);
            }
        }

        public void Release(float delay)
        {
            if (HasPool())
            {
                Pool.Release(gameObject, delay);
            }
        }

        private bool HasPool()
        {
            if (Pool != null)
            {
                return true;
            }

            Debug.LogWarning($"'{name}' was not created by an ObjectPool, so it cannot be released.", this);
            return false;
        }

        private void OnDestroy()
        {
            if (Pool != null)
            {
                Pool.Forget(gameObject);
            }
        }
    }
}
