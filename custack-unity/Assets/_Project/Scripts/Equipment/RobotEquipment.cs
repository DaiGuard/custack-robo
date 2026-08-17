using System;
using UnityEngine;

namespace Custack.Equipment
{
    /// <summary>
    /// ロボット1台の装備状態（脚・右腕・左腕）を保持・管理するコンポーネント。
    /// インスペクター上での手動装備テストおよび実機からの自動ID取得の双方に対応。
    /// </summary>
    public class RobotEquipment : MonoBehaviour
    {
        [Header("インスペクター手動装備テスト設定")]
        [Tooltip("true の場合、実機からの自動取得を無視してインスペクターで指定した装備を使用")]
        public bool overrideEquipment = false;

        [SerializeField]
        private LegDeviceType legType = LegDeviceType.Omni;

        [SerializeField]
        private ArmDeviceType rightArmType = ArmDeviceType.Gatling;

        [SerializeField]
        private ArmDeviceType leftArmType = ArmDeviceType.Sword;

        [Header("現在の有効パラメータ")]
        public LegMovementConfig CurrentLegConfig { get; private set; }
        public ArmWeaponConfig CurrentRightArmConfig { get; private set; }
        public ArmWeaponConfig CurrentLeftArmConfig { get; private set; }

        public LegDeviceType LegType => legType;
        public ArmDeviceType RightArmType => rightArmType;
        public ArmDeviceType LeftArmType => leftArmType;

        // 装備変更通知イベント
        public event Action OnEquipmentChanged;

        private LegDeviceType lastLegType;
        private ArmDeviceType lastRightArm;
        private ArmDeviceType lastLeftArm;

        void Awake()
        {
            ApplyConfigurations();
        }

        void OnValidate()
        {
            // インスペクターで値が変わった場合に即時反映
            if (legType != lastLegType || rightArmType != lastRightArm || leftArmType != lastLeftArm)
            {
                ApplyConfigurations();
            }
        }

        /// <summary>
        /// 実機から取得したデバイスIDを反映（overrideEquipment が false の場合に適用）
        /// </summary>
        public void SetFromHardware(byte legId, byte rightArmId, byte leftArmId)
        {
            if (overrideEquipment) return;

            // 0x00 (None/未検出) の場合は既存設定を維持、0x01以上で定義済みの場合に更新
            LegDeviceType newLeg = Enum.IsDefined(typeof(LegDeviceType), legId) && legId != 0 ? (LegDeviceType)legId : legType;
            ArmDeviceType newRArm = Enum.IsDefined(typeof(ArmDeviceType), rightArmId) && rightArmId != 0 ? (ArmDeviceType)rightArmId : rightArmType;
            ArmDeviceType newLArm = Enum.IsDefined(typeof(ArmDeviceType), leftArmId) && leftArmId != 0 ? (ArmDeviceType)leftArmId : leftArmType;

            if (legType != newLeg || rightArmType != newRArm || leftArmType != newLArm)
            {
                Debug.Log($"<color=#00FF88><b>[RobotEquipment]</b></color> 🤖 Robot [{gameObject.name}] DeviceID Updated from Hardware! Leg: 0x{legId:X2} ({newLeg}), ArmR: 0x{rightArmId:X2} ({newRArm}), ArmL: 0x{leftArmId:X2} ({newLArm})");
                legType = newLeg;
                rightArmType = newRArm;
                leftArmType = newLArm;
                ApplyConfigurations();
            }
        }

        /// <summary>
        /// 外部スクリプトから装備を動的設定
        /// </summary>
        public void SetEquipment(LegDeviceType newLeg, ArmDeviceType newRight, ArmDeviceType newLeft)
        {
            legType = newLeg;
            rightArmType = newRight;
            leftArmType = newLeft;
            ApplyConfigurations();
        }

        private void ApplyConfigurations()
        {
            CurrentLegConfig = LegMovementConfig.CreateDefault(legType);
            CurrentRightArmConfig = ArmWeaponConfig.CreateDefault(rightArmType);
            CurrentLeftArmConfig = ArmWeaponConfig.CreateDefault(leftArmType);

            lastLegType = legType;
            lastRightArm = rightArmType;
            lastLeftArm = leftArmType;

            OnEquipmentChanged?.Invoke();
        }
    }
}
