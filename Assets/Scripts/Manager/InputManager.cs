using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    public static InputReader Instance { get; private set; }

    private PlayerController m_InputActions;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action OnAttack;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        m_InputActions = new PlayerController();
    }

    private void OnEnable()
    {
        m_InputActions.Player.Enable();
        m_InputActions.Camera.Enable();

        m_InputActions.Player.Attack.performed += HandleAttack;
    }

    private void OnDisable()
    {
        m_InputActions.Player.Attack.performed -= HandleAttack;

        m_InputActions.Player.Disable();
        m_InputActions.Camera.Disable();
    }

    private void Update()
    {
        MoveInput = m_InputActions.Player.Move.ReadValue<Vector2>();
        LookInput = m_InputActions.Camera.Look.ReadValue<Vector2>();
        IsRunning = m_InputActions.Player.Run.IsPressed();
    }

    private void HandleAttack(InputAction.CallbackContext context)
    {
        OnAttack?.Invoke();
    }
}
