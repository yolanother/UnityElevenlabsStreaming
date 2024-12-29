using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Doubtech.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming.Interfaces;
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
        [Header("Eleven Labs")]
        [SerializeField] private ElevenLabsConfig config;

        [Header("Debugging")]
        [Tooltip("If true a debug file is written when audio is received from the server")]
        [SerializeField] private bool writeDebugFile;

        private WebSocket ws;
        private Queue<string> messageQueue = new Queue<string>();
        private bool isProcessingQueue = false;
        private bool _initialMessageSent;
        private TaskCompletionSource<bool> _connected;
        private FileStream debugStream;
        private FileStream responseStream;

        private ConcurrentQueue<Action> mainThreadActionBuffer = new ConcurrentQueue<Action>();

        private bool IsConnected => ws?.State == WebSocketState.Open;
        
        private IStreamedAudioPlayer audioPlayer;
        
        private void Awake()
        {
            audioPlayer = GetComponent<IStreamedAudioPlayer>();
        }

        public void Connect()
        {
            _ = ConnectToWebSocket();
        }

        async void Update()
        {
            if (ws == null) return;
            
            ws.DispatchMessageQueue();
            while (mainThreadActionBuffer.Count > 0 && mainThreadActionBuffer.TryDequeue(out var result))
            {
                result?.Invoke();
            }
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
            audioPlayer.Stop();
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
            messageQueue.Clear();
            audioPlayer.Stop();
        }

        public void PausePlayback()
        {
            // Pause the audio playback
            audioPlayer.Pause();
        }

        private async Task SendMessageToWebSocket(string message, bool close = false)
        {
            if (!IsConnected) await ConnectToWebSocket();

            var initialMessage = new
            {
                text = " ",
                voice_settings = new { stability = 0.5, similarity_boost = 0.8, use_speaker_boost = false },
                generation_config = new
                {
                    chunk_length_schedule = new List<int> {120, 160, 250, 290}
                },
                xi_api_key = config.apiKey
            };
            await SendData(initialMessage);
            await SendData(new { text = message });
            if(close) await SendData(new { text = "" });
        }

        private async Task SendData(object messageData)
        {
            string messageJson = JsonConvert.SerializeObject(messageData);
            await ws.SendText(messageJson);
        }

        private IEnumerator ProcessMessageQueue()
        {
            if (isProcessingQueue) yield break;

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
            if(IsConnected) return;
            if (null != _connected && !_connected.Task.IsCompleted)
            {
                await _connected.Task;
                return;
            }

            _connected = new TaskCompletionSource<bool>();
            /*string uri = string.Format(URI, voiceId, optimizeStreamingLatency);
            if (enableSsml)
            {
                uri += "&enable_ssml_parsing=true";
            }
            if (syncAlignment)
            {
                uri += "&sync_alignment=true";
            }*/
            var headers = new Dictionary<string, string>
            {
                {"xi-api-key", config.apiKey}
            };
            ws = new WebSocket(config.Url, headers);
            ws.OnOpen += OnOpen;
            ws.OnMessage += OnMessage;
            ws.OnError += OnError;
            ws.OnClose += OnClose;
            _ = ws.Connect();
            _initialMessageSent = false;
            await _connected.Task;
        }

        private void ProcessMessage(string message)
        {
            var data = JsonConvert.DeserializeObject<AudioData>(message);
            if (!string.IsNullOrEmpty(data.Audio))
            {
                byte[] audioData = System.Convert.FromBase64String(data.Audio);
                audioPlayer.AddData(audioData);

                if (null != debugStream) debugStream.Write(audioData);
                if (null != responseStream) responseStream.Write(Encoding.UTF8.GetBytes(message + "\n"));
            }
            
            audioPlayer.Play();
        }

        internal void PlayTestMessage()
        {
            var file = Path.Combine(Application.dataPath, "test.pcm");
            if(writeDebugFile) debugStream = new FileStream(file, FileMode.Create);
            ProcessMessage("{\"audio\":\"8v/r//j//v/2//X/+f/3//b///8DAP7//P/+/wAABAACAP3/AgD9//r/BwACAPz//v/+/wIABAAIAAsAAwD8////AQD+////+f/x//z//f/y//H/8f/+/woACQAGAP7/9v/0//3/AQD+//z/+/8BAP//+P/3//j/9//y//D/8//5/wAA+v/0//r//v///wMABgADAPz/AQD///T/+v/7//X/+f8BAAoACgAEAAYADAAHAAEA+v/2//r/+/8BAAwAEQAPAAgABwAJAAsA/v/z//z/AwAIAAsADAALAAYACQAKAAMAAwAGAAUABgAGAAcACgAFAPn/+f8EAAgAAAD//wQABwAHAAsAFAAMAAEAAQD//wAABQAJAAcA//////3/+P8AAAcAAwAEAA0ADAAEAAoADQAGAAgADgAOAAYABQAHAAYACAAMAA4ADgATABoAFAARABEABwD//wYAEwAbABwAGgAWAA4ABgAGAAoADAALAA8AEgAXABwAFQATABIADAARAAgA/P8AAAAAAgANAAsACAAMAAcACgAJAAMAAgD8/wUABQAAAPv/6f/f/+b/7f/l/+L/6v/t/+z/8v///wgA/P/r/+z/8P/3/wkAEAAEAPf/9//z/+n/6//4/wIA+f/2/wwADAD3//T/8//x//b/7P/u//f/+f8DAAEA+//3/wAACwD8//r/+//u/+7/8v/5//v/9//+/woAEQASAA0ACAAKAA4ABQAEAAwABQAAAAkAEQAWAAsA//8FAA0AFgAYAAwAAwADAAMABAAIAAIAAgADAAQACwAHAAYA/f/y/wAABgAJABAAAwD//xUAEwAKABIADwAMAAcAAAABAAUACAAGAAoAEwAMAAsAEAAPAA0ABgAEAAcADQAWABQADAAEAAEABAAMAAwABQD+//n/+/8AAAMA+f/v//X//P8GAA0ACwAMABEAFAAOAAQABwAIAPr/8P/y//z/AwADAAQA+v/2//f/8P/4/wAA+//0//H/8P/v//T/8f/t/+7/7P/y/wgADQD3//P/+v/6/wMADgAMAAIA/P/3//n/+P/1//T/8f/8//3/9P/2//z/AAD3//v/DwARABIADAD7//f//f8DAAAA+f/w/+7/+f8CAAYA/f/0//z//P/3//3/AwD9//z/AgD0/+7//P///wMA/P/2/wIAAQAMABIA+P/z//3/9//1//n/+P/7////BgAIAAMABQAHAP7/+v8BAAYACQATABIABgAOABIACwANAAkACgAPAAAA/P8JAAEAAgAQABIAGwARAAUACQAQABMACgAIAAQA/v////7/9v/w//v/+v/z//3/+//8/wcABQAFAAoAEwAeABoAEQANAAkACQAKAAkAEQAUAA0ADAARABgAFAAPAAoA/v8CAAQAAwAJAAYADwAcABEAAQD7//r/+v8LABsACwAAAAMA/v8AAAAA+/8EAAkACwALAAcABwAEAAQABAADAAQABgD///b/+//1/+r/6P/u//j//v8BAPD/6v/w//D/+P/2//b/+f/v//L/8v/v//T/8v/3//T/7//y/+7/6//n/+P/6v/2//H/7P/x/+//8f/4//L/9P/9//n/9f/y/+//+P/+//b/AgACAPP/+P/y/+//+v/1/+3/8P/4/wIABQADAP////8BAP//BgAGAPn/8P/p/+r/7//z//T/8f/s/+X/5//w//7/AwD7//z/9v/r//X/BwAOAAwAAwAAAP3/8f/y//f/+v/7/wMACgADAAAAAgD9//b/+f8CAAgAAgD5//3/+v/9/wcAAgAGAP//9P/4//j/+P/+/wgACQABAAMAAAADAAsAAwAFAA4AEAAOABAADQAEABUAIgAYABsAFgARABgAEQAHAAwAGgAeAB0AHAAWAAsA//8LABIAAQD//wMACwAOAAgACgAJAAQACQARABkAHgAVABIAEQAKAAUAAwAEAAcACgAHAAkAEQAQAAsABgAFAAgAAQD7////AgADAAcACQALAAoABQADAAIA+f/0/+v/4v/x//f/9f/7//H/7//8//v/9P8DAA8ABwACAP7/AQAGAAEA/f/z//P/AQD///b/8P/y//r/+P/0/+//7v/4//X/6P/q/+//9P/0//H/8v/2//D/5f/s//j//f///wEA/P/z//X/8v/t//H/8v/v//H/+f/9//X/9f///wQABwAKAAYA/f/9/wwAEgAJAAMAAAD8/wAAAwADAAEA/f/5//X/9v/z//X/9v/y//r//f/9/wYACgAEAPv/9f/3//n/+f8FAAcA/P/5//z/AgABAAIAAQAAABIAEQADAAMA/f/w//b/BgAOAAsABwAEAP3////8//r////4//v/BwADAPr/+P///wAAAAAGAAoACQD+//j/AAAAAAUACAABAAEAAQAFAAkADQASAA8AEQAWAA4ACgATABAADgATABAAEwATAAYACwAPAAcABAAHAAYABgAKAAEAAQACAP3/DAAPAAkADwAIAP3//P/6//X/BAAaABYACwAIAAMA/v/3//T/+f/5//z//f/4//f/9v/5/wIABQAEAAEA/v/+//z/+//6//3/CAAIAP3/+v/+/wMA/v///wQA+//3////CAAKAAcABQD+/wAABQD9//H/7//0//b/9//t/+v/9f/3//n/8v/q//L////9//X//v8EAAMA/f/w/+//9P/y//D/9v/6//j/9//y/+3/7v/y/+7/7P/z//n/8//w//v//P/8////9//5//z/8P/p/+D/3//r//H/8//0//f/9v/+/wgAAgAAAAMADwAXAA8AFAARAAEA9f/2/wcAEAAPABIACAD7//X/9v8CAAEA//8IAAQABAAHAAUAAAD1//r/BQACAAIACQAJAAkADAAFAAoAEAANAAcAAgAHAAkACwAQABYAGAAQAA0ACQAPABwAHAAZABoAIQAgABYAGQAbABkAHQAbABAAEwATAAAA+v8HABQAEQADAAEABwAHAAQA///+/wUABAADAAIA9/8BAAcAAgAHAAAACQAQAAIACQARAAYA///7//n/+f8BAAEA/P8HAAcAAQADAAQAAgD8//7/AgD8//j/+f/2//X/AAAIAAMA8f/r//n/+P/x//f/+//9//z/AgAHAPz/9f/1//r/AAD+//j/8//y//H/7f/w/+3/6v/r/+b/6v/0//n/AAAHAAEA9P/1//7/AwAEAP//+v/0//j/+//2//7/AQD9/wAA/f/4//X/9P/y//T//f8FAAYA+//0//b//v8BAPn/7//u//T/6v/s//v/7v/n//X/+f/4//L/8P/4//j/+f8AAP7/+/8DAAEA7//6/wgA/P8GAAcA+v8JAAUA+f8JAA4ADgAaABUABAD9//n///8CAP//BwD///z/BAAGAAkACAAEAAIACAAMAAwAEAAOAAgACgAMAAYACAAPAA4ADAAIAAcACgAOAA0ACwAHAAAABgAEAP3/CAAWABcAEgANAAoABwAGAAgABAD4//j/AQABAAMA///7/wAAAwAJAAoADQAKAAMACwAUABYAEAAHAA0ACAD9/wgADQAJAAYABAANAAwAAAD8//r//f8EAAQA///+/wAA/P/5//v/AgAEAPn/9//+/wIA+//8/wIA+v/5//j/7f/t//D/8f/4//3/AAD4//D/+P/5//j////3//D//P8LAA0ACgAHAPr/+P////v//P/6//b/+f/9/////f/9//3/+P/6//7/BAAJAPv/9v/7//T/+P/7//f//P8AAP7//P/8/wQAAgD3//z/BgAIAAUABwAFAAEA//8GAAgA/P/+/wMAAwAFAAUABAACAAAA9f/z//r//f8AAAIACQANAAwAAgD7/wEA/f/9/wAA+f/6/wEAAQD7//f/+/8DAAgABAAAAAEA+v/9/w8AFAASABEADAANAA8AAQD5/wEACQAMAAgABgD///v///8EAA4ADQAFAAQABgAIAAsAEQAIAAEAAQD4//7/CwARABQADgAHAAMAAwABAPr/9//5//7/BgAIAPv/9f8BAAMA//8DAAQAAgD///7////2//z/BQACAAEA/P/2/+//9/8FAAgADwAOAAMABQAJAAUAAAABAP7/+v/9/wIABgAFAPz/9f/5//z//v8EAAEA/f/+//X/9P////b/9P/8//j//f8IAAQA+f/1//P/9v/2//P/8//v//f//v/+/wUAAgD9/wEA/P/0//H/8f/z//j//v8EAAQA+//7/wYAAQD5//7///8GAAYA+//6//b/7v/w//T/9f/3//f/8//w//H//P/+//v/AQD9//r/+v///wUA/v/6//j/9P/8/wIABAABAP3//P/5//7/AgAAAP7/+f/6/wQACQAGAPz/9v/+/wMAAQD//wEABQAIAAoABgAAAAAABAD+//v/AAAEAAwAEQAQAAoACgANAAMAAgAKAAsABgAEAAoABgD//wEABQAIAAcABgAJAAoABwAJAAkABAAEAAYABgAIAAQA/P/8/wAAAwACAP7/+/8DAAUA/f/+/wAA/P/+/wUABgACAP7/+v/9/wEA/v8AAP//AAADAAMAAgAAAAIAAQAAAP//+v/9/wUACQAGAAIABQABAAIAAAD//woAAwD8////+f8BAAsAAwD8//j/+/8GAAMA/f/8//X/+f8HAAkAAAAAAAQA+//9/wEA/P8EAAcAAAD6//j//f/9//7/BAD//wUADAD9//f///8GAAoAAAACAAkACgAOAAoABAABAAAA/v/6//n/CAAOAP3//P8GAAsA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vQOBBT4Apfxv/e3+IgH4ABz/lf5P/3EAvgBjAML/Yv+T/87/JAA/ACUAYQAcAHP/Qv98/yQAVADQ/7//9v8ZADcA4v+2/9z/xv/y/0YAOgAPAPj/BwA1ADUAAgDp//z/DgAOAAwA+v/R/8X/+f8wABkA5//r/wQAHAArAAEA2////x8ACwDk/+b/EwAnABcAAwD5//T/7//y/wEABgD+//H/6P/1/wYADQAQAAoA+v/9/xQAFAAEAAAA+v/8/wYABQAAAAAABgAIAAQABgAEAP//AQABAAMACQAIAAYABQAGAAYAAAAAAAMABwAFAP///v8CAAUAAwACAAIABAAEAAEAAQACAAUABQABAAEABAAEAAUABAADAAEA//8CAAcABQABAAEAAQACAAQABAACAAEAAgACAAMABQADAAAAAQADAAUABAACAAMAAwAEAAUAAwABAAIABAADAAIAAgACAAAAAAACAAIAAQABAAEAAQAAAAEAAgACAAQAAgAAAAEAAgADAAIAAAAAAAIAAQABAAEAAQACAAEAAQABAAAAAAACAAEAAAABAP///v8BAAMAAgAAAP7//f/+//7//f/7//v//f/+//v/+//9//7//v/+//3/+f/6//3/AwAFAP//AAACAAIAAwADAAMAAwADAAMAAgADAAMAAwAEAAQABAAFAAUABAACAAAAAAABAAAAAQACAAEAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAQABAAIAAgACAAIAAgACAAIAAgACAAIAAQABAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAEAAQACAAEAAQACAAIAAgACAAEAAgACAAIAAgACAAIAAgACAAEAAQACAAIAAgACAAIAAgACAAEAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgABAAEAAgACAAIAAgABAAEAAQABAAIAAQABAAIAAgACAAIAAQACAAIAAgACAAIAAgACAAIAAgACAAEAAQACAAIAAQACAAIAAQACAAIAAgACAAEAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAMAAwACAAEAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAwADAAIAAwADAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAQABAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgABAAEAAQACAAIAAgACAAIAAgACAAIAAgACAAIAAgABAAEAAgACAAEAAQACAAIAAgABAAIAAgABAAIAAgABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQACAAEAAQABAAIAAgABAAEAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAIAAgACAAIAAQABAAIAAQABAAIAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAAABAAEAAQABAAEAAQABAAAAAAAAAAAAAAAAAAEAAQAAAP7//P/9//v/\",\"isFinal\":null,\"normalizedAlignment\":null,\"alignment\":null}");
            ProcessMessage("{\"audio\":null,\"isFinal\":true,\"normalizedAlignment\":null,\"alignment\":null}");
            if (writeDebugFile)
            {
                debugStream.Close();
                Debug.Log($"Wrote audio to: {file}");
            }
        }

        private void OnOpen()
        {
            _connected.SetResult(true);

            if (writeDebugFile)
            {
                debugStream = new FileStream(Path.Combine(Application.dataPath, "test.pcm"), FileMode.Create);
                responseStream = new FileStream(Path.Combine(Application.dataPath, "response.json"), FileMode.Create);
            }
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
            if(!_connected.Task.IsCompleted) _connected.SetResult(false);
            if (null != debugStream)
            {
                try
                {
                    debugStream.Close();
                    debugStream = null;
                }
                catch (Exception e)
                {
                    // Do nothing
                }
            }
            if (null != responseStream)
            {
                try
                {
                    responseStream.Close();
                    responseStream = null;
                }
                catch (Exception e)
                {
                    // Do nothing
                }
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
