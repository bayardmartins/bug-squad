using System;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Helper component attached to crafted items to notify when they are picked up/destroyed.
    /// </summary>
    public class CraftedItemTracker : MonoBehaviour
    {
        /// <summary>
        /// Called when this item is destroyed (picked up by a player).
        /// </summary>
        public Action OnItemPickedUp;

        private void OnDestroy()
        {
            OnItemPickedUp?.Invoke();
        }
    }
}