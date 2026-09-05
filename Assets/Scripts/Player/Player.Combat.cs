using System.Collections;
using UnityEngine;

public partial class Player
{
    private const float HitEffectLifetime = 2f;

    private void ResetCombo()
    {
        m_ComboStage = 0;
        m_PreparedComboStage = 0;
        m_HasEnteredAttackAnimation = false;
        m_IsComboInputWindowOpen = false;
        m_ActiveAttack = null;
        m_WeaponDamage?.DisableDamage();
        m_Animator.SetInteger(ComboStageHash, 0);
        m_Animator.ResetTrigger(OnAttackHash);
    }

    private bool IsInAttackAnimation()
    {
        AnimatorStateInfo currentState = m_Animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.tagHash == AttackTagHash)
            return true;

        return m_Animator.IsInTransition(0)
            && m_Animator.GetNextAnimatorStateInfo(0).tagHash == AttackTagHash;
    }

    private int GetMaxComboStage()
    {
        return m_AttackConfig != null ? m_AttackConfig.MaxComboStage : 0;
    }

    private bool TryGetAttack(int stage, out PlayerAttackConfigSO.AttackDefinition attack)
    {
        if (m_AttackConfig != null && m_AttackConfig.TryGetAttack(stage, out attack))
            return true;

        attack = null;
        Debug.LogWarning($"[Player] 攻击配置缺少第 {stage} 段，已忽略本次动画事件。", this);
        return false;
    }

    private void HandleAttack()
    {
        if (!MainMenuUI.IsInputEnabled || m_CurrentState == PlayerState.Dead)
            return;

        if (m_ComboStage == 0)
        {
            StartCombo(1);
            return;
        }

        // 下一段输入只在动画事件显式开启的窗口内生效，连点不会提前跳段。
        if (!m_IsComboInputWindowOpen || !IsInAttackAnimation() || m_PreparedComboStage > 0)
            return;

        if (m_ComboStage >= GetMaxComboStage())
            return;

        PrepareComboTransition(m_ComboStage + 1);
    }

    private void StartCombo(int stage)
    {
        if (!TryGetAttack(stage, out _))
            return;

        m_ComboStage = stage;
        m_PreparedComboStage = 0;
        m_HasEnteredAttackAnimation = false;
        m_IsComboInputWindowOpen = false;
        SetComboAnimatorConditions(stage);
    }

    private void PrepareComboTransition(int stage)
    {
        if (!TryGetAttack(stage, out _))
            return;

        m_PreparedComboStage = stage;
        m_IsComboInputWindowOpen = false;
        SetComboAnimatorConditions(stage);
    }

    private void SetComboAnimatorConditions(int stage)
    {
        m_Animator.SetInteger(ComboStageHash, stage);
        m_Animator.ResetTrigger(OnAttackHash);
        m_Animator.SetTrigger(OnAttackHash);
    }

    // 由每段攻击动画开头调用；阶段提交和音效因此与真正播放到的动画一致。
    public void BeginAttackStage(int stage)
    {
        if (m_CurrentState == PlayerState.Dead || !IsInAttackAnimation() || !TryGetAttack(stage, out m_ActiveAttack))
            return;

        m_ComboStage = stage;
        if (m_PreparedComboStage == stage)
            m_PreparedComboStage = 0;

        m_HasEnteredAttackAnimation = true;
        m_IsComboInputWindowOpen = false;
        AudioManager.Instance?.PlayAttackSound(m_ActiveAttack.AudioClip);
    }

    // Animation Event：武器 Collider 只在有效帧之间开启。
    public void EnableWeaponDamage()
    {
        if (m_CurrentState == PlayerState.Dead || !IsInAttackAnimation())
            return;

        if ((m_ActiveAttack == null || m_ActiveAttack.ComboStage != m_ComboStage)
            && !TryGetAttack(m_ComboStage, out m_ActiveAttack))
            return;

        m_WeaponDamage?.EnableDamage(m_ActiveAttack.Damage);
    }

    // 关闭事件即使在状态切换边缘到达也要执行，避免武器判定残留。
    public void DisableWeaponDamage()
    {
        m_WeaponDamage?.DisableDamage();
    }

    // Animation Event：这段时间内按攻击键才会缓存下一段连招。
    public void OpenComboInputWindow()
    {
        if (m_CurrentState != PlayerState.Dead
            && IsInAttackAnimation()
            && m_ComboStage < GetMaxComboStage())
        {
            m_IsComboInputWindowOpen = true;
        }
    }

    public void CloseComboInputWindow()
    {
        m_IsComboInputWindowOpen = false;
    }

    private void HandleWeaponHit(Vector3 hitPoint)
    {
        if (m_ActiveAttack == null)
            return;

        if (m_ActiveAttack.HitEffectPrefab != null)
        {
            GameObject effect = Object.Instantiate(m_ActiveAttack.HitEffectPrefab, hitPoint, Quaternion.identity);
            Object.Destroy(effect, HitEffectLifetime);
        }

        if (m_ActiveAttack.CameraShakeIntensity > 0f && CameraController.Instance != null)
            CameraController.Instance.Shake(m_ActiveAttack.CameraShakeIntensity, 0.15f);

        if (m_ActiveAttack.HitStopDuration > 0f)
            StartHitStop(m_ActiveAttack.HitStopDuration);
    }

    private void StartHitStop(float duration)
    {
        if (!m_OwnsHitStop)
        {
            m_TimeScaleBeforeHitStop = Time.timeScale;
            m_OwnsHitStop = true;
            Time.timeScale = 0f;
        }

        if (m_HitStopCoroutine != null)
            StopCoroutine(m_HitStopCoroutine);

        m_HitStopCoroutine = StartCoroutine(WaitForHitStop(duration));
    }

    private IEnumerator WaitForHitStop(float duration)
    {
        // WaitForSecondsRealtime 不受 timeScale 影响，暂停世界后仍能按时恢复。
        yield return new WaitForSecondsRealtime(duration);
        m_HitStopCoroutine = null;
        RestoreTimeScaleAfterHitStop();
    }

    private void RestoreTimeScaleAfterHitStop()
    {
        if (m_HitStopCoroutine != null)
        {
            StopCoroutine(m_HitStopCoroutine);
            m_HitStopCoroutine = null;
        }

        if (m_OwnsHitStop && Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = m_TimeScaleBeforeHitStop;

        m_OwnsHitStop = false;
    }
}
