using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// ミサイル着弾時等に範囲爆発ダメージを発生させるコンポーネント
    /// </summary>
    public class ExplosionArea : MonoBehaviour
    {
        public static void Explode(Vector2 center, float radius, float damage, int ownerRobotId)
        {
            GameObject explObj = new GameObject("ExplosionArea");
            explObj.transform.position = center;

            // 範囲内の全コライダーを検索
            Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius);
            foreach (var col in colliders)
            {
                var health = col.GetComponentInParent<Health>();
                if (health != null)
                {
                    // 自身以外の機体にダメージ
                    var robot = col.GetComponentInParent<Custack.Robot.RobotEntity>();
                    if (robot == null || robot.RobotId != ownerRobotId)
                    {
                        // 爆心地からの距離に応じた減衰
                        float dist = Vector2.Distance(center, col.transform.position);
                        float falloff = Mathf.Clamp01(1.0f - (dist / radius));
                        float finalDmg = damage * Mathf.Max(0.4f, falloff);

                        health.TakeDamage(finalDmg, col.transform.position);
                    }
                }
            }

            HitEffect.CreateHitEffect(center, new Color(1f, 0.4f, 0.1f), radius);
            Destroy(explObj, 0.5f);
        }
    }
}
