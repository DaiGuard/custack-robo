using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInputManager))]
public class PlayerAssigner : MonoBehaviour
{
    private PlayerInputManager _playerInputManager;

    [System.Serializable]
    private struct SerialPlayerPair
    {
        public string serialNumber;
        public int playerId;
    }

    [SerializeField]
    private List<SerialPlayerPair> _devicePlayerMapping;

    private Dictionary<string, int> _devicePlayerDictionary = new();

    void Start()
    {
        _playerInputManager = GetComponent<PlayerInputManager>();
        if (_playerInputManager == null)
        {
            Debug.LogError("not found PlayerInputManager");
            return;
        }

        foreach (var item in _devicePlayerMapping)
        {
            _devicePlayerDictionary.Add(item.serialNumber, item.playerId);
        }

    }

    void Update()
    {
        if (_playerInputManager != null)
        {
            if (_playerInputManager.playerCount >= _playerInputManager.maxPlayerCount
            && _playerInputManager.maxPlayerCount > 0)
            {
                return;
            }

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.selectButton.wasPressedThisFrame)
                {
                    var serial = gamepad.description.serial;
                    Debug.Log($"{serial} pressed!!");
                }
            }    
        }
    }

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        var device = playerInput.devices.Count > 0 ? playerInput.devices[0] : null;
        if (device == null)
        {
            Debug.LogError("not found device");
            return;
        }

        if (device is Gamepad)
        {
            var serial = device.description.serial;
            if (_devicePlayerDictionary.ContainsKey(serial))
            {
                var playerId = _devicePlayerDictionary[serial];
                Debug.Log($"{serial}: {playerId} add!!");

                return;
            }
        }

        Destroy(playerInput.gameObject);

        return;
    }

    public void OnPlayerLeft(PlayerInput playerInput)
    {
        var device = playerInput.devices.Count > 0 ? playerInput.devices[0] : null;
        if (device == null)
        {
            Debug.LogError("not found device");
            return;
        }

        if (device is Gamepad)
        {
            var serial = device.description.serial;
            if (_devicePlayerDictionary.ContainsKey(serial))
            {
                var playerId = _devicePlayerDictionary[serial];
                Debug.Log($"{serial}: {playerId} add!!");

                return;
            }
        }
    }
}
