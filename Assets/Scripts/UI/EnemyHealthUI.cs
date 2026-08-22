using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 使用一条原版血条显示玩家附近最近的存活敌人，支持场上同时存在多只敌人。
/// </summary>
public class EnemyHealthUI : MonoBehaviour
{
    [SerializeField] private Slider m_Slider;
    [SerializeField] private float m_DisplayRange = 20f;
    [SerializeField, Min(0.05f)] private float m_TargetRefreshInterval = 0.2f;

    private Player m_Player;
    private Enemy m_Target;
    private Health m_TargetHealth;
    private float m_NextTargetRefreshTime;

    private void OnEnable()
    {
        m_NextTargetRefreshTime = 0f;
        SetSliderVisible(false);
    }

    private void OnDisable()
    {
        UnbindTarget();
        SetSliderVisible(false);
    }

    private void Update()
    {
        if (m_Player == null)
            m_Player = FindObjectOfType<Player>();

        if (m_Player == null || Time.time < m_NextTargetRefreshTime)
            return;

        m_NextTargetRefreshTime = Time.time + m_TargetRefreshInterval;
        BindTarget(FindNearestEnemy());
    }

    private Enemy FindNearestEnemy()
    {
        Enemy nearest = null;
        float nearestDistance = m_DisplayRange;

        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
        {
            Health health = enemy.GetComponent<Health>();
            if (health == null || health.GetCurrentHP() <= 0f)
                continue;

            float distance = Vector3.Distance(m_Player.transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void BindTarget(Enemy target)
    {
        if (target == m_Target)
        {
            SetSliderVisible(target != null);
            return;
        }

        UnbindTarget();
        m_Target = target;
        m_TargetHealth = target != null ? target.GetComponent<Health>() : null;

        if (m_TargetHealth != null)
        {
            m_TargetHealth.OnHealthChanged += RefreshHealthBar;
            RefreshHealthBar();
        }

        SetSliderVisible(m_TargetHealth != null);
    }

    private void UnbindTarget()
    {
        if (m_TargetHealth != null)
            m_TargetHealth.OnHealthChanged -= RefreshHealthBar;

        m_Target = null;
        m_TargetHealth = null;
    }

    private void RefreshHealthBar()
    {
        if (m_Slider != null && m_TargetHealth != null)
            m_Slider.value = m_TargetHealth.GetHPRatio();
    }

    private void SetSliderVisible(bool visible)
    {
        if (m_Slider != null && m_Slider.gameObject.activeSelf != visible)
            m_Slider.gameObject.SetActive(visible);
    }
}
