using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Omerrgn.ObjectPooling.Samples
{
    // Sample: fires pooled cubes while Space is held and shows the pool's counters.
    // The cubes go back to the pool on their own, see PooledCube

    public class CubeLauncher : MonoBehaviour
    {
        [SerializeField] private ObjectPool pool;
        [SerializeField] private float shotsPerSecond = 10f;
        [SerializeField] private float launchSpeed = 12f;
        [SerializeField] private float spread = 0.2f;

        private float _nextShotTime;

        private void Update()
        {
            if (IsFireHeld() && Time.time >= _nextShotTime)
            {
                _nextShotTime = Time.time + 1f / shotsPerSecond;
                Launch();
            }
        }

        private void Launch()
        {
            GameObject cube = pool.Get(transform.position, Random.rotation);

            Vector3 direction = (transform.forward + transform.up + Random.insideUnitSphere * spread).normalized;
            Rigidbody body = cube.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = direction * launchSpeed;
#else
            body.velocity = direction * launchSpeed;
#endif
            body.angularVelocity = Random.insideUnitSphere * 5f;
        }

        private static bool IsFireHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 20;
            GUI.Label(new Rect(10, 10, 800, 30), "Hold Space to fire");
            GUI.Label(new Rect(10, 40, 800, 30),
                $"In use: {pool.CountActive}    Waiting in the pool: {pool.CountInactive}    Total: {pool.CountAll}");
        }
    }
}
