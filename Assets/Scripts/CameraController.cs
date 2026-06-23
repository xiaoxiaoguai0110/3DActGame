using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [SerializeField] private Transform m_Target;
    [SerializeField] private float m_MouseSensitivity = 1f;
    [SerializeField] private float m_Distance = 5f;
    [SerializeField] private float m_Height = 2f;
    [SerializeField] private float m_LockFollowSpeed = 5f;

    [SerializeField] private Camera m_CameraChild;

    private Player m_Player;
    private float m_XRotation;
    private float m_YRotation;
    private Vector3 m_OriginalCameraLocalPos;

    // 屏幕震动
    private float m_ShakeIntensity;
    private float m_ShakeDuration;
    private float m_ShakeTimer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (m_CameraChild == null)
            m_CameraChild = GetComponentInChildren<Camera>();

        m_OriginalCameraLocalPos = m_CameraChild.transform.localPosition;

        if (m_Target != null)
            m_Player = m_Target.GetComponent<Player>();
    }

    private void LateUpdate()
    {
        if (m_Target == null) return;

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

        // 屏幕震动：偏移子摄像机
        UpdateShake();
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

        float t = m_ShakeTimer / m_ShakeDuration;                // 1 → 0
        float decay = Mathf.Lerp(0f, 1f, t);                     // 震动幅度随时间衰减
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
        // 计算从摄像机指向锁定目标的水平方向
        Vector3 targetDirection = lockTarget.position - transform.position;
        targetDirection.y = 0f;

        if (targetDirection == Vector3.zero) return;

        // 计算目标水平旋转角度
        float targetYRotation = Quaternion.LookRotation(targetDirection).eulerAngles.y;

        // 计算目标垂直角度（根据目标高度自动调整）
        Vector3 directionToTarget = (lockTarget.position - transform.position).normalized;
        float targetXRotation = -Mathf.Asin(directionToTarget.y) * Mathf.Rad2Deg;
        targetXRotation = Mathf.Clamp(targetXRotation, -30f, 80f);

        // 平滑旋转到目标角度
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
