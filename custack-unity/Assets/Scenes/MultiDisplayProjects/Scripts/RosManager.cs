using UnityEngine;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Custack;
using System.Collections.Concurrent;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

public class RosManager : MonoBehaviour
{
    // シングルトン（どこからでもアクセス可能にする）
    public static RosManager Instance { get; private set; }

    // 受信データの一時保存
    private ConcurrentQueue<RobotPoseArrayMsg> robotPosesQueue = new ConcurrentQueue<RobotPoseArrayMsg>();

    [Header("プレイヤーオブジェクト")]
    public List<GameObject> playerObjects;

    // 初期ポーズ保存用
    private List<Vector3> initialPositions = new List<Vector3>();
    private List<Quaternion> initialRotations = new List<Quaternion>();

    // 目標ポーズ保存用（滑らかな移動用）
    private List<Vector3> targetPositions = new List<Vector3>();
    private List<Quaternion> targetRotations = new List<Quaternion>();

    [Header("外れ値除去（中央値＋標準偏差）の設定")]
    public int historySize = 5; // 履歴の保持数（Nフレーム）
    public float zScoreThreshold = 2.0f; // 中央値から標準偏差の何倍離れていたら外れ値とするか

    // 履歴バッファ
    private List<List<float>> xHistories = new List<List<float>>();
    private List<List<float>> yHistories = new List<List<float>>();
    private List<List<float>> thetaHistories = new List<List<float>>();

    [Header("滑らかな移動の追従速度")]
    public float lerpSpeed = 15f;

    [Header("投影範囲を決めるカメラ（平行投影）")]
    public Camera projectionCamera;

    // 移動範囲のスケール
    [SerializeField]
    private Vector2 positionScale = Vector2.one;

    // 周波数計測用の変数
    private int messageCount = 0;
    private int updateCount = 0;
    private float timer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // シーンを跨いでも破棄しない
        DontDestroyOnLoad(gameObject);

