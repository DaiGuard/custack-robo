#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
CuStack-Robo: robot_leg EEPROM Hex Editor & Uploader Tool
ATtiny1614 用の EEPROM .hex ファイルを解析、表示、生成、書き換え、実機アップロードするCLIツール
"""

import sys
import os
import struct
import argparse
import subprocess
from typing import Dict, Optional, Tuple

# -----------------------------------------------------------------------------
# 定数 & プリセット定義
# -----------------------------------------------------------------------------
EEPROM_MAP_SIZE = 12  # 0x00 ~ 0x0B (12 Bytes)

PRESETS = {
    "omni": {
        "description": "オムニホイール (Omni) デフォルト設定 (Offset=0us, Scale=100%)",
        "offsets": [0, 0, 0, 0],
        "scales": [100, 100, 100, 100],
    },
    "tire": {
        "description": "二輪差動 (Tire) デフォルト設定 (Offset=120us, Scale=100%)",
        "offsets": [120, 120, 120, 120],
        "scales": [100, 100, 100, 100],
    },
    "crawler": {
        "description": "キャタピラ (Crawler) デフォルト設定 (Offset=120us, Scale=60%)",
        "offsets": [120, 120, 120, 120],
        "scales": [60, 60, 60, 60],
    },
}

# -----------------------------------------------------------------------------
# EEPROM データクラス
# -----------------------------------------------------------------------------
class LegEepromData:
    """robot_leg の EEPROM パラメータを保持・操作するクラス"""
    def __init__(
        self,
        offset1: int = 0,
        offset2: int = 0,
        offset3: int = 0,
        offset4: int = 0,
        scale1: int = 100,
        scale2: int = 100,
        scale3: int = 100,
        scale4: int = 100,
    ):
        self.offsets = [int(offset1), int(offset2), int(offset3), int(offset4)]
        self.scales = [int(scale1), int(scale2), int(scale3), int(scale4)]

    def validate(self) -> None:
        """値の範囲を検証"""
        for i, off in enumerate(self.offsets, 1):
            if not (-32768 <= off <= 32767):
                raise ValueError(f"offset{i} ({off}) は -32768 から 32767 の範囲内である必要があります。")
        for i, sc in enumerate(self.scales, 1):
            if not (0 <= sc <= 255):
                raise ValueError(f"scale{i} ({sc}) は 0 から 255 の範囲内である必要があります。")

    def to_bytes(self) -> bytes:
        """12バイトのバイナリ列へシリアライズ (Little-Endian)"""
        self.validate()
        buf = bytearray(EEPROM_MAP_SIZE)
        # offset 1..4 (int16_t x 4, Little-Endian)
        struct.pack_into("<hhhh", buf, 0, *self.offsets)
        # scale 1..4 (uint8_t x 4)
        struct.pack_into("4B", buf, 8, *self.scales)
        return bytes(buf)

    @classmethod
    def from_bytes(cls, data: bytes) -> "LegEepromData":
        """バイナリ列からデシリアライズ"""
        if len(data) < EEPROM_MAP_SIZE:
            # 不足分は 0xFF (未初期化想定) で埋める
            data = data.ljust(EEPROM_MAP_SIZE, b"\xFF")
        
        offsets = list(struct.unpack_from("<hhhh", data, 0))
        scales = list(struct.unpack_from("4B", data, 8))
        return cls(*offsets, *scales)

    @classmethod
    def from_preset(cls, preset_name: str) -> "LegEepromData":
        """プリセットからインスタンスを生成"""
        name = preset_name.lower()
        if name not in PRESETS:
            raise ValueError(f"未知のプリセット: {preset_name} (利用可能: {', '.join(PRESETS.keys())})")
        p = PRESETS[name]
        return cls(*p["offsets"], *p["scales"])

    def to_intel_hex(self, per_record_style: str = "field") -> str:
        """
        Intel HEX 形式の文字列を出力
        per_record_style:
          - 'field': フィールド単位 (offsetは2byte, scaleは1byte) - 既存ファイル準拠
          - 'block': 12バイト一括レコード
        """
        self.validate()
        data = self.to_bytes()
        lines = []

        if per_record_style == "block":
            # 1行で 12 バイト
            rec = make_hex_record(0x0000, 0x00, data)
            lines.append(rec)
        else:
            # 既存形式互換: offset1〜4 (2Bずつ), scale1〜4 (1Bずつ)
            for i in range(4):
                addr = i * 2
                chunk = data[addr : addr + 2]
                lines.append(make_hex_record(addr, 0x00, chunk))
            for i in range(4):
                addr = 8 + i
                chunk = data[addr : addr + 1]
                lines.append(make_hex_record(addr, 0x00, chunk))

        # EOF レコード
        lines.append(make_hex_record(0x0000, 0x01, b""))
        return "\n".join(lines) + "\n"

    @classmethod
    def from_intel_hex(cls, hex_content: str) -> "LegEepromData":
        """Intel HEX 文字列をパースしてデコード"""
        memory = bytearray([0xFF] * EEPROM_MAP_SIZE)

        for line_num, line in enumerate(hex_content.splitlines(), 1):
            line = line.strip()
            if not line:
                continue
            if not line.startswith(":"):
                raise ValueError(f"HEXフォーマットエラー (行 {line_num}): ':' で始まっていません。")
            
            try:
                raw_bytes = bytes.fromhex(line[1:])
            except ValueError as e:
                raise ValueError(f"HEXパースエラー (行 {line_num}): 16進数への変換に失敗しました: {e}")

            if len(raw_bytes) < 5:
                raise ValueError(f"HEXレコードエラー (行 {line_num}): レコード長が短すぎます。")

            byte_count = raw_bytes[0]
            addr = (raw_bytes[1] << 8) | raw_bytes[2]
            record_type = raw_bytes[3]
            data = raw_bytes[4:-1]
            checksum = raw_bytes[-1]

            if len(data) != byte_count:
                raise ValueError(f"HEXレコードエラー (行 {line_num}): 指定長 {byte_count} に対し実データ長 {len(data)}")

            # チェックサム検証
            expected_checksum = calc_checksum(byte_count, addr, record_type, data)
            if checksum != expected_checksum:
                raise ValueError(
                    f"チェックサム不一致 (行 {line_num}): ファイル記載=0x{checksum:02X}, 計算値=0x{expected_checksum:02X}"
                )

            if record_type == 0x00:  # Data record
                for i, b in enumerate(data):
                    target_addr = addr + i
                    if target_addr < EEPROM_MAP_SIZE:
                        memory[target_addr] = b
            elif record_type == 0x01:  # EOF record
                break

        return cls.from_bytes(bytes(memory))


# -----------------------------------------------------------------------------
# Intel HEX ヘルパー関数
# -----------------------------------------------------------------------------
def calc_checksum(byte_count: int, addr: int, record_type: int, data: bytes) -> int:
    """Intel HEX チェックサム計算: (0x100 - (合計 & 0xFF)) & 0xFF"""
    total = byte_count + (addr >> 8) + (addr & 0xFF) + record_type + sum(data)
    return (0x100 - (total & 0xFF)) & 0xFF


def make_hex_record(addr: int, record_type: int, data: bytes) -> str:
    """単一の Intel HEX レコード行文字列を生成 (先頭 ':' 付き)"""
    byte_count = len(data)
    checksum = calc_checksum(byte_count, addr, record_type, data)
    data_hex = data.hex().upper()
    return f":{byte_count:02X}{addr:04X}{record_type:02X}{data_hex}{checksum:02X}"


# -----------------------------------------------------------------------------
# UI / 表示関数
# -----------------------------------------------------------------------------
def format_eeprom_table(data: LegEepromData, title: Optional[str] = None) -> str:
    """EEPROM データを視覚的に見やすい ASCII テーブルとして整形"""
    data_bytes = data.to_bytes()
    hex_dump = " ".join(f"{b:02X}" for b in data_bytes)
    
    wheel_names = ["PWM1 (RR: 右後)", "PWM2 (FR: 右前)", "PWM3 (RL: 左後)", "PWM4 (FL: 左前)"]
    
    lines = []
    lines.append("=" * 64)
    if title:
        lines.append(f"  {title}")
        lines.append("-" * 64)
    lines.append(" [Raw Bytes (0x00..0x0B)]")
    lines.append(f"  Hex: {hex_dump}")
    lines.append("-" * 64)
    lines.append(f" {'Port / Channel':<18} | {'Offset [us]':<12} | {'Scale [%]':<10} | {'Addr (Off/Sc)':<14}")
    lines.append("-" * 64)
    for i in range(4):
        off_val = data.offsets[i]
        sc_val = data.scales[i]
        off_addr = f"0x{i*2:02X}-0x{i*2+1:02X}"
        sc_addr = f"0x{8+i:02X}"
        addr_str = f"{off_addr} / {sc_addr}"
        
        # 符号付き表記
        off_str = f"{off_val:+d} us" if off_val != 0 else "0 us"
        sc_str = f"{sc_val} %"
        
        lines.append(f" {wheel_names[i]:<18} | {off_str:>12} | {sc_str:>10} | {addr_str:<14}")
    lines.append("=" * 64)
    return "\n".join(lines)


# -----------------------------------------------------------------------------
# avrdude 書き込み / 読み出し
# -----------------------------------------------------------------------------
def upload_to_device(hex_file_path: str, port: str, baud: int = 115200) -> bool:
    """avrdude を用いて ATtiny1614 の EEPROM へ書き込み"""
    cmd = [
        "avrdude",
        "-c", "serialupdi",
        "-p", "t1614",
        "-P", port,
        "-b", str(baud),
        "-U", f"eeprom:w:{hex_file_path}:i"
    ]
    print(f"\n🚀 [avrdude] EEPROM 書き込み実行中...")
    print(f"   Command: {' '.join(cmd)}")
    
    try:
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
        print("✅ [avrdude] 書き込み成功！")
        if res.stdout:
            print(res.stdout.strip())
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ [avrdude] エラー (終了コード: {e.returncode})", file=sys.stderr)
        if e.stderr:
            print(e.stderr.strip(), file=sys.stderr)
        elif e.stdout:
            print(e.stdout.strip(), file=sys.stderr)
        return False
    except FileNotFoundError:
        print("❌ [avrdude] avrdude コマンドが見つかりません。パスが通っているか確認してください。", file=sys.stderr)
        return False


def read_from_device(output_hex_path: str, port: str, baud: int = 115200) -> bool:
    """avrdude を用いて ATtiny1614 の EEPROM から読み出して hex ファイルに保存"""
    cmd = [
        "avrdude",
        "-c", "serialupdi",
        "-p", "t1614",
        "-P", port,
        "-b", str(baud),
        "-U", f"eeprom:r:{output_hex_path}:i"
    ]
    print(f"\n📥 [avrdude] 実機から EEPROM 読み出し中...")
    print(f"   Command: {' '.join(cmd)}")
    
    try:
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
        print(f"✅ [avrdude] 読み出し成功 -> {output_hex_path}")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ [avrdude] エラー (終了コード: {e.returncode})", file=sys.stderr)
        if e.stderr:
            print(e.stderr.strip(), file=sys.stderr)
        elif e.stdout:
            print(e.stdout.strip(), file=sys.stderr)
        return False
    except FileNotFoundError:
        print("❌ [avrdude] avrdude コマンドが見つかりません。", file=sys.stderr)
        return False


# -----------------------------------------------------------------------------
# サブコマンドハンドラ
# -----------------------------------------------------------------------------
def cmd_monitor(args: argparse.Namespace) -> int:
    """monitor サブコマンドの処理"""
    if args.port:
        # 実機から読み出して一時ファイル経由でモニタ
        import tempfile
        with tempfile.NamedTemporaryFile(suffix=".hex", delete=False) as tmp:
            tmp_path = tmp.name
        try:
            if not read_from_device(tmp_path, args.port, args.baud):
                return 1
            with open(tmp_path, "r", encoding="utf-8") as f:
                content = f.read()
            data = LegEepromData.from_intel_hex(content)
            title = f"Device EEPROM on {args.port} (ATtiny1614)"
            print("\n" + format_eeprom_table(data, title=title))
        finally:
            if os.path.exists(tmp_path):
                os.remove(tmp_path)
        return 0

    # ファイルからのモニタ
    if not args.file:
        print("エラー: モニタ対象の .hex ファイルパス、または --port を指定してください。", file=sys.stderr)
        return 1

    if not os.path.exists(args.file):
        print(f"エラー: 指定されたファイルが見つかりません: {args.file}", file=sys.stderr)
        return 1

    with open(args.file, "r", encoding="utf-8") as f:
        content = f.read()

    try:
        data = LegEepromData.from_intel_hex(content)
    except Exception as e:
        print(f"エラー: HEXパース失敗: {e}", file=sys.stderr)
        return 1

    print("\n" + format_eeprom_table(data, title=f"File: {os.path.abspath(args.file)}"))
    return 0


def cmd_set(args: argparse.Namespace) -> int:
    """set (既存ファイル変更) サブコマンドの処理"""
    if not os.path.exists(args.file):
        print(f"エラー: 編集対象のファイルが見つかりません: {args.file}", file=sys.stderr)
        return 1

    with open(args.file, "r", encoding="utf-8") as f:
        content = f.read()

    try:
        data = LegEepromData.from_intel_hex(content)
    except Exception as e:
        print(f"エラー: 元ファイルのパースに失敗しました: {e}", file=sys.stderr)
        return 1

    print(format_eeprom_table(data, title=f"変更前: {args.file}"))

    # 各パラメータの更新
    if args.offset1 is not None: data.offsets[0] = args.offset1
    if args.offset2 is not None: data.offsets[1] = args.offset2
    if args.offset3 is not None: data.offsets[2] = args.offset3
    if args.offset4 is not None: data.offsets[3] = args.offset4

    if args.offsets is not None:
        parts = [int(x.strip()) for x in args.offsets.split(",")]
        if len(parts) != 4:
            print("エラー: --offsets はカンマ区切りで4つの数値を指定してください (例: 0,0,0,0)", file=sys.stderr)
            return 1
        data.offsets = parts

    if args.scale1 is not None: data.scales[0] = args.scale1
    if args.scale2 is not None: data.scales[1] = args.scale2
    if args.scale3 is not None: data.scales[2] = args.scale3
    if args.scale4 is not None: data.scales[3] = args.scale4

    if args.scales is not None:
        parts = [int(x.strip()) for x in args.scales.split(",")]
        if len(parts) != 4:
            print("エラー: --scales はカンマ区切りで4つの数値を指定してください (例: 100,100,100,100)", file=sys.stderr)
            return 1
        data.scales = parts

    try:
        data.validate()
    except ValueError as e:
        print(f"エラー: 値の検証エラー: {e}", file=sys.stderr)
        return 1

    out_path = args.out if args.out else args.file
    new_hex = data.to_intel_hex()
    
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(new_hex)

    print("\n" + format_eeprom_table(data, title=f"変更後: {out_path}"))
    print(f"💾 .hex ファイルを保存しました: {out_path}")

    # 実機アップロード
    if args.upload:
        if not upload_to_device(out_path, args.upload, args.baud):
            return 1

    return 0


def cmd_create(args: argparse.Namespace) -> int:
    """create (新規生成) サブコマンドの処理"""
    if args.preset:
        try:
            data = LegEepromData.from_preset(args.preset)
        except ValueError as e:
            print(f"エラー: {e}", file=sys.stderr)
            return 1
    else:
        data = LegEepromData()

    if args.offsets is not None:
        parts = [int(x.strip()) for x in args.offsets.split(",")]
        if len(parts) != 4:
            print("エラー: --offsets はカンマ区切りで4つの数値を指定してください", file=sys.stderr)
            return 1
        data.offsets = parts
    else:
        if args.offset1 is not None: data.offsets[0] = args.offset1
        if args.offset2 is not None: data.offsets[1] = args.offset2
        if args.offset3 is not None: data.offsets[2] = args.offset3
        if args.offset4 is not None: data.offsets[3] = args.offset4

    if args.scales is not None:
        parts = [int(x.strip()) for x in args.scales.split(",")]
        if len(parts) != 4:
            print("エラー: --scales はカンマ区切りで4つの数値を指定してください", file=sys.stderr)
            return 1
        data.scales = parts
    else:
        if args.scale1 is not None: data.scales[0] = args.scale1
        if args.scale2 is not None: data.scales[1] = args.scale2
        if args.scale3 is not None: data.scales[2] = args.scale3
        if args.scale4 is not None: data.scales[4] = args.scale4

    try:
        data.validate()
    except ValueError as e:
        print(f"エラー: 値の検証エラー: {e}", file=sys.stderr)
        return 1

    out_path = args.file
    new_hex = data.to_intel_hex()

    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(new_hex)

    print("\n" + format_eeprom_table(data, title=f"新規生成: {out_path}"))
    print(f"✨ .hex ファイルを生成しました: {out_path}")

    # 実機アップロード
    if args.upload:
        if not upload_to_device(out_path, args.upload, args.baud):
            return 1

    return 0


def cmd_upload(args: argparse.Namespace) -> int:
    """upload サブコマンドの処理"""
    if not os.path.exists(args.file):
        print(f"エラー: アップロード対象ファイルが見つかりません: {args.file}", file=sys.stderr)
        return 1
    
    # 書き込む前に内容を表示
    try:
        with open(args.file, "r", encoding="utf-8") as f:
            data = LegEepromData.from_intel_hex(f.read())
        print(format_eeprom_table(data, title=f"書き込み内容確認: {args.file}"))
    except Exception:
        pass

    if not upload_to_device(args.file, args.port, args.baud):
        return 1
    return 0


def cmd_read(args: argparse.Namespace) -> int:
    """read (実機からEEPROMダンプ) サブコマンドの処理"""
    out_path = args.file if args.file else "read_eeprom.hex"
    if not read_from_device(out_path, args.port, args.baud):
        return 1

    try:
        with open(out_path, "r", encoding="utf-8") as f:
            data = LegEepromData.from_intel_hex(f.read())
        print("\n" + format_eeprom_table(data, title=f"Read from {args.port} -> {out_path}"))
    except Exception as e:
        print(f"警告: 読み出したデータのデコードに失敗しました: {e}", file=sys.stderr)

    return 0


# -----------------------------------------------------------------------------
# 引数パーサー
# -----------------------------------------------------------------------------
def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="CuStack-Robo: robot_leg EEPROM Hex Editor & Uploader Tool (ATtiny1614)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""\
使用例:
  # 1. 既存の hex ファイル内容をモニタ表示
  python tools/eeprom_tool.py monitor robot_leg/data/omni_eeprom.hex
  
  # 2. 実機 ATtiny1614 から現在の EEPROM を読み出してモニタ
  python tools/eeprom_tool.py monitor -p /dev/ttyUSB0

  # 3. 既存 hex の特定チャンネルのオフセット/スケールを変更して保存
  python tools/eeprom_tool.py set robot_leg/data/tire_eeprom.hex -o1 130 -s1 90

  # 4. パラメータを変更して保存し、同時に実機マイコンへアップロード
  python tools/eeprom_tool.py set robot_leg/data/omni_eeprom.hex --offsets 10,20,-10,0 -u /dev/ttyUSB0

  # 5. プリセットから新規 hex を生成
  python tools/eeprom_tool.py create robot_leg/data/new_crawler.hex --preset crawler

  # 6. hex ファイルを実機マイコンへアップロード
  python tools/eeprom_tool.py upload robot_leg/data/omni_eeprom.hex -p /dev/ttyUSB0

  # 7. 実機から EEPROM をバックアップ保存
  python tools/eeprom_tool.py read backup_eeprom.hex -p /dev/ttyUSB0
""",
    )

    subparsers = parser.add_subparsers(dest="command", help="実行するサブコマンド")

    # -------------------------------------------------------------------------
    # monitor
    # -------------------------------------------------------------------------
    p_mon = subparsers.add_parser("monitor", aliases=["mon", "show", "view"], help=".hex ファイルまたは実機の EEPROM をモニタ表示")
    p_mon.add_argument("file", nargs="?", default=None, help=".hex ファイルのパス")
    p_mon.add_argument("-p", "--port", type=str, help="実機から読み出す場合のシリアルポート (例: /dev/ttyUSB0)")
    p_mon.add_argument("-b", "--baud", type=int, default=115200, help="ボーレート (デフォルト: 115200)")

    # -------------------------------------------------------------------------
    # set / modify
    # -------------------------------------------------------------------------
    p_set = subparsers.add_parser("set", aliases=["modify", "update"], help="既存の .hex ファイルのパラメータを変更して保存/書き込み")
    p_set.add_argument("file", help="編集対象の .hex ファイルパス")
    p_set.add_argument("-o1", "--offset1", type=int, help="PWM1 (RR) オフセット [us]")
    p_set.add_argument("-o2", "--offset2", type=int, help="PWM2 (FR) オフセット [us]")
    p_set.add_argument("-o3", "--offset3", type=int, help="PWM3 (RL) オフセット [us]")
    p_set.add_argument("-o4", "--offset4", type=int, help="PWM4 (FL) オフセット [us]")
    p_set.add_argument("--offsets", type=str, help="4輪オフセット一括指定 'o1,o2,o3,o4'")
    p_set.add_argument("-s1", "--scale1", type=int, help="PWM1 (RR) スケール [%%] (0-255)")
    p_set.add_argument("-s2", "--scale2", type=int, help="PWM2 (FR) スケール [%%] (0-255)")
    p_set.add_argument("-s3", "--scale3", type=int, help="PWM3 (RL) スケール [%%] (0-255)")
    p_set.add_argument("-s4", "--scale4", type=int, help="PWM4 (FL) スケール [%%] (0-255)")
    p_set.add_argument("--scales", type=str, help="4輪スケール一括指定 's1,s2,s3,s4'")
    p_set.add_argument("-o", "--out", type=str, help="別名で保存する場合の出力パス (省略時は上書き)")
    p_set.add_argument("-u", "--upload", type=str, metavar="PORT", help="保存後にマイコンへ直接書き込むシリアルポート")
    p_set.add_argument("-b", "--baud", type=int, default=115200, help="ボーレート (デフォルト: 115200)")

    # -------------------------------------------------------------------------
    # create
    # -------------------------------------------------------------------------
    p_create = subparsers.add_parser("create", aliases=["new", "gen"], help="新規 .hex ファイルを生成")
    p_create.add_argument("file", help="生成先 .hex ファイルパス")
    p_create.add_argument("--preset", choices=list(PRESETS.keys()), help="プリセット (omni / tire / crawler)")
    p_create.add_argument("-o1", "--offset1", type=int, help="PWM1 (RR) オフセット [us]")
    p_create.add_argument("-o2", "--offset2", type=int, help="PWM2 (FR) オフセット [us]")
    p_create.add_argument("-o3", "--offset3", type=int, help="PWM3 (RL) オフセット [us]")
    p_create.add_argument("-o4", "--offset4", type=int, help="PWM4 (FL) オフセット [us]")
    p_create.add_argument("--offsets", type=str, help="4輪オフセット一括指定 'o1,o2,o3,o4'")
    p_create.add_argument("-s1", "--scale1", type=int, help="PWM1 (RR) スケール [%%] (0-255)")
    p_create.add_argument("-s2", "--scale2", type=int, help="PWM2 (FR) スケール [%%] (0-255)")
    p_create.add_argument("-s3", "--scale3", type=int, help="PWM3 (RL) スケール [%%] (0-255)")
    p_create.add_argument("-s4", "--scale4", type=int, help="PWM4 (FL) スケール [%%] (0-255)")
    p_create.add_argument("--scales", type=str, help="4輪スケール一括指定 's1,s2,s3,s4'")
    p_create.add_argument("-u", "--upload", type=str, metavar="PORT", help="生成後にマイコンへ直接書き込むシリアルポート")
    p_create.add_argument("-b", "--baud", type=int, default=115200, help="ボーレート (デフォルト: 115200)")

    # -------------------------------------------------------------------------
    # upload
    # -------------------------------------------------------------------------
    p_up = subparsers.add_parser("upload", aliases=["flash"], help=".hex ファイルを実機 ATtiny1614 マイコンへ書き込み")
    p_up.add_argument("file", help="書き込む .hex ファイルパス")
    p_up.add_argument("-p", "--port", required=True, help="シリアルポート (例: /dev/ttyUSB0)")
    p_up.add_argument("-b", "--baud", type=int, default=115200, help="ボーレート (デフォルト: 115200)")

    # -------------------------------------------------------------------------
    # read
    # -------------------------------------------------------------------------
    p_read = subparsers.add_parser("read", aliases=["dump"], help="実機 ATtiny1614 マイコンから EEPROM を読み出して保存")
    p_read.add_argument("file", nargs="?", default="read_eeprom.hex", help="保存先 .hex ファイルパス (デフォルト: read_eeprom.hex)")
    p_read.add_argument("-p", "--port", required=True, help="シリアルポート (例: /dev/ttyUSB0)")
    p_read.add_argument("-b", "--baud", type=int, default=115200, help="ボーレート (デフォルト: 115200)")

    return parser


def main() -> int:
    parser = build_parser()
    if len(sys.argv) <= 1:
        parser.print_help()
        return 0

    args = parser.parse_args()

    if args.command in ["monitor", "mon", "show", "view"]:
        return cmd_monitor(args)
    elif args.command in ["set", "modify", "update"]:
        return cmd_set(args)
    elif args.command in ["create", "new", "gen"]:
        return cmd_create(args)
    elif args.command in ["upload", "flash"]:
        return cmd_upload(args)
    elif args.command in ["read", "dump"]:
        return cmd_read(args)
    else:
        parser.print_help()
        return 0


if __name__ == "__main__":
    sys.exit(main())
