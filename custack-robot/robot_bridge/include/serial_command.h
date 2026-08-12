#ifndef SERIAL_COMMAND_H
#define SERIAL_COMMAND_H


class SerialCommand {
public:
    /**
     * @brief コンストラクタ
     */
    SerialCommand();

    /**
     * @brief シリアルコマンドの初期化
     * 
     */
    void begin();

    /**
     * @brief シリアルコマンドの更新
     * 
     */
    void update();

    /**
     * @brief 
     * 
     * @param command 
     * @param vx 
     * @param vy 
     * @param omega 
     * @param rarm 
     * @param larm 
     * @return true 
     * @return false 
     */
    bool parseSET(
        const char* command, 
        int* vx, int* vy, int* omega, int* rarm, int* larm);

    /**
     * @brief 
     * 
     * @param command 
     * @param vx 
     * @param vy 
     * @param omega 
     * @return true 
     * @return false 
     */
    bool parseVEL(
        const char* command, 
        int* vx, int* vy, int* omega);

    /**
     * @brief 
     * 
     * @param command 
     * @param rarm 
     * @param larm 
     * @return true 
     * @return false 
     */
    bool parseARM(
        const char* command, 
        int* rarm, int* larm);

    /**
     * @brief 
     * 
     * @param command 
     * @param unit_id 
     * @param cal_addr
     * @param cal_val 
     * @return true 
     * @return false 
     */
    bool parseCAL(
        const char* command,
        int* unit_id, int* cal_addr, int* cal_val);

    /**
     * @brief シリアルコマンドの取得
     * 
     * @param command 取得したコマンド
     * @return int コマンドバッファの残り数
     */
    int pop(String& command);

    /**
     * @brief シリアルコマンドのプッシュ
     * 
     * @param command 
     * @return true 
     * @return false 
     */
    bool push(String command);

    /**
     * @brief デバッグメッセージの表示
     * 
     * @param message 表示するメッセージ
     */
    void error(String message);
    
    /**
     * @brief デバッグメッセージの表示
     * 
     * @param message 表示するメッセージ
     */
    void warn(String message);

    /**
     * @brief デバッグメッセージの表示
     * 
     * @param message 表示するメッセージ
     */
    void info(String message);

    /**
     * @brief デバッグメッセージの表示
     * 
     * @param message 表示するメッセージ
     */
    void debug(String message);
};

#endif // SERIAL_COMMAND_H