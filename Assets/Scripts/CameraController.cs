using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform m_Target;
    [SerializeField] private float m_MouseSensitivity = 1f;
    [SerializeField] private float m_Distance = 5f;
    [SerializeField] private float m_Height = 2f;

    [SerializeField] private Camera m_CameraChild;

    private float m_XRotation;
    private float m_YRotation;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (m_CameraChild == null)
            m_CameraChild = GetComponentInChildren<Camera>();
    }

    private void LateUpdate()
    {
        if (m_Target == null) return;

        Vector2 lookInput = InputReader.Instance.LookInput;

        m_YRotation += lookInput.x * m_MouseSensitivity;
        m_XRotation -= lookInput.y * m_MouseSensitivity;
        m_XRotation = Mathf.Clamp(m_XRotation, -30f, 80f);

        transform.position = m_Target.position;
        transform.rotation = Quaternion.Euler(m_XRotation, m_YRotation, 0f);
    }

    private void OnValidate()
    {
        if (m_CameraChild != null)
            m_CameraChild.transform.localPosition = new Vector3(0f, m_Height, -m_Distance);
    }
}
