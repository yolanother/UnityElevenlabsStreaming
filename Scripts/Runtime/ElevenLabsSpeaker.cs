using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEngine;
#if VOICESDK
using System;
using Meta.Voice.Audio;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Utilities;
using Meta.WitAi.Composer.Integrations;
using Meta.WitAi.TTS.Data;
#endif

namespace Meta.MurderMystery.UnityElevenlabsStreaming.Scripts
{
    public class ElevenLabsSpeaker : MonoBehaviour
#if VOICESDK
        , ISpeaker
#endif
    {
        #if VOICESDK
        [SerializeField]
        private TTSSpeakerEvents _events = new TTSSpeakerEvents();
        #endif

        private IStreamedPlayer _player;
        private IElevenLabsTTS _elevenLabs;

        private void Awake()
        {
            _player = GetComponent<IStreamedPlayer>();
            _elevenLabs = GetComponent<IElevenLabsTTS>();
        }

        private Dictionary<string, List<ElevenLabsQueue>> activeRequests = new Dictionary<string, List<ElevenLabsQueue>>();

        private bool _isSpeaking;

        #region Voice SDK Speaker

        public bool IsSpeaking => _player.IsPlaying;
        public bool IsPaused => false;
        public string VoiceID { get; set; }
        


        public IEnumerator SpeakAsync(string text)
        {
            Stop();

            var finished = false;
            _elevenLabs.SendPartial(text, null, onFinished: (text) => finished = true);

            yield return new WaitUntil(() => finished);
        }

        public void Stop()
        {
            _elevenLabs.EndStream();
            _player.Stop();

            #if VOICESDK
            foreach (var sets in activeRequests.Values)
            {
                foreach (var r in sets)
                {
                    r.events?.OnPlaybackCancelled?.Invoke(null, r.clipData, r.text);
                }
            }
            #endif
        }

        public void Pause()
        {
            _player.Pause();
        }

        public void Resume()
        {
            _player.Resume();
        }

        public void PrepareToSpeak()
        {
            _elevenLabs.StartStream();
        }

        public void StartTextBlock()
        {
            _elevenLabs.StartStream();
        }

        public void EndTextBlock()
        {
            _elevenLabs.EndStream();
        }

#if VOICESDK
        public TTSSpeakerEvents Events => _events;
        public IAudioPlayer AudioPlayer { get; }

        private void RequestSpeech(string text, TTSSpeakerClipEvents playbackEvents, Action onFinished = null)
        {
            TTSClipData clipData = new TTSClipData();
            clipData.clipID = text;
            clipData.textToSpeak = text;
            
            var request = new ElevenLabsQueue(text, playbackEvents);
            request.clipData = clipData;
            if (!activeRequests.TryGetValue(request.key, out var activeList))
            {
                activeList = new List<ElevenLabsQueue>();
                activeRequests[request.key] = activeList;
            }
            activeRequests[request.key].Add(request);
            
            playbackEvents?.OnInit?.Invoke(null, clipData);
            playbackEvents?.OnLoadBegin?.Invoke(null, clipData);
            _elevenLabs.SendPartial(text, 
                (t) =>
                {
                    Debug.Log($"Starting: {clipData.textToSpeak}");
                    playbackEvents?.OnAudioClipPlaybackReady?.Invoke(_player.Clip);
                    playbackEvents?.OnLoadSuccess?.Invoke(null, clipData);
                    playbackEvents?.OnPlaybackStart?.Invoke(null, clipData);
                    playbackEvents?.OnTextPlaybackStart?.Invoke(clipData.textToSpeak);
                    playbackEvents?.OnAudioClipPlaybackStart?.Invoke(_player.Clip);
                    
                    Events.OnAudioClipPlaybackReady?.Invoke(_player.Clip);
                    Events.OnPlaybackStart?.Invoke(null, clipData);
                    Events.OnAudioClipPlaybackStart?.Invoke(_player.Clip);
                    Events.OnTextPlaybackStart?.Invoke(text);
                },
                (t) =>
                {
                    Debug.Log($"Finished: {clipData.textToSpeak}");
                    playbackEvents?.OnPlaybackComplete?.Invoke(null, clipData);
                    playbackEvents.OnTextPlaybackFinished?.Invoke(clipData.textToSpeak);
                    playbackEvents?.OnAudioClipPlaybackFinished?.Invoke(_player.Clip);
                    
                    Events.OnPlaybackComplete?.Invoke(null, null);
                    Events.OnTextPlaybackFinished?.Invoke(t);
                    
                    onFinished?.Invoke();
                    activeRequests[request.key].Remove(request);
                    Events.OnComplete?.Invoke(null, clipData);
                });
        }

        public void Speak(string textToSpeak, TTSSpeakerClipEvents playbackEvents)
        {
            Stop();
            RequestSpeech(textToSpeak, playbackEvents);
        }

        public void SpeakQueued(string text, TTSSpeakerClipEvents playbackEvents)
        {
            RequestSpeech(text, playbackEvents);
        }
        
        public IEnumerator SpeakQueuedAsync(string[] textsToSpeak, TTSSpeakerClipEvents playbackEvents)
        {
            var last = textsToSpeak.Last();

            var finished = false;

            foreach (var text in textsToSpeak)
            {
                _elevenLabs.SendPartial(text, null, onFinished: (text) => finished = true);
            }

            yield return new WaitUntil(() => finished);
        }
        
        public IEnumerator SpeakQueuedAsync(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
        {
            var text = responseNode.GetTTS();
            var finished = false;
            RequestSpeech(text, playbackEvents, () => finished = true);
            yield return new WaitUntil(() => finished);
        }

        public bool Speak(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
        {
            Stop();
            
            SpeakQueued(responseNode.GetTTS(), playbackEvents);
            return true;
        }
#endif
        #endregion
    }
    
    

    public class ElevenLabsQueue
    {
        public string text;
        public string key;
#if VOICESDK
        [SerializeField]
        public TTSSpeakerClipEvents events = new TTSSpeakerClipEvents();
        public TTSClipData clipData;
#endif

        public ElevenLabsQueue(string text)
        {
            this.text = text;
            key = CreateKey(text);
        }

#if VOICESDK
        public ElevenLabsQueue(string text, TTSSpeakerClipEvents events) : this(text)
        {
            this.events = events;
        }
#endif

        public static string CreateKey(string text) => Regex.Replace(text, @"[^\w]+", "");
    }
}