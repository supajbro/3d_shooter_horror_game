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

    [Header("Items")]
    [Tooltip("Optional prefab for a key item to spawn in exactly one generated room (one per generation).")]
    [SerializeField] private GameObject m_keyPrefab;

    [Header("Generation")]
    [SerializeField] private int m_minRooms = 7;
    [Tooltip("Maximum number of non-hallway rooms. Paired hotel rooms count individually.")]
    [SerializeField] private int m_maxRooms = 12;
    [SerializeField] private float m_tileSize = 10f;
    [SerializeField] private float m_roomSize = 50f;
    [SerializeField] private int m_seed = 0; // 0 = random

    // Hallway-specific settings
    [Header("Hallway")]
    [Tooltip("Prefab used to build the hallway/floor tiles. If empty a simple cube will be used.")]
    [SerializeField] private GameObject m_floorTilePrefab;
    [Tooltip("Each hallway segment is exactly one of these lengths, in tiles. For example: 10, 20, 30.")]
    [SerializeField] private int[] m_segmentLengths = new[] { 10, 20, 30 };
    [Tooltip("Minimum number of straight hallway segments to generate.")]
    [Min(1)]
    [SerializeField] private int m_minSegments = 4;
    [Tooltip("Maximum number of straight hallway segments to generate. More segments means more turns.")]
    [Min(1)]
    [SerializeField] private int m_maxSegments = 6;
    [Tooltip("Number of hallway tiles between paired hotel-room placements. Rooms are placed on both sides when space permits.")]
    [Min(1)]
    [SerializeField] private int m_roomSpacing = 3;

    [Header("Room Placement")]
    [Tooltip("Number of hallway tiles excluded before and after a segment boundary to prevent rooms clipping into segments.")]
    [Min(0)]
    [SerializeField] private int m_roomSegmentBuffer = 5;

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

    [Header("Roofs")]
    [Tooltip("Prefab used for hallway roofs. The roof should have its pivot centered.")]
    [SerializeField] private GameObject m_roofPrefab;
    [Tooltip("If no roof prefab is assigned, a simple cube will be used.")]
    [SerializeField] private float m_roofThickness = 0.2f;
    [SerializeField] private float m_roofYOffset = 12.75f;
    [SerializeField] private bool m_spawnHallwayRoofs = true;

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

    // Expose the rotation the player should face when spawned at StartPosition.
    public Quaternion StartRotation { get; private set; } = Quaternion.identity;

    // Track spawned key instance so we ensure only one per generation and can clear it.
    private GameObject m_keyInstance;

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

        // After generation, spawn elevator at ExitPosition center
        SpawnElevatorAtExit();
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

        StartRotation = Quaternion.identity;

        // Clear key instance reference (actual GameObject children were destroyed above)
        m_keyInstance = null;
    }

    private void BuildLayout()
    {
        // Build a hallway from multiple straight segments. Every segment has an exact
        // configured length (for example 10, 20 or 30 tiles) and every completed
        // segment is followed by a 90-degree turn.
        m_hallCells.Clear();
        m_hallCellSet.Clear();

        int minSegments = Mathf.Max(1, m_minSegments);
        int maxSegments = Mathf.Max(minSegments, m_maxSegments);
        int segments = m_rng.Next(minSegments, maxSegments + 1);

        Vector2Int cur = Vector2Int.zero;
        m_hallCells.Add(cur);
        m_hallCellSet.Add(cur);

        // Choose an initial forward direction (right or up).
        Vector2Int dir = m_rng.Next(0, 2) == 0 ? Vector2Int.right : Vector2Int.up;

        // Keep a list of where each segment starts (index in m_hallCells) so we can
        // avoid placing rooms too close to segment boundaries.
        List<int> segmentStartIndices = new List<int>();

        for (int seg = 0; seg < segments; seg++)
        {
            int segLen = GetRandomSegmentLength();

            // Record the index of the first tile for this segment (next tile to be added)
            segmentStartIndices.Add(m_hallCells.Count);

            // Before adding anything, make sure the complete segment fits. This keeps
            // the segment length exact rather than silently truncating it on collision.
            if (!CanFitSegment(cur, dir, segLen))
            {
                // Try the other direction for this segment.
                Vector2Int oppositeTurn = new Vector2Int(dir.y, -dir.x);
                if (seg > 0 && CanFitSegment(cur, oppositeTurn, segLen))
                {
                    dir = oppositeTurn;
                }
                else
                {
                    break;
                }
            }

            // Commit the entire segment.
            for (int i = 0; i < segLen; i++)
            {
                cur += dir;
                m_hallCells.Add(cur);
                m_hallCellSet.Add(cur);
            }

            // Turn after every segment except the final one.
            if (seg < segments - 1)
            {
                Vector2Int leftDir = new Vector2Int(-dir.y, dir.x);
                Vector2Int rightDir = new Vector2Int(dir.y, -dir.x);

                bool turnLeft = m_rng.Next(0, 2) == 0;
                Vector2Int newDir = turnLeft ? leftDir : rightDir;

                // Prefer the selected turn, but use the opposite turn if the first
                // tile is already occupied. The next segment itself is validated
                // before it is committed.
                if (m_hallCellSet.Contains(cur + newDir))
                {
                    Vector2Int other = turnLeft ? rightDir : leftDir;
                    if (!m_hallCellSet.Contains(cur + other))
                        newDir = other;
                    else
                        break;
                }

                dir = newDir;
            }
        }

        // Build the list of positions starting with hallway cells.
        List<Vector2Int> positions = new List<Vector2Int>(m_hallCells);

        // Place a matched room pair after every configured number of usable hallway tiles.
        // Corners and the start/exit cells are skipped, so rooms do not collide with a turn
        // or replace the special rooms at either end of the hallway.
        int tilesSinceLastRoomPair = 0;
        int roomSpacing = Mathf.Max(1, m_roomSpacing);
        int placedSideRooms = 0;

        for (int idx = 0; idx < m_hallCells.Count; idx++)
        {
            var hallCell = m_hallCells[idx];

            if (!IsUsableRoomPairAnchor(idx))
                continue;

            // Skip anchors that are too close to any segment boundary based on buffer.
            if (IsNearSegmentBoundary(idx, segmentStartIndices, m_roomSegmentBuffer))
                continue;

            tilesSinceLastRoomPair++;
            if (tilesSinceLastRoomPair < roomSpacing)
                continue;

            Vector2Int segmentDir = hallCell - m_hallCells[idx - 1];

            Vector2Int sideA = new Vector2Int(-segmentDir.y, segmentDir.x); // left
            Vector2Int sideB = new Vector2Int(segmentDir.y, -segmentDir.x); // right

            // Use a robust rounding strategy so roomTiles behaves stably at exact sizes.
            int roomTiles = Mathf.Max(1, Mathf.RoundToInt(m_roomSize / m_tileSize));
            int centerOffset = (roomTiles + 1) / 2; // distance (in tiles) from hallway to room center

            Vector2Int roomPosA = hallCell + sideA * centerOffset;
            Vector2Int roomPosB = hallCell + sideB * centerOffset;

            bool hasCapacityForPair = placedSideRooms + 2 <= m_maxRooms;
            bool canPlacePair = hasCapacityForPair
                && CanPlaceRoomFootprint(roomPosA, sideA, segmentDir, hallCell, roomTiles)
                && CanPlaceRoomFootprint(roomPosB, sideB, segmentDir, hallCell, roomTiles);

            if (canPlacePair)
            {
                positions.Add(roomPosA);
                positions.Add(roomPosB);
                placedSideRooms += 2;
                tilesSinceLastRoomPair = 0;
            }

            if (placedSideRooms >= m_maxRooms)
                break;
        }

        // If we still have fewer than m_minRooms total, grow outward from hallway ends or random hallway cells.
        int growIdx = 0;
        Vector2Int[] dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (positions.Count < m_minRooms)
        {
            Vector2Int basePos = positions[m_rng.Next(positions.Count)];
            Vector2Int dirGrow = dirs[m_rng.Next(dirs.Length)];
            Vector2Int newPos = basePos + dirGrow;
            if (!positions.Contains(newPos) && !m_hallCellSet.Contains(newPos))
                positions.Add(newPos);
            if (++growIdx > m_minRooms * 6)
                break;
        }

        foreach (var p in positions)
            m_rooms[p] = null;

        StartPosition = GridToWorld(m_hallCells.First());
        ExitPosition = GridToWorld(m_hallCells.Last());

        if (m_hallCells.Count > 1)
        {
            Vector3 a = GridToWorld(m_hallCells[0]);
            Vector3 b = GridToWorld(m_hallCells[1]);
            Vector3 forward = (b - a);
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                StartRotation = Quaternion.LookRotation(forward.normalized);
            else
                StartRotation = Quaternion.identity;
        }
        else
        {
            StartRotation = Quaternion.identity;
        }
    }

    private bool IsNearSegmentBoundary(int idx, List<int> segmentStarts, int buffer)
    {
        if (segmentStarts == null || segmentStarts.Count == 0 || buffer <= 0)
            return false;

        foreach (var s in segmentStarts)
        {
            // Disallow anchors within buffer tiles before the segment start and buffer tiles after.
            int start = s - buffer;
            int end = s + buffer - 1; // inclusive
            if (idx >= start && idx <= end)
                return true;
        }

        return false;
    }

    private int GetRandomSegmentLength()
    {
        if (m_segmentLengths == null || m_segmentLengths.Length == 0)
            return 10;

        List<int> validLengths = new List<int>();
        foreach (int length in m_segmentLengths)
        {
            if (length > 0)
                validLengths.Add(length);
        }

        if (validLengths.Count == 0)
            return 10;

        return validLengths[m_rng.Next(validLengths.Count)];
    }

    private bool CanFitSegment(Vector2Int start, Vector2Int direction, int length)
    {
        Vector2Int check = start;

        for (int i = 0; i < length; i++)
        {
            check += direction;
            if (m_hallCellSet.Contains(check))
                return false;
        }

        return true;
    }

    // A room pair must be beside a straight, interior hallway tile. This avoids placing
    // room doors at corners, where the wall orientation and neighbouring cells change.
    private bool IsUsableRoomPairAnchor(int index)
    {
        if (index <= 0 || index >= m_hallCells.Count - 1)
            return false;

        Vector2Int incomingDirection = m_hallCells[index] - m_hallCells[index - 1];
        Vector2Int outgoingDirection = m_hallCells[index + 1] - m_hallCells[index];
        return incomingDirection == outgoingDirection;
    }

    // Room footprints use m_roomSize rather than m_tileSize. The room's
    // center is offset from the hallway by half its width, while its
    // footprint is reserved on the generator grid to prevent overlaps.
    private bool CanPlaceRoomFootprint(
        Vector2Int roomCenter,
        Vector2Int side,
        Vector2Int hallDirection,
        Vector2Int anchorHallCell,
        int roomTiles)
    {
        // Use floor-based half so loops are symmetric around the center.
        int half = roomTiles / 2;

        // tangent is along the hallway
        Vector2Int tangent = hallDirection;

        // Occupy roomTiles along tangent, and roomTiles deep away from the hall
        for (int along = -half; along < roomTiles - half; along++)
        {
            for (int depth = 0; depth < roomTiles; depth++)
            {
                Vector2Int cell =
                    roomCenter
                    + tangent * along
                    + side * depth;

                if (m_hallCellSet.Contains(cell))
                    return false;

                if (m_rooms.ContainsKey(cell))
                    return false;
            }
        }

        // Leave a small grid buffer around the footprint so another similarly-sized
        // room cannot be spawned directly beside it. This checks outer cells along the tangent.
        for (int along = -half - 1; along <= roomTiles - half; along++)
        {
            Vector2Int outerCell = roomCenter + tangent * along;
            if (m_hallCellSet.Contains(outerCell))
                return false;
        }

        return true;
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
                // side room or extra room - determine rotation so the open wall faces the hallway
                Quaternion rot = Quaternion.identity;

                // Find nearest hallway cell and derive direction from the room center to that hall cell
                if (m_hallCells.Count > 0)
                {
                    int bestDist = int.MaxValue;
                    Vector2Int bestHall = Vector2Int.zero;
                    foreach (var h in m_hallCells)
                    {
                        int d = Mathf.Abs(h.x - pos.x) + Mathf.Abs(h.y - pos.y);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestHall = h;
                        }
                    }

                    if (bestDist < int.MaxValue)
                    {
                        Vector2Int delta = bestHall - pos;
                        Vector2Int hallDir;

                        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                            hallDir = new Vector2Int(Math.Sign(delta.x), 0);
                        else
                            hallDir = new Vector2Int(0, Math.Sign(delta.y));

                        // Map hall direction to rotation so the room's local +Z faces the hallway
                        if (hallDir == Vector2Int.up)
                            rot = Quaternion.Euler(0f, 0f, 0f);
                        else if (hallDir == Vector2Int.right)
                            rot = Quaternion.Euler(0f, 90f, 0f);
                        else if (hallDir == Vector2Int.down)
                            rot = Quaternion.Euler(0f, 180f, 0f);
                        else if (hallDir == Vector2Int.left)
                            rot = Quaternion.Euler(0f, 270f, 0f);
                    }
                }

                if (m_roomPrefabs != null && m_roomPrefabs.Length > 0)
                    go = Instantiate(m_roomPrefabs[m_rng.Next(m_roomPrefabs.Length)], worldPos, rot, m_parent);
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

        // After placing floors and rooms, spawn walls and roofs
        SpawnHallwayWalls();
        SpawnHallwayRoofs();

        // Spawn a single key into a random non-hall room if a prefab is assigned.
        SpawnKeyInRandomRoom();
    }

    private void SpawnHallwayWalls()
    {
        if (!m_spawnHallwayWalls)
            return;

        for (int idx = 0; idx < m_hallCells.Count; idx++)
        {
            Vector2Int hallCell = m_hallCells[idx];
            HashSet<Vector2Int> sides = new HashSet<Vector2Int>();

            if (idx > 0)
                AddPerpendicularSides(
                    m_hallCells[idx] - m_hallCells[idx - 1],
                    sides
                );

            if (idx < m_hallCells.Count - 1)
                AddPerpendicularSides(
                    m_hallCells[idx + 1] - m_hallCells[idx],
                    sides
                );

            if (sides.Count == 0)
                AddPerpendicularSides(Vector2Int.right, sides);

            foreach (var side in sides)
            {
                Vector2Int adjacent = hallCell + side;

                // Never put a wall where another hallway tile exists.
                if (m_hallCellSet.Contains(adjacent))
                    continue;

                bool hasRoomBehind =
                    m_rooms.ContainsKey(adjacent) &&
                    !IsHallwayCell(adjacent);

                GameObject chosenPrefab =
                    hasRoomBehind
                        ? (m_wallDoorPrefab ?? m_wallPrefab)
                        : m_wallPrefab;

                Vector3 spawnPos = GridToWorld(hallCell) +
                    new Vector3(
                        side.x * m_tileSize * 0.5f,
                        m_wallYOffset,
                        side.y * m_tileSize * 0.5f
                    );

                if (chosenPrefab == null)
                {
                    // -------------------------------------------------
                    // FALLBACK CUBE
                    // -------------------------------------------------

                    GameObject wall = GameObject.CreatePrimitive(
                        PrimitiveType.Cube
                    );

                    wall.transform.SetParent(m_parent, false);
                    wall.transform.position = spawnPos;

                    if (Mathf.Abs(side.x) > 0)
                    {
                        wall.transform.localScale = new Vector3(
                            m_wallThickness,
                            m_wallHeight,
                            m_tileSize
                        );
                    }
                    else
                    {
                        wall.transform.localScale = new Vector3(
                            m_tileSize,
                            m_wallHeight,
                            m_wallThickness
                        );
                    }

                    wall.name =
                        $"Wall_{hallCell.x}_{hallCell.y}_{side.x}_{side.y}";

                    continue;
                }

                // -------------------------------------------------
                // DETERMINE HALLWAY DIRECTION FOR THIS WALL
                // -------------------------------------------------

                Vector2Int hallDirection = Vector2Int.zero;

                // Prefer the direction that actually generated this side.
                if (idx > 0)
                {
                    Vector2Int incoming =
                        m_hallCells[idx] - m_hallCells[idx - 1];

                    Vector2Int incomingLeft =
                        new Vector2Int(-incoming.y, incoming.x);

                    Vector2Int incomingRight =
                        new Vector2Int(incoming.y, -incoming.x);

                    if (side == incomingLeft || side == incomingRight)
                        hallDirection = incoming;
                }

                if (idx < m_hallCells.Count - 1)
                {
                    Vector2Int outgoing =
                        m_hallCells[idx + 1] - m_hallCells[idx];

                    Vector2Int outgoingLeft =
                        new Vector2Int(-outgoing.y, outgoing.x);

                    Vector2Int outgoingRight =
                        new Vector2Int(outgoing.y, -outgoing.x);

                    if (side == outgoingLeft || side == outgoingRight)
                        hallDirection = outgoing;
                }

                // Safety fallback.
                if (hallDirection == Vector2Int.zero)
                    hallDirection = Vector2Int.right;

                // -------------------------------------------------
                // EXISTING WALL ORIENTATION
                // -------------------------------------------------

                Vector3 forward =
                    Mathf.Abs(side.x) > 0
                        ? Vector3.forward
                        : Vector3.right;

                Quaternion rot = Quaternion.LookRotation(forward);

                // -------------------------------------------------
                // LEFT / RIGHT SIDE OF HALLWAY
                // -------------------------------------------------
                //
                // This uses the exact same definition as BuildLayout:
                //
                // left  = (-dir.y, dir.x)
                // right = ( dir.y,-dir.x)
                //
                Vector2Int leftSide =
                    new Vector2Int(
                        -hallDirection.y,
                        hallDirection.x
                    );

                bool isLeftSide = side == leftSide;

                // Your wall prefab has the lamp facing correctly
                // when placed on the right side.
                //
                // When the wall is on the LEFT side, rotate it
                // 180 degrees around Y so the lamp is inside the
                // hallway instead of outside.
                if (isLeftSide)
                {
                    rot *= Quaternion.Euler(0f, 180f, 0f);
                }

                // -------------------------------------------------
                // INSTANTIATE
                // -------------------------------------------------

                GameObject wallObject = Instantiate(
                    chosenPrefab,
                    spawnPos,
                    rot,
                    m_parent
                );

                wallObject.name =
                    $"Wall_{hallCell.x}_{hallCell.y}_{side.x}_{side.y}";
            }
        }
    }

    private static void AddPerpendicularSides(Vector2Int direction, HashSet<Vector2Int> sides)
    {
        sides.Add(new Vector2Int(-direction.y, direction.x));
        sides.Add(new Vector2Int(direction.y, -direction.x));
    }

    private void SpawnHallwayRoofs()
    {
        if (!m_spawnHallwayRoofs)
            return;

        // The roof sits directly on top of the walls.
        // Example:
        // wall height = 3
        // wall centre Y = 1.5
        // roof centre Y = 3
        float roofY = m_roofYOffset; //m_wallYOffset + (m_wallHeight * 0.5f);

        foreach (var hallCell in m_hallCells)
        {
            Vector3 floorPos = GridToWorld(hallCell);
            Vector3 roofPos = new Vector3(floorPos.x, roofY, floorPos.z);

            if (m_roofPrefab != null)
            {
                GameObject roof = Instantiate(
                    m_roofPrefab,
                    roofPos,
                    Quaternion.identity,
                    m_parent
                );

                roof.name = $"Roof_{hallCell.x}_{hallCell.y}";
            }
            else
            {
                // Fallback roof cube
                GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);

                roof.transform.SetParent(m_parent, false);
                roof.transform.position = roofPos;
                roof.transform.localScale = new Vector3(
                    m_tileSize,
                    m_roofThickness,
                    m_tileSize
                );

                roof.name = $"Roof_{hallCell.x}_{hallCell.y}";
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

    /// <summary>
    /// Spawn exactly one key into a random non-hall room. If no key prefab is assigned or
    /// no valid rooms exist, nothing will be spawned. This ensures a single key per generation.
    /// </summary>
    private void SpawnKeyInRandomRoom()
    {
        if (m_keyPrefab == null)
            return;

        // Collect candidate room GameObjects (exclude hallway tiles)
        var candidates = m_rooms.Where(kv => !IsHallwayCell(kv.Key) && kv.Value != null)
                                .Select(kv => kv.Value)
                                .ToList();

        if (candidates.Count == 0)
            return;

        int idx = m_rng.Next(candidates.Count);
        var chosenRoom = candidates[idx];

        // Prefer a child transform named "Center" if it exists so the key is placed at the intended marker.
        Transform center = chosenRoom.transform.Find("Center");

        // Instantiate and parent to the chosen room so it gets cleaned up with the level.
        m_keyInstance = Instantiate(m_keyPrefab, chosenRoom.transform);
        m_keyInstance.name = "Key";

        if (center != null)
        {
            m_keyInstance.transform.localPosition = center.localPosition;
            m_keyInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            m_keyInstance.transform.localPosition = Vector3.zero;
            m_keyInstance.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Generate a level while preserving a specified "preserved" transform (endpoint). The preserved transform
    /// will remain as the start/anchor for the next generation. All other children are cleared.
    /// </summary>
    public void GenerateFromPreservedEndpoint(Transform preserved)
    {
        if (preserved == null)
        {
            Generate();
            return;
        }

        // Clear all children except preserved
        if (m_parent == null)
        {
            GameObject go = new GameObject("ProceduralLevel");
            m_parent = go.transform;
        }

        for (int i = m_parent.childCount - 1; i >= 0; i--)
        {
            var child = m_parent.GetChild(i);
            if (child == preserved)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        m_rooms.Clear();
        RoomCenters.Clear();
        m_hallCells.Clear();
        m_hallCellSet.Clear();

        // Temporarily move generator root so that GridToWorld aligns preserved endpoint to grid origin
        Vector3 originalPos = transform.position;
        transform.position = preserved.position;

        // Rebuild layout and instantiate under the same parent
        int s = m_seed;
        if (s == 0) s = Environment.TickCount;
        m_rng = new System.Random(s);

        BuildLayout();
        InstantiateRooms();

        if (m_autoBuildNavMesh)
            BuildNavMesh();

        // restore original position
        transform.position = originalPos;

        OnLevelGenerated?.Invoke();

        // After generation, spawn elevator at ExitPosition
        SpawnElevatorAtExit();
    }

    private void SpawnElevatorAtExit()
    {
        if (m_parent == null)
            return;

        // Remove existing elevator(s)
        var existing = m_parent.GetComponentsInChildren<Elevator>(true);
        foreach (var e in existing)
        {
            if (e != null)
                Destroy(e.gameObject);
        }

        GameObject exitGO = new GameObject("Exit");
        exitGO.transform.SetParent(m_parent, false);
        exitGO.transform.position = ExitPosition + Vector3.up * 0.5f;

        SphereCollider sc = exitGO.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.25f;

        // Add Elevator behaviour
        var elev = exitGO.AddComponent<Elevator>();

        // Visual marker optional
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "ExitMarker";
        marker.transform.SetParent(exitGO.transform, false);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localScale = new Vector3(1f, 0.15f, 1f);
        Collider markerCol = marker.GetComponent<Collider>();
        if (markerCol != null)
            Destroy(markerCol);
    }
}
