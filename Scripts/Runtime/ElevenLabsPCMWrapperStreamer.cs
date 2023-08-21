using System;
using System.Threading.Tasks;
using DoubTech.Elevenlabs.Streaming;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
#if VOICESDK
using Meta.WitAi.Attributes;
#endif
using UnityEngine;
using IWebSocket = Doubtech.ElevenLabs.Streaming.Interfaces.IWebSocket;

namespace Doubtech.ElevenLabs.Streaming
{
    public class ElevenLabsPCMWrapperStreamer : BaseElevenLabsStreamer, IElevenLabsTTS, IWebSocket
    {
        [SerializeField] private string _host;
        [SerializeField] private int _port;
        #if VOICESDK
        [HiddenText]
        #endif
        [SerializeField] private string _apiKey;
        [SerializeField] private string _voiceId;

        private string _url = "ws://{0}:{1}/ws/synthesize?apikey={2}&voice={3}";
        private WebSocket _webSocket;
        private bool _open;
        private bool _ready;
        private bool _connecting;
        public string Url => string.Format(_url, _host, _port, _apiKey, _voiceId);
        public bool IsConnected => _open;

        protected override void OnMessage(JSONNode json)
        {
            if (!_ready)
            {
                _ready = json.HasKey("ready");
                Debug.Log("Ready!");
            }
            base.OnMessage(json);
        }

        public async Task Disconnect()
        {
            if (null != _webSocket)
            {
                await _webSocket.Close();
                _webSocket = null;
            }
            _open = false;
            _ready = false;
        }
        
        public async Task Connect()
        {
            if (_open || _connecting)
            {
                while (!_ready && enabled)
                {
                    await Task.Yield();
                }
                return;
            }

            _connecting = true;
            _ready = false;
            _webSocket = new WebSocket(Url);
            _webSocket.OnOpen += OnOpen;
            _webSocket.OnMessage += HandleByteMessage;
            _webSocket.OnError += OnError;
            _webSocket.OnClose += OnClose;
            Debug.Log("Connecting...");
            
            // Don't await the connection, Connect stays open until the server is closed.
            _webSocket.Connect();
            
            while (!_ready && enabled)
            {
                await Task.Yield();
            }

            _connecting = false;
            Debug.Log("Connected!");
        }

        private void OnOpen()
        {
            _connecting = false;
            _open = true;
        }

        private void OnTimeHit()
        {
            throw new NotImplementedException();
        }

        private void OnError(string errorMsg)
        {
            Debug.Log("OnError! " + errorMsg);
        }

        private void OnClose(WebSocketCloseCode code)
        {
            _connecting = false;
            Debug.Log("OnClose! " + code);
            Disconnect();
        }

        private void OnDisable()
        {
            Disconnect();
        }

        public override async Task StartStreamAsync()
        {
            await Connect();
            await base.StartStreamAsync();
            var json = new JSONObject();
            json["text"] = " ";
            _webSocket.SendText(json);
        }

        protected override async Task OnStartStreamAsync(JSONObject json)
        {
            if (!enabled) return;
            
            await Connect();
            await _webSocket.SendText(json.ToString());
        }

        protected override async Task OnSendChunk(JSONObject json)
        {
            await _webSocket.SendText(json.ToString());
        }

        public override async Task SendChunkAsync(string text, Action<string> onStarted, Action<string> onFinished)
        {
            await Connect();
            await StartStreamAsync();
            await base.SendChunkAsync(text, onStarted, onFinished);
        }

        protected override async Task OnEndStreamAsync(JSONObject json)
        {
            if (null == _webSocket) return;
            
            json["final"] = true;
            await _webSocket.SendText(json.ToString());
        }

        private void Update()
        {
            if (null != _webSocket) _webSocket.DispatchMessageQueue();
        }
    }
}