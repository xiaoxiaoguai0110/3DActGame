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

    private Animator m_Animator;
    private NavMeshAgent m_Agent;
    private Transform m_Player;

    private float m_StateTimer;

    private void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_Agent = GetComponent<NavMeshAgent>();
        m_Agent.stoppingDistance = 0.5f;

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
                m_Animator.SetFloat("Speed", 0f);
                CheckDetection();
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                m_Agent.speed = m_PatrolSpeed;
                m_Animator.SetFloat("Speed", 0.5f);
                CheckDetection();
                UpdatePatrol();
                break;
            case EnemyState.Pursuit:
                m_Agent.speed = m_PursuitSpeed;
                m_Animator.SetFloat("Speed", 1f);
                UpdatePursuit();
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

        float distance = Vector3.Distance(transform.position, m_Player.position);
        if (distance > m_AbandonRange)
        {
            m_CurrentState = EnemyState.Patrol;
            m_StateTimer = m_IdleDuration;
            return;
        }

        m_Agent.SetDestination(m_Player.position);
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
