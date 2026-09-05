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
            m_LockOnTarget = null;
            m_Animator.SetTrigger(OnDeadHash);
            m_DeadTimer.Start(3f);
            return;
        }

        m_Animator.SetTrigger(OnGetHitHash);
    }

    private void ReloadScene()
    {
        if (m_IsReloadingScene)
            return;

        // 场景加载是一次性操作；防止重复回调在同一帧再次发起加载。
        m_IsReloadingScene = true;
        MainMenuUI.IsInputEnabled = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PlayIntroAnimation()
    {
        if (m_Animator == null)
        {
            Debug.LogWarning("[Player] 缺少 Animator，无法播放入场动画。", this);
            return;
        }

        m_Animator.SetTrigger(OnIntroHash);
    }
}
