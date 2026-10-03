namespace Omerrgn.ObjectPooling
{
    /// <summary>Implement on a component of a pooled prefab to reset it between uses.</summary>
    public interface IPoolable
    {
        /// <summary>Called right after the instance was activated.</summary>
        void OnTakenFromPool();

        /// <summary>Called right before the instance is deactivated.</summary>
        void OnReturnedToPool();
    }
}
