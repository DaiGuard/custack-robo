using System;
using System.Collections.Generic;
using UnityEngine;

namespace Custack.Audio
{
    public enum SoundEffectType
    {
        None = 0,
        ShotGatling = 1,     // 0x01: ガトリング発射音
        ShotLaser = 2,       // 0x03: 大型レーザー発射音
        SwordSlash = 3,      // 0x02: 近接ソード斬撃音
        HitDamage = 4,       // 通常被弾音
        HitShield = 5,       // 無敵/バリア被弾音
        Explosion = 6,       // 爆発音
        Stun = 7,            // スタン・放電音
        Defeat = 8,          // 機体大破・撃破音
        TerrainMud = 9,      // 泥沼突入音
        TerrainIce = 10,     // 氷上スリップ音
        TerrainLava = 11,    // 溶岩ダメージ音
        GameStart = 12,      // ラウンド開始音
        Victory = 13,        // 勝利ファンファーレ
        LockOn = 14          // ターゲット切替音
    }

    /// <summary>
    /// CuStack-Robo オーディオ統括マネージャー。
    /// 効果音 (SE) のプール再生・ピッチ揺らぎ・音量バランス、BGM ループ再生、
    /// および音源未設定時のプロシージャル波形自動合成フォールバックを完備。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("マスター音量設定")]
        [Range(0f, 1f)] public float masterVolume = 1.0f;
        [Range(0f, 1f)] public float seVolume = 0.85f;
        [Range(0f, 1f)] public float bgmVolume = 0.55f;

        [Header("SE 同時発音プール数")]
        [SerializeField] private int seVoicePoolSize = 16;

        [Header("BGM 自動再生")]
        public bool playBgmOnStart = true;
        public AudioClip battleBgmClip;

        [Header("SE クリップ個別登録 (未登録時は自動生成/ロード)")]
        public AudioClip seShotGatling;
        public AudioClip seShotLaser;
        public AudioClip seSwordSlash;
        public AudioClip seHitDamage;
        public AudioClip seHitShield;
        public AudioClip seExplosion;
        public AudioClip seStun;
        public AudioClip seDefeat;
        public AudioClip seTerrainMud;
        public AudioClip seTerrainIce;
        public AudioClip seTerrainLava;
        public AudioClip seGameStart;
        public AudioClip seVictory;
        public AudioClip seLockOn;

        private AudioSource bgmSource;
        private List<AudioSource> seSourcePool = new List<AudioSource>();
        private int currentSePoolIndex = 0;
        private Dictionary<SoundEffectType, AudioClip> clipCache = new Dictionary<SoundEffectType, AudioClip>();

        // SE 発音頻度リミッター（同一SEが超高頻度で鳴りすぎるのを防止）
        private Dictionary<SoundEffectType, float> lastPlayTime = new Dictionary<SoundEffectType, float>();

