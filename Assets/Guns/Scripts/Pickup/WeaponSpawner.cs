using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WeaponSpawner : MonoBehaviour
{
    private LevelManager m_manager;

    // Track spawned pickups to avoid overlap and to respawn when removed
    private readonly List<GunPickup> m_spawnedPickups = new List<GunPickup>();
    private readonly List<Transform> m_spawnLocations = new List<Transform>();

    public enum Difficulty { EASY, MEDIUM, HARD }

    [Header("Spawn probabilities (percentage of tiles)")]
    [Range(0f, 100f)] public float easyPercentMin = 5f;
    [Range(0f, 100f)] public float easyPercentMax = 7f;
    [Range(0f, 100f)] public float mediumPercentMin = 3f;
    [Range(0f, 100f)] public float mediumPercentMax = 5f;
    [Range(0f, 100f)] public float hardPercentMin = 0.5f;
    [Range(0f, 100f)] public float hardPercentMax = 2f;

    [Header("Respawn timing")]
    [Tooltip("Seconds before a removed weapon can respawn")]
    public float respawnDelayMin = 10f;
    public float respawnDelayMax = 30f;

    private Difficulty m_difficulty = Difficulty.MEDIUM;

    public void Init()
    {
        m_manager = Object.FindFirstObjectByType<LevelManager>();

        // Gather candidate spawn locations from level generator room centers
        var gen = m_manager.GetLevelGenerator();
        if (gen != null)
        {
            foreach (var t in gen.RoomCenters)
            {
                if (t != null)
                    m_spawnLocations.Add(t);
            }
        }
    }

    public void SpawnWeapon(BaseGunController.GunType gunType, Transform trans, UnityEvent onPickup)
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }
        var gun = Instantiate(m_manager.GetGunPickup(gunType), trans.position, Quaternion.identity);

        var gp = gun.GetComponent<GunPickup>();
        if (gp != null)
            m_spawnedPickups.Add(gp);

        // null check as we don't always add a new listener when calling this
        if (onPickup != null)
        {
            gun.OnGunPickup.AddListener(() => OnPickupCollected(gun.GetComponent<GunPickup>(), onPickup));
        }
    }

    public void SpawnWeaponRandom(Transform trans, UnityEvent onPickup)
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }

        var values = System.Enum.GetValues(typeof(BaseGunController.GunType));
        var randomIndex = Random.Range(0, values.Length);
        var randomGun = (BaseGunController.GunType)values.GetValue(randomIndex);
        var gun = Instantiate(m_manager.GetGunPickup(randomGun), trans.position, Quaternion.identity);

        var gp = gun.GetComponent<GunPickup>();
        if (gp != null)
            m_spawnedPickups.Add(gp);

        if (onPickup != null)
        {
            gun.OnGunPickup.AddListener(() => OnPickupCollected(gun.GetComponent<GunPickup>(), onPickup));
        }
    }

    public void SpawnShotgunOrPistol(Transform trans, UnityEvent onPickup)
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }

        var values = System.Enum.GetValues(typeof(BaseGunController.GunType));
        var randomIndex = Random.Range(1, 3); // 1 or 2
        var randomGun = (BaseGunController.GunType)values.GetValue(randomIndex);
        var gun = Instantiate(m_manager.GetGunPickup(randomGun), trans.position, Quaternion.identity);

        var gp = gun.GetComponent<GunPickup>();
        if (gp != null)
            m_spawnedPickups.Add(gp);

        // null check as we don't always add a new listener when calling this
        if (onPickup != null)
        {
            gun.OnGunPickup.AddListener(() => OnPickupCollected(gun.GetComponent<GunPickup>(), onPickup));
        }
    }

    private void OnPickupCollected(GunPickup pickup, UnityEvent onPickup)
    {
        if (pickup != null)
            m_spawnedPickups.Remove(pickup);

        // invoke external listeners (like WeaponPad)
        onPickup?.Invoke();

        // Schedule respawn
        StartCoroutine(ScheduleRespawn());
    }

    private IEnumerator ScheduleRespawn()
    {
        // wait random respawn delay
        float wait = Random.Range(respawnDelayMin, respawnDelayMax);
        yield return new WaitForSeconds(wait);

        TrySpawnOneRandom();
    }

    public void TrySpawnInitialDistribution(Difficulty difficulty)
    {
        m_difficulty = difficulty;

        if (m_spawnLocations.Count == 0)
            return;

        float percent = GetRandomPercentForDifficulty(difficulty) / 100f;
        int spawnCount = Mathf.Clamp(Mathf.RoundToInt(m_spawnLocations.Count * percent), 1, m_spawnLocations.Count);

        HashSet<int> used = new HashSet<int>();
        int tries = 0;
        while (used.Count < spawnCount && tries < m_spawnLocations.Count * 4)
        {
            int idx = Random.Range(0, m_spawnLocations.Count);
            if (used.Contains(idx)) { tries++; continue; }
            var t = m_spawnLocations[idx];
            if (IsLocationOccupied(t.position)) { tries++; continue; }

            t.position = new Vector3(t.position.x, t.position.y + 2.0f, t.position.z);

            // Spawn random weapon at that center
            //SpawnWeaponRandom(t, null);
            SpawnShotgunOrPistol(t, null);
            used.Add(idx);
        }
    }

    private void TrySpawnOneRandom()
    {
        if (m_spawnLocations.Count == 0)
            return;

        // pick a random location that's not occupied
        int tries = 0;
        while (tries < 50)
        {
            var t = m_spawnLocations[Random.Range(0, m_spawnLocations.Count)];
            if (!IsLocationOccupied(t.position))
            {
                SpawnWeaponRandom(t, null);
                return;
            }
            tries++;
        }
    }

    private bool IsLocationOccupied(Vector3 pos)
    {
        foreach (var p in m_spawnedPickups)
        {
            if (p == null) continue;
            if (Vector3.Distance(p.transform.position, pos) < 1.0f)
                return true;
        }
        return false;
    }

    private float GetRandomPercentForDifficulty(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.EASY:
                return Random.Range(easyPercentMin, easyPercentMax);
            case Difficulty.MEDIUM:
                return Random.Range(mediumPercentMin, mediumPercentMax);
            case Difficulty.HARD:
                return Random.Range(hardPercentMin, hardPercentMax);
        }
        return Random.Range(mediumPercentMin, mediumPercentMax);
    }
}
