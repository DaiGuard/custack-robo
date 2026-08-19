#!/usr/bin/env python3
"""
CuStack-Robo Unity オーディオアセット生成スクリプト
外部ライブラリ不要（Python 標準ライブラリ: wave, struct, math, random のみ使用）
対戦ゲーム演出用の高品位なプロシージャル SE (効果音) & BGM ループを .wav として生成します。
"""

import os
import wave
import struct
import math
import random

SAMPLE_RATE = 44100

def clamp(val, min_val=-1.0, max_val=1.0):
    return max(min_val, min(max_val, val))

def save_wav(filename, samples, sample_rate=SAMPLE_RATE):
    """32-bit float samples (-1.0 to 1.0) を 16-bit PCM WAV として保存"""
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    with wave.open(filename, 'w') as wf:
        wf.setnchannels(1)       # モノラル
        wf.setsampwidth(2)       # 16-bit
        wf.setframerate(sample_rate)
        
        packed = bytearray()
        for s in samples:
            s_clamped = clamp(s, -0.999, 0.999)
            s_int = int(s_clamped * 32767.0)
            packed.extend(struct.pack('<h', s_int))
        wf.writeframes(packed)
    print(f"Generated: {filename} ({len(samples)/sample_rate:.2f}s)")

# =========================================================================
# 1. 武器・戦闘系 SE
# =========================================================================

def gen_shot_gatling():
    """0x01 ガトリング発射音: 鋭いアタック + 矩形波/ノイズの高速ピッチドロップ"""
    duration = 0.085
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # ピッチドロップ (900Hz -> 100Hz)
        freq = 900.0 * math.exp(-norm_t * 5.0) + 80.0
        phase = 2.0 * math.pi * freq * t
        
        # 矩形波 + サイン波 + ノイズの混成
        tone = 0.6 * (1.0 if math.sin(phase) > 0 else -1.0) + 0.4 * math.sin(phase * 0.5)
        noise = (random.random() * 2.0 - 1.0) * math.exp(-norm_t * 12.0)
        
        # 音量エンベロープ (高速アタック、急峻な指数減衰)
        env = math.exp(-norm_t * 6.5)
        sig = (tone * 0.7 + noise * 0.5) * env
        samples.append(sig * 0.85)
        
    return samples

def gen_shot_laser():
    """0x03 大型レーザーキャノン発射音: 高周波FM変調スイープ + 重厚なエネルギー放出"""
    duration = 0.45
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # チャージ＆発射周波数スイープ (2800Hz -> 180Hz)
        carrier_freq = 2800.0 * (1.0 - norm_t)**2.5 + 160.0
        mod_freq = 180.0 * (1.0 - norm_t) + 40.0
        mod_index = 8.0 * (1.0 - norm_t)
        
        mod = math.sin(2.0 * math.pi * mod_freq * t) * mod_index
        phase = 2.0 * math.pi * carrier_freq * t + mod
        tone = math.sin(phase) + 0.35 * math.sin(phase * 2.0)
        
        # アタック時のエネルギー放電ノイズ
        noise = (random.random() * 2.0 - 1.0) * math.exp(-norm_t * 10.0)
        
        # 低音サブベース (80Hz)
        sub = math.sin(2.0 * math.pi * 80.0 * t) * math.exp(-norm_t * 4.0) * 0.6
        
        env = math.exp(-norm_t * 3.8)
        sig = (tone * 0.65 + noise * 0.35 + sub * 0.45) * env
        samples.append(sig * 0.9)
        
    return samples

def gen_sword_slash():
    """0x02 近接ソード斬撃音: 高速風切りノイズ + エメラルド光刃のレゾナント残響"""
    duration = 0.22
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    filter_val = 0.0
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # アタック -> ピーク -> リリース
        if norm_t < 0.15:
            env = norm_t / 0.15
        else:
            env = math.exp(-(norm_t - 0.15) * 6.0)
            
        # フィルタスイープによる風切り音
        cutoff = 0.05 + 0.45 * (1.0 - norm_t)**1.5
        raw_noise = random.random() * 2.0 - 1.0
        filter_val += cutoff * (raw_noise - filter_val)
        
        # 光刃の金属・シンセ成分 (1200Hz -> 400Hz)
        freq = 1200.0 * (1.0 - norm_t)**1.8 + 250.0
        blade_tone = math.sin(2.0 * math.pi * freq * t) * math.exp(-norm_t * 5.0)
        
        sig = (filter_val * 0.7 + blade_tone * 0.5) * env
        samples.append(sig * 0.88)
        
    return samples

