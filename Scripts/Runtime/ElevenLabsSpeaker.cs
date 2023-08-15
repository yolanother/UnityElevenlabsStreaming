using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Doubtech.ElevenLabs.Streaming;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using Meta.Voice.Audio;
using Meta.WitAi.Composer.Integrations;
using UnityEngine;
#if VOICESDK
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Utilities;
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

        public class ElevenLabsQueue
        {
            public string text;
            public string key;
            #if VOICESDK
            [SerializeField]
            private TTSSpeakerClipEvents events = new TTSSpeakerClipEvents();
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
        };

        private Dictionary<string, List<ElevenLabsQueue>> activeRequests = new Dictionary<string, List<ElevenLabsQueue>>();

        private bool _isSpeaking;

        #region Voice SDK Speaker

        public bool IsSpeaking => _player.IsPlaying;
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
        }

        public void Pause()
        {
            _player.Pause();
        }

        public void Resume()
        {
            _player.Resume();
        }

#if VOICESDK
        public TTSSpeakerEvents Events => _events;
        public IAudioPlayer AudioPlayer { get; }

        public void SpeakQueued(string text, TTSSpeakerClipEvents events)
        {
            var request = new ElevenLabsQueue(text, events);
            if (!activeRequests.TryGetValue(request.key, out var activeList))
            {
                activeList = new List<ElevenLabsQueue>();
                activeRequests[request.key] = activeList;
            }
            activeRequests[request.key].Add(request);
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
            _elevenLabs.SendPartial(responseNode.GetTTS(), 
                (t) =>
                {
                    playbackEvents?.OnPlaybackStart?.Invoke(null, null);
                    playbackEvents?.OnTextPlaybackStart?.Invoke(text);
                },
                (t) =>
                {
                    finished = true;
                    playbackEvents?.OnPlaybackComplete?.Invoke(null, null);
                    playbackEvents.OnTextPlaybackFinished?.Invoke(text);
                });
            yield return new WaitUntil(() => finished);
        }

        public bool Speak(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
        {
            var text = responseNode.GetTTS();
            var finished = false;
            _elevenLabs.SendPartial(responseNode.GetTTS(), 
                (t) =>
                {
                    playbackEvents?.OnPlaybackStart?.Invoke(null, null);
                    playbackEvents?.OnTextPlaybackStart?.Invoke(text);
                },
                (t) =>
                {
                    finished = true;
                    playbackEvents?.OnPlaybackComplete?.Invoke(null, null);
                    playbackEvents.OnTextPlaybackFinished?.Invoke(text);
                });
            
            return true;
        }
#endif
        #endregion
    }
}