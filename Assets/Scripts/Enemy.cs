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
    [SerializeField] private GameObject playerObj;

    private Animator m_Animator;
    private NavMeshAgent m_Agent;
    private Transform m_Player;
    private Health m_Health;

    private float m_StateTimer;
    private float m_AttackType;
    private float m_AttackCooldownTimer;

    private void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_Agent = GetComponent<NavMeshAgent>();
        m_Agent.stoppingDistance = m_AttackRange;
        m_Health = GetComponent<Health>();

        if (playerObj != null)
            m_Player = playerObj.transform;

        m_StateTimer = m_IdleDuration;

        if (m_Health != null)
            m_Health.OnHealthChanged += HandleGetHit;
    }

    private void OnDestroy()
    {
        if (m_Health != null)
            m_Health.OnHealthChanged -= HandleGetHit;
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
            case EnemyState.GetHit:
                m_Animator.SetFloat("MoveSpeed", 0f);
                UpdateGetHit();
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

        // 面朝玩家
        Vector3 lookDirection = m_Player.position - transform.position;
        lookDirection.y = 0f;
        if (lookDirection != Vector3.zero)
            transform.forward = lookDirection;
    }

    private float m_GetHitTimer;

    /// <summary>
    /// 由 Health.OnHealthChanged 触发，进入受击状态。
    /// </summary>
    private void HandleGetHit()
    {
        if (m_CurrentState == EnemyState.Dead) return;

        m_CurrentState = EnemyState.GetHit;
        m_Agent.ResetPath();
        m_Agent.velocity = Vector3.zero;
        m_Animator.SetTrigger("OnGetHit");

        // 设置超时保护，受击动画一般约 0.5~1s
        m_GetHitTimer = 0.8f;
    }

    /// <summary>
    /// 受击状态：等待动画播完自动回到 Idle。
    /// </summary>
    private void UpdateGetHit()
    {
        m_GetHitTimer -= Time.deltaTime;
        if (m_GetHitTimer <= 0f)
        {
            OnGetHitEnd();
        }
    }

    /// <summary>
    /// 由 Animation Event 在受击动画结束时调用。
    /// </summary>
    private void OnGetHitEnd()
    {
        m_CurrentState = EnemyState.Idle;
        m_StateTimer = m_IdleDuration;
    }

    /// <summary>
    /// 由 Animation Event 在攻击动画的关键帧调用。
    /// 玩家在攻击范围内则造成伤害，不在则跳过。
    /// </summary>
    private void OnAttackHit()
    {
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
            playerHealth.TakeDamage(m_AttackDamage);
    }

    /// <summary>
    /// 由 Animation Event 在攻击动画结束时调用。
    /// 切回追击状态并进入攻击冷却。
    /// </summary>
    private void OnAttackEnd()
    {
        m_AttackCooldownTimer = m_AttackCooldown;
        m_CurrentState = EnemyState.Pursuit;
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
