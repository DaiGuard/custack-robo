using UnityEngine;

namespace Custack.Input
{
    /// <summary>
    /// 単一プレイヤーのコントローラー入力状態構造体
    /// </summary>
    [System.Serializable]
    public struct PlayerInputCommand
    {
        public Vector2 Move;           // 左スティック: 移動 (X: 左右, Y: 前後)
        public float Omega;            // 右スティック X またはトリガー: 旋回
        public bool ArmRightPressed;   // 右武器ボタン (R1 / 〇)
        public bool ArmRightHeld;      // 右武器ボタン長押し (ガトリング用)
        public bool ArmLeftPressed;    // 左武器ボタン (L1 / □)
        public bool ArmLeftHeld;       // 左武器ボタン長押し
        public bool TargetSwitchPressed; // △ボタン (ホーミング対象切り替え)
        public bool IsConnected;       // コントローラーが接続・有効か
    }
}
