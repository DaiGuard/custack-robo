using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.NCalc;
using Unity.VisualScripting;


namespace ZeroMQ
{
    public class Manager : MonoBehaviour
    {
        public static Manager Instance { get; private set; } = null;

        [SerializeField]
        private int _publisherPort = 5555;
        [SerializeField]
        private int _subscriberPort = 5556;

        [SerializeField]
        private int _responsePort = 5557;

        private PublisherSocket _publisherSocket = null;
        private SubscriberSocket _subscriberSocket = null;
        private ResponseSocket _responseSocket = null;

        private Dictionary<string, string> _publishedTopics = new ();
        private Dictionary<string, string> _subscribedTopics = new ();
        private Dictionary<string, Func<string, string>> _responseTopics = new ();

        // バックグランドタスク
        private CancellationTokenSource _cancellationTokenSource = null;
        private Task _backgroundLoopTask = null;
        private int loopIntervalMs = 5;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("ZeroMQManger is already initialized.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            StartZeroMQManager();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            StopBackgroundLoop();
            StartBackgroundLoop();
        }

        void Update()
        {

        }

        public void PubMessageForTopic(string topic, string message)
        {
            lock (_publishedTopics)
            {
                _publishedTopics[topic] = message;
            }
        }

        public string SubMessageForTopic(string topic)
        {
            string message = "";

            lock (_subscribedTopics)
            {
                if (_subscribedTopics.ContainsKey(topic))
                {
                    message = _subscribedTopics[topic];
                }
            }

            return message;
        }

        public void RegisterResponseForTopic(string topic, Func<string, string> callback)
        {
            lock (_responseTopics)
            {
                _responseTopics[topic] = (message) =>
                {
                    return callback.Invoke(message);
                };
            }
        }

        private void StartBackgroundLoop()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            var stopwatch = new System.Diagnostics.Stopwatch();

            _backgroundLoopTask = Task.Run(async () =>
            {
                stopwatch.Start();

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Debug.Log($"stop watch = {stopwatch.ElapsedMilliseconds}");

                        // Publish
                        lock (_publishedTopics)
                        {
                            foreach (KeyValuePair<string, string> kv in _publishedTopics)
                            {
                                string topic = kv.Key;
                                string message = kv.Value;

                                _publisherSocket
                                    .SendMoreFrame(topic)
                                    .SendFrame(message);

                                Debug.Log($"PUB {topic}: {message}");
                            }
                        }

                        // Subscribe
                        List<string> messages = new List<string>();
                        if (_subscriberSocket.TryReceiveMultipartStrings(
                            TimeSpan.FromMilliseconds(loopIntervalMs),
                            ref messages))
                        {
                            if (messages.Count > 1)
                            {
                                string topic = messages[0];
                                string message = messages[1];

                                Debug.Log($"SUB {topic}: {message}");

                                lock (_subscribedTopics)
                                {
                                    _subscribedTopics[topic] = message;
                                }
                            }
                        }

                        // Response
                        if (_responseSocket.TryReceiveMultipartStrings(
                            TimeSpan.FromMilliseconds(loopIntervalMs),
                            ref messages))
                        {
                            string topic = "";
                            string request = "";
                            string response = "";
                            if (messages.Count > 1)
                            {
                                topic = messages[0];
                                request = messages[1];

                                lock (_responseTopics)
                                {
                                    response = _responseTopics[topic].Invoke(request);
                                }
                            }

                            _responseSocket
                                .SendMoreFrame(topic)
                                .SendFrame(response);
                        }

                        await Task.Delay(loopIntervalMs, token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log("backgroud loop canceled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"backgroud loop error: {ex.Message}");
                        break;
                    }
                }

                stopwatch.Stop();

                Debug.Log("background loop finished");

            }, token);
        }

        private void StopBackgroundLoop()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_backgroundLoopTask != null)
            {
                _backgroundLoopTask.ContinueWith(t =>
                {
                    Debug.Log("background loop task finished");
                });
                _backgroundLoopTask = null;
            }
        }

        private void StartZeroMQManager()
        {
            ForceDotNet.Force();

            NetMQConfig.Cleanup();

            _publisherSocket = new PublisherSocket();
            _publisherSocket.Bind($"tcp://*:{_publisherPort}");

            _subscriberSocket = new SubscriberSocket();
            _subscriberSocket.Bind($"tcp://*:{_subscriberPort}");
            _subscriberSocket.Subscribe("");
            _subscriberSocket.Options.ReceiveHighWatermark = 100;

            _responseSocket = new ResponseSocket();
            _responseSocket.Bind($"tcp://*:{_responsePort}");
            _responseSocket.Options.ReceiveHighWatermark = 100;
        }

        private void StopZeroMQManager()
        {
            if (_publisherSocket != null)
            {
                _publisherSocket.Close();
                _publisherSocket.Dispose();
                _publisherSocket = null;
            }

            if (_subscriberSocket != null)
            {
                _subscriberSocket.Close();
                _subscriberSocket.Dispose();
                _subscriberSocket = null;
            }

            if (_responseSocket != null)
            {
                _responseSocket.Close();
                _responseSocket.Dispose();
                _responseSocket = null;
            }

            NetMQConfig.Cleanup();

            ForceDotNet.Unforce();
        }

        void OnApplicationQuit()
        {
            StopBackgroundLoop();

            StopZeroMQManager();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Debug.Log("ZeroMQManager destroyed");
            }
        }
    }
}