using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerMove : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem _attackParticle = null;
    [SerializeField]
    private WeaponSystem _subAttackSystem = null;

    private PlayerInput _playerInput = null;
    private Vector2 _moveInput = Vector2.zero;
    private Vector2 _rotateInput = Vector2.zero;

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

        Vector3 movement = new Vector3(_moveInput.x, 0.0f, _moveInput.y);
        transform.Translate(movement * 2.0f * Time.deltaTime);

        Vector3 rotation = new Vector3(0.0f, _rotateInput.x, 0.0f);
        transform.Rotate(rotation * 50.0f * Time.deltaTime);
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

    public void RotateEvent(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _rotateInput = context.ReadValue<Vector2>();
        }
        else if (context.canceled)
        {
            _rotateInput = Vector2.zero;
        }
    }

    public void AttackEvent(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (_attackParticle != null)
            {
                if (!_attackParticle.isPlaying)
                {
                    _attackParticle.Play();
                }
            }
        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {

        }
    }

    public void SubAttackEvent(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (_subAttackSystem != null)
            {
                _subAttackSystem.Play();
            }
        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {

        }
    }
}
