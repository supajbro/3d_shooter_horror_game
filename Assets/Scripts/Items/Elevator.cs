using UnityEngine;
using StarterAssets;

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

        // Ensure radius is large as requested
        if (col is SphereCollider sc)
        {
            sc.radius = 15f;
        }
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
            Debug.Log("Elevator: E pressed while inside");
            var gsm = GameStateManager.Instance;
            if (gsm == null)
                return;

            var current = gsm.GetCurrentState();
            if (!(current is GameplayState gameplay))
                return;

            if (gameplay.keyCollected <= 0)
            {
                Debug.Log("Elevator: no keys collected, cannot use");
                return;
            }

            // begin transition
            Debug.Log("Elevator: starting transition");
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

        Debug.Log("Elevator: StartTransition called - state set to Transitioning");
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

        // Determine which child contains this elevator (we expect elevator parented under the exit room)
        Transform preserved = null;

        // If elevator is parented to the exit room, transform.parent will be that room and will be among root children
        if (transform.parent != null && transform.parent.parent == root)
        {
            preserved = transform.parent;
        }
        else
        {
            // Fallback: pick the child closest to this elevator's position (in case parenting isn't as expected)
            float best = float.MaxValue;
            foreach (Transform child in root)
            {
                float d = Vector3.Distance(child.position, transform.position);
                if (d < best)
                {
                    best = d;
                    preserved = child;
                }
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

        Debug.Log("Elevator: CleanupOldTerrainPreserveEndpoint completed");
    }

    private void GenerateNextSectionFromEndpoint()
    {
        if (m_generator == null)
            m_generator = FindObjectOfType<ProceduralLevelGenerator>();
        if (m_generator == null)
            return;

        // Use the elevator's parent (the exit room) as the preserved endpoint
        var preservedParent = transform.parent;
        if (preservedParent != null)
        {
            m_state = State.Generating;
            Debug.Log("Elevator: Calling GenerateFromPreservedEndpoint");
            m_generator.GenerateFromPreservedEndpoint(preservedParent);
            // After calling the generator, the generator will spawn a new elevator and remove existing ones.
            // This GameObject may be destroyed by the generator; do not attempt to reposition it here.
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Elevator: OnTriggerEnter with {other.gameObject.name} tag={other.gameObject.tag}");

        // Detect player by tag or by player controller/component
        if (other.CompareTag("Player") || other.GetComponentInParent<FirstPersonController>() != null || other.GetComponent<CharacterController>() != null)
        {
            m_playerInside = true;
            Debug.Log("Elevator: Player entered elevator trigger");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"Elevator: OnTriggerExit with {other.gameObject.name} tag={other.gameObject.tag}");

        if (other.CompareTag("Player") || other.GetComponentInParent<FirstPersonController>() != null || other.GetComponent<CharacterController>() != null)
        {
            m_playerInside = false;
            Debug.Log("Elevator: Player exited elevator trigger");
        }
    }
}