        private static void EnsureInstance()
        {
            if (Instance == null)
            {
                var existing = FindFirstObjectByType<AudioManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    var go = new GameObject("[AudioManager]");
                    Instance = go.AddComponent<AudioManager>();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            PreloadAudioClips();
        }

        void Start()
        {
            if (playBgmOnStart)
            {
                PlayBattleBGM();
            }
        }

        private void InitializeAudioSources()
        {
            // BGM 用 AudioSource
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = masterVolume * bgmVolume;

            // SE プール用 AudioSource 群
            for (int i = 0; i < seVoicePoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                seSourcePool.Add(src);
            }
        }

        private void PreloadAudioClips()
        {
            RegisterClip(SoundEffectType.ShotGatling, seShotGatling, "se_shot_gatling");
            RegisterClip(SoundEffectType.ShotLaser, seShotLaser, "se_shot_laser");
            RegisterClip(SoundEffectType.SwordSlash, seSwordSlash, "se_sword_slash");
            RegisterClip(SoundEffectType.HitDamage, seHitDamage, "se_hit_damage");
            RegisterClip(SoundEffectType.HitShield, seHitShield, "se_hit_shield");
            RegisterClip(SoundEffectType.Explosion, seExplosion, "se_explosion");
            RegisterClip(SoundEffectType.Stun, seStun, "se_stun");
            RegisterClip(SoundEffectType.Defeat, seDefeat, "se_defeat");
            RegisterClip(SoundEffectType.TerrainMud, seTerrainMud, "se_terrain_mud");
            RegisterClip(SoundEffectType.TerrainIce, seTerrainIce, "se_terrain_ice");
            RegisterClip(SoundEffectType.TerrainLava, seTerrainLava, "se_terrain_lava");
            RegisterClip(SoundEffectType.GameStart, seGameStart, "se_game_start");
            RegisterClip(SoundEffectType.Victory, seVictory, "se_victory");
            RegisterClip(SoundEffectType.LockOn, seLockOn, "se_lockon");

            if (battleBgmClip == null)
            {
                battleBgmClip = Resources.Load<AudioClip>("Audio/bgm_battle_loop") ?? Resources.Load<AudioClip>("bgm_battle_loop");
            }
        }

        private void RegisterClip(SoundEffectType type, AudioClip clip, string resourceName)
        {
            if (clip != null)
            {
                clipCache[type] = clip;
                return;
            }

            // Resources または動的ロード試行
            var loaded = Resources.Load<AudioClip>($"Audio/{resourceName}") ?? Resources.Load<AudioClip>(resourceName);
            if (loaded != null)
            {
                clipCache[type] = loaded;
            }
            else
            {
                // フォールバック: プロシージャル合成
                clipCache[type] = GenerateFallbackClip(type);
            }
        }

        /// <summary>
        /// 効果音を再生（ピッチ微小ランダム揺らぎ・音量バランス・発音制御付き）
        /// </summary>
        public void PlaySE(SoundEffectType type, float volumeScale = 1.0f, float pitchVariance = 0.06f)
        {
            if (type == SoundEffectType.None) return;

            // 超高頻度（0.03秒以内）の同一SE重複再生を間引き
            float now = Time.time;
            if (lastPlayTime.TryGetValue(type, out float lastTime))
            {
                if (now - lastTime < 0.025f) return;
            }
            lastPlayTime[type] = now;

            if (!clipCache.TryGetValue(type, out var clip) || clip == null)
            {
                clip = GenerateFallbackClip(type);
                clipCache[type] = clip;
            }

            if (clip == null) return;

            // プールから空いている AudioSource を取得
            var src = seSourcePool[currentSePoolIndex];
            currentSePoolIndex = (currentSePoolIndex + 1) % seSourcePool.Count;

            src.clip = clip;
            src.volume = Mathf.Clamp01(masterVolume * seVolume * volumeScale);
            src.pitch = 1.0f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            src.Play();
        }

        /// <summary>
        /// BGM 再生開始
        /// </summary>
        public void PlayBGM(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null || bgmSource == null) return;

            bgmSource.clip = clip;
            bgmSource.volume = Mathf.Clamp01(masterVolume * bgmVolume * volumeScale);
            bgmSource.Play();
        }

        public void PlayBattleBGM()
        {
            if (battleBgmClip != null)
            {
                PlayBGM(battleBgmClip);
            }
            else
            {
                // プロシージャル BGM フォールバック
                battleBgmClip = GenerateFallbackBGM();
                PlayBGM(battleBgmClip);
            }
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            if (bgmSource != null) bgmSource.volume = masterVolume * bgmVolume;
        }

        // =========================================================================
        // プロシージャル波形自動合成フォールバック (AudioClip.Create)
        // =========================================================================

