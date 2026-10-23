using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>Difficulty values applied to every guard the GuardManager spawns.</summary>
[Serializable]
public class CopDifficulty
{
    public float patrolSpeed = 1.6f;
    public float chaseSpeed = 3f;
    [Tooltip("Radius that notices a walking player carrying a cake.")]
    public float detectionRadius = 6f;
    [Tooltip("Radius that notices a sneaking player, or one with cakes only in the inventory.")]
    public float sneakDetectionRadius = 2.5f;
    [Tooltip("Seconds to notice a walking player at point blank (lower = harder).")]
    public float timeToDetect = 1.2f;
    public int moneyStolen = 40;

    public CopDifficulty Clone()
    {
        return (CopDifficulty)MemberwiseClone();
    }
}

/// <summary>Difficulty preset that starts at a given day. The highest tier whose day has been reached is used.</summary>
[Serializable]
public class GuardDayTier
{
    [Min(1)] public int fromDay = 1;
    [Min(0)] public int guardCount = 1;
    [Tooltip("Seconds between each gradually arriving guard (lower = harder).")]
    [Min(0f)] public float spawnIntervalSeconds = 8f;
    [Tooltip("Seconds a leaving guard has to reach the exit before it vanishes (higher = guards linger longer).")]
    [Min(0f)] public float despawnTimeoutSeconds = 30f;
    public CopDifficulty difficulty = new CopDifficulty();
}

