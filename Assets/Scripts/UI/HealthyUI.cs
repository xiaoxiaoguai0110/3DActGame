using UnityEngine;
using UnityEngine.UI;

public class HealthyUI : MonoBehaviour
{
    [SerializeField] private Slider m_HealthSlider;

    private Health m_PlayerHealth;

    private void Start()
    {
        // 自动查找玩家身上的 Health 组件
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            m_PlayerHealth = player.GetComponent<Health>();

        if (m_PlayerHealth != null)
        {
            UpdateHealthBar();
            m_PlayerHealth.OnHealthChanged += UpdateHealthBar;
        }
    }

    private void OnDestroy()
    {
        if (m_PlayerHealth != null)
            m_PlayerHealth.OnHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar()
    {
        if (m_HealthSlider != null)
            m_HealthSlider.value = m_PlayerHealth.GetHPRatio();
    }
}