# =========================================================================
# 2. 被弾・爆発・撃破系 SE
# =========================================================================

def gen_hit_damage():
    """通常被弾音: メタリックな装甲衝突＋衝撃"""
    duration = 0.12
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # メタリックな周波数成分 (480Hz, 820Hz, 1350Hz)
        tone = (math.sin(2.0 * math.pi * 480.0 * t) * 0.5 +
                math.sin(2.0 * math.pi * 820.0 * t) * 0.35 +
                math.sin(2.0 * math.pi * 1350.0 * t) * 0.2)
        
        noise = (random.random() * 2.0 - 1.0) * math.exp(-norm_t * 18.0)
        env = math.exp(-norm_t * 9.0)
        sig = (tone * 0.6 + noise * 0.6) * env
        samples.append(sig * 0.85)
        
    return samples

def gen_hit_shield():
    """無敵時間 / シールド被弾音: 高音バリア跳弾チャイム"""
    duration = 0.15
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # 高音ベル (1800Hz & 2400Hz)
        tone = (math.sin(2.0 * math.pi * 1800.0 * t) * 0.6 +
                math.sin(2.0 * math.pi * 2400.0 * t) * 0.4)
        env = math.exp(-norm_t * 7.0)
        samples.append(tone * env * 0.75)
        
    return samples

def gen_explosion():
    """大爆発音: 重厚な低域ランブル (40-90Hz) + ホワイトノイズ減衰"""
    duration = 0.85
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    lowpass = 0.0
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # 低周波ランブルスイープ
        sub_freq = 110.0 * (1.0 - norm_t * 0.7)
        sub = math.sin(2.0 * math.pi * sub_freq * t) * 0.7
        
        # 爆発ノイズ (ローパスフィルタ)
        cutoff = 0.15 * math.exp(-norm_t * 2.5) + 0.01
        raw_noise = random.random() * 2.0 - 1.0
        lowpass += cutoff * (raw_noise - lowpass)
        
        env = math.exp(-norm_t * 3.2)
        sig = (sub * 0.6 + lowpass * 0.7) * env
        # ソフトクリッピング歪み
        distorted = math.tanh(sig * 1.8)
        samples.append(distorted * 0.95)
        
    return samples

def gen_stun():
    """スタン音: 放電・電磁サージパルス (60Hz/120Hz ハム + ランダムスパーク)"""
    duration = 0.38
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # 120Hz パルス波
        hum = 1.0 if math.sin(2.0 * math.pi * 120.0 * t) > 0 else -1.0
        # 揺らぐ高周波スパーク
        spark_freq = 1400.0 + 800.0 * math.sin(2.0 * math.pi * 35.0 * t)
        spark = math.sin(2.0 * math.pi * spark_freq * t) * (1.0 if random.random() > 0.3 else 0.0)
        
        env = math.exp(-norm_t * 4.0)
        sig = (hum * 0.4 + spark * 0.5) * env
        samples.append(sig * 0.8)
        
    return samples

def gen_defeat():
    """機体撃破音: ピッチダウン + 連続大破爆発"""
    duration = 1.3
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    lowpass = 0.0
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # 連続爆発トリガー
        burst = math.sin(2.0 * math.pi * 8.0 * t)
        noise = (random.random() * 2.0 - 1.0)
        
        cutoff = 0.2 * math.exp(-norm_t * 1.5) + 0.02
        lowpass += cutoff * (noise - lowpass)
        
        # 機体ダウン・パワー降下トーン
        power_freq = 400.0 * math.exp(-norm_t * 4.0) + 30.0
        tone = math.sin(2.0 * math.pi * power_freq * t) * 0.5
        
        env = math.exp(-norm_t * 2.2)
        sig = (lowpass * 0.65 + tone * 0.45) * env * (0.8 + 0.2 * burst)
        samples.append(math.tanh(sig * 1.5) * 0.95)
        
    return samples

# =========================================================================
# 3. 地形効果系 SE
# =========================================================================

def gen_terrain_mud():
    """泥沼侵入・減速音: グチャッとした低音ノイズ"""
    duration = 0.18
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    filter_val = 0.0
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        cutoff = 0.06 + 0.08 * math.sin(2.0 * math.pi * 20.0 * t)
        noise = random.random() * 2.0 - 1.0
        filter_val += cutoff * (noise - filter_val)
        
        env = math.exp(-norm_t * 5.0)
        samples.append(filter_val * env * 0.8)
        
    return samples

