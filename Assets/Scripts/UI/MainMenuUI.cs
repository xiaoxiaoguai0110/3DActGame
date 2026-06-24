using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    /// <summary>
    /// 游戏是否已开始。为 false 时所有输入（移动/攻击/视角）被禁止。
    /// </summary>
    public static bool IsInputEnabled { get; internal set; }

    [SerializeField] private GameObject m_MenuRoot;
    [SerializeField] private Player m_PlayerIntro;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 冻结摄像机在初始位置
        if (CameraController.Instance != null)
            CameraController.Instance.FreezeAtCurrentPosition();

        ShowMenu();
    }

    /// <summary>
    /// 显示主菜单，冻结游戏。
    /// </summary>
    public void ShowMenu()
    {
        if (m_MenuRoot != null)
            m_MenuRoot.SetActive(true);

        IsInputEnabled = false;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;

        if (CameraController.Instance != null)
            CameraController.Instance.FreezeAtCurrentPosition();
    }

    /// <summary>
    /// 按钮事件：开始游戏。
    /// 先播玩家入场动画，一帧后摄像机再开始移动到角色身后。
    /// </summary>
    public void OnStartGame()
    {
        if (m_MenuRoot != null)
            m_MenuRoot.SetActive(false);

        // 恢复时间，让动画能够播放
        Time.timeScale = 1f;

        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {
        // 先播放玩家入场动画
        if (m_PlayerIntro != null)
            m_PlayerIntro.PlayIntroAnimation();

        // 等当前帧结束，确保 Animator 已处理触发器的状态更新
        yield return new WaitForEndOfFrame();
        // 再等一帧，让动画过渡真正开始播放
        yield return null;

        // 摄像机再开始移动
        if (CameraController.Instance != null)
            CameraController.Instance.BeginGameTransition();
        // 过渡完成后 CameraController 自动设置 IsInputEnabled = true
    }

    /// <summary>
    /// 按钮事件：退出游戏。
    /// </summary>
    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
