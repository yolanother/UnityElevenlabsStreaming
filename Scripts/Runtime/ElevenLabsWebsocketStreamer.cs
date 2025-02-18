using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DoubTech.AI.ThirdParty.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming;
using Doubtech.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
using DoubTech.ElevenLabs.Streaming.Testing;
using DoubTech.ElevenLabs.Streaming.Threading;
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
        private Queue<AudioRequest> messageQueue = new();
        private Queue<AudioRequest> activeRequests = new();
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
            Enqueue(message);
            _ = ProcessMessageQueueAsync();
        }

        /// <summary>
        /// Sends a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be spoken.</param>
        public async Task SpeakAsync(string message)
        {
            audioPlayer.Stop();
            messageQueue.Clear();
            var request = await SpeakQueuedAsync(message);
            await request.Task;
        }

        /// <summary>
        /// Queues a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be queued and spoken.</param>
        public void SpeakQueued(string message)
        {
            _ = SpeakQueuedAsync(message);
        }

        /// <summary>
        /// Queues a message to be spoken by the Eleven Labs API.
        /// </summary>
        /// <param name="message">The message to be queued and spoken.</param>
        public async Task<AudioRequest> SpeakQueuedAsync(string message)
        {
            var request = Enqueue(message);
            await ProcessMessageQueueAsync();
            await request.Task;
            return request;
        }

        private AudioRequest Enqueue(string message)
        {
            var request = new AudioRequest()
            {
                message = message,
                charsRemaining = message.Length
            };
            request.onComplete += HandleComplete; 
            messageQueue.Enqueue(request);
            return request;
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

            ProcessMessage(TestMessage.PART1);
            ProcessMessage(TestMessage.PART2);

            
            if (writeDebugFile)
            {
                debugStream.Close();
                Debug.Log($"Wrote audio to: {file}");
            }
        }

        private async Task<AudioRequest> SendMessageToWebSocket(AudioRequest request, bool close = false)
        {
            if (!IsConnected) await ConnectToWebSocket();
            if (!IsConnected)
            {
                Debug.LogError("Connection was not established, message not sent.");
                return null;
            }
            activeRequests.Enqueue(request);

            if (!_initialMessageSent)
            {
                var initialMessage = new
                {
                    text = request.message,
                    voice_settings = new { stability = 0.5, similarity_boost = 0.8, use_speaker_boost = false },
                    generation_config = new { chunk_length_schedule = new List<int> { 120, 160, 250, 290 } },
                    xi_api_key = config.apiKey
                };
                await SendData(initialMessage);
                _initialMessageSent = true;
            }
            else
            {
                await SendData(new { text = request.message });
            }

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
            
            return request;
        }

        private async Task SendCloseTextMessage()
        {
            await SendData(new { text = "" });
        }

        private IEnumerator EnforceSpeechTimeout()
        {
            yield return new WaitForSeconds(timeout);
            _ = SendCloseTextMessage();
            enforcedTimeCoroutine = null;
        }

        private async Task SendData(object messageData)
        {
            string messageJson = JsonConvert.SerializeObject(messageData);
            await ws.SendText(messageJson);
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
                    var message = messageQueue.Dequeue();
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
            if (null != data?.Audio)
            {
                var request = activeRequests.Peek();
                request.charsRemaining -= data.Alignment?.Chars?.Count ?? request.charsRemaining;
                Debug.Log($"Processing {request.message} leaving {request.charsRemaining} characters to process.");
                // Combine the chars and log it
                Debug.Log("Chunk contains: " + string.Join("", data.Alignment.Chars));
                var events = new List<PlaybackEvent>();
                if (data.Alignment.CharStartTimesMs.First() == 0)
                {
                    events.Add(new PlaybackEvent(0, () => request.onPlaybackStarted?.Invoke(request)));
                }
                
                var startTime = data.Alignment.CharStartTimesMs[0];
                var endTime = data.Alignment.CharStartTimesMs[data.Alignment.CharStartTimesMs.Count - 1] + data.Alignment.CharDurationsMs[data.Alignment.CharDurationsMs.Count - 1];
                var length = endTime - startTime;
                
                for(int i = 0; i < data.Alignment.CharStartTimesMs.Count; i++)
                {
                    events.Add(new PlaybackEvent(length, () => request.onCharacterPlayed?.Invoke(request, i)));
                }
                
                if (request.charsRemaining <= 0)
                {
                    Debug.Log($"Enqueuing completion events at {length}");
                    events.Add(new PlaybackEvent(length, () => request.onPlaybackComplete?.Invoke(request)));
                    events.Add(new PlaybackEvent(length, () => request.onComplete?.Invoke(request)));
                }

                audioPlayer.AddData(data.Audio, 0, data.Audio.Length, events.ToArray());
                if (request.charsRemaining <= 0) activeRequests.Dequeue();

                debugStream?.Write(data?.Audio);
                responseStream?.Write(Encoding.UTF8.GetBytes(message + "\n"));

                RunOnMainThread(audioPlayer.Play);
            }
        }

        private void HandleComplete(AudioRequest request)
        {
            Debug.Log("Audio clip completed: " + request.message);
            if(!request.Task.IsCompleted) request.taskCompletionSource.SetResult(request);
            else Debug.LogError($"HandleComplete was called twice for {request.message}");
            request.onComplete -= HandleComplete;
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
            yield return new WaitUntil(() => ws.State == WebSocketState.Open);
            _connected?.SetResult(true);
        }

        private void OnMessage(byte[] bytes)
        {
            string message = Encoding.UTF8.GetString(bytes);
            ProcessMessage(message);
        }

        private void OnError(string errorMsg)
        {
            if (activeRequests.Count > 0)
            {
                foreach (var request in activeRequests)
                {
                    request.onError?.Invoke(request, errorMsg);
                    request.onComplete?.Invoke(request);
                }
                activeRequests.Clear();
            }

            Debug.LogError(errorMsg);
        }

        private void OnClose(WebSocketCloseCode code)
        {
            if (activeRequests.Count > 0)
            {
                foreach (var request in activeRequests)
                {
                    request.onComplete?.Invoke(request);
                }
                activeRequests.Clear();
            }
            if (null != _connected && !_connected.Task.IsCompleted)
            {
                _connected?.SetResult(false);
            }

            _connected = null;
            _initialMessageSent = false;
            activeRequests.Clear();

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
