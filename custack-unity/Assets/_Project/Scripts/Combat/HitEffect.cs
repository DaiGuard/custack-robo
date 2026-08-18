using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 被弾・爆発時のパーティクル・エフェクト生成クラス (EffectFactory へのブリッジ)
    /// </summary>
    public class HitEffect : MonoBehaviour
    {
        public static void CreateHitEffect(Vector2 position, Color color, float size = 0.5f)
        {
            if (size >= 0.8f)
            {
                EffectFactory.PlayExplosion(position, color, size);
            }
            else
            {
                EffectFactory.PlayHitSparks(position, Vector2.up, color, size, (int)(12 * size + 4));
            }
        }
    }
}
