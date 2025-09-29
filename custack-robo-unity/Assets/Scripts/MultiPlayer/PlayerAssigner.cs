using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;


[RequireComponent(typeof(PlayerInputManager))]
public class PlayerAssigner : MonoBehaviour
{
    private PlayerInputManager _playerInputManager;

    [SerializeField]
    private PlayerInput _player1Input;

    [SerializeField]
    private PlayerInput _player2Input;

    [SerializeField]
    private PlayerInput _player3Input;

    [SerializeField]
    private List<SerialPlayerPair> _devicePlayerMapping;

    [SerializeField]
    private PlayerTransform _player1Transform;
    [SerializeField]
    private PlayerTransform _player2Transform;
    [SerializeField]
    private PlayerTransform _player3Transform;


    private Dictionary<string, int> _playerIdPairs = new();    
    private List<PlayerInput> _connectedPlayer = new();


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
            _playerIdPairs.Add(item.serialNumber, item.playerId);
        }
    }

    void Update()
    {
        if (_playerInputManager != null)
        {
            // 接続されているGamepadを確認
            foreach (var gamepad in Gamepad.all)
            {
                // SELECTボタンを押しているか確認
                if (gamepad.selectButton.wasPressedThisFrame)
                {
                    var serial = gamepad.description.serial;

                    // 許可シリアル番号を持つデバイスを追加
                    if (_playerIdPairs.ContainsKey(serial))
                    {
                        var playerId = _playerIdPairs[serial];

                        // switch (playerId)
                        // {
                        //     case 1:
                        //         _player1Input.user.UnpairDevices();
                        //         InputUser.PerformPairingWithDevice(gamepad, _player1Input.user);
                        //         break;
                        //     case 2:
                        //         _player2Input.user.UnpairDevices();
                        //         InputUser.PerformPairingWithDevice(gamepad, _player2Input.user);
                        //         break;
                        //     case 3:
                        //         _player3Input.user.UnpairDevices();
                        //         InputUser.PerformPairingWithDevice(gamepad, _player3Input.user);
                        //         break;
                        //     default:
                        //         break;
                        // }

                        // var playerInput = _playerInputManager.JoinPlayer(
                        //     playerId, -1, null, gamepad);

                        // // 接続済みプレイヤーを確認する
                        // if (playerInput != null)
                        // {
                        //     _connectedPlayer.Add(playerInput);
                        // }
                    }
                }
            }

            // // 切断されたGamepadを確認
                // for(var i=_connectedPlayer.Count; i>0; i--)
                // {
                //     var playerInput = _connectedPlayer[i-1];
                //     if (!playerInput.devices.Any())
                //     {
                //         Destroy(playerInput.gameObject);
                //         _connectedPlayer.RemoveAt(i-1);
                //     }
                // }
            }
    }

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        Debug.Log($"player joined: {playerInput.user.id}");
    }

    public void OnPlayerLeft(PlayerInput playerInput)
    {
        Debug.Log($"player left: {playerInput.user.id}");
    }
}