        private AudioClip GenerateFallbackClip(SoundEffectType type)
        {
            int sampleRate = 44100;
            float duration = 0.1f;
            switch (type)
            {
                case SoundEffectType.ShotGatling: duration = 0.08f; break;
                case SoundEffectType.ShotLaser: duration = 0.45f; break;
                case SoundEffectType.SwordSlash: duration = 0.22f; break;
                case SoundEffectType.HitDamage: duration = 0.12f; break;
                case SoundEffectType.HitShield: duration = 0.15f; break;
                case SoundEffectType.Explosion: duration = 0.85f; break;
                case SoundEffectType.Stun: duration = 0.35f; break;
                case SoundEffectType.Defeat: duration = 1.2f; break;
                case SoundEffectType.TerrainMud: duration = 0.18f; break;
                case SoundEffectType.TerrainIce: duration = 0.25f; break;
                case SoundEffectType.TerrainLava: duration = 0.28f; break;
                case SoundEffectType.GameStart: duration = 0.5f; break;
                case SoundEffectType.Victory: duration = 1.5f; break;
                case SoundEffectType.LockOn: duration = 0.06f; break;
                default: duration = 0.1f; break;
            }

            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float normT = t / duration;

                switch (type)
                {
                    case SoundEffectType.ShotGatling:
                        float gFreq = 850f * Mathf.Exp(-normT * 6f) + 80f;
                        float gTone = Mathf.Sin(2f * Mathf.PI * gFreq * t);
                        float gNoise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-normT * 12f);
                        samples[i] = (gTone * 0.7f + gNoise * 0.5f) * Mathf.Exp(-normT * 6f);
                        break;

                    case SoundEffectType.ShotLaser:
                        float lFreq = 2600f * Mathf.Pow(1f - normT, 2.5f) + 180f;
                        float lMod = Mathf.Sin(2f * Mathf.PI * 80f * t) * 6f * (1f - normT);
                        samples[i] = Mathf.Sin(2f * Mathf.PI * lFreq * t + lMod) * Mathf.Exp(-normT * 4f);
                        break;

                    case SoundEffectType.SwordSlash:
                        float sNoise = (UnityEngine.Random.value * 2f - 1f) * (1f - normT);
                        float sTone = Mathf.Sin(2f * Mathf.PI * (1000f * (1f - normT) + 300f) * t);
                        samples[i] = (sNoise * 0.6f + sTone * 0.4f) * Mathf.Sin(Mathf.Clamp01(normT / 0.2f) * Mathf.PI * 0.5f) * Mathf.Exp(-normT * 5f);
                        break;

                    case SoundEffectType.Explosion:
                        float eSub = Mathf.Sin(2f * Mathf.PI * (100f * (1f - normT * 0.6f)) * t);
                        float eNoise = (UnityEngine.Random.value * 2f - 1f);
                        samples[i] = (eSub * 0.6f + eNoise * 0.6f) * Mathf.Exp(-normT * 3.5f);
                        break;

                    case SoundEffectType.HitDamage:
                        samples[i] = (Mathf.Sin(2f * Mathf.PI * 450f * t) + (UnityEngine.Random.value * 2f - 1f) * 0.8f) * Mathf.Exp(-normT * 9f);
                        break;

                    case SoundEffectType.HitShield:
                        samples[i] = (Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.4f) * Mathf.Exp(-normT * 7f);
                        break;

                    case SoundEffectType.Stun:
                        float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) > 0 ? 0.5f : -0.5f;
                        float spark = UnityEngine.Random.value > 0.4f ? Mathf.Sin(2f * Mathf.PI * 1600f * t) : 0f;
                        samples[i] = (hum * 0.4f + spark * 0.6f) * Mathf.Exp(-normT * 4f);
                        break;

                    case SoundEffectType.Defeat:
                        float dFreq = 350f * Mathf.Exp(-normT * 3.5f) + 40f;
                        samples[i] = (Mathf.Sin(2f * Mathf.PI * dFreq * t) * 0.5f + (UnityEngine.Random.value * 2f - 1f) * 0.5f) * Mathf.Exp(-normT * 2.5f);
                        break;

                    case SoundEffectType.Victory:
                        float vFreq = normT < 0.3f ? 523.25f : (normT < 0.6f ? 659.25f : 1046.5f);
                        samples[i] = (Mathf.Sin(2f * Mathf.PI * vFreq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * vFreq * 2f * t)) * Mathf.Exp(-normT * 2f);
                        break;

                    case SoundEffectType.LockOn:
                        samples[i] = Mathf.Sin(2f * Mathf.PI * 1600f * t) * Mathf.Exp(-normT * 8f);
                        break;

                    default:
                        samples[i] = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-normT * 6f);
                        break;
                }
            }

            AudioClip clip = AudioClip.Create($"Procedural_{type}", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateFallbackBGM()
        {
            int sampleRate = 44100;
            float duration = 7.38f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                // 4つ打ちキック + 16分ベース
                float kickT = t % 0.4615f;
                float kick = Mathf.Sin(2f * Mathf.PI * (120f * Mathf.Exp(-kickT * 12f) + 45f) * kickT) * Mathf.Exp(-kickT * 6f);

                float stepT = t % 0.1154f;
                float bassFreq = 55f * (1f + ((int)(t / 0.4615f) % 4) * 0.2f);
                float bass = (Mathf.Sin(2f * Mathf.PI * bassFreq * t) > 0 ? 0.3f : -0.3f) * Mathf.Exp(-stepT * 5f);

                samples[i] = Mathf.Clamp(kick * 0.6f + bass * 0.3f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("Procedural_BGM_Loop", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
