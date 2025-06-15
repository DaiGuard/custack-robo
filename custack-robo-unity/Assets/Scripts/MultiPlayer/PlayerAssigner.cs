using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInputManager))]
public class PlayerAssigner : MonoBehaviour
{
    private PlayerInputManager _playerInputManager;

    void Awake()
    {
        _playerInputManager = GetComponent<PlayerInputManager>();
        if (_playerInputManager == null)
        {
            Debug.LogError("not found PlayerInputManager");
            return;
        }

        InputSystem.onDeviceChange += OnDeviceChange;

    }

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        Debug.Log($"{playerInput.devices} joined!");
    }

    public void OnPlayerLeft(PlayerInput playerInput)
    {
        Debug.Log($"{playerInput.devices} left!");
    }

    public void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        
        Debug.Log($"{device.description.serial} change!!");
    }
}
