using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private float m_WalkSpeed = 5f;
    [SerializeField] private float m_RunSpeed = 10f;
    [SerializeField] private float m_RotationSpeed = 10f;
    [SerializeField] private float m_ComboWindowStartDelay = 1.2f;
    [SerializeField] private float m_ComboWindowDuration = 0.5f;

    private Transform m_CameraTransform;
    private PlayerState m_CurrentState;
    private Animator m_Animator;
    private CharacterController m_Controller;

    private int m_ComboStage;
    private float m_ComboTimer;
    private float m_VerticalVelocity;

    private void Start()
    {
        m_CameraTransform = Camera.main.transform;
        m_Animator = GetComponent<Animator>();
        m_Controller = GetComponent<CharacterController>();
        InputReader.Instance.OnAttack += HandleAttack;
    }

    private void OnDisable()
    {
        InputReader.Instance.OnAttack -= HandleAttack;
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

        Vector2 input = InputReader.Instance.MoveInput;
        bool hasInput = input.magnitude > 0.01f;
        bool isRunning = InputReader.Instance.IsRunning;

        UpdateState(hasInput, isRunning);

        if (m_CurrentState != PlayerState.Idle)
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

        m_ComboTimer = m_ComboStage >= 5 ? 2f : m_ComboWindowStartDelay + m_ComboWindowDuration;
    }

    private enum PlayerState
    {
        Idle,
        Walk,
        Run
    }
}