        // 各プレイヤーオブジェクトの初期ポーズを記録
        foreach (var player in playerObjects)
        {
            xHistories.Add(new List<float>());
            yHistories.Add(new List<float>());
            thetaHistories.Add(new List<float>());

            if (player != null)
            {
                initialPositions.Add(player.transform.localPosition);
                initialRotations.Add(player.transform.localRotation);
                targetPositions.Add(player.transform.localPosition);
                targetRotations.Add(player.transform.localRotation);
            }
            else
            {
                initialPositions.Add(Vector3.zero);
                initialRotations.Add(Quaternion.identity);
                targetPositions.Add(Vector3.zero);
                targetRotations.Add(Quaternion.identity);
            }
        }
    }

    void Start()
    {
        // ROSの初期化
        Debug.Log("Initialize ROS");
        InitializeROS();        
    }

    void InitializeROS()
    {
        ROSConnection.GetOrCreateInstance()
            .Subscribe<RobotPoseArrayMsg>("/robot_poses", CallbackRobotPoses);
    }

    void CallbackRobotPoses(RobotPoseArrayMsg msg)
    {
        // Debug.Log("Subscribe msg..."); // 頻度計測の邪魔になるためコメントアウト
        robotPosesQueue.Enqueue(msg);
        messageCount++;
    }

    void Update()
    {
        // 周波数（Hz）の計測と1秒ごとのログ出力
        updateCount++;
        timer += Time.deltaTime;
        if (timer >= 1.0f)
        {
            Debug.Log($"[Frequency] Update: {updateCount} Hz / ROS Message: {messageCount} Hz");
            updateCount = 0;
            messageCount = 0;
            timer -= 1.0f;
        }

        // --- 毎フレーム必要な処理 ---
        // すべてのプレイヤーオブジェクトを目標位置・回転へ滑らかに移動させる
        for (int i = 0; i < playerObjects.Count; i++)
        {
            GameObject player = playerObjects[i];
            if (player != null)
            {
                player.transform.localPosition = Vector3.Lerp(player.transform.localPosition, targetPositions[i], Time.deltaTime * lerpSpeed);
                player.transform.localRotation = Quaternion.Lerp(player.transform.localRotation, targetRotations[i], Time.deltaTime * lerpSpeed);
            }
        }

        // --- ROSメッセージの受信処理 ---
        RobotPoseArrayMsg latestMsg = null;

        // キューから全てのメッセージを取り出し、最新のメッセージだけを保持する
        while(robotPosesQueue.TryDequeue(out var msg))
        {
            latestMsg = msg;
        }

        // キューにデータが入っていなかったら以降の処理を抜ける
        if (latestMsg == null) return;

        // カメラが設定されていて平行投影の場合、スケールを自動計算
        if (projectionCamera != null && projectionCamera.orthographic)
        {
            positionScale.y = projectionCamera.orthographicSize;
            positionScale.x = projectionCamera.orthographicSize * projectionCamera.aspect;
        }

        foreach (RobotPoseMsg pose in latestMsg.poses)
        {
            int id = pose.id;
            
            // IDがplayerObjectsのリストの範囲内かチェック
            if (id >= 0 && id < playerObjects.Count)
            {
                GameObject player = playerObjects[id];

                if (player != null)
                {
                    Vector3 initialPos = initialPositions[id];
                    Quaternion initialRot = initialRotations[id];

                    // 【右手座標系(ROS) から 左手座標系(Unity) への変換】
                    
                    // 2Dの投影面(XY平面)にマッピング
                    // ROS側: 左上(-1.0, -1.0) / 右下(1.0, 1.0)
                    // Unity側: Xは右が正、Yは上が正となるため、Yの符号を反転させます
                    float targetX = pose.x * positionScale.x;
                    float targetY = pose.y * positionScale.y;
                    // ※ pose.theta がラジアンの場合は -pose.theta * Mathf.Rad2Deg としてください
                    float targetTheta = -pose.theta * Mathf.Rad2Deg;

                    // 履歴に追加
                    AddHistory(xHistories[id], targetX, historySize);
                    AddHistory(yHistories[id], targetY, historySize);
                    AddAngleHistory(thetaHistories[id], targetTheta, historySize);

                    // 外れ値を除外した平均を計算
                    float meanX = GetFilteredMean(xHistories[id], zScoreThreshold);
                    float meanY = GetFilteredMean(yHistories[id], zScoreThreshold);
                    float meanTheta = GetFilteredMean(thetaHistories[id], zScoreThreshold);

                    targetPositions[id] = initialPos + new Vector3(meanX, meanY, 0);
                    targetRotations[id] = initialRot * Quaternion.Euler(0, 0, meanTheta);

                    // 毎フレームの大量のログ出力はパフォーマンス低下の原因になるためコメントアウト
                    Debug.LogFormat("ID: {0}, X: {1}, Y: {2}, Theta: {3}", id, pose.x, pose.y, pose.theta);
                }
            }
        }
    }

    // --- トリム平均用の補助メソッド群 ---

    private void AddHistory(List<float> history, float value, int maxSize)
    {
        history.Add(value);
        if (history.Count > maxSize)
        {
            history.RemoveAt(0); // 最も古いデータを削除
        }
    }

    private void AddAngleHistory(List<float> history, float newAngle, int maxSize)
    {
        // 角度が180度や-180度を跨いだときに不連続にならないよう、直近の角度からの相対値（連続値）に補正する
        if (history.Count > 0)
        {
            float lastAngle = history[history.Count - 1];
            float diff = Mathf.DeltaAngle(lastAngle, newAngle);
            newAngle = lastAngle + diff;
        }
        AddHistory(history, newAngle, maxSize);
    }

    private float GetFilteredMean(List<float> history, float threshold)
    {
        if (history.Count == 0) return 0f;
        
        // データ数が少ない場合は最新の値を返す
        if (history.Count < 3) return history[history.Count - 1];

        // 1. 中央値（Median）を求める
        List<float> sorted = new List<float>(history);
        sorted.Sort();
        float median = sorted[sorted.Count / 2];
        if (sorted.Count % 2 == 0)
        {
            median = (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2f;
        }

        // 2. 標準偏差（Standard Deviation）を求める
        float sum = 0f;
        foreach (float val in history) sum += val;
        float mean = sum / history.Count;

        float sqDiffSum = 0f;
        foreach (float val in history) sqDiffSum += (val - mean) * (val - mean);
        float standardDeviation = Mathf.Sqrt(sqDiffSum / history.Count);

        // 散らばりが極端に小さい場合はそのまま中央値を返す
        if (standardDeviation < 0.0001f) return median;

        // 3. 中央値から (標準偏差 × しきい値) 以上離れているものを除外し、残りの平均を求める
        float filteredSum = 0f;
        int filteredCount = 0;
        foreach (float val in history)
        {
            if (Mathf.Abs(val - median) <= standardDeviation * threshold)
            {
                filteredSum += val;
                filteredCount++;
            }
        }

        Debug.LogFormat("History {0} -> {1}", history.Count, filteredCount);

        // 全て除外されてしまった場合（しきい値が厳しすぎる場合など）は中央値を返す
        if (filteredCount == 0) return median;
        
        return filteredSum / filteredCount;
    }
}
