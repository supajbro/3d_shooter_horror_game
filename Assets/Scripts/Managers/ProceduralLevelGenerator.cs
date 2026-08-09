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

    [Header("NavMesh (requires NavMeshComponents)")]
    [Tooltip("If true the generator will add/ensure a NavMeshSurface on the level root and call BuildNavMesh() after generation.")]
    [SerializeField] private bool m_autoBuildNavMesh = true;

    private System.Random m_rng;
    private readonly Dictionary<Vector2Int, GameObject> m_rooms = new Dictionary<Vector2Int, GameObject>();

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
    }

    private void BuildLayout()
    {
        int roomCount = Mathf.Clamp(m_rng.Next(m_minRooms, m_maxRooms + 1), 1, 1000);

        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(Vector2Int.zero);

        Vector2Int[] dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // Random walk / growth until we have roomCount unique cells
        while (positions.Count < roomCount)
        {
            Vector2Int basePos = positions[m_rng.Next(positions.Count)];
            Vector2Int dir = dirs[m_rng.Next(dirs.Length)];
            Vector2Int newPos = basePos + dir;
            if (!positions.Contains(newPos))
                positions.Add(newPos);
        }

        foreach (var p in positions)
            m_rooms[p] = null;

        // Precompute start and exit positions
        StartPosition = GridToWorld(Vector2Int.zero);
        Vector2Int exitKey = m_rooms.Keys.OrderByDescending(k => Math.Abs(k.x) + Math.Abs(k.y)).FirstOrDefault();
        ExitPosition = GridToWorld(exitKey);
    }

    private void InstantiateRooms()
    {
        if (m_rooms.Count == 0)
            return;

        Vector2Int exitKey = m_rooms.Keys.OrderByDescending(k => Math.Abs(k.x) + Math.Abs(k.y)).FirstOrDefault();

        foreach (var kv in m_rooms.ToList())
        {
            Vector2Int pos = kv.Key;
            Vector3 worldPos = GridToWorld(pos);

            GameObject prefab = null;
            if (pos == Vector2Int.zero && m_startRoomPrefab != null)
                prefab = m_startRoomPrefab;
            else if (pos == exitKey && m_exitRoomPrefab != null)
                prefab = m_exitRoomPrefab;
            else if (m_roomPrefabs != null && m_roomPrefabs.Length > 0)
                prefab = m_roomPrefabs[m_rng.Next(m_roomPrefabs.Length)];

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, worldPos, Quaternion.identity, m_parent);
            }
            else
            {
                // Fallback placeholder cube room (never fail generation)
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(m_parent, false);
                go.transform.position = worldPos;
                go.transform.localScale = new Vector3(m_tileSize, 2f, m_tileSize);
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