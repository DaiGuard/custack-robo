using UnityEngine;

public class DisplayInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeDisplays()
    {
        Debug.Log("マルチディスプレイの初期化を開始します...");
        Debug.Log("接続ディスプレイ数: " + Display.displays.Length);

        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
            Debug.Log($"ディスプレイ {i+1} を有効にしました");
        }
    }
}
