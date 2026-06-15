using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private float m_WalkSpeed = 5f;
    [SerializeField] private float m_RunSpeed = 10f;
    [SerializeField] private float m_RotationSpeed = 10f;
    [SerializeField] private float m_ComboWindowStartDelay = 1.2f;
    [SerializeField] private float m_ComboWindowDuration = 0.5f;
    [SerializeField] private float[] m_ComboDamages = { 10f, 15f, 20f, 25f, 30f };
    [SerializeField] private WeaponDamage m_WeaponDamage;
    [SerializeField] private float m_LockOnRange = 15f;
    [SerializeField] private float m_LockOnAngle = 60f;

    private Transform m_CameraTransform;
    private PlayerState m_CurrentState;
    private Animator m_Animator;
    private CharacterController m_Controller;

    private int m_ComboStage;
    private float m_ComboTimer;
    private float m_VerticalVelocity;
    private Transform m_LockOnTarget;

    private void Start()
    {
        m_CameraTransform = Camera.main.transform;
        m_Animator = GetComponent<Animator>();
        m_Controller = GetComponent<CharacterController>();
        InputReader.Instance.OnAttack += HandleAttack;
        InputReader.Instance.OnLock += HandleLock;
    }

    private void OnDisable()
    {
        InputReader.Instance.OnAttack -= HandleAttack;
        InputReader.Instance.OnLock -= HandleLock;
    }

    private void Update()
    {
        if (m_ComboStage > 0)
        {
            m_ComboTimer -= Time.deltaTime;
            if (m_ComboTimer <= 0f)
            {
                m_ComboStage = 0;
            }
            return;
        }

        // 锁定目标死亡时自动解锁
        if (m_LockOnTarget != null)
        {
            Health targetHealth = m_LockOnTarget.GetComponent<Health>();
            if (targetHealth == null || targetHealth.GetCurrentHP() <= 0f)
                m_LockOnTarget = null;
        }

        Vector2 input = InputReader.Instance.MoveInput;
        bool hasInput = input.magnitude > 0.01f;
        bool isRunning = InputReader.Instance.IsRunning;

        UpdateState(hasInput, isRunning);

        if (m_LockOnTarget != null)
        {
            // 锁定时：面朝目标，移动方向以目标为基准
            Vector3 moveDirection = GetLockedMoveDirection(input);
            Move(moveDirection);
            FaceTarget();
        }
        else if (m_CurrentState != PlayerState.Idle)
        {
            Vector3 moveDirection = GetMoveDirection(input);
            Move(moveDirection);
            Rotate(moveDirection);
        }
    }

    private void UpdateState(bool hasInput, bool isRunning)
    {
        if (!hasInput)
            m_CurrentState = PlayerState.Idle;
        else if (isRunning)
            m_CurrentState = PlayerState.Run;
        else
            m_CurrentState = PlayerState.Walk;

        float normalizedSpeed = m_CurrentState switch
        {
            PlayerState.Idle => 0f,
            PlayerState.Walk => 0.5f,
            PlayerState.Run => 1f,
            _ => 0f
        };
        m_Animator.SetFloat("Speed", normalizedSpeed);
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 cameraForward = m_CameraTransform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 cameraRight = m_CameraTransform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * input.y + cameraRight * input.x;
        return moveDirection.normalized;
    }

    private void Move(Vector3 direction)
    {
        float speed = m_CurrentState == PlayerState.Run ? m_RunSpeed : m_WalkSpeed;

        if (m_Controller.isGrounded && m_VerticalVelocity < 0f)
            m_VerticalVelocity = -2f;

        m_VerticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 motion = direction * speed + Vector3.up * m_VerticalVelocity;
        m_Controller.Move(motion * Time.deltaTime);
    }

    private void Rotate(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 锁定时获取移动方向：前后为靠近/远离目标，左右为横移。
    /// </summary>
    private Vector3 GetLockedMoveDirection(Vector2 input)
    {
        Vector3 forward = (m_LockOnTarget.position - transform.position).normalized;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        return (forward * input.y + right * input.x).normalized;
    }

    /// <summary>
    /// 锁定时始终面朝目标。
    /// </summary>
    private void FaceTarget()
    {
        Vector3 direction = (m_LockOnTarget.position - transform.position).normalized;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 按锁定键时切换锁定状态。
    /// </summary>
    private void HandleLock()
    {
        if (m_LockOnTarget != null)
        {
            // 已锁定 → 解锁
            m_LockOnTarget = null;
            return;
        }

        // 未锁定 → 找目标
        m_LockOnTarget = FindLockOnTarget();
    }

    /// <summary>
    /// 在摄像机前方一定范围内查找最近的敌人。
    /// </summary>
    private Transform FindLockOnTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, m_LockOnRange);
        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.GetCurrentHP() <= 0f) continue;

            Transform enemyRoot = hit.transform.root;

            // 判断是否在摄像机前方
            Vector3 directionToEnemy = (enemyRoot.position - m_CameraTransform.position).normalized;
            float angle = Vector3.Angle(m_CameraTransform.forward, directionToEnemy);
            if (angle > m_LockOnAngle) continue;

            // 选最近的
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

    public float GetCurrentAttackDamage()
    {
        int index = Mathf.Clamp(m_ComboStage - 1, 0, m_ComboDamages.Length - 1);
        return m_ComboDamages[index];
    }

    /// <summary>
    /// 收剑时关闭伤害检测（由 Invoke 回调）。
    /// </summary>
    private void DisableDamage()
    {
        if (m_WeaponDamage != null)
            m_WeaponDamage.DisableDamage();
    }

    private void HandleAttack()
    {
        if (m_ComboStage >= 5)
            return;

        if (m_ComboStage > 0 && m_ComboTimer > m_ComboWindowDuration)
            return;

        m_ComboStage++;

        m_Animator.SetInteger("ComboStage", m_ComboStage);
        m_Animator.SetTrigger("OnAttack");

        AudioManager.Instance.PlayAttackSound();

        // 开启武器伤害检测（立即执行 OverlapSphere）
        if (m_WeaponDamage != null)
            m_WeaponDamage.EnableDamage();

        // 攻击动画结束后自动关闭
        CancelInvoke(nameof(DisableDamage));
        Invoke(nameof(DisableDamage), 0.5f);

        m_ComboTimer = m_ComboStage >= 5 ? 2f : m_ComboWindowStartDelay + m_ComboWindowDuration;
    }

    private enum PlayerState
    {
        Idle,
        Walk,
        Run
    }
}
