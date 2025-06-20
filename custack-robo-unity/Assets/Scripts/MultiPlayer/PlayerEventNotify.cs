using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerEventNotify : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        print($"{playerInput.user.index} joined!");
    }


    public void OnPlayerLeft(PlayerInput playerInput)
    {
        print($"{playerInput.user.index} left!");
    }
}
