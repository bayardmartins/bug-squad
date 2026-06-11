using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Server-side structural stability graph using float-based Dijkstra propagation.
    /// 
    /// Ground sources (foundations on terrain) have stability = 1.0.
    /// Stability propagates through connections with multiplicative decay factors.
    /// E.g. a wall connected to ground with decay 0.85 gets stability 0.85.
    /// A floor connected to that wall with decay 0.7 gets stability 0.85 * 0.7 = 0.595.
    /// 
    /// When multiple paths exist, the BEST (highest) stability wins (max-Dijkstra).
    /// Pieces below the collapse threshold are marked for destruction.
    /// </summary>
    public class BuildSupportGraph
    {
        private class Node
        {
            public int PieceId;
            public float CurrentStability;
            public bool IsGrounded;
            public List<Connection> Connections = new List<Connection>();
        }

        private class Connection
        {
            public int TargetId;
            public float DecayFactor; // Multiplier: 0.0–1.0
        }

        private Dictionary<int, Node> nodes = new Dictionary<int, Node>();

        /// <summary>
        /// Register a newly placed piece. Grounded pieces (foundations on terrain) start at 1.0.
        /// </summary>
        public void RegisterPiece(int pieceId, bool isGrounded)
        {
            if (!nodes.ContainsKey(pieceId))
            {
                nodes[pieceId] = new Node { PieceId = pieceId };
            }
            nodes[pieceId].IsGrounded = isGrounded;
            nodes[pieceId].CurrentStability = isGrounded ? 1.0f : 0.0f;
        }

        /// <summary>
        /// Add a bidirectional connection between two pieces with a stability decay factor.
        /// If a connection already exists, keeps the better (higher) decay factor.
        /// </summary>
        public void AddConnection(int pieceA, int pieceB, float decayFactor)
        {
            if (!nodes.ContainsKey(pieceA) || !nodes.ContainsKey(pieceB))
            {
                Debug.LogWarning($"[BuildSupportGraph] Cannot add connection: piece {pieceA} or {pieceB} not registered.");
                return;
            }

            decayFactor = Mathf.Clamp01(decayFactor);

            // A → B
            AddOrUpdateLink(nodes[pieceA], pieceB, decayFactor);
            // B → A
            AddOrUpdateLink(nodes[pieceB], pieceA, decayFactor);
        }

        private void AddOrUpdateLink(Node fromNode, int toId, float decay)
        {
            for (int i = 0; i < fromNode.Connections.Count; i++)
            {
                if (fromNode.Connections[i].TargetId == toId)
                {
                    // Keep the better (higher) decay factor
                    if (decay > fromNode.Connections[i].DecayFactor)
                    {
                        fromNode.Connections[i].DecayFactor = decay;
                    }
                    return;
                }
            }
            fromNode.Connections.Add(new Connection { TargetId = toId, DecayFactor = decay });
        }

        /// <summary>
        /// Recalculates stability for ALL pieces using max-stability Dijkstra propagation.
        /// Ground sources start at 1.0 and propagate outward with multiplicative decay.
        /// Returns list of piece IDs that have collapsed (stability below threshold).
        /// </summary>
        public List<int> RecalculateAll(float collapseThreshold)
        {
            List<int> toDestroy = new List<int>();

            // Priority queue: highest stability first (max-Dijkstra)
            // Using SortedSet with descending stability, tiebreak on pieceId
            var queue = new SortedSet<(float stability, int id)>(
                Comparer<(float stability, int id)>.Create((a, b) =>
                {
                    int cmp = b.stability.CompareTo(a.stability); // Descending
                    return cmp != 0 ? cmp : a.id.CompareTo(b.id);
                })
            );

            // 1. Reset all stabilities; enqueue ground sources at 1.0
            foreach (var node in nodes.Values)
            {
                if (node.IsGrounded)
                {
                    node.CurrentStability = 1.0f;
                    queue.Add((1.0f, node.PieceId));
                }
                else
                {
                    node.CurrentStability = 0.0f;
                }
            }

            // 2. Max-Dijkstra propagation
            while (queue.Count > 0)
            {
                var current = queue.Min; // Highest stability (descending sort)
                queue.Remove(current);

                if (!nodes.TryGetValue(current.id, out var uNode))
                    continue;

                float uStab = current.stability;

                // Skip if we already found a better path
                if (uStab < uNode.CurrentStability)
                    continue;

                foreach (var conn in uNode.Connections)
                {
                    if (!nodes.TryGetValue(conn.TargetId, out var vNode))
                        continue;

                    float newStab = uStab * conn.DecayFactor;

                    if (newStab > vNode.CurrentStability)
                    {
                        // Remove old entry if present
                        queue.Remove((vNode.CurrentStability, vNode.PieceId));
                        vNode.CurrentStability = newStab;
                        queue.Add((newStab, vNode.PieceId));
                    }
                }
            }

            // 3. Identify collapsed pieces
            foreach (var node in nodes.Values)
            {
                if (node.CurrentStability < collapseThreshold)
                {
                    toDestroy.Add(node.PieceId);
                }
            }

            return toDestroy;
        }

        /// <summary>
        /// Called when a piece is removed. Removes it from the graph and recalculates.
        /// Returns list of piece IDs that should collapse (stability below threshold).
        /// </summary>
        public List<int> OnPieceRemoved(int removedPieceId, float collapseThreshold)
        {
            // Remove the piece and all its connections
            if (nodes.TryGetValue(removedPieceId, out var removedNode))
            {
                foreach (var conn in removedNode.Connections)
                {
                    if (nodes.TryGetValue(conn.TargetId, out var neighbor))
                    {
                        neighbor.Connections.RemoveAll(c => c.TargetId == removedPieceId);
                    }
                }
                nodes.Remove(removedPieceId);
            }

            // Recalculate all stability levels
            List<int> collapsed = RecalculateAll(collapseThreshold);

            // Clean up collapsed pieces from the graph
            foreach (int id in collapsed)
            {
                if (nodes.TryGetValue(id, out var node))
                {
                    foreach (var conn in node.Connections)
                    {
                        if (nodes.TryGetValue(conn.TargetId, out var neighbor))
                        {
                            neighbor.Connections.RemoveAll(c => c.TargetId == id);
                        }
                    }
                    nodes.Remove(id);
                }
            }

            return collapsed;
        }

        /// <summary>
        /// Get the current stability of a piece. Returns 0 if not found.
        /// </summary>
        public float GetStability(int pieceId)
        {
            return nodes.TryGetValue(pieceId, out var node) ? node.CurrentStability : 0f;
        }

        /// <summary>
        /// Check if a piece exists in the graph.
        /// </summary>
        public bool ContainsPiece(int pieceId)
        {
            return nodes.ContainsKey(pieceId);
        }

        /// <summary>
        /// Clear all data. Called when the build system is reset.
        /// </summary>
        public void Clear()
        {
            nodes.Clear();
        }

        /// <summary>
        /// Debug: Log all connections and stability levels.
        /// </summary>
        public void DebugLog()
        {
            Debug.Log($"[BuildSupportGraph] {nodes.Count} pieces tracked");
            foreach (var kvp in nodes)
            {
                var node = kvp.Value;
                string conns = node.Connections.Count > 0
                    ? string.Join(", ", node.Connections.ConvertAll(c => $"{c.TargetId}(d:{c.DecayFactor:F2})"))
                    : "none";
                Debug.Log($"  Piece {node.PieceId}: Stability {node.CurrentStability:P0}, Grounded={node.IsGrounded}, Connected to: [{conns}]");
            }
        }
    }
}
