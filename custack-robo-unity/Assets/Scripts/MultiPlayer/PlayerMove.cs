using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerMove : MonoBehaviour
{
    [SerializeField]
    private GameObject _attackEffectPrefab;
    
    private PlayerInput _playerInput;
    private Vector2 _moveInput;

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_playerInput != null)
        {
            print($"player start: {_playerInput.user.id}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_playerInput == null)
        {
            return;
        }

        Vector3 movement = new Vector3(_moveInput.x, _moveInput.y, 0f);
        transform.Translate(movement * 2.0f * Time.deltaTime);
    }

    public void MoveEvent(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _moveInput = context.ReadValue<Vector2>();
        }
        else if (context.canceled)
        {
            _moveInput = Vector2.zero;
        }
    }

    public void AttackEvent(InputAction.CallbackContext context)
    {
        if (context.started)
        {

        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {

        }
    }
}
