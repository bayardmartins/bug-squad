using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Lightweight service registry. Managers register themselves on Awake,
    /// other scripts resolve via ServiceLocator.Get&lt;T&gt;().
    /// Replaces the old MultiplayerServices static class with a generic, type-safe approach.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        /// <summary>
        /// Registers a service instance for the given type.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service for {type.Name}.");
            }
            services[type] = service;
        }

        /// <summary>
        /// Returns the registered service, or null if none is registered.
        /// </summary>
        public static T Get<T>() where T : class
        {
            services.TryGetValue(typeof(T), out var service);
            return service as T;
        }

        /// <summary>
        /// Unregisters the service for the given type.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            services.Remove(typeof(T));
        }

        /// <summary>
        /// Clears all registered services. Call on application quit or full cleanup.
        /// </summary>
        public static void Clear()
        {
            services.Clear();
        }
    }
}
