using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 在已烘焙 NavMesh 上随机生成敌人，并把场上存活数量限制在五只以内。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private const int HardEnemyLimit = 5;
    private const int SpawnPointAttempts = 40;

    [Header("Spawn Rules")]
    [SerializeField] private Enemy m_EnemyPrefab;
    [SerializeField, Range(1, HardEnemyLimit)] private int m_MaxAliveEnemies = HardEnemyLimit;
    [SerializeField, Range(1, HardEnemyLimit)] private int m_InitialEnemyCount = 3;
    [SerializeField, Min(0.5f)] private float m_SpawnInterval = 6f;
    [SerializeField, Min(0f)] private float m_MinDistanceFromPlayer = 15f;
    [SerializeField, Min(0f)] private float m_MinEnemySpacing = 6f;

    private readonly HashSet<Enemy> m_AliveEnemies = new();
    private NavMeshPath m_Path;
    private Player m_Player;
    private Vector3[] m_NavMeshVertices;
    private int[] m_NavMeshIndices;
    private float[] m_CumulativeTriangleAreas;
    private float m_TotalNavMeshArea;
    private float m_SpawnTimer;
    private bool m_InitialSpawnCompleted;

    private void Awake()
    {
        // NavMeshPath 包装了 Unity 原生对象，不能在 MonoBehaviour 字段初始化阶段创建。
        m_Path = new NavMeshPath();
    }

    private void Start()
    {
        m_Player = FindObjectOfType<Player>();
        CacheNavMeshTriangles();

        // 场景中如果仍保留了手工放置的敌人，也纳入数量上限，避免超过五只。
        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
            RegisterEnemy(enemy);
    }

    private void Update()
    {
        // 主菜单期间 Time.timeScale 为 0；等待正式取得操作权后再生成敌人。
        if (!MainMenuUI.IsInputEnabled || m_EnemyPrefab == null || m_TotalNavMeshArea <= 0f)
            return;

        if (!m_InitialSpawnCompleted)
        {
            int targetCount = Mathf.Min(m_InitialEnemyCount, m_MaxAliveEnemies);
            while (m_AliveEnemies.Count < targetCount && TrySpawnEnemy())
            {
            }

            m_InitialSpawnCompleted = true;
            m_SpawnTimer = m_SpawnInterval;
            return;
        }

        if (m_AliveEnemies.Count >= m_MaxAliveEnemies)
            return;

        m_SpawnTimer -= Time.deltaTime;
        if (m_SpawnTimer > 0f)
            return;

        TrySpawnEnemy();
        m_SpawnTimer = m_SpawnInterval;
    }

    private void OnDestroy()
    {
        foreach (Enemy enemy in m_AliveEnemies)
        {
            if (enemy != null)
                enemy.Destroyed -= HandleEnemyDestroyed;
        }
    }

    private void OnValidate()
    {
        m_MaxAliveEnemies = Mathf.Clamp(m_MaxAliveEnemies, 1, HardEnemyLimit);
        m_InitialEnemyCount = Mathf.Clamp(m_InitialEnemyCount, 1, m_MaxAliveEnemies);
        m_SpawnInterval = Mathf.Max(0.5f, m_SpawnInterval);
    }

    private bool TrySpawnEnemy()
    {
        if (m_AliveEnemies.Count >= m_MaxAliveEnemies || !TryGetSpawnPosition(out Vector3 spawnPosition))
            return false;

        Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        Enemy enemy = Instantiate(m_EnemyPrefab, spawnPosition, rotation, transform);
        enemy.name = $"Enemy_{m_AliveEnemies.Count + 1:00}";

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.Warp(spawnPosition))
        {
            Debug.LogWarning("[EnemySpawner] 生成点不在有效 NavMesh 上，本次生成已取消。", this);
            Destroy(enemy.gameObject);
            return false;
        }

        RegisterEnemy(enemy);
        return true;
    }

    private void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null || !m_AliveEnemies.Add(enemy))
            return;

        enemy.Destroyed += HandleEnemyDestroyed;
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy != null)
            enemy.Destroyed -= HandleEnemyDestroyed;

        m_AliveEnemies.Remove(enemy);
        m_SpawnTimer = m_SpawnInterval;
    }

    private void CacheNavMeshTriangles()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        m_NavMeshVertices = triangulation.vertices;
        m_NavMeshIndices = triangulation.indices;

        int triangleCount = m_NavMeshIndices.Length / 3;
        m_CumulativeTriangleAreas = new float[triangleCount];
        m_TotalNavMeshArea = 0f;

        // 按三角形面积加权，避免细碎的小三角形获得过高的生成概率。
        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            int index = triangle * 3;
            Vector3 a = m_NavMeshVertices[m_NavMeshIndices[index]];
            Vector3 b = m_NavMeshVertices[m_NavMeshIndices[index + 1]];
            Vector3 c = m_NavMeshVertices[m_NavMeshIndices[index + 2]];
            m_TotalNavMeshArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            m_CumulativeTriangleAreas[triangle] = m_TotalNavMeshArea;
        }

        if (m_TotalNavMeshArea <= 0f)
            Debug.LogError("[EnemySpawner] 场景没有可用的 NavMesh，请先烘焙导航网格。", this);
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPosition)
    {
        for (int attempt = 0; attempt < SpawnPointAttempts; attempt++)
        {
            Vector3 candidate = SampleRandomNavMeshPoint();
            if (!IsFarEnoughFromPlayer(candidate) || !IsFarEnoughFromEnemies(candidate))
                continue;

            if (!IsReachableFromPlayer(candidate))
                continue;

            spawnPosition = candidate;
            return true;
        }

        spawnPosition = default;
        Debug.LogWarning("[EnemySpawner] 连续多次未找到合适生成点，将在下个生成周期重试。", this);
        return false;
    }

    private Vector3 SampleRandomNavMeshPoint()
    {
        float areaSample = UnityEngine.Random.value * m_TotalNavMeshArea;
        int triangle = Array.BinarySearch(m_CumulativeTriangleAreas, areaSample);
        if (triangle < 0)
            triangle = ~triangle;
        triangle = Mathf.Clamp(triangle, 0, m_CumulativeTriangleAreas.Length - 1);

        int index = triangle * 3;
        Vector3 a = m_NavMeshVertices[m_NavMeshIndices[index]];
        Vector3 b = m_NavMeshVertices[m_NavMeshIndices[index + 1]];
        Vector3 c = m_NavMeshVertices[m_NavMeshIndices[index + 2]];

        float sqrtR1 = Mathf.Sqrt(UnityEngine.Random.value);
        float r2 = UnityEngine.Random.value;
        return (1f - sqrtR1) * a + sqrtR1 * (1f - r2) * b + sqrtR1 * r2 * c;
    }

    private bool IsFarEnoughFromPlayer(Vector3 candidate)
    {
        return m_Player == null || Vector3.Distance(candidate, m_Player.transform.position) >= m_MinDistanceFromPlayer;
    }

    private bool IsFarEnoughFromEnemies(Vector3 candidate)
    {
        foreach (Enemy enemy in m_AliveEnemies)
        {
            if (enemy != null && Vector3.Distance(candidate, enemy.transform.position) < m_MinEnemySpacing)
                return false;
        }

        return true;
    }

    private bool IsReachableFromPlayer(Vector3 candidate)
    {
        if (m_Player == null)
            return true;

        if (!NavMesh.SamplePosition(m_Player.transform.position, out NavMeshHit playerHit, 5f, NavMesh.AllAreas))
            return true;

        return NavMesh.CalculatePath(playerHit.position, candidate, NavMesh.AllAreas, m_Path)
            && m_Path.status == NavMeshPathStatus.PathComplete;
    }
}
