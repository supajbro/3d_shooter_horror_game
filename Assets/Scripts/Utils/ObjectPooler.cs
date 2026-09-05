using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance;

    [System.Serializable]
    public class Pool
    {
        public string key;
        public GameObject prefab;
        public int initialSize;
    }

    [SerializeField] private List<Pool> m_pools;

    private readonly Dictionary<string, Queue<GameObject>> m_poolDictionary = new();
    private readonly Dictionary<string, Pool> m_poolDefinitions = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitialisePools();
    }

    private void InitialisePools()
    {
        if (m_pools == null)
            return;

        foreach (var pool in m_pools)
            RegisterPool(pool.key, pool.prefab, pool.initialSize);
    }

    /// <summary>
    /// Adds a pool at runtime. This is useful for systems whose prefabs are not
    /// scene specific, such as combat feedback.
    /// </summary>
    public bool RegisterPool(string key, GameObject prefab, int initialSize = 0)
    {
        if (string.IsNullOrWhiteSpace(key) || prefab == null)
        {
            Debug.LogError("A pool needs a key and prefab.");
            return false;
        }

        if (m_poolDictionary.ContainsKey(key))
            return false;

        Pool pool = new Pool
        {
            key = key,
            prefab = prefab,
            initialSize = Mathf.Max(0, initialSize)
        };

        Queue<GameObject> queue = new();
        m_poolDefinitions.Add(key, pool);
        m_poolDictionary.Add(key, queue);

        for (int i = 0; i < pool.initialSize; i++)
            queue.Enqueue(CreatePooledObject(pool));

        return true;
    }

    public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
    {
        if (!m_poolDictionary.ContainsKey(key))
        {
            Debug.LogError($"Pool with key {key} does not exist.");
            return null;
        }

        var queue = m_poolDictionary[key];

        GameObject obj = queue.Count > 0 ? queue.Dequeue() : CreatePooledObject(m_poolDefinitions[key]);

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        return obj;
    }

    public void ReturnToPool(string key, GameObject obj)
    {
        if (obj == null || !m_poolDictionary.ContainsKey(key))
            return;

        obj.SetActive(false);
        m_poolDictionary[key].Enqueue(obj);
    }

    private GameObject CreatePooledObject(Pool pool)
    {
        GameObject obj = Instantiate(pool.prefab);
        obj.SetActive(false);
        obj.GetComponent<IPoolable>()?.SetPoolKey(pool.key);
        return obj;
    }

    public bool HasPool(string key)
    {
        return m_poolDictionary.ContainsKey(key);
    }
}
