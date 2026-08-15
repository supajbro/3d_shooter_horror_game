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

    [Header("Walls")]
    [Tooltip("Plain wall prefab (used when there is no room behind the wall).")]
    [SerializeField] private GameObject m_wallPrefab;
    [Tooltip("Wall prefab that contains a door opening facing the hallway (used when a room is behind the wall).")]
    [SerializeField] private GameObject m_wallDoorPrefab;
    [Tooltip("If no wall prefab is assigned a simple cube will be created with this height.")]
    [SerializeField] private float m_wallHeight = 3f;
    [Tooltip("Half-height / Y offset for spawned wall prefabs (default centers wall on floor).")]
    [SerializeField] private float m_wallYOffset = 1.5f;
    [Tooltip("Thickness used for fallback cube walls.")]
    [SerializeField] private float m_wallThickness = 0.25f;
    [SerializeField] private bool m_spawnHallwayWalls = true;

    [Header("NavMesh (requires NavMeshComponents)")]
    [Tooltip("If true the generator will add/ensure a NavMeshSurface on the level root and call BuildNavMesh() after generation.")]
    [SerializeField] private bool m_autoBuildNavMesh = true;

    private System.Random m_rng;
    private readonly Dictionary<Vector2Int, GameObject> m_rooms = new Dictionary<Vector2Int, GameObject>();

    // remember hallway layout for instantiation
    private List<Vector2Int> m_hallCells = new List<Vector2Int>();
    private HashSet<Vector2Int> m_hallCellSet = new HashSet<Vector2Int>();

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

        m_hallCells.Clear();
        m_hallCellSet.Clear();
    }

    private void BuildLayout()
    {
        // Build a hallway composed of straight segments. Each segment is a "long" straight run
        // (length in [m_hallMinLength, m_hallMaxLength]) then optionally turns left or right and continues.
        m_hallCells.Clear();
        m_hallCellSet.Clear();

        // Choose how many segments to attempt (1 = straight, 2 = one turn, etc.).
        // Keep this small so we don't blow past other room limits.
        int minSegments = 1;
        int maxSegments = 3; // allows up to 2 turns (3 segments)
        int segments = m_rng.Next(minSegments, maxSegments + 1);

        Vector2Int cur = Vector2Int.zero;
        m_hallCells.Add(cur);
        m_hallCellSet.Add(cur);

        // Choose an initial forward direction (right or up)
        Vector2Int dir = m_rng.Next(0, 2) == 0 ? Vector2Int.right : Vector2Int.up;

        bool stopGenerating = false;

        for (int seg = 0; seg < segments && !stopGenerating; seg++)
        {
            int segLen = Mathf.Clamp(m_rng.Next(m_hallMinLength, m_hallMaxLength + 1), 1, 1000);

            for (int i = 0; i < segLen; i++)
            {
                Vector2Int candidate = cur + dir;

                // Avoid self intersection
                if (m_hallCellSet.Contains(candidate))
                {
                    stopGenerating = true;
                    break;
                }

                cur = candidate;
                m_hallCells.Add(cur);
                m_hallCellSet.Add(cur);

                // Safety guard
                if (m_hallCells.Count > 1000)
                {
                    stopGenerating = true;
                    break;
                }
            }

            if (stopGenerating)
                break;

            // If this is not the last segment, choose a 90-degree turn (left or right)
            if (seg < segments - 1)
            {
                bool turnLeft = m_rng.Next(0, 2) == 0;
                Vector2Int leftDir = new Vector2Int(-dir.y, dir.x);
                Vector2Int rightDir = new Vector2Int(dir.y, -dir.x);
                Vector2Int newDir = turnLeft ? leftDir : rightDir;

                // If chosen turn immediately collides, try the opposite turn. If both collide, stop.
                if (m_hallCellSet.Contains(cur + newDir))
                {
                    Vector2Int other = turnLeft ? rightDir : leftDir;
                    if (m_hallCellSet.Contains(cur + other))
                    {
                        stopGenerating = true;
                        break;
                    }
                    else
                    {
                        newDir = other;
                    }
                }

                dir = newDir;
            }
        }

        // Build the list of positions starting with hallway cells
        List<Vector2Int> positions = new List<Vector2Int>(m_hallCells);

        // For each hallway tile, attempt to spawn rooms on the two sides (left/right relative to segment direction)
        for (int idx = 0; idx < m_hallCells.Count; idx++)
        {
            var hallCell = m_hallCells[idx];

            // Determine local segment direction: prefer next cell, else previous
            Vector2Int segmentDir = Vector2Int.zero;
            if (idx < m_hallCells.Count - 1)
                segmentDir = m_hallCells[idx + 1] - hallCell;
            else if (idx > 0)
                segmentDir = hallCell - m_hallCells[idx - 1];
            else
                segmentDir = Vector2Int.right; // fallback

            Vector2Int sideA = new Vector2Int(-segmentDir.y, segmentDir.x); // left
            Vector2Int sideB = new Vector2Int(segmentDir.y, -segmentDir.x); // right

            // attempt side A
            if (positions.Count < m_maxRooms && m_rng.NextDouble() <= m_sideRoomChance)
            {
                Vector2Int roomPos = hallCell + sideA;
                if (!positions.Contains(roomPos) && !WouldRoomClipIntoHall(roomPos, hallCell))
                    positions.Add(roomPos);
            }

            // attempt side B
            if (positions.Count < m_maxRooms && m_rng.NextDouble() <= m_sideRoomChance)
            {
                Vector2Int roomPos = hallCell + sideB;
                if (!positions.Contains(roomPos) && !WouldRoomClipIntoHall(roomPos, hallCell))
                    positions.Add(roomPos);
            }

            if (positions.Count >= m_maxRooms)
                break;
        }

        // If we still have fewer than m_minRooms total, grow outward from hallway ends or random hallway cells
        int growIdx = 0;
        Vector2Int[] dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (positions.Count < m_minRooms)
        {
            Vector2Int basePos = positions[m_rng.Next(positions.Count)];
            Vector2Int dirGrow = dirs[m_rng.Next(dirs.Length)];
            Vector2Int newPos = basePos + dirGrow;
            if (!positions.Contains(newPos) && !m_hallCellSet.Contains(newPos))
                positions.Add(newPos);
            if (++growIdx > m_minRooms * 6) // safety
                break;
        }

        foreach (var p in positions)
            m_rooms[p] = null;

        // Start is origin; exit is the far end of the hallway
        StartPosition = GridToWorld(m_hallCells.First());
        ExitPosition = GridToWorld(m_hallCells.Last());
    }

    // Check that placing a room at roomPos won't clip into other hallway cells besides anchorHallCell
    private bool WouldRoomClipIntoHall(Vector2Int roomPos, Vector2Int anchorHallCell)
    {
        // If the position itself is a hall cell, it's invalid
        if (m_hallCellSet.Contains(roomPos))
            return true;

        // If any neighbor is a hall cell other than the anchor, it would touch multiple hallway tiles -> clip
        Vector2Int[] neighbors = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var n in neighbors)
        {
            Vector2Int check = roomPos + n;
            if (m_hallCellSet.Contains(check) && check != anchorHallCell)
                return true;
        }

        return false;
    }

    private void InstantiateRooms()
    {
        if (m_rooms.Count == 0)
            return;

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
            else if (pos == m_hallCells.Last() && m_exitRoomPrefab != null)
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

        // After placing floors and rooms, spawn walls along the hallway edges
        SpawnHallwayWalls();
    }

    private void SpawnHallwayWalls()
    {
        if (!m_spawnHallwayWalls)
            return;

        // For each hallway cell, compute the segment direction and place walls on its sides (left/right relative to segment)
        foreach (var hallCell in m_hallCells)
        {
            int idx = m_hallCells.IndexOf(hallCell);

            // Determine local segment direction: prefer next cell, else previous
            Vector2Int segmentDir = Vector2Int.zero;
            if (idx < m_hallCells.Count - 1)
                segmentDir = m_hallCells[idx + 1] - hallCell;
            else if (idx > 0)
                segmentDir = hallCell - m_hallCells[idx - 1];
            else
                segmentDir = Vector2Int.right; // fallback

            Vector2Int[] sides = new[] { new Vector2Int(-segmentDir.y, segmentDir.x), new Vector2Int(segmentDir.y, -segmentDir.x) };

            foreach (var side in sides)
            {
                Vector2Int adjacent = hallCell + side;

                // if the adjacent cell is also a hallway cell, do not place a wall there
                if (m_hallCellSet.Contains(adjacent))
                    continue;

                // Decide whether there's a room behind the wall
                bool hasRoomBehind = m_rooms.ContainsKey(adjacent) && !IsHallwayCell(adjacent);

                GameObject chosenPrefab = null;
                if (hasRoomBehind)
                    chosenPrefab = m_wallDoorPrefab ?? m_wallPrefab;
                else
                    chosenPrefab = m_wallPrefab;

                Vector3 spawnPos = GridToWorld(hallCell) + new Vector3(side.x * m_tileSize * 0.5f, m_wallYOffset, side.y * m_tileSize * 0.5f);

                if (chosenPrefab == null)
                {
                    // fallback cube wall
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.SetParent(m_parent, false);
                    wall.transform.position = spawnPos;

                    // if hallway runs along X (segmentDir.x != 0) wall length along X, else along Z
                    if (Mathf.Abs(segmentDir.x) > 0)
                    {
                        wall.transform.localScale = new Vector3(m_tileSize, m_wallHeight, m_wallThickness);
                    }
                    else
                    {
                        wall.transform.localScale = new Vector3(m_wallThickness, m_wallHeight, m_tileSize);
                    }

                    wall.name = $"Wall_{hallCell.x}_{hallCell.y}_{side.x}_{side.y}";
                }
                else
                {
                    // rotate so the wall's forward aligns with the hallway axis (length along hallway)
                    Vector3 forward = Math.Abs(segmentDir.x) > 0 ? Vector3.right : Vector3.forward;
                    Quaternion rot = Quaternion.LookRotation(forward);
                    GameObject wall = Instantiate(chosenPrefab, spawnPos, rot, m_parent);
                    wall.name = $"Wall_{hallCell.x}_{hallCell.y}_{side.x}_{side.y}";
                }
            }
        }
    }

    private bool IsHallwayCell(Vector2Int pos)
    {
        return m_hallCellSet.Contains(pos);
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