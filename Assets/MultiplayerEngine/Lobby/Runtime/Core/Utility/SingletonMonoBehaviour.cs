#if UNITY_SERVICES || STEAM_SERVICES
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// A generic base class for singleton MonoBehaviour patterns.
    /// Ensures only one instance exists and optionally persists across scenes.
    /// </summary>
    /// <typeparam name="T">The type of the singleton class.</typeparam>
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// Override this property to control whether the singleton persists across scene loads.
        /// Default is true (persists across scenes).
        /// </summary>
        protected virtual bool ShouldPersist => true;

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Handles singleton initialization and duplicate destruction.
        /// </summary>
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;

            if (ShouldPersist)
            {
                DontDestroyOnLoad(gameObject);
            }

            OnAwakeInitialize();
        }

        /// <summary>
        /// Called after singleton initialization is complete.
        /// Override this method to add custom initialization logic.
        /// </summary>
        protected virtual void OnAwakeInitialize() { }
    }
}
#endif
