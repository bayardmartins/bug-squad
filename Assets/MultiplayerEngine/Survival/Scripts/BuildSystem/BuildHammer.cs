using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Marker component for the build hammer tool.
    /// Attach this script to any tool's localPrefab to make it act as a build hammer.
    /// When this tool is equipped, the player can:
    /// - Open the build menu (B key)
    /// - Place/delete build pieces
    /// - Fill blueprints
    /// - See hover outlines on placed pieces
    /// </summary>
    public class BuildHammer : MonoBehaviour
    {
        // Marker component — no logic needed.
        // BuildManager checks for this component on the equipped item's prefab.
    }
}