def gen_terrain_ice():
    """氷上スリップ音: スキッド・摩擦高音ノイズ"""
    duration = 0.25
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # ハイパス風高音ノイズ
        noise = random.random() * 2.0 - 1.0
        shimmer = math.sin(2.0 * math.pi * 3200.0 * t) * 0.3
        env = (1.0 - norm_t) * (0.6 + 0.4 * math.sin(2.0 * math.pi * 15.0 * t))
        samples.append((noise * 0.5 + shimmer * 0.5) * env * 0.65)
        
    return samples

def gen_terrain_lava():
    """溶岩・電磁サージダメージ音: ジリジリした熱スパーク"""
    duration = 0.28
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        crackle = (random.random() * 2.0 - 1.0) if random.random() < 0.25 else 0.0
        low_hum = math.sin(2.0 * math.pi * 90.0 * t) * 0.4
        env = math.exp(-norm_t * 4.0)
        samples.append((crackle * 0.7 + low_hum * 0.3) * env * 0.75)
        
    return samples

# =========================================================================
# 4. UI / システム系 SE
# =========================================================================

def gen_game_start():
    """ラウンド開始チャイム: 明るい2音 (880Hz -> 1760Hz)"""
    duration = 0.5
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        if t < 0.2:
            tone = math.sin(2.0 * math.pi * 880.0 * t) * math.exp(-t * 10.0)
        else:
            t2 = t - 0.2
            tone = math.sin(2.0 * math.pi * 1760.0 * t2) * math.exp(-t2 * 6.0)
        samples.append(tone * 0.8)
        
    return samples

def gen_victory():
    """勝利ファンファーレ: サイバーメジャートライアドコード (C5 -> E5 -> G5 -> C6)"""
    duration = 1.6
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    notes = [523.25, 659.25, 783.99, 1046.50] # C5, E5, G5, C6
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        sig = 0.0
        for idx, freq in enumerate(notes):
            note_start = idx * 0.12
            if t >= note_start:
                dt = t - note_start
                env = math.exp(-dt * (2.0 if idx == len(notes)-1 else 3.5))
                tone = math.sin(2.0 * math.pi * freq * dt) + 0.3 * math.sin(2.0 * math.pi * freq * 2.0 * dt)
                sig += tone * env * 0.35
        samples.append(sig * 0.85)
        
    return samples

