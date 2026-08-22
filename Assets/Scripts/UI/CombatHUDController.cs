using UnityEngine;

/// <summary>
/// 战斗场景加载后统一启用新版提示层和原版血量 HUD。
/// 血量显示继续由 HealthyUI、EnemyHealthUI 负责，避免两套脚本重复订阅生命事件。
/// </summary>
public class CombatHUDController : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private GameObject m_HudRoot;
    [SerializeField] private GameObject m_LegacyHudRoot;

    private void Awake()
    {
        // 兼容已经由旧版本生成过的场景：即使 Inspector 还没写入新引用，
        // 也能在当前已加载场景中找回被禁用的原版 Canvas。
        if (m_LegacyHudRoot == null)
            m_LegacyHudRoot = FindLegacyHudRoot();

        DisableReplacedHealthPanel("PlayerStatus");
        DisableReplacedHealthPanel("EnemyStatus");
    }

    private void Start()
    {
        SetActiveIfChanged(m_HudRoot, true);
        SetActiveIfChanged(m_LegacyHudRoot, true);
    }

    private static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private void DisableReplacedHealthPanel(string panelName)
    {
        Transform panel = m_HudRoot != null ? m_HudRoot.transform.Find(panelName) : null;
        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    private GameObject FindLegacyHudRoot()
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.scene != gameObject.scene)
                continue;

            if (candidate.name == "LegacyCanvasHUD_Disabled" || candidate.name == "LegacyCanvasHUD" || candidate.name == "CanvasHUD")
                return candidate;
        }

        Debug.LogWarning("[CombatHUDController] 未找到原版 CanvasHUD，血量 UI 不会显示。", this);
        return null;
    }
}
