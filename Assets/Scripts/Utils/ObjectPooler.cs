using System.Collections.Generic;
using Unity.VisualScripting;
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

    private Dictionary<string, Queue<GameObject>> m_poolDictionary = new();

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
        foreach (var pool in m_pools)
        {
            Queue<GameObject> queue = new();

            for (int i = 0; i < pool.initialSize; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);

                obj.GetComponent<IPoolable>()?.SetPoolKey(pool.key);

                queue.Enqueue(obj);
            }

            m_poolDictionary.Add(pool.key, queue);
        }
    }

    public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
    {
        if (!m_poolDictionary.ContainsKey(key))
        {
            Debug.LogError($"Pool with key {key} does not exist.");
            return null;
        }

        var queue = m_poolDictionary[key];

        GameObject obj = queue.Count > 0 ? queue.Dequeue() : null;

        if (obj == null)
        {
            Debug.LogWarning($"Pool {key} empty, consider increasing size.");
            return null;
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        return obj;
    }

    public void ReturnToPool(string key, GameObject obj)
    {
        obj.SetActive(false);
        m_poolDictionary[key].Enqueue(obj);
    }
}