namespace Custack.Terrain
{
    /// <summary>
    /// フィールド上の地形タイプ定義
    /// </summary>
    public enum TerrainType
    {
        Normal,   // 平地 (標準)
        Forest,   // 森 (視界減衰、減速)
        Mud,      // 泥 / 沼 (大減速、足取られ)
        Ice,      // 氷 (スリップ、低摩擦)
        Lava      // 溶岩 / ダメージ床 (スリップダメージ)
    }
}
