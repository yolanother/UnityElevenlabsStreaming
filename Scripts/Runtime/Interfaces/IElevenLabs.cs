using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoubTech.Elevenlabs.Streaming;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming.Interfaces
{
    public interface IElevenLabsTTS
    {
        void Send(string text);
        public void StartStream();
        public void EndStream();
        public void SendPartial(string text);
        public void SendPartial(string text, Action<string> onStarted, Action<string> onFinished);

        public void SendFinal(string text);
    }

    public interface IWebSocket
    {
        Task Connect();
        Task Disconnect();
        bool IsConnected { get; }
    }

    public interface IStreamedPlayer
    {
        /// <summary>
        /// Triggered when a new audio clip begins playing.
        /// </summary>
        event Action OnClipStart;

        /// <summary>
        /// Triggered when the audio clip stops playing.
        /// </summary>
        event Action OnClipStop;

        /// <summary>
        /// Indicates whether the player is currently streaming audio.
        /// </summary>
        bool IsStreaming { get; }

        /// <summary>
        /// Returns the total length of the currently playing or buffered audio clip in seconds.
        /// </summary>
        float Length { get; }

        /// <summary>
        /// Returns the current playback time of the audio clip in seconds.
        /// </summary>
        float CurrentTime { get; }

        bool IsPlaying { get; }
        int StreamBufferSize { get; }
        AudioClip Clip { get; }

        /// <summary>
        /// Initializes a new audio clip with the specified channels and frequency.
        /// </summary>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="frequency">Sample frequency of the audio.</param>
        void StartClip(int channels, int frequency);

        /// <summary>
        /// Indicates that no more data will be added to the audio clip, marking it as complete.
        /// </summary>
        void CompleteClip();

        /// <summary>
        /// Adds PCM data to the audio buffer. Optional callbacks can be provided to be triggered at specific times relative to the added data.
        /// </summary>
        /// <param name="buffer">The PCM data buffer to add.</param>
        /// <param name="callbacks">List of callbacks with their associated trigger times relative to the start of the added data.</param>
        void AddData(byte[] buffer, List<ITimedCallback> callbacks = null);

        /// <summary>
        /// Starts playback of a PCM audio buffer with the specified channels and frequency.
        /// </summary>
        /// <param name="pcmData">PCM audio data to play.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="frequency">Sample frequency of the audio.</param>
        void PlayData(byte[] pcmData, int channels, int frequency);
        
        void Stop();
        void Pause();
        void Resume();
        
        /// <summary>
        /// Gets the duration of the specified audio data in seconds.
        /// </summary>
        /// <param name="audioBytesLength"></param>
        /// <param name="frequency"></param>
        /// <param name="channels"></param>
        /// <returns></returns>
        float Duration(int audioBytesLength, int frequency, int channels);
    }
}