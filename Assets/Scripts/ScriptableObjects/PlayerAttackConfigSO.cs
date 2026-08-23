using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAttackConfig", menuName = "3DActGame/Combat/Player Attack Config")]
public class PlayerAttackConfigSO : ScriptableObject
{
    [Serializable]
    public class AttackDefinition
    {
        [SerializeField, Min(1)] private int m_ComboStage = 1;
        [SerializeField, Min(0f)] private float m_Damage = 10f;
        [SerializeField] private AudioClip m_AudioClip;
        [SerializeField] private GameObject m_HitEffectPrefab;
        [SerializeField, Min(0f)] private float m_CameraShakeIntensity = 0.15f;
        [SerializeField, Min(0f)] private float m_HitStopDuration = 0.05f;

        [Header("Animation Event Timeline (Normalized)")]
        [SerializeField, Range(0f, 1f)] private float m_DamageStartTime = 0.2f;
        [SerializeField, Range(0f, 1f)] private float m_DamageEndTime = 0.4f;
        [SerializeField, Range(0f, 1f)] private float m_ComboInputStartTime = 0.25f;
        [SerializeField, Range(0f, 1f)] private float m_ComboInputEndTime = 0.5f;

        public int ComboStage => m_ComboStage;
        public float Damage => m_Damage;
        public AudioClip AudioClip => m_AudioClip;
        public GameObject HitEffectPrefab => m_HitEffectPrefab;
        public float CameraShakeIntensity => m_CameraShakeIntensity;
        public float HitStopDuration => m_HitStopDuration;
        public float DamageStartTime => m_DamageStartTime;
        public float DamageEndTime => m_DamageEndTime;
        public float ComboInputStartTime => m_ComboInputStartTime;
        public float ComboInputEndTime => m_ComboInputEndTime;
    }

    [SerializeField] private AttackDefinition[] m_Attacks = Array.Empty<AttackDefinition>();

    public int MaxComboStage
    {
        get
        {
            int maxStage = 0;
            foreach (AttackDefinition attack in m_Attacks)
            {
                if (attack != null)
                    maxStage = Mathf.Max(maxStage, attack.ComboStage);
            }

            return maxStage;
        }
    }

    public bool TryGetAttack(int comboStage, out AttackDefinition attack)
    {
        foreach (AttackDefinition candidate in m_Attacks)
        {
            if (candidate != null && candidate.ComboStage == comboStage)
            {
                attack = candidate;
                return true;
            }
        }

        attack = null;
        return false;
    }
}