/// <summary>
/// Controller for the patrolling guards. Put it on an empty object, assign the guard prefab, the spawn
/// points and the patrol route. Guard count and difficulty can be changed at any time from code
/// (SetGuardCount, ApplyDifficulty, ApplyDay) or by editing the values in the Inspector during play.
/// </summary>
[DisallowMultipleComponent]
public class GuardManager : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Guard prefab (drag it from the Project window). It must contain a CopEnemy and a NavMeshAgent.")]
    [SerializeField] private GameObject guardPrefab;
    [Tooltip("Where guards appear. If empty, they appear at this object's position.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Patrol route shared by every spawned guard (each starts at a random point).")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool randomWaypoints = true;
    [SerializeField] private float navMeshSnapDistance = 3f;
    [Tooltip("Guards that appear immediately, so the prison is never empty. The rest arrive gradually.")]
    [SerializeField] private int instantGuards = 1;
    [Tooltip("Seconds between each gradually arriving guard.")]
    [SerializeField] private float spawnIntervalSeconds = 8f;
    [Tooltip("A despawning guard walks to the nearest spawn point; it vanishes after this many seconds if it cannot reach it.")]
    [SerializeField] private float despawnTimeoutSeconds = 30f;
    [Tooltip("Hard limit, so a typo in the Inspector cannot flood the scene.")]
    [SerializeField] private int maxGuards = 8;

    [Header("Manual Control")]
    [Tooltip("Used when 'Use Day Tiers' is off. Editing it in play mode spawns or despawns guards right away.")]
    [SerializeField] private int guardCount = 1;
    [SerializeField] private CopDifficulty difficulty = new CopDifficulty();

    [Header("Day Difficulty")]
    [Tooltip("When on, guard count and difficulty come from the tiers below, using DayProgress.CurrentDay.")]
    [SerializeField] private bool useDayTiers = false;
    [SerializeField] private List<GuardDayTier> dayTiers = new List<GuardDayTier>();

    private readonly List<CopEnemy> guards = new List<CopEnemy>();
    private bool inspectorChanged;
    private int targetGuardCount;
    private float nextSpawnTime;

    public int ActiveGuardCount
    {
        get
        {
            PruneDestroyedGuards();
            return guards.Count;
        }
    }

    public CopDifficulty Difficulty
    {
        get { return difficulty; }
    }

    private void Start()
    {
        if (useDayTiers)
        {
            ApplyDay(DayProgress.CurrentDay);
        }
        else
        {
            SetGuardCount(guardCount);
        }
    }

    // Inspector edits are applied on the next Update: spawning from OnValidate is not allowed by Unity.
    private void OnValidate()
    {
        guardCount = Mathf.Clamp(guardCount, 0, Mathf.Max(0, maxGuards));
        inspectorChanged = Application.isPlaying;
    }

    private void Update()
    {
        if (inspectorChanged)
        {
            inspectorChanged = false;

            if (!useDayTiers)
            {
                ApplyDifficulty(difficulty);
                SetGuardCount(guardCount);
            }
        }

        UpdateGradualSpawn();
    }

    // Guards beyond the instant ones arrive one at a time, spawnIntervalSeconds apart.
    private void UpdateGradualSpawn()
    {
        PruneDestroyedGuards();

        if (guards.Count >= targetGuardCount || Time.time < nextSpawnTime)
        {
            return;
        }

        if (SpawnGuard())
        {
            nextSpawnTime = Time.time + Mathf.Max(0f, spawnIntervalSeconds);
        }
        else
        {
            // No prefab assigned: avoid trying (and warning) every frame.
            nextSpawnTime = Time.time + 1f;
        }
    }

    /// <summary>
    /// Sets how many guards should exist. The first <c>Instant Guards</c> appear immediately,
    /// the rest arrive gradually. Extra guards leave right away.
    /// </summary>
    public void SetGuardCount(int count)
    {
        targetGuardCount = Mathf.Clamp(count, 0, Mathf.Max(0, maxGuards));
        PruneDestroyedGuards();

        int instant = Mathf.Min(targetGuardCount, Mathf.Max(0, instantGuards));
        while (guards.Count < instant)
        {
            if (!SpawnGuard())
            {
                break;
            }
        }

        while (guards.Count > targetGuardCount)
        {
            DespawnGuard();
        }
    }

    /// <summary>Adds guards on top of the target; they arrive gradually.</summary>
    public void SpawnGuards(int amount)
    {
        SetGuardCount(targetGuardCount + Mathf.Max(0, amount));
    }

    /// <summary>Despawns guards. Those that are not chasing the player go first.</summary>
    public void DespawnGuards(int amount)
    {
        SetGuardCount(targetGuardCount - Mathf.Max(0, amount));
    }

    public void DespawnAll()
    {
        SetGuardCount(0);
    }

    /// <summary>Sets the difficulty for current and future guards.</summary>
    public void ApplyDifficulty(CopDifficulty newDifficulty)
    {
        if (newDifficulty == null)
        {
            return;
        }

        difficulty = newDifficulty.Clone();
        PruneDestroyedGuards();

        for (int i = 0; i < guards.Count; i++)
        {
            guards[i].ApplyDifficulty(difficulty);
        }
    }

    /// <summary>Applies the tier for the given day: guard count and difficulty.</summary>
    public void ApplyDay(int day)
    {
        GuardDayTier tier = FindTier(day);
        if (tier == null)
        {
            SetGuardCount(guardCount);
            return;
        }

        guardCount = tier.guardCount;
        spawnIntervalSeconds = tier.spawnIntervalSeconds;
        despawnTimeoutSeconds = tier.despawnTimeoutSeconds;
        ApplyDifficulty(tier.difficulty);
        SetGuardCount(tier.guardCount);
    }

    private GuardDayTier FindTier(int day)
    {
        GuardDayTier best = null;
        for (int i = 0; i < dayTiers.Count; i++)
        {
            GuardDayTier tier = dayTiers[i];
            if (tier != null && tier.fromDay <= day && (best == null || tier.fromDay > best.fromDay))
            {
                best = tier;
            }
        }

        return best;
    }

    private bool SpawnGuard()
    {
        if (guardPrefab == null)
        {
            Debug.LogWarning("GuardManager: assign a guard prefab to spawn guards.", this);
            return false;
        }

        Vector3 position = PickSpawnPosition();
        GameObject instance = Instantiate(guardPrefab, position, Quaternion.identity, transform);
        CopEnemy guard = instance.GetComponentInChildren<CopEnemy>();
        if (guard == null)
        {
            Debug.LogWarning("GuardManager: the guard prefab has no CopEnemy component.", this);
            Destroy(instance);
            return false;
        }

        // Warp makes sure the agent really starts at the spawn point instead of wherever it was auto-placed.
        NavMeshAgent agent = guard.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Warp(position);
        }

        instance.name = guardPrefab.name + " " + (guards.Count + 1);
        guard.ApplyDifficulty(difficulty);
        guard.SetWaypoints(patrolWaypoints, randomWaypoints);
        guards.Add(guard);
        nextSpawnTime = Time.time + Mathf.Max(0f, spawnIntervalSeconds);
        return true;
    }

    private void DespawnGuard()
    {
        int index = guards.Count - 1;

        // Prefer a guard that is not hunting the player, so despawning never feels like a cheat.
        for (int i = guards.Count - 1; i >= 0; i--)
        {
            if (guards[i].State == CopEnemy.GuardState.Patrol)
            {
                index = i;
                break;
            }
        }

        CopEnemy guard = guards[index];
        guards.RemoveAt(index);

        if (guard == null)
        {
            return;
        }

        // It is no longer counted as active; it walks to the nearest spawn point and disappears there.
        guard.LeaveAndDespawn(FindNearestSpawnPosition(guard.transform.position), despawnTimeoutSeconds);
    }

    private Vector3 FindNearestSpawnPosition(Vector3 from)
    {
        Vector3 best = transform.position;
        float bestDistance = float.MaxValue;

        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null)
                {
                    continue;
                }

                float distance = (spawnPoints[i].position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spawnPoints[i].position;
                }
            }
        }

        NavMeshHit hit;
        return NavMesh.SamplePosition(best, out hit, navMeshSnapDistance, NavMesh.AllAreas) ? hit.position : best;
    }

    private Vector3 PickSpawnPosition()
    {
        Vector3 position = transform.position;
        string pointName = name;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point != null)
            {
                position = point.position;
                pointName = point.name;
            }
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            if ((hit.position - position).magnitude > 0.5f)
            {
                Debug.LogWarning("GuardManager: spawn point '" + pointName + "' is not on the NavMesh; the guard was moved "
                    + (hit.position - position).magnitude.ToString("F1") + " units to the nearest NavMesh point. "
                    + "Move the spawn point onto the blue area.", this);
            }

            position = hit.position;
        }
        else
        {
            Debug.LogWarning("GuardManager: no NavMesh within " + navMeshSnapDistance + " units of spawn point '"
                + pointName + "'. The guard will not be able to move.", this);
        }

        return position;
    }

    private void PruneDestroyedGuards()
    {
        guards.RemoveAll(g => g == null);
    }
}
