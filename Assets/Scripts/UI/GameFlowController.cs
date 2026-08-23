using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗场景的胜负流程所有者：监听玩家死亡和有限敌人遭遇，统一更新目标与结算界面。
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public enum FlowState
    {
        Playing,
        Victory,
        Defeat
    }

    [Header("Gameplay")]
    [SerializeField] private Player m_Player;
    [SerializeField] private EnemySpawner m_EnemySpawner;
    [SerializeField] private string m_MenuSceneName = "0-GameMenu";
    [SerializeField, Min(0f)] private float m_ResultDelay = 1f;

    [Header("HUD")]
    [SerializeField] private TMP_Text m_ObjectiveText;
    [SerializeField] private GameObject m_ResultPanel;
    [SerializeField] private TMP_Text m_ResultTitle;
    [SerializeField] private TMP_Text m_ResultDescription;

    private Coroutine m_ResultCoroutine;
    private bool m_IsLoadingScene;

    public FlowState CurrentState { get; private set; } = FlowState.Playing;

    private void Start()
    {
        if (m_Player == null)
            m_Player = FindObjectOfType<Player>();
        if (m_EnemySpawner == null)
            m_EnemySpawner = FindObjectOfType<EnemySpawner>();

        if (m_Player != null)
            m_Player.Died += HandlePlayerDied;

        if (m_EnemySpawner != null)
        {
            m_EnemySpawner.ProgressChanged += HandleEncounterProgress;
            m_EnemySpawner.EncounterCompleted += HandleEncounterCompleted;
            HandleEncounterProgress(m_EnemySpawner.TotalDefeated, m_EnemySpawner.TotalEnemyCount);
        }

        if (m_ResultPanel != null)
            m_ResultPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (m_Player != null)
            m_Player.Died -= HandlePlayerDied;

        if (m_EnemySpawner != null)
        {
            m_EnemySpawner.ProgressChanged -= HandleEncounterProgress;
            m_EnemySpawner.EncounterCompleted -= HandleEncounterCompleted;
        }

        if (m_ResultCoroutine != null)
            StopCoroutine(m_ResultCoroutine);

        // 在编辑器停止 Play Mode 或异常卸载场景时也恢复全局时间，避免污染下一次运行。
        if (!Mathf.Approximately(Time.timeScale, 1f))
            Time.timeScale = 1f;
    }

    private void HandleEncounterProgress(int defeated, int total)
    {
        if (m_ObjectiveText != null)
            m_ObjectiveText.text = $"DEFEAT THE ENEMIES   {defeated} / {total}";
    }

    private void HandleEncounterCompleted()
    {
        BeginResult(FlowState.Victory);
    }

    private void HandlePlayerDied()
    {
        BeginResult(FlowState.Defeat);
    }

    private void BeginResult(FlowState result)
    {
        if (CurrentState != FlowState.Playing)
            return;

        CurrentState = result;
        MainMenuUI.IsInputEnabled = false;
        if (m_EnemySpawner != null)
            m_EnemySpawner.enabled = false;

        if (m_ResultCoroutine != null)
            StopCoroutine(m_ResultCoroutine);
        m_ResultCoroutine = StartCoroutine(ShowResultAfterDelay(result));
    }

    private IEnumerator ShowResultAfterDelay(FlowState result)
    {
        // 结算前保留短暂实时播放，让最后一击或死亡动画有完整的视觉收尾。
        yield return new WaitForSecondsRealtime(m_ResultDelay);

        if (m_ResultTitle != null)
            m_ResultTitle.text = result == FlowState.Victory ? "ENCOUNTER CLEARED" : "YOU HAVE FALLEN";
        if (m_ResultDescription != null)
        {
            m_ResultDescription.text = result == FlowState.Victory
                ? "The threat has been driven from these ruins."
                : "Rise again and return to the fight.";
        }

        if (m_ResultPanel != null)
            m_ResultPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        m_ResultCoroutine = null;
    }

    public void OnRetry()
    {
        LoadSceneOnce(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnReturnToMenu()
    {
        if (m_IsLoadingScene)
            return;

        m_IsLoadingScene = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(m_MenuSceneName, LoadSceneMode.Single);
    }

    private void LoadSceneOnce(int buildIndex)
    {
        if (m_IsLoadingScene)
            return;

        // UI 连点或同帧重复事件只能发起一次加载，保护跨场景单例的生命周期。
        m_IsLoadingScene = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
    }
}
