using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Elevator : MonoBehaviour
{
    public enum State
    {
        Available,
        Transitioning,
        Generating
    }

    private State m_state = State.Available;
    private bool m_playerInside = false;
    private bool m_transitionInProgress = false;

    private ProceduralLevelGenerator m_generator;
    private LevelManager m_levelManager;

    private float m_timer = 0f;
    private int m_step = 0;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col == null)
            col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
    }

    private void Start()
    {
        m_generator = FindObjectOfType<ProceduralLevelGenerator>();
        m_levelManager = FindObjectOfType<LevelManager>();
    }

    private void Update()
    {
        // Interaction key is E
        if (m_playerInside && !m_transitionInProgress && Input.GetKeyDown(KeyCode.E))
        {
            var gsm = GameStateManager.Instance;
            if (gsm == null)
                return;

            var current = gsm.GetCurrentState();
            if (!(current is GameplayState gameplay))
                return;

/*            if (gameplay.keyCollected <= 0)
                return;*/

            // begin transition
            StartTransition();
        }

        if (m_transitionInProgress)
        {
            m_timer += Time.deltaTime;

            if (m_step == 1)
            {
                // wait for 2 seconds
                if (m_timer >= 2f)
                {
                    m_timer = 0f;
                    m_step = 2;
                    // Remove previous generated terrain except current endpoint
                    CleanupOldTerrainPreserveEndpoint();
                }
            }
            else if (m_step == 2)
            {
                // wait another 2 seconds before generating
                if (m_timer >= 2f)
                {
                    m_timer = 0f;
                    m_step = 3;
                    // Generate next procedural section using current endpoint as start
                    GenerateNextSectionFromEndpoint();
                }
            }
            else if (m_step == 3)
            {
                // generation finished
                m_transitionInProgress = false;
                m_state = State.Available;
            }
        }
    }

    private void StartTransition()
    {
        if (m_state != State.Available)
            return;

        m_state = State.Transitioning;
        m_transitionInProgress = true;
        m_timer = 0f;
        m_step = 1;
    }

    private void CleanupOldTerrainPreserveEndpoint()
    {
        if (m_generator == null)
            m_generator = FindObjectOfType<ProceduralLevelGenerator>();
        if (m_generator == null)
            return;

        // LevelRoot contains generated children. We'll remove all children except this elevator's parent endpoint object.
        var root = m_generator.LevelRoot;
        if (root == null)
            return;

        // Determine which child contains this elevator (we expect elevator parented under generator's LevelRoot)
        Transform preserved = null;
        foreach (Transform child in root)
        {
            if (child == transform.parent)
            {
                preserved = child;
                break;
            }
        }

        // Destroy all other children
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child == preserved)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void GenerateNextSectionFromEndpoint()
    {
        if (m_generator == null)
            m_generator = FindObjectOfType<ProceduralLevelGenerator>();
        if (m_generator == null)
            return;

        // Set the generator start position to this preserved endpoint's world position
        var preservedParent = transform.parent;
        if (preservedParent != null)
        {
            var gen = m_generator;
            // Move the generator's parent to preserved endpoint so new children are created under it
            // But to preserve API we will set a temporary parent and reposition grid so StartPosition aligns.
            // Simplest approach: set StartPosition and StartRotation via reflection-like assignment if available

            // The generator exposes public StartPosition and StartRotation only as getters. We'll mimic behavior by
            // moving the generator's parent to the preserved position and calling Generate().
            Vector3 oldParentPos = gen.LevelRoot != null ? gen.LevelRoot.position : Vector3.zero;

            // Move generator root to preservedParent position so generated GridToWorld aligns with preserved endpoint
            gen.transform.position = preservedParent.position;

            gen.Generate();

            // Re-parent elevator under new exit position: find new exit and move elevator there
            var newExit = gen.ExitPosition;
            transform.SetParent(gen.LevelRoot, true);
            transform.position = newExit + Vector3.up * 0.5f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        m_playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        m_playerInside = false;
    }
}
