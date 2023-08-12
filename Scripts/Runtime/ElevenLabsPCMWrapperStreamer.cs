using System;
using System.Text;
using System.Threading.Tasks;
using DoubTech.Elevenlabs.Streaming;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public class ElevenLabsPCMWrapperStreamer : MonoBehaviour
    {
        [SerializeField] private string _host;
        [SerializeField] private int _port;
        [SerializeField] private string _apiKey;
        [SerializeField] private string _voiceId;

        private string _url = "ws://{0}:{1}/ws/synthesize?apikey={2}&voice={3}";
        private WebSocket _webSocket;
        private IPcmPlayer[] _players;
        private bool _open;
        private bool _ready;
        public string Url => string.Format(_url, _host, _port, _apiKey, _voiceId);

        private void Start()
        {
            _players = GetComponents<IPcmPlayer>();
        }

        [Button]
        public void Disconnect()
        {
            if (null != _webSocket)
            {
                _webSocket.Close();
                _webSocket = null;
            }
            _open = false;
            _ready = false;
        }
        
        private async Task Connect()
        {
            _ready = false;
            _webSocket = new WebSocket(Url);
            _webSocket.OnOpen += OnOpen;
            _webSocket.OnMessage += OnMessage;
            _webSocket.OnError += OnError;
            _webSocket.OnClose += OnClose;
            await _webSocket.Connect();
            
            while (!_ready)
            {
                await Task.Yield();
            }
            Debug.Log("Connected!");
        }

        private void OnOpen()
        {
            Debug.Log("Connection open!");
            _open = true;
        }

        private void OnMessage(byte[] msg)
        {
            var text = Encoding.UTF8.GetString(msg);
            Debug.Log("OnMessage: " + text);
            var json = JSON.Parse(text);
            if (!_ready)
            {
                _ready = json.HasKey("ready");
            }
            if (json.HasKey("audio") && json["audio"].HasKey("data"))
            {
                var audio = json["audio"]["data"].Value;
                var channels = json["audio"]["channels"];
                var frequency = json["audio"]["frequency"];
                var audioBytes = System.Convert.FromBase64String(audio);
                foreach(var player in _players)
                {
                    player.PlayPcm(audioBytes, channels, frequency);
                }
            }
        }

        private void OnError(string errorMsg)
        {
            Debug.Log("OnError! " + errorMsg);
        }

        private void OnClose(WebSocketCloseCode code)
        {
            Debug.Log("OnClose! " + code);
            Disconnect();
        }

        private void OnDisable()
        {
            Disconnect();
        }

        [Button]
        public async void Send(string text)
        {
            if (null == _webSocket) await Connect();
            else if (_webSocket.State != WebSocketState.Open) await Connect();
            if (null == _webSocket)
            {
                Debug.LogError("Not connected.");
                return;
            }
            Debug.Log("Sending BOM");
            await _webSocket.SendText("{}");
            var json = new JSONObject();
            json["text"] = text;
            json["final"] = true;
            Debug.Log("Sending text: " + text);
            await _webSocket.SendText(json.ToString());
        }

        [Button]
        public void StartStream() => StartStreamAsync();
        
        [Button]
        public void EndStream() => EndStreamAsync();
        
        [Button]
        public void SendPartial(string text) => SendChunkAsync(text);

        public void SendFinal(string text) => SendFinalAsync(text);
        
        public async Task SendFinalAsync(string text)
        {
            await SendChunkAsync(text);
            await EndStreamAsync();
        }

        public async Task StartStreamAsync()
        {
            if (null == _webSocket) await Connect();
            else if (_webSocket.State != WebSocketState.Open) await Connect();
            var json = new JSONObject();
            await _webSocket.SendText(json.ToString());
        }

        public async Task SendChunkAsync(string text)
        {
            if (null == _webSocket) await StartStreamAsync();
            var json = new JSONObject();
            json["text"] = text;
            await _webSocket.SendText(json.ToString());
        }
        
        public async Task EndStreamAsync()
        {
            if (null == _webSocket) return;
            
            var json = new JSONObject();
            json["final"] = true;
            await _webSocket.SendText(json.ToString());
        }

        private void Update()
        {
            if (null != _webSocket) _webSocket.DispatchMessageQueue();
        }
    }
}