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
        public bool overrideEquipment = true;

        [SerializeField]
        private LegDeviceType legType = LegDeviceType.Omni;

        [SerializeField]
        private ArmDeviceType rightArmType = ArmDeviceType.Pistol;

        [SerializeField]
        private ArmDeviceType leftArmType = ArmDeviceType.Gatling;

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

            LegDeviceType newLeg = Enum.IsDefined(typeof(LegDeviceType), legId) ? (LegDeviceType)legId : LegDeviceType.Omni;
            ArmDeviceType newRArm = Enum.IsDefined(typeof(ArmDeviceType), rightArmId) ? (ArmDeviceType)rightArmId : ArmDeviceType.Pistol;
            ArmDeviceType newLArm = Enum.IsDefined(typeof(ArmDeviceType), leftArmId) ? (ArmDeviceType)leftArmId : ArmDeviceType.Gatling;

            if (legType != newLeg || rightArmType != newRArm || leftArmType != newLArm)
            {
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
