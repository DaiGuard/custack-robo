using UnityEngine;
using UnityEngine.UI;
using Custack.Combat;
using Custack.Robot;

namespace Custack.UI
{
    /// <summary>
    /// 対戦画面の HUD 表示（P1/P2 HPゲージ、装備状態、勝敗メッセージ）コンポーネント。
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        public static BattleHUD Instance { get; private set; }

        [Header("Player 1 UI")]
        public Image p1HpBarFill;
        public Text p1HpText;
        public Text p1EquipmentText;

        [Header("Player 2 UI")]
        public Image p2HpBarFill;
        public Text p2HpText;
        public Text p2EquipmentText;

        [Header("勝敗アナウンス UI")]
        public GameObject winnerBannerObject;
        public Text winnerText;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (winnerBannerObject != null) winnerBannerObject.SetActive(false);
        }

        public void UpdatePlayerHp(int playerIndex, float currentHp, float maxHp)
        {
            float ratio = Mathf.Clamp01(currentHp / maxHp);

            if (playerIndex == 0)
            {
                if (p1HpBarFill != null) p1HpBarFill.fillAmount = ratio;
                if (p1HpText != null) p1HpText.text = $"P1 HP: {Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
            }
            else if (playerIndex == 1)
            {
                if (p2HpBarFill != null) p2HpBarFill.fillAmount = ratio;
                if (p2HpText != null) p2HpText.text = $"P2 HP: {Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
            }
        }

        public void UpdatePlayerEquipment(int playerIndex, string legName, string rightWeapon, string leftWeapon)
        {
            string equipStr = $"Leg: {legName}\n[R]: {rightWeapon} | [L]: {leftWeapon}";
            if (playerIndex == 0 && p1EquipmentText != null) p1EquipmentText.text = equipStr;
            if (playerIndex == 1 && p2EquipmentText != null) p2EquipmentText.text = equipStr;
        }

        public void ShowWinner(string winnerName)
        {
            if (winnerBannerObject != null)
            {
                winnerBannerObject.SetActive(true);
                if (winnerText != null) winnerText.text = $"{winnerName} VICTORY!";
            }
        }

        public void HideWinner()
        {
            if (winnerBannerObject != null)
            {
                winnerBannerObject.SetActive(false);
            }
        }
    }
}
