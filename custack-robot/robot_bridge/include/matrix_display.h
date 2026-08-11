#ifndef MATRIX_DISPLAY_H
#define MATRIX_DISPLAY_H

#include <M5Atom.h>

class MatrixDisplay {
public:
    /**
     * @brief コンストラクタ
     */
    MatrixDisplay();

    /**
     * @brief LED表示の初期化
     */
    void begin();

    /**
     * @brief LED表示のテスト
     * 
     */
    void test();

    /**
     * @brief ロボットの各ステータスに応じてLEDマトリクスの表示を更新する
     * 
     * @param host_status ホストPCとの通信状態 (true: 正常, false: タイムアウト)
     * @param robot_status 脚やアームなどのロボット本体との通信状態 (true: 正常, false: タイムアウト)
     * @param leg_active 脚モジュールがアクティブかどうか (true: アクティブ, false: 非アクティブ)
     * @param rarm_active 右アームがアクティブかどうか (true: アクティブ, false: 非アクティブ)
     * @param larm_active 左アームがアクティブかどうか (true: アクティブ, false: 非アクティブ)
     * @param now 現在時刻 (millis()など) アニメーションに使用
     */
    void update(
        bool host_status, bool robot_status,
        bool leg_active,
        bool rarm_active,
        bool larm_active, uint32_t now);

    /**
     * @brief 全てのLEDを消灯する
     */
    void clear();
};

#endif // MATRIX_DISPLAY_H