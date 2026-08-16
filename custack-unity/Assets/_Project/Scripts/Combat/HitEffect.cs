using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 被弾・爆発時のパーティクル・エフェクト生成クラス
    /// </summary>
    public class HitEffect : MonoBehaviour
    {
        public static void CreateHitEffect(Vector2 position, Color color, float size = 0.5f)
        {
            GameObject effectObj = new GameObject("HitEffect");
            effectObj.transform.position = position;

            // スプライトによる簡易エフェクト (Prefabがない場合のフォールバック)
            var sr = effectObj.AddComponent<SpriteRenderer>();
            sr.color = color;

            // 簡易アニメーション & 自動消滅
            Destroy(effectObj, 0.3f);
        }
    }
}
