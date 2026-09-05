using UnityEngine;

public partial class Player
{
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

        return (cameraForward * input.y + cameraRight * input.x).normalized;
    }

    private void MoveHorizontal(Vector3 direction)
    {
        float speed = m_CurrentState == PlayerState.Run ? m_RunSpeed : m_WalkSpeed;
        m_Controller.Move(direction * speed * Time.deltaTime);
    }

    private void UpdateGravity()
    {
        if (m_Controller == null || !m_Controller.enabled)
            return;

        if (m_Controller.isGrounded && m_VerticalVelocity < 0f)
            m_VerticalVelocity = -2f;
        else
            m_VerticalVelocity += Physics.gravity.y * Time.deltaTime;

        // CharacterController 不会自动受 Physics.gravity 影响，必须每帧显式提交垂直位移。
        m_Controller.Move(Vector3.up * m_VerticalVelocity * Time.deltaTime);
    }

    private void Rotate(Vector3 direction)
    {
        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed * Time.deltaTime);
    }

    private Vector3 GetLockedMoveDirection(Vector2 input)
    {
        Vector3 forward = m_LockOnTarget.position - transform.position;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return (forward * input.y + right * input.x).normalized;
    }

    private void FaceTarget()
    {
        Vector3 direction = m_LockOnTarget.position - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed * Time.deltaTime);
    }
}

