using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 大型レーザーキャノンの超高速高出力ビーム飛翔体。
    /// 太い多重レイヤーのレーザー光とトレイル、着弾時の大爆発・電撃スパークを発生させます。
    /// </summary>
    public class LaserBeamProjectile : Projectile
    {
        public override void Initialize(int ownerId, Vector2 direction, float speedVal, float dmgVal, float lifeVal, Color col)
        {
            enableTrail = true;
            trailTime = 0.35f;
            trailStartWidth = 0.56f;
            trailEndWidth = 0.08f;

            base.Initialize(ownerId, direction, speedVal, dmgVal, lifeVal, col);
        }

        protected override void OnHitTarget(GameObject target)
        {
            // キャノン着弾時の大爆発・電撃スパーク・衝撃波
            EffectFactory.PlayLaserImpact(transform.position, bulletColor);
            Destroy(gameObject);
        }

        protected override void OnHitObstacle()
        {
            EffectFactory.PlayLaserImpact(transform.position, bulletColor * 0.7f);
            Destroy(gameObject);
        }
    }
}
