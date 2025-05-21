using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerMove : MonoBehaviour
{
    private PlayerInput playerInput;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (playerInput != null)
        {
            print($"player start: {playerInput.user.id}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playerInput == null)
        {
            return;
        }

        Vector2 moveInput = playerInput.actions["Move"].ReadValue<Vector2>();
        Vector3 movement = new Vector3(moveInput.x, moveInput.y, 0f);
        transform.Translate(movement * 2.0f * Time.deltaTime);
    }        
}
