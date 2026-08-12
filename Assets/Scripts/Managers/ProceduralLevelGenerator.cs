using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.AI.Navigation; // NavMeshSurface is provided by the NavMeshComponents package

/// <summary>
/// Small grid-based procedural level generator with optional automatic NavMesh bake.
/// Requires the NavMeshComponents package (adds NavMeshSurface).
/// - Place small room prefabs (with an optional child named "Center") in m_roomPrefabs.
/// - If m_parent is left empty the generator will create a "ProceduralLevel" GameObject.
/// - If m_autoBuildNavMesh is true the generator will add/ensure a NavMeshSurface on the root and call BuildNavMesh().
/// </summary>
public class ProceduralLevelGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] m_roomPrefabs;
    [SerializeField] private GameObject m_startRoomPrefab;
    [SerializeField] private GameObject m_exitRoomPrefab;
    [SerializeField] private Transform m_parent;

    [Header("Generation")]
    [SerializeField] private int m_minRooms = 7;
    [SerializeField] private int m_maxRooms = 12;
    [SerializeField] private float m_tileSize = 10f;
    [SerializeField] private int m_seed = 0; // 0 = random

    // Hallway-specific settings
    [Header("Hallway")]
    [Tooltip("Prefab used to build the hallway/floor tiles. If empty a simple cube will be used.")]
    [SerializeField] private GameObject m_floorTilePrefab;
    [SerializeField] private int m_hallMinLength = 8;
    [SerializeField] private int m_hallMaxLength = 16;
    [Range(0f, 1f)]
    [SerializeField] private float m_sideRoomChance = 0.5f; // chance to spawn a room on either side of a hall segment

    [Header("NavMesh (requires NavMeshComponents)")]
    [Tooltip("If true the generator will add/ensure a NavMeshSurface on the level root and call BuildNavMesh() after generation.")]
    [SerializeField] private bool m_autoBuildNavMesh = true;

    private System.Random m_rng;
    private readonly Dictionary<Vector2Int, GameObject> m_rooms = new Dictionary<Vector2Int, GameObject>();

    // remember hallway layout for instantiation
    private bool m_hallHorizontal = true;
    private int m_hallLength = 0;

    public Action OnLevelGenerated;
    public Vector3 StartPosition { get; private set; }
    public Vector3 ExitPosition { get; private set; }
    public List<Transform> RoomCenters { get; private set; } = new List<Transform>();

    // Public accessor for the generated root - useful for other systems (EnemySpawner, NavMeshSurface, etc.)
    public Transform LevelRoot => m_parent;

    /// <summary>
    /// Generate a level. If seed is null, uses the inspector seed (0 = random) or system tick.
    /// </summary>
    public void Generate(int? seed = null)
    {
        int s = seed ?? m_seed;
        if (s == 0) s = Environment.TickCount;
        m_rng = new System.Random(s);

        Clear();
        BuildLayout();
        InstantiateRooms();

        if (m_autoBuildNavMesh)
            BuildNavMesh();

        OnLevelGenerated?.Invoke();
    }

    private void Clear()
    {
        if (m_parent == null)
        {
            GameObject go = new GameObject("ProceduralLevel");
            m_parent = go.transform;
        }

        // Destroy existing children immediately in edit mode, or normally at runtime
        for (int i = m_parent.childCount - 1; i >= 0; i--)
        {
            var child = m_parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        m_rooms.Clear();
        RoomCenters.Clear();
        StartPosition = Vector3.zero;
        ExitPosition = Vector3.zero;

        m_hallLength = 0;
        m_hallHorizontal = true;
    }

    private void BuildLayout()
    {
        // Create a single long hallway and attach rooms to the sides.
        // Hallway length is independent from min/max rooms so the hallway can be long.
        m_hallLength = Mathf.Clamp(m_rng.Next(m_hallMinLength, m_hallMaxLength + 1), 1, 1000);
        m_hallHorizontal = m_rng.Next(0, 2) == 0; // choose orientation randomly

        List<Vector2Int> positions = new List<Vector2Int>();

        // Build hallway starting at origin and extending in the positive axis
        for (int i = 0; i < m_hallLength; i++)
        {
            Vector2Int p = m_hallHorizontal ? new Vector2Int(i, 0) : new Vector2Int(0, i);
            positions.Add(p);
        }

        // For each hallway tile, attempt to spawn rooms on the two sides
        Vector2Int sideA = m_hallHorizontal ? Vector2Int.up : Vector2Int.left;
        Vector2Int sideB = m_hallHorizontal ? Vector2Int.down : Vector2Int.right;

        foreach (var hallCell in positions.ToList())
        {
            if (positions.Count >= m_maxRooms)
                break;

            if (m_rng.NextDouble() <= m_sideRoomChance)
            {
                Vector2Int roomPos = hallCell + sideA;
                if (!positions.Contains(roomPos))
                    positions.Add(roomPos);
            }

            if (positions.Count >= m_maxRooms)
                break;

            if (m_rng.NextDouble() <= m_sideRoomChance)
            {
                Vector2Int roomPos = hallCell + sideB;
                if (!positions.Contains(roomPos))
                    positions.Add(roomPos);
            }
        }

        // If we still have fewer than m_minRooms total, grow outward from hallway ends or random hallway cells
        int idx = 0;
        Vector2Int[] dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (positions.Count < m_minRooms)
        {
            Vector2Int basePos = positions[m_rng.Next(positions.Count)];
            Vector2Int dir = dirs[m_rng.Next(dirs.Length)];
            Vector2Int newPos = basePos + dir;
            if (!positions.Contains(newPos))
                positions.Add(newPos);
            if (++idx > m_minRooms * 4) // safety
                break;
        }

        foreach (var p in positions)
            m_rooms[p] = null;

        // Start is origin; exit is the far end of the hallway
        StartPosition = GridToWorld(Vector2Int.zero);
        Vector2Int exitKey = m_hallHorizontal ? new Vector2Int(m_hallLength - 1, 0) : new Vector2Int(0, m_hallLength - 1);
        ExitPosition = GridToWorld(exitKey);
    }

    private void InstantiateRooms()
    {
        if (m_rooms.Count == 0)
            return;

        Vector2Int exitKey = m_hallHorizontal ? new Vector2Int(m_hallLength - 1, 0) : new Vector2Int(0, m_hallLength - 1);

        foreach (var kv in m_rooms.ToList())
        {
            Vector2Int pos = kv.Key;
            Vector3 worldPos = GridToWorld(pos);

            GameObject go = null;

            // Start room override
            if (pos == Vector2Int.zero && m_startRoomPrefab != null)
            {
                go = Instantiate(m_startRoomPrefab, worldPos, Quaternion.identity, m_parent);
            }
            else if (pos == exitKey && m_exitRoomPrefab != null)
            {
                go = Instantiate(m_exitRoomPrefab, worldPos, Quaternion.identity, m_parent);
            }
            else if (IsHallwayCell(pos))
            {
                if (m_floorTilePrefab != null)
                {
                    go = Instantiate(m_floorTilePrefab, worldPos, Quaternion.identity, m_parent);
                }
                else
                {
                    // fallback thin cube as floor tile
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(m_parent, false);
                    go.transform.position = worldPos;
                    go.transform.localScale = new Vector3(m_tileSize, 0.2f, m_tileSize);
                }
            }
            else
            {
                // side room or extra room
                if (m_roomPrefabs != null && m_roomPrefabs.Length > 0)
                    go = Instantiate(m_roomPrefabs[m_rng.Next(m_roomPrefabs.Length)], worldPos, Quaternion.identity, m_parent);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(m_parent, false);
                    go.transform.position = worldPos;
                    go.transform.localScale = new Vector3(m_tileSize, 2f, m_tileSize);
                }
            }

            go.name = $"Room_{pos.x}_{pos.y}";
            m_rooms[pos] = go;

            // Attempt to find a child named "Center" for spawn markers; otherwise create one
            Transform center = go.transform.Find("Center");
            if (center != null)
                RoomCenters.Add(center);
            else
            {
                GameObject marker = new GameObject("Center");
                marker.transform.SetParent(go.transform, false);
                marker.transform.localPosition = Vector3.zero;
                RoomCenters.Add(marker.transform);
            }
        }
    }

    private bool IsHallwayCell(Vector2Int pos)
    {
        if (m_hallHorizontal)
            return pos.y == 0 && pos.x >= 0 && pos.x < m_hallLength;
        else
            return pos.x == 0 && pos.y >= 0 && pos.y < m_hallLength;
    }

    private Vector3 GridToWorld(Vector2Int grid) => new Vector3(grid.x * m_tileSize, 0f, grid.y * m_tileSize);

    /// <summary>
    /// Ensures a NavMeshSurface is present on the level root and builds the NavMesh.
    /// Requires the NavMeshComponents package (NavMeshSurface).
    /// </summary>
    private void BuildNavMesh()
    {
        if (m_parent == null)
        {
            Debug.LogWarning("ProceduralLevelGenerator.BuildNavMesh: level root is null.");
            return;
        }

        // Try to get an existing NavMeshSurface
        var surface = m_parent.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = m_parent.gameObject.AddComponent<NavMeshSurface>();
            // sensible defaults: only collect children (the generated rooms)
            surface.collectObjects = CollectObjects.Children;
        }

        try
        {
            surface.BuildNavMesh();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ProceduralLevelGenerator: NavMesh build failed. Ensure NavMeshComponents package is installed. Exception: {ex.Message}");
        }
    }
}