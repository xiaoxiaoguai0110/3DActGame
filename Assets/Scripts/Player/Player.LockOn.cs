using UnityEngine;

public partial class Player
{
    private void HandleLock()
    {
        if (!MainMenuUI.IsInputEnabled)
            return;

        if (m_LockOnTarget != null)
        {
            m_LockOnTarget = null;
            return;
        }

        m_LockOnTarget = FindLockOnTarget();
    }

    private Transform FindLockOnTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, m_LockOnRange);
        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.GetCurrentHP() <= 0f)
                continue;

            Transform enemyRoot = hit.transform.root;
            Vector3 directionToEnemy = (enemyRoot.position - m_CameraTransform.position).normalized;
            float angle = Vector3.Angle(m_CameraTransform.forward, directionToEnemy);
            if (angle > m_LockOnAngle)
                continue;

            float distance = Vector3.Distance(transform.position, enemyRoot.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemyRoot;
            }
        }

        return nearest;
    }

    public Transform GetLockOnTarget()
    {
        return m_LockOnTarget;
    }
}

