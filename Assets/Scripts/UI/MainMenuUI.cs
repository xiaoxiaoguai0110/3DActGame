using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 仅存在于 0-GameMenu，负责菜单按钮和场景跳转。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    /// <summary>
    /// 战斗场景通过 GameSceneBootstrap 开启输入；菜单场景始终保持为 false。
    /// </summary>
    public static bool IsInputEnabled { get; internal set; }

    [SerializeField] private GameObject m_MenuRoot;
    [SerializeField] private string m_GameSceneName = "1-GameScene";

    private bool m_IsLoading;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        IsInputEnabled = false;
    }

    private void Start()
    {
        if (m_MenuRoot != null)
            m_MenuRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 按钮事件：从独立菜单场景进入战斗场景。
    /// </summary>
    public void OnStartGame()
    {
        if (m_IsLoading)
            return;

        m_IsLoading = true;
        SceneManager.LoadScene(m_GameSceneName, LoadSceneMode.Single);
    }

    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
