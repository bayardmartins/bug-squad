using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Central database for all build pieces. Uses Unity's Sub-Asset pattern
    /// to store all pieces as hidden sub-assets within a single database file.
    /// Similar to ItemDatabase pattern.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildDatabase", menuName = "Multiplayer Engine/Building/Build Database", order = 0)]
    public class BuildDatabase : ScriptableObject
    {
        [SerializeField]
        private List<BuildPieceEntry> pieces = new List<BuildPieceEntry>();

        /// <summary>
        /// Read-only access to all pieces in the database.
        /// </summary>
        public IReadOnlyList<BuildPieceEntry> Pieces => pieces;

        /// <summary>
        /// Total count of pieces in the database.
        /// </summary>
        public int Count => pieces.Count;

        /// <summary>
        /// Gets a piece by its unique ID.
        /// </summary>
        public BuildPieceEntry GetPieceByID(int id)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].pieceId == id)
                    return pieces[i];
            }
            return null;
        }

        /// <summary>
        /// Gets a piece by its name (case-insensitive).
        /// </summary>
        public BuildPieceEntry GetPieceByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null &&
                    string.Equals(pieces[i].pieceName, name, System.StringComparison.OrdinalIgnoreCase))
                    return pieces[i];
            }
            return null;
        }

        /// <summary>
        /// Gets all pieces of a specific category.
        /// </summary>
        public List<BuildPieceEntry> GetPiecesByCategory(BuildCategory category)
        {
            var result = new List<BuildPieceEntry>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].category == category)
                    result.Add(pieces[i]);
            }
            return result;
        }

        /// <summary>
        /// Gets the prefab at the given index. Used by BuildPieceData for networked spawning.
        /// </summary>
        public GameObject GetPrefab(ushort index)
        {
            if (index < 0 || index >= pieces.Count)
            {
                Debug.LogError($"BuildDatabase: Invalid piece index {index}. Database has {pieces.Count} pieces.");
                return null;
            }
            return pieces[index]?.buildPrefab;
        }

        /// <summary>
        /// Gets the index of a piece's prefab. Returns -1 if not found.
        /// </summary>
        public int GetPrefabIndex(GameObject prefab)
        {
            if (prefab == null) return -1;

            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].buildPrefab == prefab)
                    return i;
            }

            // Fallback: match by name
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i]?.buildPrefab != null && pieces[i].buildPrefab.name == prefab.name)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Checks if a piece with the given ID exists.
        /// </summary>
        public bool ContainsID(int id)
        {
            return GetPieceByID(id) != null;
        }

        /// <summary>
        /// Gets the next available unique ID for a new piece.
        /// </summary>
        public int GetNextAvailableID()
        {
            int maxId = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].pieceId > maxId)
                    maxId = pieces[i].pieceId;
            }
            return maxId + 1;
        }

        /// <summary>
        /// Validates the database has no null entries.
        /// </summary>
        public bool Validate()
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null)
                {
                    Debug.LogError($"BuildDatabase: Null piece at index {i}!");
                    return false;
                }
                if (pieces[i].buildPrefab == null)
                {
                    Debug.LogWarning($"BuildDatabase: Piece '{pieces[i].pieceName}' (ID: {pieces[i].pieceId}) has no build prefab!");
                }
            }
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Adds a piece to the database. Editor-only.
        /// The piece should already be added as a sub-asset before calling this.
        /// </summary>
        public void AddPiece(BuildPieceEntry piece)
        {
            if (piece != null && !pieces.Contains(piece))
            {
                pieces.Add(piece);
            }
        }

        /// <summary>
        /// Removes a piece from the database. Editor-only.
        /// This only removes from the list - caller must handle asset deletion.
        /// </summary>
        public void RemovePiece(BuildPieceEntry piece)
        {
            if (piece != null)
            {
                pieces.Remove(piece);
            }
        }

        /// <summary>
        /// Cleans up any null references in the pieces list. Editor-only.
        /// </summary>
        public void CleanupNullReferences()
        {
            pieces.RemoveAll(piece => piece == null);
        }
#endif
    }
}
