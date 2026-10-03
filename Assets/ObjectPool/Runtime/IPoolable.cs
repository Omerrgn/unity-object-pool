/// <summary>
/// Implement this on a component of a pooled prefab to be told when an instance
/// is taken from its pool and when it goes back.
/// </summary>
/// <remarks>
/// Use it to reset whatever an instance changed while it was in use: health, velocity,
/// a target, a trail. OnEnable and OnDisable are not a good place for that, because they
/// also run when the pool creates the instance and whenever a parent object is toggled.
/// </remarks>
public interface IPoolable
{
    /// <summary>Called by ObjectPool.Get, right after the instance was activated.</summary>
    void OnTakenFromPool();

    /// <summary>Called by ObjectPool.Release, right before the instance is deactivated.</summary>
    void OnReturnedToPool();
}
