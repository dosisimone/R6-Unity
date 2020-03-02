using UnityEngine;

public class PlayerMouseLook : MonoBehaviour
{
    [SerializeField, Range(0f, 200f)]
    private float mouseSensitivityX = 50f;
    [SerializeField, Range(0f, 200f)]
    private float mouseSensitivityY = 50f;

    [SerializeField]
    private Transform playerBodyTransform;
    [SerializeField]
    private Transform playerHeadTransform;

    private float xRotation = 0f;

    void Start()
    {
        //lock cursor at center of the screen
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        //sensitivity
        mouseX *= mouseSensitivityX;
        mouseY *= mouseSensitivityY;
        //indipendent from framerate
        mouseX *= Time.deltaTime;
        mouseY *= Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        playerBodyTransform.Rotate(Vector3.up * mouseX);
        playerHeadTransform.localRotation = Quaternion.Euler(Vector3.right * xRotation);
    }
}
