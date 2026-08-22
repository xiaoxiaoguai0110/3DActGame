using UnityEngine;
using UnityEngine.AI;

public partial class Enemy
{
    private void UpdateIdle()
    {
        m_StateTimer.Tick(Time.deltaTime);
    }

    private void OnIdleTimerEnd()
    {
        if (m_CurrentState != EnemyState.Idle)
            return;

        PickRandomPatrolPoint();
        m_CurrentState = EnemyState.Patrol;
    }

    private void UpdatePatrol()
    {
        if (m_Agent.pathPending)
            return;

        if (m_Agent.remainingDistance <= m_Agent.stoppingDistance)
        {
            m_CurrentState = EnemyState.Idle;
            m_StateTimer.Start(m_IdleDuration);
        }
    }

    private void UpdatePursuit()
    {
        if (m_Player == null)
        {
            m_CurrentState = EnemyState.Idle;
            m_StateTimer.Start(m_IdleDuration);
            return;
        }

        if (m_AttackCooldownTimer.IsRunning)
        {
            m_AttackCooldownTimer.Tick(Time.deltaTime);
            m_Agent.SetDestination(m_Player.position);
            return;
        }

        float distance = Vector3.Distance(transform.position, m_Player.position);

        if (distance > m_AbandonRange)
        {
            m_CurrentState = EnemyState.Patrol;
            m_StateTimer.Start(m_IdleDuration);
            return;
        }

        if (distance <= m_AttackRange)
        {
            EnterAttackState();
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

