using System;
using UnityEngine;

/// <summary>
/// 玩家组件协调器。
/// 具体职责拆分到 Player.Movement、Player.LockOn、Player.Combat 和 Player.Lifecycle。
/// </summary>
public partial class Player : MonoBehaviour
{
    public event Action Died;

    [Header("Movement")]
    [SerializeField] private float m_WalkSpeed = 5f;
    [SerializeField] private float m_RunSpeed = 10f;
    [SerializeField] private float m_RotationSpeed = 10f;

    [Header("Combat")]
    [SerializeField] private PlayerAttackConfigSO m_AttackConfig;
    [SerializeField] private WeaponDamage m_WeaponDamage;

    [Header("Hit Reaction")]
    [SerializeField] private Transform m_HitReactionRoot;
    [SerializeField, Min(0.05f)] private float m_HitReactionDuration = 0.22f;
    [SerializeField, Min(0f)] private float m_HitReactionDistance = 0.12f;
    [SerializeField, Min(0f)] private float m_HitReactionTilt = 10f;

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
    private float m_VerticalVelocity;
    private Transform m_LockOnTarget;
    private bool m_HasEnteredAttackAnimation;
    private bool m_IsComboInputWindowOpen;
    private PlayerAttackConfigSO.AttackDefinition m_ActiveAttack;
    private Coroutine m_HitStopCoroutine;
    private float m_TimeScaleBeforeHitStop = 1f;
    private bool m_OwnsHitStop;
    private Coroutine m_HitReactionCoroutine;
    private Vector3 m_HitReactionBasePosition;
    private Quaternion m_HitReactionBaseRotation;
    private bool m_IsHitReacting;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ComboStageHash = Animator.StringToHash("ComboStage");
    private static readonly int OnAttackHash = Animator.StringToHash("OnAttack");
    private static readonly int OnDeadHash = Animator.StringToHash("OnDead");
    private static readonly int OnIntroHash = Animator.StringToHash("OnIntro");
    private static readonly int AttackTagHash = Animator.StringToHash("Attack");
    private static readonly int LocomotionStateHash = Animator.StringToHash("Base Layer.Blend Tree");

    private void Awake()
    {
        // 自身组件在 Awake 缓存，保证其他对象的 Start 调用玩家公开方法时引用已经就绪。
        m_Animator = GetComponent<Animator>();
        m_Controller = GetComponent<CharacterController>();
        m_Health = GetComponent<Health>();

        if (m_HitReactionRoot == null)
            m_HitReactionRoot = FindHitReactionRoot();
        if (m_HitReactionRoot != null)
        {
            m_HitReactionBasePosition = m_HitReactionRoot.localPosition;
            m_HitReactionBaseRotation = m_HitReactionRoot.localRotation;
        }
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

        if (m_WeaponDamage != null)
            m_WeaponDamage.HitConfirmed += HandleWeaponHit;

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

        if (m_WeaponDamage != null)
            m_WeaponDamage.HitConfirmed -= HandleWeaponHit;

        RestoreTimeScaleAfterHitStop();
        StopHitReaction();
    }

    private void Update()
    {
        if (m_CurrentState == PlayerState.Dead)
            return;

        if (!MainMenuUI.IsInputEnabled)
        {
            // 开场动画期间虽然不接收水平输入，CharacterController 仍负责贴地和下落。
            UpdateGravity();
            return;
        }

        ClearDeadLockOnTarget();

        if (m_IsHitReacting)
        {
            UpdateGravity();
            return;
        }

        if (m_ComboStage > 0)
        {
            UpdateCombo();
        }
        else
        {
            UpdateMovement();
        }

        // 重力与水平状态解耦：Idle、攻击和无输入时都会执行一次垂直移动。
        UpdateGravity();
    }

    private void UpdateCombo()
    {
        if (IsInAttackAnimation())
            m_HasEnteredAttackAnimation = true;

        if (m_HasEnteredAttackAnimation && !IsInAttackAnimation() && !m_Animator.IsInTransition(0))
        {
            ResetCombo();
            return;
        }

        if (m_LockOnTarget != null)
            FaceTarget();
    }

    private void UpdateMovement()
    {
        InputReader inputReader = InputReader.Instance;
        if (inputReader == null)
        {
            UpdateState(false, false);
            return;
        }

        Vector2 input = inputReader.MoveInput;
        bool hasInput = input.magnitude > 0.01f;
        bool isRunning = inputReader.IsRunning;

        UpdateState(hasInput, isRunning);

        if (m_LockOnTarget != null)
        {
            MoveHorizontal(GetLockedMoveDirection(input));
            FaceTarget();
        }
        else if (m_CurrentState != PlayerState.Idle)
        {
            Vector3 moveDirection = GetMoveDirection(input);
            MoveHorizontal(moveDirection);
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

    private void OnDestroy()
    {
        Died = null;
    }
}
