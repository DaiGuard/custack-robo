using System;
using System.Collections.Generic;
using UnityEngine;

namespace Custack.Terrain
{
    /// <summary>
    /// フィールド上の地形マップ種別
    /// </summary>
    public enum MapType
    {
        Forest = 0,   // 🌲 森マップ (Normal, Forest, Mud)
        Snow = 1,     // ❄️ 雪山マップ (Normal, Ice, Mud/深雪)
        City = 2,     // 🏙️ 市街地マップ (Normal/幹線道路, Forest/瓦礫, Lava/高圧ハザード)
        Volcano = 3   // 🌋 火山マップ (Normal, Mud/火山灰, Lava/マグマ)
    }

    /// <summary>
    /// 4種類のプロジェクション地形マップ（森、雪山、市街地、火山）を統括し、
    /// 管理画面やショートカットキーからの動的切り替えを制御するマネージャー。
    /// </summary>
    public class TerrainMapManager : MonoBehaviour
    {
        public static TerrainMapManager Instance { get; private set; }

        [Header("マップ設定")]
        public MapType currentMapType = MapType.Forest;

        [Header("各マップのルート GameObject")]
        public GameObject forestMapObject;
        public GameObject snowMapObject;
        public GameObject cityMapObject;
        public GameObject volcanoMapObject;

        public event Action<MapType> OnMapChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            SwitchMap(currentMapType);
        }

        /// <summary>
        /// マップを指定の種別に切り替える
        /// </summary>
        public void SwitchMap(MapType mapType)
        {
            currentMapType = mapType;

            if (forestMapObject != null) forestMapObject.SetActive(mapType == MapType.Forest);
            if (snowMapObject != null) snowMapObject.SetActive(mapType == MapType.Snow);
            if (cityMapObject != null) cityMapObject.SetActive(mapType == MapType.City);
            if (volcanoMapObject != null) volcanoMapObject.SetActive(mapType == MapType.Volcano);

            // 地形マネージャーのゾーンリストを即座に再構築
            if (TerrainManager.Instance != null)
            {
                TerrainManager.Instance.RefreshZones();
            }

            OnMapChanged?.Invoke(mapType);
            Debug.Log($"<color=#00FF88>[TerrainMapManager]</color> 🗺️ マップを切り替えました: <b>{GetMapDisplayName(mapType)}</b>");
        }

        public string GetMapDisplayName(MapType mapType)
        {
            switch (mapType)
            {
                case MapType.Forest: return "🌲 森マップ (Forest Arena)";
                case MapType.Snow: return "❄️ 雪山マップ (Snow Arena)";
                case MapType.City: return "🏙️ 市街地マップ (Cyber City)";
                case MapType.Volcano: return "🌋 火山マップ (Volcano Arena)";
                default: return mapType.ToString();
            }
        }

        public string GetMapDescription(MapType mapType)
        {
            switch (mapType)
            {
                case MapType.Forest:
                    return "【配置地形】Normal (平地) / Forest (森林 50%減速) / Mud (泥沼 30%大減速)";
                case MapType.Snow:
                    return "【配置地形】Normal (平地) / Ice (氷原 スリップ大) / Mud (深雪 40%減速)";
                case MapType.City:
                    return "【配置地形】Normal (幹線道路 最高速) / Forest (瓦礫 40%減速) / Lava (高圧電線 毎秒30ダメ)";
                case MapType.Volcano:
                    return "【配置地形】Normal (岩盤) / Mud (火山灰 30%減速) / Lava (マグマ 毎秒25ダメ)";
                default:
                    return "";
            }
        }
    }
}
