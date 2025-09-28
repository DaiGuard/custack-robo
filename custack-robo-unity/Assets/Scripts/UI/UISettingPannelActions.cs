using UnityEngine;

public class SettingPannelActions : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnSettingPannelSwitch()
    {
        var active = gameObject.activeSelf;
        gameObject.SetActive(!active);
    }
}