def gen_lockon():
    """ロックオンターゲット切替音: ピッという短い高音 (1600Hz)"""
    duration = 0.06
    n_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    for i in range(n_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        tone = math.sin(2.0 * math.pi * 1600.0 * t)
        env = math.exp(-norm_t * 6.0)
        samples.append(tone * env * 0.75)
        
    return samples

# =========================================================================
# 5. サイバーパンク バトル BGM ループ (130 BPM / 4小節 / 7.38秒)
# =========================================================================

def gen_battle_bgm():
    """サイバーパンク・メカバトル風のシンセベース＆キック＆アルペジオ BGM ループ"""
    bpm = 130.0
    beat_sec = 60.0 / bpm
    bar_sec = beat_sec * 4.0
    duration = bar_sec * 4.0 # 4小節ループ (約7.38秒)
    n_samples = int(SAMPLE_RATE * duration)
    samples = [0.0] * n_samples
    
    # 1. 4つ打ちキックドラム
    n_beats = int(duration / beat_sec)
    for b in range(n_beats):
        beat_t = b * beat_sec
        kick_dur = 0.18
        kick_samples = int(SAMPLE_RATE * kick_dur)
        for ki in range(kick_samples):
            idx = int(beat_t * SAMPLE_RATE) + ki
            if idx >= n_samples: break
            t = ki / SAMPLE_RATE
            norm_t = t / kick_dur
            freq = 150.0 * math.exp(-norm_t * 12.0) + 45.0
            tone = math.sin(2.0 * math.pi * freq * t)
            env = math.exp(-norm_t * 6.0)
            samples[idx] += tone * env * 0.65
            
    # 2. スネア / クラップ (2拍目, 4拍目)
    for b in range(n_beats):
        if b % 2 == 1:
            beat_t = b * beat_sec
            snare_dur = 0.15
            snare_samples = int(SAMPLE_RATE * snare_dur)
            for si in range(snare_samples):
                idx = int(beat_t * SAMPLE_RATE) + si
                if idx >= n_samples: break
                t = si / SAMPLE_RATE
                norm_t = t / snare_dur
                noise = (random.random() * 2.0 - 1.0)
                body = math.sin(2.0 * math.pi * 180.0 * t) * 0.4
                env = math.exp(-norm_t * 8.0)
                samples[idx] += (noise * 0.6 + body * 0.4) * env * 0.4
                
    # 3. 16分音符シンセベースライン (Aマイナー)
    # A1(55Hz), C2(65.4Hz), D2(73.4Hz), E2(82.4Hz), G2(98Hz)
    bass_scale = [55.0, 55.0, 65.4, 55.0, 73.4, 55.0, 82.4, 65.4]
    sixteenth_sec = beat_sec / 4.0
    n_sixteenths = int(duration / sixteenth_sec)
    for step in range(n_sixteenths):
        step_t = step * sixteenth_sec
        note_freq = bass_scale[step % len(bass_scale)]
        note_dur = sixteenth_sec * 0.85
        note_samples = int(SAMPLE_RATE * note_dur)
        for ni in range(note_samples):
            idx = int(step_t * SAMPLE_RATE) + ni
            if idx >= n_samples: break
            t = ni / SAMPLE_RATE
            norm_t = t / note_dur
            # Sawtooth 鋸波風
            saw = (2.0 * (t * note_freq - math.floor(t * note_freq + 0.5)))
            env = math.exp(-norm_t * 5.0)
            samples[idx] += saw * env * 0.28
            
    # 4. 高音サイバーアルペジオ (A Minor Arp)
    arp_notes = [440.0, 523.25, 659.25, 783.99, 880.0, 783.99, 659.25, 523.25]
    eighth_sec = beat_sec / 2.0
    n_eighths = int(duration / eighth_sec)
    for step in range(n_eighths):
        step_t = step * eighth_sec
        note_freq = arp_notes[step % len(arp_notes)]
        note_dur = eighth_sec * 0.7
        note_samples = int(SAMPLE_RATE * note_dur)
        for ni in range(note_samples):
            idx = int(step_t * SAMPLE_RATE) + ni
            if idx >= n_samples: break
            t = ni / SAMPLE_RATE
            norm_t = t / note_dur
            tone = math.sin(2.0 * math.pi * note_freq * t)
            env = math.exp(-norm_t * 6.0)
            samples[idx] += tone * env * 0.16
            
    # 全体マスタリング (リミッター / ノーマライズ)
    max_val = max(max(abs(s) for s in samples), 0.001)
    return [s / max_val * 0.85 for s in samples]

# =========================================================================
# エントリーポイント
# =========================================================================

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    output_dirs = [
        os.path.join(script_dir, "..", "Assets", "_Project", "Audio"),
        os.path.join(script_dir, "..", "Assets", "_Project", "Resources", "Audio")
    ]
    for out_dir in output_dirs:
        os.makedirs(out_dir, exist_ok=True)
    
    print(f"=== CuStack-Robo オーディオアセット生成開始 ===")
    print(f"出力先: {output_dirs[0]} & {output_dirs[1]}\n")
    
    sounds = {
        # 武器系
        "se_shot_gatling.wav": gen_shot_gatling,
        "se_shot_laser.wav": gen_shot_laser,
        "se_sword_slash.wav": gen_sword_slash,
        
        # 被弾・爆発系
        "se_hit_damage.wav": gen_hit_damage,
        "se_hit_shield.wav": gen_hit_shield,
        "se_explosion.wav": gen_explosion,
        "se_stun.wav": gen_stun,
        "se_defeat.wav": gen_defeat,
        
        # 地形系
        "se_terrain_mud.wav": gen_terrain_mud,
        "se_terrain_ice.wav": gen_terrain_ice,
        "se_terrain_lava.wav": gen_terrain_lava,
        
        # システム・UI系
        "se_game_start.wav": gen_game_start,
        "se_victory.wav": gen_victory,
        "se_lockon.wav": gen_lockon,
        
        # BGM
        "bgm_battle_loop.wav": gen_battle_bgm,
    }
    
    for filename, gen_func in sounds.items():
        samples = gen_func()
        for out_dir in output_dirs:
            filepath = os.path.join(out_dir, filename)
            save_wav(filepath, samples)
        
    print("\n✅ 全15種のオーディオアセット（SE 14種 + BGM 1種）の生成が完了しました！")

if __name__ == "__main__":
    main()
