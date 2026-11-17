using UnityEngine;

namespace Omerrgn.ObjectPooling.Tests
{
    // Calls Get from an ordinary Update on the first frame at or after DueTime, like a game script would
    public class GetWhenDue : MonoBehaviour
    {
        public ObjectPool Pool;
        public float DueTime;

        public GameObject Received { get; private set; }

        private void Update()
        {
            if (Received == null && Time.time >= DueTime)
            {
                Received = Pool.Get();
            }
        }
    }
}
