using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Doubtech.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
using DoubTech.ElevenLabs.Streaming.Threading;
using Meta.WitAi.Attributes;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DoubTech.ElevenLabs.Streaming
{
    /// <summary>
    /// Handles the WebSocket connection to Eleven Labs, sending and receiving streamed audio messages.
    /// </summary>
    public class ElevenLabsWebsocketStreamer : BaseAsyncMonoBehaviour
    {
        [Header("Eleven Labs")]
        [Tooltip("Configuration settings for Eleven Labs API.")]
        [SerializeField] private ElevenLabsConfig config;

        [Header("Audio Player")] [Tooltip("Audio player used for outputting audio")]
        #if VOICESDK
        [ObjectType(typeof(IStreamedAudioPlayer))]
        #endif
        [SerializeField] private MonoBehaviour streamedAudioPlayer;

        [Header("Debugging")]
        [Tooltip("If true, debug files will be written when audio is received from the server.")]
        [SerializeField] private bool writeDebugFile;

        private WebSocket ws;
        private Queue<string> messageQueue = new();
        private TaskCompletionSource<bool> _connected;
        private FileStream debugStream;
        private FileStream responseStream;

        private bool isProcessingQueue = false;
        private bool _initialMessageSent;

        private IStreamedAudioPlayer audioPlayer;
        private TaskCompletionSource<bool> _messageQueueTask;

        private float timeout = .25f;
        private Coroutine enforcedTimeCoroutine;

        private bool IsConnected => ws?.State == WebSocketState.Open;

        private void OnValidate()
        {
            if (!streamedAudioPlayer) streamedAudioPlayer = GetComponentInChildren<IStreamedAudioPlayer>() as MonoBehaviour;
        }

        protected override void Awake()
        {
            base.Awake();
            if (streamedAudioPlayer) audioPlayer = streamedAudioPlayer.GetComponent<IStreamedAudioPlayer>();
            else audioPlayer = GetComponent<IStreamedAudioPlayer>();
        }

        private async void Update()
        {
            ws?.DispatchMessageQueue();
        }

        private async void OnDestroy()
        {
            if (ws != null)
            {
                await ws.Close();
            }
        }

        /// <summary>
        /// Initiates a WebSocket connection.
        /// </summary>
        public void Connect()
        {
            ConnectToWebSocket();
        }

        /// <summary>
        /// Sends a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be spoken.</param>
        public void Speak(string message)
        {
            audioPlayer.Stop();
            messageQueue.Clear();
            messageQueue.Enqueue(message);
            _ = ProcessMessageQueue();
        }

        /// <summary>
        /// Sends a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be spoken.</param>
        public async Task SpeakAsync(string message)
        {
            audioPlayer.Stop();
            messageQueue.Clear();
            await SpeakQueuedAsync(message);
        }

        /// <summary>
        /// Queues a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be queued and spoken.</param>
        public void SpeakQueued(string message)
        {
            messageQueue.Enqueue(message);
            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessMessageQueue());
            }
        }

        /// <summary>
        /// Queues a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be queued and spoken.</param>
        public async Task SpeakQueuedAsync(string message)
        {
            messageQueue.Enqueue(message);
            await ProcessMessageQueueAsync();
        }

        /// <summary>
        /// Stops the audio playback and clears the message queue.
        /// </summary>
        public void StopPlayback()
        {
            messageQueue.Clear();
            audioPlayer.Stop();
        }

        /// <summary>
        /// Pauses the audio playback.
        /// </summary>
        public void PausePlayback()
        {
            audioPlayer.Pause();
        }

        /// <summary>
        /// Sends a test message to the Eleven Labs API for playback.
        /// </summary>
        internal void PlayTestMessage()
        {
            var file = Path.Combine(Application.dataPath, "test.pcm");
            if (writeDebugFile) debugStream = new FileStream(file, FileMode.Create);

            ProcessMessage("{\"audio\":\"8v/r//j//v/2//X/+f/3//b///8DAP7//P/+/wAABAACAP3/AgD9...\"}"); // Truncated for brevity

            if (writeDebugFile)
            {
                debugStream.Close();
                Debug.Log($"Wrote audio to: {file}");
            }
        }

        private async Task SendMessageToWebSocket(string message, bool close = false)
        {
            if (!IsConnected) await ConnectToWebSocket();
            if (!IsConnected)
            {
                Debug.LogError("Connection was not established, message not sent.");
                return;
            }

            Debug.Log("Sending message to Eleven Labs WebSocket: " + message);
            if (!_initialMessageSent)
            {
                var initialMessage = new
                {
                    text = " ",
                    voice_settings = new { stability = 0.5, similarity_boost = 0.8, use_speaker_boost = false },
                    generation_config = new { chunk_length_schedule = new List<int> { 120, 160, 250, 290 } },
                    xi_api_key = config.apiKey
                };
                await SendData(initialMessage);
                _initialMessageSent = true;
            }

            await SendData(new { text = message });
            if (close) await SendCloseTextMessage();
            else
            {
                RunOnMainThread(() =>
                {
                    if (null != enforcedTimeCoroutine)
                    {
                        StopCoroutine(enforcedTimeCoroutine);
                    }

                    StartCoroutine(EnforceSpeechTimeout());
                });
            }
        }

        private async Task SendCloseTextMessage()
        {
            await SendData(new { text = "" });
        }

        private IEnumerator EnforceSpeechTimeout()
        {
            yield return new WaitForSeconds(timeout);
            _ = SendCloseTextMessage();
        }

        private async Task SendData(object messageData)
        {
            string messageJson = JsonConvert.SerializeObject(messageData);
            await ws.SendText(messageJson);
        }

        private IEnumerator ProcessMessageQueue()
        {
            yield return AwaitCoroutine(ProcessMessageQueueAsync);
        }

        private Task ProcessMessageQueueAsync()
        {
            if (null != _messageQueueTask && !_messageQueueTask.Task.IsCompleted)
            {
                return _messageQueueTask.Task;
            }
            _messageQueueTask = new TaskCompletionSource<bool>();

            _ = RunOnBackground(async () =>
            {
                isProcessingQueue = true;
                while (messageQueue.Count > 0)
                {
                    string message = messageQueue.Dequeue();
                    await SendMessageToWebSocket(message);
                }
                isProcessingQueue = false;
                _messageQueueTask.SetResult(true);
                _messageQueueTask = null;
            });
            return _messageQueueTask.Task;
        }

        private Task<bool> ConnectToWebSocket()
        {
            if (IsConnected) return Task.FromResult(true);

            if (_connected != null && !_connected.Task.IsCompleted)
            {
                return _connected.Task;
            }

            _connected = new TaskCompletionSource<bool>();

            var headers = new Dictionary<string, string> { { "xi-api-key", config.apiKey } };
            ws = new WebSocket(config.Url, headers);

            ws.OnOpen += OnOpen;
            ws.OnMessage += OnMessage;
            ws.OnError += OnError;
            ws.OnClose += OnClose;

            RunOnBackground(ws.Connect);
            return _connected.Task;
        }

        private void ProcessMessage(string message)
        {
            var data = JsonConvert.DeserializeObject<AudioData>(message);
            if (!string.IsNullOrEmpty(data.Error))
            {
                OnError(data.Message);
                return;
            }
            if (!string.IsNullOrEmpty(data?.Audio))
            {
                byte[] audioData = Convert.FromBase64String(data.Audio);
                audioPlayer.AddData(audioData);

                debugStream?.Write(audioData);
                responseStream?.Write(Encoding.UTF8.GetBytes(message + "\n"));

                RunOnMainThread(audioPlayer.Play);
            }
        }

        private void OnOpen()
        {
            RunOnMainThread(() => StartCoroutine(WaitForConnection()));

            if (writeDebugFile)
            {
                debugStream = new FileStream(Path.Combine(Application.dataPath, "test.pcm"), FileMode.Create);
                responseStream = new FileStream(Path.Combine(Application.dataPath, "response.json"), FileMode.Create);
            }
        }

        private IEnumerator WaitForConnection()
        {
            Debug.Log("Waiting for state to go to open...");
            yield return new WaitUntil(() => ws.State == WebSocketState.Open);
            Debug.Log("Connected to Eleven Labs WebSocket");
            _connected?.SetResult(true);
        }

        private void OnMessage(byte[] bytes)
        {
            string message = Encoding.UTF8.GetString(bytes);
            ProcessMessage(message);
        }

        private void OnError(string errorMsg)
        {
            Debug.LogError(errorMsg);
        }

        private void OnClose(WebSocketCloseCode code)
        {
            if (null != _connected && !_connected.Task.IsCompleted)
            {
                _connected?.SetResult(false);
            }

            _connected = null;
            _initialMessageSent = false;

            CloseStream(ref debugStream);
            CloseStream(ref responseStream);
        }

        private void CloseStream(ref FileStream stream)
        {
            if (stream == null) return;
            try
            {
                stream.Close();
                stream = null;
            }
            catch (Exception)
            {
                // Ignored
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ElevenLabsWebsocketStreamer))]
    public class ElevenLabsWebsocketStreamerEditor : Editor
    {
        private string speakMessage = "";
        private string speakQueuedMessage = "";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ElevenLabsWebsocketStreamer myScript = (ElevenLabsWebsocketStreamer)target;

            speakMessage = EditorGUILayout.TextField("Speak Message", speakMessage);
            if (GUILayout.Button("Speak"))
            {
                myScript.Speak(speakMessage);
            }

            speakQueuedMessage = EditorGUILayout.TextField("Speak Queued Message", speakQueuedMessage);
            if (GUILayout.Button("Speak Queued"))
            {
                myScript.SpeakQueued(speakQueuedMessage);
            }

            if (GUILayout.Button("Play Test Message"))
            {
                myScript.PlayTestMessage();
            }
        }
    }
#endif
}
