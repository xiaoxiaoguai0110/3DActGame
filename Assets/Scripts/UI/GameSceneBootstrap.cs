using System.Collections;
using UnityEngine;

/// <summary>
/// 仅存在于 1-GameScene，接管原来由主菜单负责的玩家入场和输入启用流程。
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    [SerializeField] private Player m_PlayerIntro;

    private void Awake()
    {
        // 场景切换前可能曾暂停游戏；战斗场景必须主动恢复全局时间状态。
        Time.timeScale = 1f;
        MainMenuUI.IsInputEnabled = false;
        Cursor.visible = false;
    }

    private IEnumerator Start()
    {
        if (m_PlayerIntro == null)
            m_PlayerIntro = FindObjectOfType<Player>();

        if (CameraController.Instance != null)
            CameraController.Instance.FreezeAtCurrentPosition();

        if (m_PlayerIntro != null)
            m_PlayerIntro.PlayIntroAnimation();

        // Animator 要经过帧末和下一帧才会真正进入过渡状态，随后再移动摄像机。
        yield return new WaitForEndOfFrame();
        yield return null;

        if (CameraController.Instance != null)
        {
            CameraController.Instance.BeginGameTransition();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            MainMenuUI.IsInputEnabled = true;
        }
    }
}
