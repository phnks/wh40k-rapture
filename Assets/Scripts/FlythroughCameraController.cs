using UnityEngine;

public class FlythroughCameraController : MonoBehaviour
{
    public float moveSpeed = 80f;        // Normal speed of the camera movement
    public float lookSpeed = 5f;         // Speed of the camera rotation
    public float fastMoveMultiplier = 2f; // Multiplier for faster movement

    private float rotationX = 0.0f;      // Rotation around the X axis
    private float rotationY = 0.0f;      // Rotation around the Y axis
    private bool isFastMode = false;     // Boolean to check if fast mode is enabled
    private bool isCursorLocked = true;  // Boolean to check if cursor is locked

    void Start()
    {
        LockCursor();
    }

    void Update()
    {
        // Toggle fast mode on Shift press
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            isFastMode = !isFastMode; // Toggle fast mode
        }

        // Handle camera movement only if the cursor is locked
        if (isCursorLocked)
        {
            // Mouse look
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            rotationX += mouseX;
            rotationY -= mouseY;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f); // Limit vertical look

            transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0);

            // Movement speed calculation based on toggle
            float currentMoveSpeed = isFastMode ? moveSpeed * fastMoveMultiplier : moveSpeed;

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            // Move forward/backward
            if (Input.GetKey(KeyCode.W))
            {
                transform.position += forward * currentMoveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.S))
            {
                transform.position -= forward * currentMoveSpeed * Time.deltaTime;
            }

            // Move left/right
            if (Input.GetKey(KeyCode.A))
            {
                transform.position -= right * currentMoveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.D))
            {
                transform.position += right * currentMoveSpeed * Time.deltaTime;
            }

            // Move up
            if (Input.GetKey(KeyCode.Space))
            {
                transform.position += up * currentMoveSpeed * Time.deltaTime;
            }

            // Move down
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                transform.position -= up * currentMoveSpeed * Time.deltaTime;
            }
        }

        // Toggle cursor lock state with Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (isCursorLocked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;  // Lock the cursor to the center of the screen
        Cursor.visible = true;                     // Make the cursor visible for clicking
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;    // Unlock the cursor
        Cursor.visible = true;                     // Show the cursor for free movement
        isCursorLocked = false;
    }
}

