using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Sample: fires pooled cubes while Space is held and shows how many objects the pool has created

public class CubeLauncher : MonoBehaviour
{
    [SerializeField] private ObjectPool pool;
    [SerializeField] private float shotsPerSecond = 10f;
    [SerializeField] private float launchSpeed = 12f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float spread = 0.2f;

    private float _nextShotTime;
    private Transform _poolContainer;

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
        GameObject cube = pool.GetInstanceFromPool();
        cube.transform.SetPositionAndRotation(transform.position, Random.rotation);
        cube.SetActive(true);

        Vector3 direction = (transform.forward + transform.up + Random.insideUnitSphere * spread).normalized;
        Rigidbody body = cube.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = direction * launchSpeed;
#else
        body.velocity = direction * launchSpeed;
#endif
        body.angularVelocity = Random.insideUnitSphere * 5f;

        StartCoroutine(ObjectPool.ReturnToPoolWithDelay(cube, lifetime));

        _poolContainer = cube.transform.parent;
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
        GUI.Label(new Rect(10, 10, 600, 30), "Hold Space to fire");

        if (_poolContainer != null)
        {
            GUI.Label(new Rect(10, 40, 600, 30), $"Objects created by the pool: {_poolContainer.childCount}");
        }
    }
}
