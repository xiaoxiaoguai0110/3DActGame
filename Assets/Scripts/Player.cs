using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    [SerializeField] private float m_WalkSpeed = 5f;
    [SerializeField] private float m_RunSpeed = 10f;
    [SerializeField] private float m_RotationSpeed = 10f;
    [SerializeField] private float m_ComboWindowDuration = 1.5f;
    [SerializeField] private int m_ComboMaxStage = 5;
    [SerializeField] private float[] m_ComboDamages = { 10f, 15f, 20f, 25f, 30f };
    [SerializeField] private WeaponDamage m_WeaponDamage;
    [SerializeField] private float m_LockOnRange = 15f;
    [SerializeField] private float m_LockOnAngle = 60f;

    private Transform m_CameraTransform;
    private PlayerState m_CurrentState;
    private Animator m_Animator;
    private CharacterController m_Controller;
    private Health m_Health;

    private int m_ComboStage;
    private int m_PreparedComboStage;
    private float m_ComboTimer;
    private float m_VerticalVelocity;
    private Transform m_LockOnTarget;
    private bool m_HasEnteredComboAnimation;
    private bool m_QueuedAttackAfterPrepared;

    private float m_DeadTimer;

    private void Start()
    {
        m_CameraTransform = Camera.main.transform;
        m_Animator = GetComponent<Animator>();
        m_Controller = GetComponent<CharacterController>();
        m_Health = GetComponent<Health>();
        InputReader.Instance.OnAttack += HandleAttack;
        InputReader.Instance.OnLock += HandleLock;

        if (m_Health != null)
            m_Health.OnHealthChanged += HandlePlayerGetHit;
    }

    private void OnDisable()
    {
        InputReader.Instance.OnAttack -= HandleAttack;
        InputReader.Instance.OnLock -= HandleLock;

        if (m_Health != null)
            m_Health.OnHealthChanged -= HandlePlayerGetHit;
    }

    private void Update()
    {
        if (m_CurrentState == PlayerState.Dead)
        {
            UpdateDead();
            return;
        }

        if (!MainMenuUI.IsInputEnabled) return;

        if (m_LockOnTarget != null)
        {
            Health targetHealth = m_LockOnTarget.GetComponent<Health>();
            if (targetHealth == null || targetHealth.GetCurrentHP() <= 0f)
                m_LockOnTarget = null;
        }

        if (m_ComboStage > 0)
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

            m_ComboTimer -= Time.deltaTime;
            if (m_ComboTimer <= 0f && m_ComboStage < m_ComboMaxStage)
            {
                ResetCombo();
                return;
            }

            if (m_LockOnTarget != null)
                FaceTarget();

            return;
        }

        Vector2 input = InputReader.Instance.MoveInput;
        bool hasInput = input.magnitude > 0.01f;
        bool isRunning = InputReader.Instance.IsRunning;

        UpdateState(hasInput, isRunning);

        if (m_LockOnTarget != null)
        {
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

    private void ResetCombo()
    {
        m_ComboStage = 0;
        m_PreparedComboStage = 0;
        m_HasEnteredComboAnimation = false;
        m_QueuedAttackAfterPrepared = false;
        m_Animator.SetInteger("ComboStage", 0);
        m_Animator.ResetTrigger("OnAttack");
    }

    private bool IsInComboAnimation()
    {
        AnimatorStateInfo currentState = m_Animator.GetCurrentAnimatorStateInfo(0);
        if (IsComboState(currentState))
            return true;

        if (!m_Animator.IsInTransition(0))
            return false;

        AnimatorStateInfo nextState = m_Animator.GetNextAnimatorStateInfo(0);
        return IsComboState(nextState);
    }

    private bool IsComboState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("combo_04_1 0")
            || stateInfo.IsName("combo_04_2 0")
            || stateInfo.IsName("combo_04_3 0")
            || stateInfo.IsName("combo_04_4 0")
            || stateInfo.IsName("combo_04_5 0");
    }

    private bool IsCurrentComboStage(int stage)
    {
        return GetComboStage(m_Animator.GetCurrentAnimatorStateInfo(0)) == stage;
    }

    private int GetComboStage(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("combo_04_1 0")) return 1;
        if (stateInfo.IsName("combo_04_2 0")) return 2;
        if (stateInfo.IsName("combo_04_3 0")) return 3;
        if (stateInfo.IsName("combo_04_4 0")) return 4;
        if (stateInfo.IsName("combo_04_5 0")) return 5;
        return 0;
    }

    private bool IsInLocomotionAnimation()
    {
        return !m_Animator.IsInTransition(0)
            && m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Blend Tree");
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

    private Vector3 GetLockedMoveDirection(Vector2 input)
    {
        Vector3 forward = (m_LockOnTarget.position - transform.position).normalized;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        return (forward * input.y + right * input.x).normalized;
    }

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

    private void HandleLock()
    {
        if (!MainMenuUI.IsInputEnabled) return;

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
            if (!hit.CompareTag("Enemy")) continue;

            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.GetCurrentHP() <= 0f) continue;

            Transform enemyRoot = hit.transform.root;

            Vector3 directionToEnemy = (enemyRoot.position - m_CameraTransform.position).normalized;
            float angle = Vector3.Angle(m_CameraTransform.forward, directionToEnemy);
            if (angle > m_LockOnAngle) continue;

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
        int damageStage = m_PreparedComboStage > 0 ? m_PreparedComboStage : m_ComboStage;
        int index = Mathf.Clamp(damageStage - 1, 0, m_ComboDamages.Length - 1);
        return m_ComboDamages[index];
    }

    private void DisableDamage()
    {
        if (m_WeaponDamage != null)
            m_WeaponDamage.DisableDamage();
    }

    private void HandleAttack()
    {
        if (!MainMenuUI.IsInputEnabled) return;

        if (m_ComboStage == 0)
        {
            StartCombo(1);
            return;
        }

        if (!IsInComboAnimation())
            return;

        if (m_PreparedComboStage > 0)
        {
            m_QueuedAttackAfterPrepared = true;
            m_ComboTimer = m_ComboWindowDuration;
            return;
        }

        if (m_ComboStage >= m_ComboMaxStage)
            return;

        PrepareComboTransition(m_ComboStage + 1);
    }

    private void StartCombo(int stage)
    {
        m_ComboStage = stage;
        m_PreparedComboStage = 0;
        m_QueuedAttackAfterPrepared = false;

        SetComboAnimatorConditions(stage);
        PlayComboEffects();
        m_ComboTimer = m_ComboWindowDuration;
    }

    private void PrepareComboTransition(int stage)
    {
        m_PreparedComboStage = stage;
        SetComboAnimatorConditions(stage);
        m_ComboTimer = m_ComboWindowDuration;
    }

    private void CommitPreparedComboStage()
    {
        m_ComboStage = m_PreparedComboStage;
        m_PreparedComboStage = 0;

        PlayComboEffects();
        m_ComboTimer = m_ComboWindowDuration;

        if (m_QueuedAttackAfterPrepared && m_ComboStage < m_ComboMaxStage)
        {
            m_QueuedAttackAfterPrepared = false;
            PrepareComboTransition(m_ComboStage + 1);
            return;
        }

        m_QueuedAttackAfterPrepared = false;
    }

    private void SetComboAnimatorConditions(int stage)
    {
        m_Animator.SetInteger("ComboStage", stage);
        m_Animator.ResetTrigger("OnAttack");
        m_Animator.SetTrigger("OnAttack");
    }

    private void PlayComboEffects()
    {
        AudioManager.Instance.PlayAttackSound();

        if (m_WeaponDamage != null)
            m_WeaponDamage.EnableDamage();
        CancelInvoke(nameof(DisableDamage));
        Invoke(nameof(DisableDamage), 0.5f);
    }

    /// <summary>
    /// 由 Health.OnHealthChanged 触发，处理玩家受击或死亡。
    /// </summary>
    private void HandlePlayerGetHit()
    {
        if (m_CurrentState == PlayerState.Dead) return;

        // HP归零 → 死亡
        if (m_Health.GetCurrentHP() <= 0f)
        {
            m_CurrentState = PlayerState.Dead;
            m_Controller.enabled = false;
            ResetCombo();
            DisableDamage();
            m_LockOnTarget = null;
            m_Animator.SetTrigger("OnDead");
            m_DeadTimer = 3f;
            return;
        }

        m_Animator.SetTrigger("OnGetHit");
    }

    /// <summary>
    /// 死亡状态：等待动画播完，然后重载场景回到主菜单。
    /// </summary>
    private void UpdateDead()
    {
        m_DeadTimer -= Time.deltaTime;
        if (m_DeadTimer <= 0f)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    /// <summary>
    /// 播放玩家入场动画（开场/主菜单点击开始后调用）。
    /// </summary>
    public void PlayIntroAnimation()
    {
        m_Animator.SetTrigger("OnIntro");
    }

    private enum PlayerState
    {
        Idle,
        Walk,
        Run,
        Dead
    }
}