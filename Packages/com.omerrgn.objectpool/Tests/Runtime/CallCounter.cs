using UnityEngine;

namespace Omerrgn.ObjectPooling.Tests
{
    public class CallCounter : MonoBehaviour, IPoolable
    {
        public int TakenCount { get; private set; }
        public int ReturnedCount { get; private set; }
        public bool WasActiveWhenReturned { get; private set; }
        public Vector3 PositionOnEnable { get; private set; }

        private void OnEnable()
        {
            PositionOnEnable = transform.position;
        }

        public void OnTakenFromPool()
        {
            TakenCount++;
        }

        public void OnReturnedToPool()
        {
            ReturnedCount++;
            WasActiveWhenReturned = gameObject.activeSelf;
        }
    }
}
