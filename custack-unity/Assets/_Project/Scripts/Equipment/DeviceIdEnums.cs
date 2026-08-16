namespace Custack.Equipment
{
    /// <summary>
    /// アームユニットのデバイスID定義
    /// </summary>
    public enum ArmDeviceType : byte
    {
        None = 0x00,
        Pistol = 0x01,   // 0x01: ピストル (単発・高速・長射程)
        Gatling = 0x02,  // 0x02: ガトリング (連射・短射程・弾幕)
        Missile = 0x03   // 0x03: ミサイル (緩やかなホーミング・範囲爆発)
    }

    /// <summary>
    /// 脚ユニットのデバイスID定義
    /// </summary>
    public enum LegDeviceType : byte
    {
        None = 0x00,
        Omni = 0x01,     // 0x01: 全方向 (4輪オムニ: 全方向スライド、悪路減速大)
        Tire = 0x02,     // 0x02: タイヤ (前後高速・旋回鋭い、泥でスタック)
        Crawler = 0x03   // 0x03: キャタピラ (超信地旋回、悪路走破性最強)
    }
}
