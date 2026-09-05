using System.Collections;
using UnityEngine;

public partial class Player
{
    private void HandlePlayerGetHit()
    {
        if (m_CurrentState == PlayerState.Dead)
            return;

        if (m_Health.GetCurrentHP() <= 0f)
        {
            StopHitReaction();
            m_CurrentState = PlayerState.Dead;
            m_Controller.enabled = false;
            ResetCombo();
            m_LockOnTarget = null;
            m_Animator.SetTrigger(OnDeadHash);

            // 死亡只发布结果；胜负界面、重试和返回菜单由 GameFlowController 统一负责。
            Died?.Invoke();
            return;
        }

        StartHitReaction();
    }

    private void StartHitReaction()
    {
        ResetCombo();
        m_Animator.CrossFade(LocomotionStateHash, 0.05f);

        if (m_HitReactionCoroutine != null)
            StopCoroutine(m_HitReactionCoroutine);

        m_HitReactionCoroutine = StartCoroutine(PlayHitReaction());
    }

    private IEnumerator PlayHitReaction()
    {
        m_IsHitReacting = true;
        float elapsed = 0f;

        while (elapsed < m_HitReactionDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / m_HitReactionDuration);
            float recoil = Mathf.Sin(normalizedTime * Mathf.PI);

            if (m_HitReactionRoot != null)
            {
                m_HitReactionRoot.localPosition = m_HitReactionBasePosition
                    + Vector3.back * (m_HitReactionDistance * recoil);
                m_HitReactionRoot.localRotation = m_HitReactionBaseRotation
                    * Quaternion.Euler(-m_HitReactionTilt * recoil, 0f, 0f);
            }

            yield return null;
        }

        RestoreHitReactionPose();
        m_IsHitReacting = false;
        m_HitReactionCoroutine = null;
    }

    private void StopHitReaction()
    {
        if (m_HitReactionCoroutine != null)
        {
            StopCoroutine(m_HitReactionCoroutine);
            m_HitReactionCoroutine = null;
        }

        RestoreHitReactionPose();
        m_IsHitReacting = false;
    }

    private void RestoreHitReactionPose()
    {
        if (m_HitReactionRoot == null)
            return;

        m_HitReactionRoot.localPosition = m_HitReactionBasePosition;
        m_HitReactionRoot.localRotation = m_HitReactionBaseRotation;
    }

    private Transform FindHitReactionRoot()
    {
        Renderer renderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null)
            renderer = GetComponentInChildren<MeshRenderer>(true);
        if (renderer == null)
            return null;

        Transform candidate = renderer.transform;
        while (candidate.parent != null && candidate.parent != transform)
            candidate = candidate.parent;

        return candidate.parent == transform ? candidate : null;
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
