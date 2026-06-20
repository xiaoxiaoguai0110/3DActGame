using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [SerializeField] private Slider m_Slider;

    private Health m_Health;
    private Player m_Player;

    private void Start()
    {
        m_Health = GetComponent<Health>();
        m_Player = FindObjectOfType<Player>();

        if (m_Slider != null)
            m_Slider.gameObject.SetActive(false);

        if (m_Health == null) return;

        m_Health.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDestroy()
    {
        if (m_Health != null)
            m_Health.OnHealthChanged -= UpdateHealthBar;
    }

    private void Update()
    {
        if (m_Slider == null || m_Player == null) return;

        bool isLocked = m_Player.GetLockOnTarget() == transform;
        m_Slider.gameObject.SetActive(isLocked);
    }

    private void UpdateHealthBar()
    {
        if (m_Slider != null)
            m_Slider.value = m_Health.GetHPRatio();
    }
}
