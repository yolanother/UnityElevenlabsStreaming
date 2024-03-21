using System.IO;
using System.Threading.Tasks;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
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
        [SerializeField] private string elevenLabsApiKey = "<ELEVENLABS_API_KEY>"; // Replace with your actual API key
        private const string OPENAI_API_KEY = "<OPENAI_API_KEY>"; // Replace with your actual API key
        private const string VOICE_ID = "21m00Tcm4TlvDq8ikWAM";

        private const string URI =
            "wss://api.elevenlabs.io/v1/text-to-speech/{0}/stream-input?model_id=eleven_monolingual_v1&optimize_streaming_latency={1}";

        private WebSocket ws;
        private Queue<string> messageQueue = new Queue<string>();
        private bool isProcessingQueue = false;
        private AudioClip audioClip;
        private MemoryStream audioBuffer;
        private int bufferWriteIndex = 0;
        private bool _initialMessageSent;
        private TaskCompletionSource<bool> _connected;

        async void Start()
        {
            audioBuffer = new MemoryStream();
            // Initialize the audio clip and buffer
            audioClip = AudioClip.Create("ElevenLabsTTS", 44100, 1, 44100, true, PcmReader);

            await ConnectToWebSocket();
            audioSource.clip = audioClip;
            audioSource.Play();
        }

        private void PcmReader(float[] data)
        {
            if (null == audioBuffer || audioBuffer.Length == 0) return;
            
            // Create a binary reader for the memory buffer
            using (BinaryReader reader = new BinaryReader(audioBuffer))
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (audioBuffer.Position < audioBuffer.Length)
                    {
                        // Read a 16-bit sample from the memory buffer
                        short sample = reader.ReadInt16();
                        // Convert the sample to a float in the range -1 to 1 and store it in the data array
                        data[i] = sample / 32768f;
                    }
                    else
                    {
                        // If there is no more data in the memory buffer, fill the rest of the data array with zeros
                        data[i] = 0f;
                    }
                }
            }

            if(audioBuffer.Position >= audioBuffer.Length) audioBuffer.SetLength(0);
        }

        async void Update()
        {
            if (null == ws) return;
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
            audioBuffer.SetLength(0);
            // Clear the queue and send the message
            messageQueue.Clear();
            _ = SendMessageToWebSocket(message);
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
            audioBuffer.SetLength(0);
        }

        public void PausePlayback()
        {
            // Pause the audio playback
            audioSource.Pause();
        }

        private async Task SendMessageToWebSocket(string message)
        {
            if (ws.State != WebSocketState.Open)
            {
                await ConnectToWebSocket();
            }

            var initialMessage = new
            {
                text = " ",
                voice_settings = new { stability = 0.5, similarity_boost = 0.8 },
                xi_api_key = elevenLabsApiKey,
                try_trigger_generation = true
            };
            string initialMessageJson = JsonUtility.ToJson(initialMessage);
            await ws.SendText(initialMessageJson);

            var messageData = new
            {
                text = message,
                try_trigger_generation = true
            };
            string messageJson = JsonUtility.ToJson(messageData);
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
            string uri = string.Format(URI, VOICE_ID, optimizeStreamingLatency);
            ws = new WebSocket(uri);
            ws.OnMessage += (bytes) =>
            {
                string message = Encoding.UTF8.GetString(bytes);
                var data = JsonUtility.FromJson<MessageData>(message);
                if (data.audio != null)
                {
                    byte[] audioData = System.Convert.FromBase64String(data.audio);
                    audioBuffer.Write(audioData);
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
            Debug.Log("OnMessage!");
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

    [System.Serializable]
    class MessageData
    {
        public string audio;
        public bool isFinal;
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