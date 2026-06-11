using System;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Lightweight data structure for networked build pieces.
    /// Uses ~42 bytes per piece instead of full NetworkObject overhead.
    /// Stability is stored as a packed ushort (0–10000) representing float 0.0–1.0.
    /// </summary>
    [Serializable]
    public struct BuildPieceData : INetworkSerializable, IEquatable<BuildPieceData>
    {
        public int pieceId;           // Unique ID for this piece
        public ushort prefabIndex;    // Index in BuildDatabase
        public Vector3 position;      // World position
        public Quaternion rotation;   // World rotation
        public bool isBlueprint;      // True if this is a blueprint (transparent placeholder)
        public ushort stabilityPacked; // Structural stability packed: 0–10000 maps to 0.0–1.0

        /// <summary>
        /// Stability as a float (0.0 = no support, 1.0 = grounded/foundation).
        /// </summary>
        public float Stability
        {
            get => stabilityPacked / 10000f;
            set => stabilityPacked = (ushort)Mathf.Clamp(Mathf.RoundToInt(value * 10000f), 0, 10000);
        }

        public BuildPieceData(int id, ushort prefabIdx, Vector3 pos, Quaternion rot, bool blueprint = false, float stability = 0f)
        {
            pieceId = id;
            prefabIndex = prefabIdx;
            position = pos;
            rotation = rot;
            isBlueprint = blueprint;
            stabilityPacked = (ushort)Mathf.Clamp(Mathf.RoundToInt(stability * 10000f), 0, 10000);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref pieceId);
            serializer.SerializeValue(ref prefabIndex);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref isBlueprint);
            serializer.SerializeValue(ref stabilityPacked);
        }

        public bool Equals(BuildPieceData other)
        {
            return pieceId == other.pieceId &&
                   prefabIndex == other.prefabIndex &&
                   position == other.position &&
                   rotation == other.rotation &&
                   isBlueprint == other.isBlueprint &&
                   stabilityPacked == other.stabilityPacked;
        }

        public override bool Equals(object obj)
        {
            return obj is BuildPieceData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(pieceId, prefabIndex, position, rotation, isBlueprint, stabilityPacked);
        }

        public static bool operator ==(BuildPieceData left, BuildPieceData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BuildPieceData left, BuildPieceData right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Wrapper class for JSON serialization of build data.
    /// </summary>
    [Serializable]
    public class BuildSaveData
    {
        public BuildPieceSaveEntry[] pieces;
        public int nextPieceId;

        public BuildSaveData()
        {
            pieces = Array.Empty<BuildPieceSaveEntry>();
            nextPieceId = 0;
        }
    }

    /// <summary>
    /// JSON-serializable version of BuildPieceData.
    /// Unity's JsonUtility doesn't serialize structs well in arrays.
    /// </summary>
    [Serializable]
    public class BuildPieceSaveEntry
    {
        public int pieceId;
        public ushort prefabIndex;
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;
        public bool isBlueprint;
        public float stability; // Stored as plain float for readability in JSON

        public BuildPieceSaveEntry() { }

        public BuildPieceSaveEntry(BuildPieceData data)
        {
            pieceId = data.pieceId;
            prefabIndex = data.prefabIndex;
            posX = data.position.x;
            posY = data.position.y;
            posZ = data.position.z;
            rotX = data.rotation.x;
            rotY = data.rotation.y;
            rotZ = data.rotation.z;
            rotW = data.rotation.w;
            isBlueprint = data.isBlueprint;
            stability = data.Stability;
        }

        public BuildPieceData ToData()
        {
            return new BuildPieceData(
                pieceId,
                prefabIndex,
                new Vector3(posX, posY, posZ),
                new Quaternion(rotX, rotY, rotZ, rotW),
                isBlueprint,
                stability
            );
        }
    }
}
