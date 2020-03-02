using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Range(1f, 10f)]
    private float speed;

    [SerializeField]
    private CharacterController characterController;    

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movementVector = horizontal * transform.right + vertical * transform.forward;
        movementVector *= speed;
        movementVector *= Time.deltaTime;

        characterController.Move(movementVector);
    }
}
