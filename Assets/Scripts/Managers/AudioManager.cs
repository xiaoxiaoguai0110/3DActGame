using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioClipRefsSO m_AudioClipRefsSO;

    private AudioSource m_AttackSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        m_AttackSource = GetComponent<AudioSource>();
        if (m_AttackSource == null)
            m_AttackSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayAttackSound()
    {
        if (m_AudioClipRefsSO == null || m_AudioClipRefsSO.attack == null || m_AudioClipRefsSO.attack.Length == 0)
            return;

        AudioClip clip = m_AudioClipRefsSO.attack[0];
        if (clip == null) return;

        m_AttackSource.PlayOneShot(clip);
    }
}
