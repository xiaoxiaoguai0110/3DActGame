using UnityEngine;

public partial class Player
{
    private void ResetCombo()
    {
        m_ComboStage = 0;
        m_PreparedComboStage = 0;
        m_HasEnteredComboAnimation = false;
        m_QueuedAttackAfterPrepared = false;
        m_Animator.SetInteger("ComboStage", 0);
        m_Animator.ResetTrigger("OnAttack");
    }

    private bool IsInComboAnimation()
    {
        AnimatorStateInfo currentState = m_Animator.GetCurrentAnimatorStateInfo(0);
        if (IsComboState(currentState))
            return true;

        if (!m_Animator.IsInTransition(0))
            return false;

        return IsComboState(m_Animator.GetNextAnimatorStateInfo(0));
    }

    private bool IsComboState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("combo_04_1 0")
            || stateInfo.IsName("combo_04_2 0")
            || stateInfo.IsName("combo_04_3 0")
            || stateInfo.IsName("combo_04_4 0")
            || stateInfo.IsName("combo_04_5 0");
    }

    private bool IsCurrentComboStage(int stage)
    {
        return GetComboStage(m_Animator.GetCurrentAnimatorStateInfo(0)) == stage;
    }

    private int GetComboStage(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("combo_04_1 0")) return 1;
        if (stateInfo.IsName("combo_04_2 0")) return 2;
        if (stateInfo.IsName("combo_04_3 0")) return 3;
        if (stateInfo.IsName("combo_04_4 0")) return 4;
        if (stateInfo.IsName("combo_04_5 0")) return 5;
        return 0;
    }

    private bool IsInLocomotionAnimation()
    {
        return !m_Animator.IsInTransition(0)
            && m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Blend Tree");
    }

    public float GetCurrentAttackDamage()
    {
        if (m_ComboDamages == null || m_ComboDamages.Length == 0)
            return 0f;

        int damageStage = m_PreparedComboStage > 0 ? m_PreparedComboStage : m_ComboStage;
        int index = Mathf.Clamp(damageStage - 1, 0, m_ComboDamages.Length - 1);
        return m_ComboDamages[index];
    }

    private void DisableDamage()
    {
        if (m_WeaponDamage != null)
            m_WeaponDamage.DisableDamage();
    }

    private void HandleAttack()
    {
        if (!MainMenuUI.IsInputEnabled)
            return;

        if (m_ComboStage == 0)
        {
            StartCombo(1);
            return;
        }

        if (!IsInComboAnimation())
            return;

        if (m_PreparedComboStage > 0)
        {
            m_QueuedAttackAfterPrepared = true;
            m_ComboTimer.Start(m_ComboWindowDuration);
            return;
        }

        if (m_ComboStage >= m_ComboMaxStage)
            return;

        PrepareComboTransition(m_ComboStage + 1);
    }

    private void StartCombo(int stage)
    {
        m_ComboStage = stage;
        m_PreparedComboStage = 0;
        m_QueuedAttackAfterPrepared = false;

        SetComboAnimatorConditions(stage);
        PlayComboEffects();
        m_ComboTimer.Start(m_ComboWindowDuration);
    }

    private void PrepareComboTransition(int stage)
    {
        m_PreparedComboStage = stage;
        SetComboAnimatorConditions(stage);
        m_ComboTimer.Start(m_ComboWindowDuration);
    }

    private void CommitPreparedComboStage()
    {
        m_ComboStage = m_PreparedComboStage;
        m_PreparedComboStage = 0;

        PlayComboEffects();
        m_ComboTimer.Start(m_ComboWindowDuration);

        if (m_QueuedAttackAfterPrepared && m_ComboStage < m_ComboMaxStage)
        {
            m_QueuedAttackAfterPrepared = false;
            PrepareComboTransition(m_ComboStage + 1);
            return;
        }

        m_QueuedAttackAfterPrepared = false;
    }

    private void SetComboAnimatorConditions(int stage)
    {
        m_Animator.SetInteger("ComboStage", stage);
        m_Animator.ResetTrigger("OnAttack");
        m_Animator.SetTrigger("OnAttack");
    }

    private void PlayComboEffects()
    {
        AudioManager.Instance?.PlayAttackSound();

        if (m_WeaponDamage != null)
            m_WeaponDamage.EnableDamage();

        CancelInvoke(nameof(DisableDamage));
        Invoke(nameof(DisableDamage), 0.5f);
    }

    private void OnComboTimerEnd()
    {
        if (m_ComboStage > 0 && m_ComboStage < m_ComboMaxStage)
            ResetCombo();
    }
}

