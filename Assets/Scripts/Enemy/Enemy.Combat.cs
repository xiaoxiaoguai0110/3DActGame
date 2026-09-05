using UnityEngine;

public partial class Enemy
{
    private void EnterAttackState()
    {
        m_CurrentState = EnemyState.Attack;
        m_Agent.ResetPath();
        m_Agent.velocity = Vector3.zero;

        float[] attackValues = { 0f, 0.33f, 0.66f, 1f };
        m_AttackType = attackValues[Random.Range(0, attackValues.Length)];

        m_Animator.SetFloat(AttackIndexHash, m_AttackType);
        m_Animator.SetTrigger(OnAttackHash);
    }

    private void UpdateAttack()
    {
        if (m_Player == null)
        {
            m_CurrentState = EnemyState.Idle;
            m_StateTimer.Start(m_IdleDuration);
            return;
        }

        float distance = Vector3.Distance(transform.position, m_Player.position);
        if (distance > m_AttackRange)
        {
            m_CurrentState = EnemyState.Pursuit;
            return;
        }

        Vector3 lookDirection = m_Player.position - transform.position;
        lookDirection.y = 0f;
        if (lookDirection != Vector3.zero)
            transform.forward = lookDirection;
    }

    private void HandleGetHit()
    {
        if (m_CurrentState == EnemyState.Dead)
            return;

        SpawnBloodEffect();

        if (CameraController.Instance != null)
            CameraController.Instance.Shake(0.3f, 0.15f);

        if (m_Health.GetCurrentHP() <= 0f)
        {
            m_CurrentState = EnemyState.Dead;
            m_Agent.ResetPath();
            m_Agent.velocity = Vector3.zero;
            m_Agent.isStopped = true;
            m_Animator.SetTrigger(OnDeadHash);
            m_DeadTimer.Start(2.7f);
            Died?.Invoke(this);
            return;
        }

        m_CurrentState = EnemyState.GetHit;
        m_Agent.ResetPath();
        m_Agent.velocity = Vector3.zero;
        m_Animator.SetTrigger(OnGetHitHash);
        m_GetHitTimer.Start(1f);
    }

    private void SpawnBloodEffect()
    {
        if (bloodPrefab == null || bloodSpawn == null)
            return;

        GameObject blood = Instantiate(bloodPrefab, bloodSpawn.position, bloodSpawn.rotation);
        Destroy(blood, 1f);
    }

    private void UpdateDead()
    {
        m_DeadTimer.Tick(Time.deltaTime);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }

    private void UpdateGetHit()
    {
        m_GetHitTimer.Tick(Time.deltaTime);
    }

    private void OnGetHitEnd()
    {
        if (m_CurrentState != EnemyState.GetHit)
            return;

        m_CurrentState = EnemyState.Idle;
        m_StateTimer.Start(m_IdleDuration);
    }

    private void OnAttackHit()
    {
        // Animation Event 可能在状态切换后才到达；只有仍处于 Attack 才允许造成伤害。
        if (m_CurrentState != EnemyState.Attack)
            return;

        if (m_Player == null)
            return;

        float distance = Vector3.Distance(transform.position, m_Player.position);
        if (distance > m_AttackRange)
            return;

        Vector3 directionToPlayer = (m_Player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > 60f)
            return;

        Health playerHealth = m_Player.GetComponent<Health>();
        if (playerHealth != null)
            playerHealth.TakeDamage(m_AttackDamage);
    }

    private void OnAttackEnd()
    {
        // 旧攻击动画的结束事件不能把 GetHit 或 Dead 覆盖回 Pursuit。
        if (m_CurrentState != EnemyState.Attack)
            return;

        m_AttackCooldownTimer.Start(m_AttackCooldown);
        m_CurrentState = EnemyState.Pursuit;
    }
}
