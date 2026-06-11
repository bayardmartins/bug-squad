using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
public class SnapPointRegistry : MonoBehaviour
{
    public static SnapPointRegistry Instance;

    // Grid cell size (tune: usually snap distance * 2)
    public float cellSize = 1f;

    // Dictionary<CellKey, List<SnapPoint>>
    private Dictionary<Vector3Int, List<SnapPoint>> grid =
        new Dictionary<Vector3Int, List<SnapPoint>>();

    private void Awake()
    {
        Instance = this;
    }

    public void Register(SnapPoint sp)
    {
        Vector3Int cell = WorldToCell(sp.transform.position);

        if (!grid.ContainsKey(cell))
            grid[cell] = new List<SnapPoint>();

        grid[cell].Add(sp);
    }

    public void Unregister(SnapPoint sp)
    {
        Vector3Int cell = WorldToCell(sp.transform.position);
        if (grid.ContainsKey(cell))
            grid[cell].Remove(sp);
    }

    public List<SnapPoint> GetNearby(Vector3 worldPos, int cellRange)
    {
        Vector3Int cell = WorldToCell(worldPos);
        List<SnapPoint> result = new List<SnapPoint>();

        for (int x = -cellRange; x <= cellRange; x++)
            for (int y = -cellRange; y <= cellRange; y++)
                for (int z = -cellRange; z <= cellRange; z++)
                {
                    Vector3Int c = cell + new Vector3Int(x, y, z);
                    if (grid.ContainsKey(c))
                        result.AddRange(grid[c]);
                }

        return result;
    }

    private Vector3Int WorldToCell(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / cellSize),
            Mathf.FloorToInt(pos.y / cellSize),
            Mathf.FloorToInt(pos.z / cellSize)
        );
    }
}
}