using System;
using System.Collections.Generic;
using System.Text;
using DoubTech.Elevenlabs.Streaming.NativeWebSocket;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public class ElevenLabsStreamer : MonoBehaviour
    {
        [SerializeField] private string _apiKey;
        [SerializeField] private string _voiceId;
        [SerializeField] private string _modelId;
        
        private string _url = "wss://api.elevenlabs.io/v1/text-to-speech/{0}/stream-input?model_id={1}";
        private WebSocket _webSocket;
        public string Url => string.Format(_url, _voiceId, _modelId);

        private void Connect()
        {
            _webSocket = new WebSocket(Url, new Dictionary<string, string>
            {
                { "xi-api-key", _apiKey }
            });
            _webSocket.OnOpen += OnOpen;
            _webSocket.OnMessage += OnMessage;
            _webSocket.OnError += OnError;
            _webSocket.OnClose += OnClose;
        }
        
        private void OnOpen()
        {
            Debug.Log("Connection open!");
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
        }

        private void Update()
        {
            if(null != _webSocket) _webSocket.DispatchMessageQueue();
        }
    }
}