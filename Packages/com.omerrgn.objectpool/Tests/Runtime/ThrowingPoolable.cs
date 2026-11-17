using System;
using UnityEngine;

namespace Omerrgn.ObjectPooling.Tests
{
    public class ThrowingPoolable : MonoBehaviour, IPoolable
    {
        public void OnTakenFromPool()
        {
            throw new InvalidOperationException("Thrown from OnTakenFromPool");
        }

        public void OnReturnedToPool()
        {
            throw new InvalidOperationException("Thrown from OnReturnedToPool");
        }
    }
}
