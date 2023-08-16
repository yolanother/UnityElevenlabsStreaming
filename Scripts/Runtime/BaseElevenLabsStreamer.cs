using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DoubTech.Elevenlabs.Streaming;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEditor;
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
        private List<WordTrigger> chunkQueue = new List<WordTrigger>();

        private Coroutine _terminateChunk;
        private bool _streaming;
        public bool IsStreaming => _streaming;
        
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
            if (_streaming) return;
            
            Debug.Log("StartStreamAsync");
            _streaming = true;
            wordStack.Clear();
            var json = new JSONObject();
            await OnStartStreamAsync(json);
        }

        protected abstract Task OnStartStreamAsync(JSONObject json);

        public async Task EndStreamAsync()
        {
            if (!_streaming) return;
            
            _streaming = false;
            await OnEndStreamAsync(new JSONObject());
            _player.CompleteClip();
        }

        protected abstract Task OnEndStreamAsync(JSONObject json);

        public virtual async Task SendChunkAsync(string text, Action<string> onStarted, Action<string> onFinished)
        {
            await StartStreamAsync();
            if (!enabled) return;
            var json = new JSONObject();
            json["text"] = text;
            
            // Super hacky way to track if a particular chunk was started, but not tracked in the
            // output msg.
            var chunkTrigger = new WordTrigger
            {
                word = text,
                onStartChunk = (s) =>
                {
                    OnStartChunk(text);
                    onStarted?.Invoke(text);
                },
                onFinishChunk = (s) =>
                {
                    OnFinishChunk(text);
                    onFinished?.Invoke(text);
                }
            };
            chunkQueue.Add(chunkTrigger);
            
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

                if (i == words.Length - 1)
                {
                    word.onFinishChunk = (s) =>
                    {
                        int index = chunkQueue.IndexOf(chunkTrigger);
                        for (int i = 0; i < index; i++)
                        {
                            chunkQueue[i].onStartChunk?.Invoke(chunkQueue[i].word);
                            chunkQueue[i].onStartChunk = null;
                            chunkQueue[i].onFinishChunk?.Invoke(chunkQueue[i].word);
                            chunkQueue[i].onFinishChunk = null;
                            chunkQueue.RemoveAt(0);
                        }
                        
                        chunkQueue.Remove(chunkTrigger);
                        
                        // If the start wasn't called, invoke it before calling finish.
                        var start = chunkTrigger.onStartChunk;
                        var finish = chunkTrigger.onFinishChunk;
                        chunkTrigger.onStartChunk = null;
                        chunkTrigger.onFinishChunk = null;
                        start?.Invoke(chunkTrigger.word);
                        finish?.Invoke(chunkTrigger.word);
                    };
                }
                wordStack.Add(word);
            }

            await OnSendChunk(json);
            if(null != _terminateChunk) StopCoroutine(_terminateChunk);
            _terminateChunk = StartCoroutine(TerminateChunk());
        }

        private IEnumerator TerminateChunk()
        {
            yield return new WaitForSeconds(.25f);
            EndStream();
        }

        protected virtual void OnFinishChunk(string text)
        {
            
        }

        protected virtual void OnStartChunk(string text)
        {
            
        }

        protected abstract Task OnSendChunk(JSONObject json);

        protected virtual void OnMessage(JSONNode json)
        {
            if (json.HasKey("audio") && json["audio"].HasKey("data"))
            {
                var audio = json["audio"]["data"].Value;
                var channels = json["audio"]["channels"].AsInt;
                var frequency = json["audio"]["frequency"].AsInt;
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

                    StringBuilder word = new StringBuilder();
                    for (int i = 0; i < chars.Count; i++)
                    {
                        string c = chars[i].Value;
                        float t = times[i].AsInt / 1000f;
                        float d = durations[i].AsInt / 1000f;
                        
                        // If c is Regex word character
                        if (Regex.IsMatch(c, @"\w"))
                        {
                            word.Append(c);
                        }
                        else
                        {
                            var w = word.ToString();
                            ProcessWord(w, callbacks);
                            word.Clear();
                        }
                    }
                    ProcessWord(word.ToString(), callbacks);
                }
                
                callbacks.Add(new TimedCallback(_player.Duration(audioBytes.Length, frequency, channels), () =>
                {
                    // Flush the buffer if this segment ended and there is no more data to play.
                    if (_player.StreamBufferSize == 0)
                    {
                        foreach (var word in wordStack)
                        {
                            word.onStartChunk?.Invoke(word.word);
                            word.onFinishChunk?.Invoke(word.word);
                        }
                        wordStack.Clear();
                    }
                }));
                _player.AddData(audioBytes, callbacks);
                json["audio"]["data"] = "Read and played.";
            }
            else if (_player.StreamBufferSize == 0)
            {
                foreach (var word in wordStack)
                {
                    word.onStartChunk?.Invoke(word.word);
                    word.onFinishChunk?.Invoke(word.word);
                }
                wordStack.Clear();
            }

            Debug.Log("OnMessage: \n" + json.ToString());
        }

        private void ProcessWord(string word, List<ITimedCallback> callbacks)
        {
            if (wordStack.Count > 0 && !string.IsNullOrEmpty(word?.Trim()))
            {
                while (wordStack[0].word != word)
                {
                    // Flush the queue, we missed a word.
                    var wordTrigger = wordStack[0];
                    ProcessWordTrigger(wordTrigger, callbacks, wordTrigger.word);
                }

                if (wordStack[0].word == word)
                {
                    var wordTrigger = wordStack[0];
                    ProcessWordTrigger(wordTrigger, callbacks, word);
                }
            }
        }

        private void ProcessWordTrigger(WordTrigger wordTrigger, List<ITimedCallback> callbacks, string word)
        {
            wordStack.RemoveAt(0);
            if (null != wordTrigger.onStartChunk)
            {
                callbacks.Add(new TimedCallback(0,
                    () => wordTrigger.onStartChunk(word)));
            }

            if (null != wordTrigger.onFinishChunk)
            {
                callbacks.Add(new TimedCallback(0,
                    () => wordTrigger.onFinishChunk(word)));
            }
        }
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(BaseElevenLabsStreamer), true)]
    public class ElevenLabsPCMWrapperStreamerEditor : UnityEditor.Editor
    {
        private bool _simulateStreamed;
        private float _delayBetweenSentences = .1f;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            GUILayout.Space(16);
            GUILayout.Label("Debug");
            
            var streamer = (BaseElevenLabsStreamer) target;
            var text = EditorPrefs.GetString(target.GetType().FullName + "::text", "");
            var t = GUILayout.TextArea(text);
            if (t != text)
            {
                EditorPrefs.SetString(target.GetType().FullName + "::text", t);
            }
            
            if (GUILayout.Button("Send"))
            {
                if (_simulateStreamed) SimulateResponse(t);
                else streamer.Send(t);
            }

            var toggle = GUILayout.Toggle(streamer.IsStreaming, "Text Stream");
            if (!streamer.IsStreaming && toggle)
            {
                streamer.StartStream();
            }
            else if (streamer.IsStreaming && !toggle)
            {
                streamer.EndStream();
            }
            _simulateStreamed = GUILayout.Toggle(_simulateStreamed, "Simulate Streamed");
            _delayBetweenSentences = EditorGUILayout.FloatField("Delay Between Sentences", _delayBetweenSentences);

            if (GUILayout.Button("Ping"))
            {
                streamer.Send("ping");
            }

            if ((streamer is IWebSocket ws))
            {
                GUILayout.BeginHorizontal();
                if (!ws.IsConnected && GUILayout.Button("Connect"))
                {
                    ws.Connect();
                }

                if (ws.IsConnected && GUILayout.Button("Disconnect"))
                {
                    ws.Disconnect();
                }
                GUILayout.EndHorizontal();
            }
        }

        private async void SimulateResponse(string text)
        {
            var streamer = (BaseElevenLabsStreamer) target;
            var sentences = text.Split(new[] {". "}, StringSplitOptions.RemoveEmptyEntries);
            streamer.StartStream();
            for (int i = 0; i < sentences.Length; i++)
            {
                var sentence = sentences[i];
                streamer.SendPartial(sentence, (s) =>
                {
                    Debug.Log("Started submitted sentence: " + sentence);
                }, (s) =>
                {
                    Debug.Log("Finished submitted sentence: " + sentence);
                });
                if (i < sentences.Length - 1)
                {
                    await Task.Delay((int) (_delayBetweenSentences * 1000));
                }
            }
            streamer.EndStream();
        }
    }
#endif
}