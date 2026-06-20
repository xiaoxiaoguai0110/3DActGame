using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public event Action OnHealthChanged;

    [SerializeField] private float m_MaxHP = 100f;
    [SerializeField] private float m_CurrentHP;

    private void Start()
    {
        m_CurrentHP = m_MaxHP;
    }

    public void TakeDamage(float damage)
    {
        if (m_CurrentHP <= 0f) return;

        m_CurrentHP -= damage;
        m_CurrentHP = Mathf.Max(m_CurrentHP, 0f);
        Debug.Log($"{name} 受到 {damage} 点伤害，剩余 {m_CurrentHP} HP");
        OnHealthChanged?.Invoke();
    }

    public float GetCurrentHP()
    {
        return m_CurrentHP;
    }

    public float GetMaxHP()
    {
        return m_MaxHP;
    }

    public float GetHPRatio()
    {
        return m_CurrentHP / m_MaxHP;
    }
}
