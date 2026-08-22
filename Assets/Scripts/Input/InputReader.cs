using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    public static InputReader Instance { get; private set; }

    private PlayerController m_InputActions;
    private bool m_AreCallbacksRegistered;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action OnAttack;
    public event Action OnLock;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 重载场景会创建场景内的新副本；先禁用，避免它进入 OnEnable 访问未创建的输入对象。
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        m_InputActions = new PlayerController();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (m_InputActions == null)
            return;

        m_InputActions.Player.Enable();
        m_InputActions.Camera.Enable();

        if (m_AreCallbacksRegistered)
            return;

        m_InputActions.Player.Attack.performed += HandleAttack;
        m_InputActions.Player.LockOn.performed += HandleLock;
        m_AreCallbacksRegistered = true;
    }

    private void OnDisable()
    {
        if (m_InputActions == null)
            return;

        UnregisterCallbacks();

        m_InputActions.Player.Disable();
        m_InputActions.Camera.Disable();
    }

    private void OnDestroy()
    {
        if (m_InputActions != null)
        {
            UnregisterCallbacks();
            // 自动生成的 InputActionAsset 持有原生资源，最终销毁时需要显式释放。
            m_InputActions.Dispose();
            m_InputActions = null;
        }

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (m_InputActions == null)
            return;

        MoveInput = m_InputActions.Player.Move.ReadValue<Vector2>();
        LookInput = m_InputActions.Camera.Look.ReadValue<Vector2>();
        IsRunning = m_InputActions.Player.Run.IsPressed();
    }

    private void HandleAttack(InputAction.CallbackContext context)
    {
        OnAttack?.Invoke();
    }

    private void HandleLock(InputAction.CallbackContext context)
    {
        OnLock?.Invoke();
    }

    private void UnregisterCallbacks()
    {
        if (!m_AreCallbacksRegistered || m_InputActions == null)
            return;

        m_InputActions.Player.Attack.performed -= HandleAttack;
        m_InputActions.Player.LockOn.performed -= HandleLock;
        m_AreCallbacksRegistered = false;
    }
}
