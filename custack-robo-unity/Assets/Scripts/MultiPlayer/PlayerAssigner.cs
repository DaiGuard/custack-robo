using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInputManager))]
public class PlayerAssigner : MonoBehaviour
{
    private PlayerInputManager _playerInputManager;

    [SerializeField]
    GameObject _playerPrefab;

    [SerializeField]
    private List<SerialPlayerPair> _devicePlayerMapping;

    [SerializeField]
    private PlayerTransform _player1Transform;
    [SerializeField]
    private PlayerTransform _player2Transform;


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
        _playerInputManager.playerPrefab = _playerPrefab;

        foreach (var item in _devicePlayerMapping)
        {
            _playerIdPairs.Add(item.serialNumber, item.playerId);
        }
    }

    void Update()
    {
        if (_playerInputManager != null)
        {
            // // プレイヤー最大数を超えていないか確認
            // if (_playerInputManager.playerCount >= _playerInputManager.maxPlayerCount
            // && _playerInputManager.maxPlayerCount > 0)
            // {
            //     return;
            // }

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

                        var playerInput = _playerInputManager.JoinPlayer(
                            playerId, -1, null, gamepad);

                        // 接続済みプレイヤーを確認する
                        if (playerInput != null)
                        {
                            _connectedPlayer.Add(playerInput);
                        }
                    }
                }
            }

            // 切断されたGamepadを確認
            for(var i=_connectedPlayer.Count; i>0; i--)
            {
                var playerInput = _connectedPlayer[i-1];
                if (!playerInput.devices.Any())
                {
                    Destroy(playerInput.gameObject);
                    _connectedPlayer.RemoveAt(i-1);
                }
            }
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
