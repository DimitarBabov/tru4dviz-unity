using UnityEngine;
using UnityEngine.EventSystems;
public class SimpleOrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 10.0f;
    public float zoomSpeed = 2.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 50.0f;
    public float xSpeed = 250.0f;
    public float ySpeed = 120.0f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;
    public float panSpeed = 0.5f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
        if (target == null)
            Debug.LogWarning("SimpleOrbitCamera: No target set!");
    }

    void LateUpdate()
    {
        // Prevent camera controls if pointer is over UI
    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        return;
        if (target == null) return;

        // Orbit
        if (Input.GetMouseButton(0))
        {
            x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            y = ClampAngle(y, yMinLimit, yMaxLimit);
        }

        // Pan (right or middle mouse)
        if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            float panX = -Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime * distance;
            float panY = -Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime * distance;
            Vector3 move = transform.right * panX + transform.up * panY;
            target.position += move;
        }

        // Keyboard pan
        float keyPanX = Input.GetAxis("Horizontal") * panSpeed * Time.deltaTime * distance;
        float keyPanY = Input.GetAxis("Vertical") * panSpeed * Time.deltaTime * distance;
        if (keyPanX != 0 || keyPanY != 0)
            target.position += transform.right * keyPanX + transform.up * keyPanY;

        // Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance = Mathf.Clamp(distance - scroll * zoomSpeed * distance, minDistance, maxDistance);

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        transform.position = rotation * negDistance + target.position;
        transform.rotation = rotation;
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
} 