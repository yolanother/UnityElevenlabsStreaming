using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DoubTech.Elevenlabs.Streaming;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public abstract class BaseElevenLabsStreamer : MonoBehaviour
    {
        private IStreamedPlayer _player;
        
        private class WordTrigger {
            public string word;
            public Action<string> onStartChunk;
            public Action<string> onFinishChunk;
        }
        
        private List<WordTrigger> wordStack = new List<WordTrigger>();
        
        private void Start()
        {
            _player = GetComponent<IStreamedPlayer>();
        }
        
        protected virtual void HandleByteMessage(byte[] msg)
        {
            var text = Encoding.UTF8.GetString(msg);
            var json = JSON.Parse(text);
            OnMessage(json);
        }

        public void StartStream() => StartStreamAsync();
        
        public void EndStream() => EndStreamAsync();
        
        public void SendPartial(string text) => SendChunkAsync(text, null, null);
        
        public void SendPartial(string text, Action<string> onStarted, Action<string> onFinished)
        {
            SendChunkAsync(text, onStarted, onFinished);
        }

        public void SendFinal(string text) => SendFinalAsync(text);
        
        public async Task SendFinalAsync(string text)
        {
            if(!string.IsNullOrEmpty(text)) await SendChunkAsync(text, null, null);
            await EndStreamAsync();
        }

        public async void Send(string text)
        {
            await StartStreamAsync();
            await SendChunkAsync(text, null, null);
            await EndStreamAsync();
        }

        public virtual async Task StartStreamAsync()
        {
            wordStack.Clear();
            var json = new JSONObject();
            await OnStartStreamAsync(json);
        }

        protected abstract Task OnStartStreamAsync(JSONObject json);

        public async Task EndStreamAsync()
        {
            await OnEndStreamAsync(new JSONObject());
            _player.CompleteClip();
        }

        protected abstract Task OnEndStreamAsync(JSONObject json);

        public virtual async Task SendChunkAsync(string text, Action<string> onStarted, Action<string> onFinished)
        {
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
                    word.onStartChunk = (s) =>
                    {
                        OnStartChunk(text);
                        onStarted?.Invoke(text);
                    };
                }

                if (i == words.Length - 1)
                {
                    word.onFinishChunk = (s) =>
                    {
                        OnFinishChunk(text);
                        onFinished?.Invoke(text);
                    };
                }
                wordStack.Add(word);
            }

            await OnSendChunk(json);
        }

        protected virtual void OnFinishChunk(string text)
        {
            Debug.Log("OnFinishChunk: " + text);
        }

        protected virtual void OnStartChunk(string text)
        {
            Debug.Log("OnStartChunk: " + text);
        }

        protected abstract Task OnSendChunk(JSONObject json);

        protected virtual void OnMessage(JSONNode json)
        {
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
                
                _player.AddData(audioBytes, callbacks);
                json["audio"]["data"] = "Read and played.";
            }

            Debug.Log("OnMessage: \n" + json.ToString());
        }
    }
}