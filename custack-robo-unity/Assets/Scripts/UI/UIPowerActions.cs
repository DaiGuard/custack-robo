using UnityEngine;

public class UIPowerActions : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnShutdown()
    {
        Application.Quit();
        
#if UNITY_EDITOR
        // エディタで実行している場合のみ、Playモードを停止します
        UnityEditor.EditorApplication.isPlaying = false; // これはUnityエディタの機能であり、最終ビルドには含まれません
#endif        
    }
}
