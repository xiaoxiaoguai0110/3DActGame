using UnityEngine;
using UnityEngine.SceneManagement;

public partial class Player
{
    private void HandlePlayerGetHit()
    {
        if (m_CurrentState == PlayerState.Dead)
            return;

        if (m_Health.GetCurrentHP() <= 0f)
        {
            m_CurrentState = PlayerState.Dead;
            m_Controller.enabled = false;
            ResetCombo();
            DisableDamage();
            m_LockOnTarget = null;
            m_Animator.SetTrigger("OnDead");
            m_DeadTimer.Start(3f);
            return;
        }

        m_Animator.SetTrigger("OnGetHit");
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PlayIntroAnimation()
    {
        if (m_Animator == null)
        {
            Debug.LogWarning("[Player] 缺少 Animator，无法播放入场动画。", this);
            return;
        }

        m_Animator.SetTrigger("OnIntro");
    }
}
