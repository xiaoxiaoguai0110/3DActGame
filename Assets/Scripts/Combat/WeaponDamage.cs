using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class WeaponDamage : MonoBehaviour
{
    private Collider m_Collider;
    private bool m_IsDamageActive;
    private float m_ActiveDamage;
    private readonly HashSet<Health> m_HitTargets = new();

    private void Awake()
    {
        m_Collider = GetComponent<Collider>();
        m_Collider.enabled = false;
    }

    public void EnableDamage(float damage)
    {
        if (m_Collider == null)
            return;

        // 伤害在窗口开启时保存，碰撞发生得再晚也不会读到下一段连招状态。
        m_ActiveDamage = Mathf.Max(0f, damage);
        m_Collider.enabled = true;
        m_IsDamageActive = true;
        m_HitTargets.Clear();
    }

    public void DisableDamage()
    {
        m_IsDamageActive = false;
        if (m_Collider != null)
            m_Collider.enabled = false;
        m_HitTargets.Clear();
    }

    private void FixedUpdate()
    {
        if (!m_IsDamageActive) return;

        Vector3 center = m_Collider.bounds.center;
        Vector3 halfExtents = m_Collider.bounds.extents;
        Quaternion rotation = m_Collider.transform.rotation;

        Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            Health enemyHealth = hit.GetComponentInParent<Health>();
            if (enemyHealth == null) continue;

            if (m_HitTargets.Contains(enemyHealth)) continue;

            m_HitTargets.Add(enemyHealth);

            enemyHealth.TakeDamage(m_ActiveDamage);
        }
    }
}
