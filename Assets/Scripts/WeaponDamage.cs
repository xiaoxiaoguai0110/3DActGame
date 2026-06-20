using UnityEngine;

public class WeaponDamage : MonoBehaviour
{
    private Player m_Player;
    private Collider m_Collider;
    private bool m_IsDamageActive;

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
    }

    public void DisableDamage()
    {
        m_IsDamageActive = false;
        m_Collider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 只对敌人造成伤害
        if (!other.CompareTag("Enemy")) return;

        Health enemyHealth = other.GetComponentInParent<Health>();
        if (enemyHealth == null) return;

        float damage = m_Player.GetCurrentAttackDamage();
        enemyHealth.TakeDamage(damage);
    }
}
