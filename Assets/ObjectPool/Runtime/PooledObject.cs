using UnityEngine;

namespace Omerrgn.ObjectPooling
{
    /// <summary>
    /// Remembers which pool an instance came from, so the instance can send itself back
    /// without anyone having to hand it a reference to the pool.
    /// </summary>
    /// <remarks>
    /// The pool adds this component to every instance it creates. There is no need to put it
    /// on the prefab.
    /// </remarks>
    [DisallowMultipleComponent]
    public class PooledObject : MonoBehaviour
    {
        /// <summary>The pool this instance belongs to.</summary>
        public ObjectPool Pool { get; internal set; }

        /// <summary>Puts this instance back into its pool.</summary>
        public void Release()
        {
            if (HasPool())
            {
                Pool.Release(gameObject);
            }
        }

        /// <summary>Puts this instance back into its pool after a delay in seconds.</summary>
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
            // The instance was destroyed instead of released: let the pool stop counting it
            if (Pool != null)
            {
                Pool.Forget(gameObject);
            }
        }
    }
}
