using System;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Patrol,
    Attack,
    Dead,
    Pursuit,
    GetHit
}

/// <summary>
/// 敌人状态机协调器。
/// 移动/巡逻逻辑位于 Enemy.Movement，战斗/受击逻辑位于 Enemy.Combat。
/// </summary>
public partial class Enemy : MonoBehaviour
{
    public event Action<Enemy> Destroyed;

    public EnemyState m_CurrentState = EnemyState.Idle;

    [Header("Movement")]
    [SerializeField] private float m_PatrolSpeed = 2f;
    [SerializeField] private float m_PatrolRadius = 10f;
    [SerializeField] private float m_IdleDuration = 3f;
    [SerializeField] private float m_DetectionRange = 10f;
    [SerializeField] private float m_AbandonRange = 20f;
    [SerializeField] private float m_PursuitSpeed = 5f;

    [Header("Combat")]
    [SerializeField] private float m_AttackRange = 3.5f;
    [SerializeField] private float m_AttackDamage = 20f;
    [SerializeField] private float m_AttackCooldown = 3f;

    [Header("References")]
    [SerializeField] private GameObject playerObj;
    [SerializeField] private GameObject bloodPrefab;
    [SerializeField] private Transform bloodSpawn;

    private Animator m_Animator;
    private NavMeshAgent m_Agent;
    private Transform m_Player;
    private Health m_Health;
    private float m_AttackType;

    private readonly CountdownTimer m_StateTimer = new();
    private readonly CountdownTimer m_AttackCooldownTimer = new();
    private readonly CountdownTimer m_GetHitTimer = new();
    private readonly CountdownTimer m_DeadTimer = new();

    private void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_Agent = GetComponent<NavMeshAgent>();
        m_Agent.stoppingDistance = m_AttackRange;
        m_Health = GetComponent<Health>();

        if (playerObj != null)
            m_Player = playerObj.transform;
        else
            m_Player = FindObjectOfType<Player>()?.transform;

        if (bloodSpawn == null)
            bloodSpawn = FindChildByName("CHIMERA_ Spine");

        m_StateTimer.OnTimerEnd += OnIdleTimerEnd;
        m_GetHitTimer.OnTimerEnd += OnGetHitEnd;
        m_DeadTimer.OnTimerEnd += DestroySelf;
        m_StateTimer.Start(m_IdleDuration);

        if (m_Health != null)
            m_Health.OnHealthChanged += HandleGetHit;
    }

    private void OnDestroy()
    {
        if (m_Health != null)
            m_Health.OnHealthChanged -= HandleGetHit;

        Destroyed?.Invoke(this);
        Destroyed = null;
    }

    private void Update()
    {
        switch (m_CurrentState)
        {
            case EnemyState.Idle:
                SetMovementState(m_PatrolSpeed, 0f);
                CheckDetection();
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                SetMovementState(m_PatrolSpeed, 0.5f);
                CheckDetection();
                UpdatePatrol();
                break;
            case EnemyState.Pursuit:
                SetMovementState(m_PursuitSpeed, 1f);
                UpdatePursuit();
                break;
            case EnemyState.Attack:
                m_Animator.SetFloat("MoveSpeed", 0f);
                UpdateAttack();
                break;
            case EnemyState.GetHit:
                m_Animator.SetFloat("MoveSpeed", 0f);
                UpdateGetHit();
                break;
            case EnemyState.Dead:
                UpdateDead();
                break;
        }
    }

    private void SetMovementState(float speed, float normalizedSpeed)
    {
        m_Agent.speed = speed;
        m_Animator.SetFloat("MoveSpeed", normalizedSpeed);
    }

    private void CheckDetection()
    {
        if (m_Player == null)
            return;

        float distance = Vector3.Distance(transform.position, m_Player.position);
        if (distance <= m_DetectionRange)
            m_CurrentState = EnemyState.Pursuit;
    }

    private Transform FindChildByName(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
