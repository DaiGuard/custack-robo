using UnityEngine;

public class WeaponHiit : MonoBehaviour
{
    private AudioSource audioSource;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // AudioSourceコンポーネントを取得
        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {

    }
    
    private void OnParticleCollision(GameObject other)
    {
        // サウンドが設定されていれば再生する
        if (audioSource != null)
        {
            // PlayOneShotを使うと、ヒットが連続しても音が途切れず再生される
            audioSource.PlayOneShot(audioSource.clip);
        }

        // どのパーティクルシステムから来たかデバッグ表示することも可能
        // Debug.Log(other.name + " のパーティクルが当たりました！");
    }
}
