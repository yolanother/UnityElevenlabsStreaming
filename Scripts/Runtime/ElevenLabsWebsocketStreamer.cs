using System.IO;
using System.Threading.Tasks;
using Doubtech.ElevenLabs.Streaming.Data;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
using Newtonsoft.Json;
using UnityEditor;

namespace Doubtech.ElevenLabs.Streaming
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;

    public class ElevenLabsWebsocketStreamer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private int optimizeStreamingLatency = 4; // Default value
        
        [Header("Eleven Labs")]
        [SerializeField] private string elevenLabsApiKey = "<ELEVENLABS_API_KEY>"; // Replace with your actual API key
        [SerializeField] private string model = "eleven_flash_v2_5";
        [SerializeField] private string voiceId = "21m00Tcm4TlvDq8ikWAM";
        
        [Header("Open AI")]
        [SerializeField] private string OPENAI_API_KEY = "<OPENAI_API_KEY>"; // Replace with your actual API key

        private const string URI =
            "wss://api.elevenlabs.io/v1/text-to-speech/{0}/stream-input?model_id=eleven_monolingual_v1&optimize_streaming_latency={1}";

        private WebSocket ws;
        private Queue<string> messageQueue = new Queue<string>();
        private bool isProcessingQueue = false;
        private AudioClip audioClip;
        private float[] audioBuffer;
        private int bufferWriteIndex = 0;
        private bool _initialMessageSent;
        private TaskCompletionSource<bool> _connected;

        async void Start()
        {
            audioBuffer = new float[24000 * 10]; // 10 seconds of buffer at 24kHz
            // Initialize the audio clip
            audioClip = AudioClip.Create("ElevenLabsTTS", 24000 * 10, 1, 24000, false, PcmReader);

            audioSource.clip = audioClip;
            audioSource.loop = true;

            await ConnectToWebSocket();
        }

        private void PcmReader(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (i < bufferWriteIndex)
                {
                    data[i] = audioBuffer[i];
                }
                else
                {
                    data[i] = 0f;
                }
            }
        }

        async void Update()
        {
            if (ws == null) return;
            if (!audioClip) return;
            
            ws.DispatchMessageQueue();
        }

        async void OnDestroy()
        {
            if (ws != null)
            {
                await ws.Close();
            }
        }

        public void Speak(string message)
        {
            bufferWriteIndex = 0;
            // Clear the queue and send the message
            messageQueue.Clear();
            _ = SendMessageToWebSocket(message, true);
        }

        public void SpeakQueued(string message)
        {
            // Add the message to the queue
            messageQueue.Enqueue(message);
            // Start processing the queue if not already doing so
            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessMessageQueue());
            }
        }

        public void StopPlayback()
        {
            // Stop the audio playback and clear the queue
            audioSource.Stop();
            messageQueue.Clear();
            bufferWriteIndex = 0;
        }

        public void PausePlayback()
        {
            // Pause the audio playback
            audioSource.Pause();
        }

        private async Task SendMessageToWebSocket(string message, bool close = false)
        {
            if (ws.State != WebSocketState.Open)
            {
                await ConnectToWebSocket();
            }

            var initialMessage = new
            {
                text = " ",
                voice_settings = new { stability = 0.5, similarity_boost = 0.8, use_speaker_boost = false },
                generation_config = new  { chunk_length_schedule = new List<int> {120, 160, 250, 290}},
                xi_api_key = elevenLabsApiKey,
                output_format = "pcm_24000"
            };
            await SendData(initialMessage);
            await SendData(new { text = message });
            if(close) await SendData(new { text = "" });
        }

        private async Task SendData(object messageData)
        {
            string messageJson = JsonConvert.SerializeObject(messageData);
            Debug.Log("Sending text: " + messageJson);
            await ws.SendText(messageJson);
        }

        private IEnumerator ProcessMessageQueue()
        {
            isProcessingQueue = true;
            while (messageQueue.Count > 0)
            {
                string message = messageQueue.Dequeue();
                SendMessageToWebSocket(message);
                yield return null;
            }

            isProcessingQueue = false;
        }

        private async Task ConnectToWebSocket()
        {
            _connected = new TaskCompletionSource<bool>();
            string uri = string.Format(URI, voiceId, optimizeStreamingLatency);
            var headers = new Dictionary<string, string>
            {
                {"xi-api-key", elevenLabsApiKey}
            };
            ws = new WebSocket(uri, headers);
            ws.OnMessage += (bytes) =>
            {
                string message = Encoding.UTF8.GetString(bytes);
                Debug.Log("Received message: " + message);
                var data = JsonConvert.DeserializeObject<AudioData>(message);
                if (!string.IsNullOrEmpty(data.Audio))
                {
                    Debug.Log("Enqueuing audio...");
                    byte[] audioData = System.Convert.FromBase64String(data.Audio);
                    for (int i = 0; i < audioData.Length / 2; i++)
                    {
                        short sample = (short)(audioData[i * 2] | (audioData[i * 2 + 1] << 8));
                        audioBuffer[bufferWriteIndex++ % audioBuffer.Length] = sample / 32768f;
                    }
                }

                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            };
            ws.OnOpen += OnOpen;
            ws.OnMessage += OnMessage;
            ws.OnError += OnError;
            ws.OnClose += OnClose;
            _ = ws.Connect();
            _initialMessageSent = false;
            await _connected.Task;
        }

        private void OnOpen()
        {
            Debug.Log("Connection open!");
            _connected.SetResult(true);
        }
        
        private void OnMessage(byte[] msg)
        {
            Debug.Log("OnMessage! " + Encoding.UTF8.GetString(msg));
        }
        
        private void OnError(string errorMsg)
        {
            Debug.Log("OnError! " + errorMsg);
        }
        
        private void OnClose(WebSocketCloseCode code)
        {
            Debug.Log("OnClose! " + code);
            _connected.SetResult(false);
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
        }
    }
#endif
}
