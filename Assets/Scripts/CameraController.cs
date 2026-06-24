using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [SerializeField] private Transform m_Target;
    [SerializeField] private float m_MouseSensitivity = 1f;
    [SerializeField] private float m_Distance = 5f;
    [SerializeField] private float m_Height = 2f;
    [SerializeField] private float m_LockFollowSpeed = 5f;

    [Header("开场动画")]
    [SerializeField] private float m_IntroTransitionDuration = 2.5f;

    [SerializeField] private Camera m_CameraChild;

    private Player m_Player;
    private float m_XRotation;
    private float m_YRotation;
    private Vector3 m_OriginalCameraLocalPos;

    // 屏幕震动
    private float m_ShakeIntensity;
    private float m_ShakeDuration;
    private float m_ShakeTimer;

    // 开场动画
    private enum CameraMode { Intro, Transition, Normal }
    private CameraMode m_CamMode = CameraMode.Normal;
    private float m_TransitionTimer;
    private Vector3 m_TransitionStartPos;
    private Quaternion m_TransitionStartRot;
    private Vector3 m_TransitionEndPos;
    private Quaternion m_TransitionEndRot;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (m_CameraChild == null)
            m_CameraChild = GetComponentInChildren<Camera>();

        m_OriginalCameraLocalPos = m_CameraChild.transform.localPosition;

        if (m_Target != null)
            m_Player = m_Target.GetComponent<Player>();
    }

    private void LateUpdate()
    {
        if (m_Target == null) return;

        if (m_CamMode == CameraMode.Transition)
        {
            m_TransitionTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_TransitionTimer / m_IntroTransitionDuration);
            float smoothT = t * t * (3f - 2f * t); // smoothstep

            transform.position = Vector3.Lerp(m_TransitionStartPos, m_TransitionEndPos, smoothT);
            transform.rotation = Quaternion.Slerp(m_TransitionStartRot, m_TransitionEndRot, smoothT);

            if (t >= 1f)
            {
                m_CamMode = CameraMode.Normal;
                Cursor.lockState = CursorLockMode.Locked;
                Time.timeScale = 1f;
                MainMenuUI.IsInputEnabled = true;
            }
            return;
        }

        if (m_CamMode == CameraMode.Intro) return;

        // Normal mode
        Transform lockTarget = m_Player != null ? m_Player.GetLockOnTarget() : null;

        if (lockTarget != null)
        {
            UpdateLockedCamera(lockTarget);
        }
        else
        {
            UpdateFreeCamera();
        }

        transform.position = m_Target.position;

        UpdateShake();
    }

    /// <summary>
    /// 冻结摄像机在当前位置，不跟随玩家。
    /// </summary>
    public void FreezeAtCurrentPosition()
    {
        m_CamMode = CameraMode.Intro;
    }

    /// <summary>
    /// 从当前冻结位置平滑移动到角色身后（正常跟随位置）。
    /// </summary>
    public void BeginGameTransition()
    {
        m_CamMode = CameraMode.Transition;
        m_TransitionTimer = 0f;
        m_TransitionStartPos = transform.position;
        m_TransitionStartRot = transform.rotation;

        // 计算目标：摄像机在角色身后（即正常跟随位置）
        m_YRotation = m_Target.eulerAngles.y;
        m_XRotation = 10f;

        m_TransitionEndPos = m_Target.position;
        m_TransitionEndRot = Quaternion.Euler(m_XRotation, m_YRotation, 0f);
    }

    /// <summary>
    /// 触发屏幕震动。
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        m_ShakeIntensity = intensity;
        m_ShakeDuration = duration;
        m_ShakeTimer = duration;
    }

    private void UpdateShake()
    {
        if (m_ShakeTimer <= 0f)
        {
            m_CameraChild.transform.localPosition = m_OriginalCameraLocalPos;
            return;
        }

        m_ShakeTimer -= Time.deltaTime;

        float t = m_ShakeTimer / m_ShakeDuration;
        float decay = Mathf.Lerp(0f, 1f, t);
        float currentIntensity = m_ShakeIntensity * decay;

        Vector3 offset = Random.insideUnitSphere * currentIntensity;
        offset.z *= 0.3f;

        m_CameraChild.transform.localPosition = m_OriginalCameraLocalPos + offset;
    }

    private void UpdateFreeCamera()
    {
        Vector2 lookInput = InputReader.Instance.LookInput;

        m_YRotation += lookInput.x * m_MouseSensitivity;
        m_XRotation -= lookInput.y * m_MouseSensitivity;
        m_XRotation = Mathf.Clamp(m_XRotation, -30f, 80f);

        transform.rotation = Quaternion.Euler(m_XRotation, m_YRotation, 0f);
    }

    private void UpdateLockedCamera(Transform lockTarget)
    {
        Vector3 targetDirection = lockTarget.position - transform.position;
        targetDirection.y = 0f;

        if (targetDirection == Vector3.zero) return;

        float targetYRotation = Quaternion.LookRotation(targetDirection).eulerAngles.y;

        Vector3 directionToTarget = (lockTarget.position - transform.position).normalized;
        float targetXRotation = -Mathf.Asin(directionToTarget.y) * Mathf.Rad2Deg;
        targetXRotation = Mathf.Clamp(targetXRotation, -30f, 80f);

        m_YRotation = Mathf.LerpAngle(m_YRotation, targetYRotation, m_LockFollowSpeed * Time.deltaTime);
        m_XRotation = Mathf.LerpAngle(m_XRotation, targetXRotation, m_LockFollowSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(m_XRotation, m_YRotation, 0f);
    }

    private void OnValidate()
    {
        if (m_CameraChild != null)
            m_CameraChild.transform.localPosition = new Vector3(0f, m_Height, -m_Distance);
    }
}
