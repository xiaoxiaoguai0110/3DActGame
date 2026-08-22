using UnityEngine;

/// <summary>
/// 玩家组件协调器。
/// 具体职责拆分到 Player.Movement、Player.LockOn、Player.Combat 和 Player.Lifecycle。
/// </summary>
public partial class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float m_WalkSpeed = 5f;
    [SerializeField] private float m_RunSpeed = 10f;
    [SerializeField] private float m_RotationSpeed = 10f;

    [Header("Combat")]
    [SerializeField] private float m_ComboWindowDuration = 1.5f;
    [SerializeField] private int m_ComboMaxStage = 5;
    [SerializeField] private float[] m_ComboDamages = { 10f, 15f, 20f, 25f, 30f };
    [SerializeField] private WeaponDamage m_WeaponDamage;

    [Header("Lock On")]
    [SerializeField] private float m_LockOnRange = 15f;
    [SerializeField] private float m_LockOnAngle = 60f;

    private Transform m_CameraTransform;
    private PlayerState m_CurrentState;
    private Animator m_Animator;
    private CharacterController m_Controller;
    private Health m_Health;

    private int m_ComboStage;
    private int m_PreparedComboStage;
    private readonly CountdownTimer m_ComboTimer = new();
    private float m_VerticalVelocity;
    private Transform m_LockOnTarget;
    private bool m_HasEnteredComboAnimation;
    private bool m_QueuedAttackAfterPrepared;
    private readonly CountdownTimer m_DeadTimer = new();

    private void Awake()
    {
        // 自身组件在 Awake 缓存，保证其他对象的 Start 调用玩家公开方法时引用已经就绪。
        m_Animator = GetComponent<Animator>();
        m_Controller = GetComponent<CharacterController>();
        m_Health = GetComponent<Health>();
    }

    private void Start()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            m_CameraTransform = mainCamera.transform;

        if (InputReader.Instance != null)
        {
            InputReader.Instance.OnAttack += HandleAttack;
            InputReader.Instance.OnLock += HandleLock;
        }

        if (m_Health != null)
            m_Health.OnHealthChanged += HandlePlayerGetHit;

        m_ComboTimer.OnTimerEnd += OnComboTimerEnd;
        m_DeadTimer.OnTimerEnd += ReloadScene;
    }

    private void OnDisable()
    {
        if (InputReader.Instance != null)
        {
            InputReader.Instance.OnAttack -= HandleAttack;
            InputReader.Instance.OnLock -= HandleLock;
        }

        if (m_Health != null)
            m_Health.OnHealthChanged -= HandlePlayerGetHit;
    }

    private void Update()
    {
        if (m_CurrentState == PlayerState.Dead || !MainMenuUI.IsInputEnabled)
            return;

        ClearDeadLockOnTarget();

        if (m_ComboStage > 0)
        {
            UpdateCombo();
            return;
        }

        UpdateMovement();
    }

    private void UpdateCombo()
    {
        if (m_PreparedComboStage > 0 && IsCurrentComboStage(m_PreparedComboStage))
            CommitPreparedComboStage();

        if (IsInComboAnimation())
            m_HasEnteredComboAnimation = true;

        if (m_HasEnteredComboAnimation && IsInLocomotionAnimation())
        {
            ResetCombo();
            return;
        }

        m_ComboTimer.Tick(Time.deltaTime);

        if (m_LockOnTarget != null)
            FaceTarget();
    }

    private void UpdateMovement()
    {
        Vector2 input = InputReader.Instance.MoveInput;
        bool hasInput = input.magnitude > 0.01f;
        bool isRunning = InputReader.Instance.IsRunning;

        UpdateState(hasInput, isRunning);

        if (m_LockOnTarget != null)
        {
            Move(GetLockedMoveDirection(input));
            FaceTarget();
        }
        else if (m_CurrentState != PlayerState.Idle)
        {
            Vector3 moveDirection = GetMoveDirection(input);
            Move(moveDirection);
            Rotate(moveDirection);
        }
    }

    private void ClearDeadLockOnTarget()
    {
        if (m_LockOnTarget == null)
            return;

        Health targetHealth = m_LockOnTarget.GetComponent<Health>();
        if (targetHealth == null || targetHealth.GetCurrentHP() <= 0f)
            m_LockOnTarget = null;
    }

    private enum PlayerState
    {
        Idle,
        Walk,
        Run,
        Dead
    }
}
