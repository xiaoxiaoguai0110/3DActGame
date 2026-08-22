using UnityEngine;
using System.Collections.Generic;

public class WeaponDamage : MonoBehaviour
{
    private Player m_Player;
    private Collider m_Collider;
    private bool m_IsDamageActive;
    private HashSet<Health> m_HitTargets = new HashSet<Health>();

    private void Awake()
    {
        m_Collider = GetComponent<Collider>();
        m_Collider.enabled = false;
        m_Player = GetComponentInParent<Player>();
    }

    public void EnableDamage()
    {
        m_Collider.enabled = true;
        m_IsDamageActive = true;
        m_HitTargets.Clear();
    }

    public void DisableDamage()
    {
        m_IsDamageActive = false;
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

            float damage = m_Player.GetCurrentAttackDamage();
            enemyHealth.TakeDamage(damage);
        }
    }
}
