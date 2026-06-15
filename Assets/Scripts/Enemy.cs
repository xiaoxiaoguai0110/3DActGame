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

public class Enemy : MonoBehaviour
{
    public EnemyState m_CurrentState = EnemyState.Idle;

    [SerializeField] private float m_PatrolSpeed = 2f;
    [SerializeField] private float m_PatrolRadius = 10f;
    [SerializeField] private float m_IdleDuration = 3f;
    [SerializeField] private float m_DetectionRange = 10f;
    [SerializeField] private float m_AbandonRange = 20f;
    [SerializeField] private float m_PursuitSpeed = 5f;
    [SerializeField] private float m_AttackRange = 3.5f;
    [SerializeField] private float m_AttackDamage = 20f;
    [SerializeField] private float m_AttackCooldown = 3f;

    private Animator m_Animator;
    private NavMeshAgent m_Agent;
    private Transform m_Player;

    private float m_StateTimer;
    private float m_AttackType;
    private bool m_HasDealtDamage;
    private float m_AttackCooldownTimer;

    private void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_Agent = GetComponent<NavMeshAgent>();
        m_Agent.stoppingDistance = m_AttackRange;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            m_Player = playerObj.transform;

        m_StateTimer = m_IdleDuration;
    }

    private void Update()
    {
        switch (m_CurrentState)
        {
            case EnemyState.Idle:
                m_Agent.speed = m_PatrolSpeed;
                m_Animator.SetFloat("MoveSpeed", 0f);
                CheckDetection();
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                m_Agent.speed = m_PatrolSpeed;
                m_Animator.SetFloat("MoveSpeed", 0.5f);
                CheckDetection();
                UpdatePatrol();
                break;
            case EnemyState.Pursuit:
                m_Agent.speed = m_PursuitSpeed;
                m_Animator.SetFloat("MoveSpeed", 1f);
                UpdatePursuit();
                break;
            case EnemyState.Attack:
                m_Animator.SetFloat("MoveSpeed", 0f);
                UpdateAttack();
                break;
        }
    }

    private void CheckDetection()
    {
        if (m_Player == null) return;

        float distance = Vector3.Distance(transform.position, m_Player.position);
        if (distance <= m_DetectionRange)
        {
            m_CurrentState = EnemyState.Pursuit;
        }
    }

    private void UpdateIdle()
    {
        m_StateTimer -= Time.deltaTime;
        if (m_StateTimer <= 0f)
        {
            PickRandomPatrolPoint();
            m_CurrentState = EnemyState.Patrol;
        }
    }

    private void UpdatePatrol()
    {
        if (m_Agent.pathPending) return;

        if (m_Agent.remainingDistance <= m_Agent.stoppingDistance)
        {
            m_CurrentState = EnemyState.Idle;
            m_StateTimer = m_IdleDuration;
        }
    }

    private void UpdatePursuit()
    {
        if (m_Player == null)
        {
            m_CurrentState = EnemyState.Idle;
            m_StateTimer = m_IdleDuration;
            return;
        }

        // 攻击冷却中，倒计时
        if (m_AttackCooldownTimer > 0f)
        {
            m_AttackCooldownTimer -= Time.deltaTime;
            m_Agent.SetDestination(m_Player.position);
            return;
        }

        float distance = Vector3.Distance(transform.position, m_Player.position);

        if (distance > m_AbandonRange)
        {
            m_CurrentState = EnemyState.Patrol;
            m_StateTimer = m_IdleDuration;
            return;
        }

        if (distance <= m_AttackRange)
        {
            EnterAttackState();
            return;
        }

        m_Agent.SetDestination(m_Player.position);
    }

    private void EnterAttackState()
    {
        m_CurrentState = EnemyState.Attack;
        m_Agent.ResetPath();
        m_Agent.velocity = Vector3.zero;

        m_HasDealtDamage = false;

        float[] attackValues = { 0f, 0.33f, 0.66f, 1f };
        m_AttackType = attackValues[Random.Range(0, attackValues.Length)];

        m_Animator.SetFloat("AttackIndex", m_AttackType);
        m_Animator.SetTrigger("OnAttack");
    }

    private void UpdateAttack()
    {
        if (m_Player == null)
        {
            m_CurrentState = EnemyState.Idle;
            m_StateTimer = m_IdleDuration;
            return;
        }

        float distance = Vector3.Distance(transform.position, m_Player.position);

        if (distance > m_AttackRange)
        {
            m_CurrentState = EnemyState.Pursuit;
            return;
        }

        // 已经造成过伤害，切回 Pursuit 并进入冷却
        if (m_HasDealtDamage)
        {
            m_AttackCooldownTimer = m_AttackCooldown;
            m_CurrentState = EnemyState.Pursuit;
            return;
        }

        // 面朝玩家
        Vector3 lookDirection = m_Player.position - transform.position;
        lookDirection.y = 0f;
        if (lookDirection != Vector3.zero)
            transform.forward = lookDirection;
    }

    /// <summary>
    /// 由 Animation Event 在攻击动画的关键帧调用。
    /// 检测玩家是否在攻击范围（距离 + 前方扇区）内，若是则造成伤害。
    /// </summary>
    private void OnAttackHit()
    {
        // 防止同一段攻击动画中重复造成伤害
        if (m_HasDealtDamage) return;
        if (m_Player == null) return;

        // 距离检测
        float distance = Vector3.Distance(transform.position, m_Player.position);
        if (distance > m_AttackRange) return;

        // 角度检测 —— 玩家必须在怪物前方 ±60°（共 120° 扇形）
        Vector3 directionToPlayer = (m_Player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > 60f) return;

        Health playerHealth = m_Player.GetComponent<Health>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(m_AttackDamage);
            m_HasDealtDamage = true;
        }
    }

    private void PickRandomPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * m_PatrolRadius;
            randomOffset.y = 0f;
            Vector3 randomPoint = transform.position + randomOffset;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, m_PatrolRadius, NavMesh.AllAreas))
            {
                m_Agent.SetDestination(hit.position);
                return;
            }
        }
    }
}
