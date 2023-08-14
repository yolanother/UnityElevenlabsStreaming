using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private IPcmPlayer _player;
        private bool _open;
        private bool _ready;
        public string Url => string.Format(_url, _host, _port, _apiKey, _voiceId);
        
        private class WordTrigger {
            public string word;
            public Action<string> onStartChunk;
            public Action<string> onFinishChunk;
        }
        
        private List<WordTrigger> wordStack = new List<WordTrigger>();

        private void Start()
        {
            _player = GetComponent<IPcmPlayer>();
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
            
            while (!_ready && enabled)
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
                if(!_player.IsStreaming) _player.StartClip(channels, frequency);
                
                // Create callbacks for each played letter
                List<ITimedCallback> callbacks = new List<ITimedCallback>();
                if (json.HasKey("normalizedAlignment")
                    && json["normalizedAlignment"].HasKey("chars")
                    && json["normalizedAlignment"].HasKey("charStartTimesMs")
                    && json["normalizedAlignment"].HasKey("charDurationsMs"))
                {
                    var chars = json["normalizedAlignment"]["chars"].AsArray;
                    var times = json["normalizedAlignment"]["charStartTimesMs"].AsArray;
                    var durations = json["normalizedAlignment"]["charDurationsMs"].AsArray;
                    
                    // Combine characters into string
                    var sb = new StringBuilder();
                    for(int i = 0; i < chars.Count; i++)
                    {
                        sb.Append(chars[i].Value);
                    }
                    // Add a callback for the first letter
                    float first = times[0].AsInt / 1000f;
                    callbacks.Add(new TimedCallback(first, () =>
                    {
                        Debug.Log("Started playing " + sb.ToString());
                    }));

                    StringBuilder word = new StringBuilder();
                    for (int i = 0; i < chars.Count; i++)
                    {
                        string c = chars[i].Value;
                        float t = times[i].AsInt / 1000f;
                        float d = durations[i].AsInt / 1000f;
                        callbacks.Add(new TimedCallback(t, () =>
                        {
                            Debug.Log($"Played {c} at {t} for {d}");
                            Debug.Log("Wordstack: " + string.Join(", ", wordStack.Select(w => w.word)));

                        }));
                        
                        // If c is Regex word character
                        if (Regex.IsMatch(c, @"\w"))
                        {
                            word.Append(c);
                        }
                        else
                        {
                            Debug.Log("Word: " + word.ToString());
                            if (wordStack.Count > 0 && wordStack[0].word == word.ToString())
                            {
                                var wordTrigger = wordStack[0];
                                wordStack.RemoveAt(0);
                                if(null != wordTrigger.onStartChunk) callbacks.Add(new TimedCallback(0, () => wordTrigger.onStartChunk(word.ToString())));
                                if(null != wordTrigger.onFinishChunk) callbacks.Add(new TimedCallback(0, () => wordTrigger.onFinishChunk(word.ToString())));
                            }
                            word.Clear();
                        }
                    }
                    if (wordStack.Count > 0 && wordStack[0].word == word.ToString())
                    {
                        var wordTrigger = wordStack[0];
                        wordStack.RemoveAt(0);
                        if(null != wordTrigger.onStartChunk) callbacks.Add(new TimedCallback(0, () => wordTrigger.onStartChunk(word.ToString())));
                        if(null != wordTrigger.onFinishChunk) callbacks.Add(new TimedCallback(0, () => wordTrigger.onFinishChunk(word.ToString())));
                    }
                    
                    // Add a callback for after the last letter
                    float last = (times[times.Count - 1].AsInt + durations[durations.Count - 1].AsInt) / 1000f;
                    callbacks.Add(new TimedCallback(last, () =>
                    {
                        Debug.Log("Finished playing " + sb.ToString());
                        // Print the wordstack use linq to join the words
                        Debug.Log("Wordstack: " + string.Join(", ", wordStack.Select(w => w.word)));
                        
                    }));
                }
                
                _player.AddPcmData(audioBytes, callbacks);
                json["audio"]["data"] = "Read and played.";
            }

            Debug.Log("OnMessage: \n" + json.ToString());
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
            await StartStreamAsync();
            await SendChunkAsync(text);
            await EndStreamAsync();
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
            if(!string.IsNullOrEmpty(text)) await SendChunkAsync(text);
            await EndStreamAsync();
        }

        public async Task StartStreamAsync()
        {
            if (null == _webSocket) await Connect();
            else if (_webSocket.State != WebSocketState.Open) await Connect();
            if (!enabled) return;
            var json = new JSONObject();
            wordStack.Clear();
            await _webSocket.SendText(json.ToString());
        }

        public async Task SendChunkAsync(string text)
        {
            if (null == _webSocket) await StartStreamAsync();
            if (!enabled) return;
            var json = new JSONObject();
            json["text"] = text;
            
            // Get all of the words in the text and ignore empty strings and trim any whitespace
            var regex = new Regex(@"(\w+)", RegexOptions.Compiled);
            var matches = regex.Matches(text);
            var words = matches.Select(m => m.Value.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            for (int i = 0; i < words.Length; i++)
            {
                var word = new WordTrigger
                {
                    word = words[i]
                };
                if (i == 0)
                {
                    word.onStartChunk = (s) => OnStartChunk(text);
                }

                if (i == words.Length - 1)
                {
                    word.onFinishChunk = (s) => OnFinishChunk(text);
                }
                wordStack.Add(word);
            }
            
            await _webSocket.SendText(json.ToString());
        }

        private void OnFinishChunk(string text)
        {
            Debug.Log("OnFinishChunk: " + text);
        }

        private void OnStartChunk(string text)
        {
            Debug.Log("OnStartChunk: " + text);
        }

        public async Task EndStreamAsync()
        {
            if (null == _webSocket) return;
            
            var json = new JSONObject();
            json["final"] = true;
            await _webSocket.SendText(json.ToString());
            _player.CompleteClip();
        }

        private void Update()
        {
            if (null != _webSocket) _webSocket.DispatchMessageQueue();
        }
    }
}