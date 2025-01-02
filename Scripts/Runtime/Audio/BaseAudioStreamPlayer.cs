using System;
using System.Collections.Concurrent;
using Doubtech.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    /// <summary>
    /// Base class for streaming audio playback in Unity using Eleven Labs' TTS system.
    /// </summary>
    public abstract class BaseAudioStreamPlayer : MonoBehaviour, IStreamedAudioPlayer
    {
        [Tooltip("AudioSource component used for playback")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Audio encoding format")]
        [SerializeField] private AudioEncoding encoding = AudioEncoding.pcm_24000;

        [Tooltip("The length of the buffer in seconds")]
        [SerializeField] private int bufferLength = 10;

        private AudioClip audioClip;
        private float[] audioBuffer;
        private long bufferRead;
        private long bufferWritten;
        private int bufferWriteIndex;
        private int bufferReadIndex;
        private ConcurrentQueue<PlaybackEvent> eventQueue = new ConcurrentQueue<PlaybackEvent>();

        public bool IsPlaying => audioSource.isPlaying;

        protected virtual void Start()
        {
            InitializeBuffer();
            InitializeAudioClip();
        }

        /// <summary>
        /// Resets the audio buffer and related states.
        /// </summary>
        private void ResetBuffer()
        {
            audioSource.time = 0;
            bufferWriteIndex = 0;
            bufferReadIndex = 0;
            bufferWritten = 0;
            bufferRead = 0;
            OnResetBuffer();
        }

        /// <summary>
        /// Invoked when the audio buffer is reset. Can be overridden for custom behavior.
        /// </summary>
        protected virtual void OnResetBuffer() { }

        /// <summary>
        /// Callback for PCM reader to fill audio data for playback.
        /// </summary>
        /// <param name="data">The buffer to fill with audio data.</param>
        private void PcmReader(float[] data)
        {
            bool stop = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (bufferRead < bufferWritten)
                {
                    if (bufferReadIndex >= audioBuffer.Length) bufferReadIndex = 0;
                    data[i] = audioBuffer[bufferReadIndex];
                    bufferReadIndex++;
                    bufferRead++;
                }
                else
                {
                    data[i] = 0f;
                    stop = true;
                }
                
                if(eventQueue.Count > 0 && eventQueue.TryPeek(out var playbackEvent))
                {
                    if (bufferRead >= playbackEvent.bufferOffset && eventQueue.TryDequeue(out _))
                    {
                        playbackEvent.callback?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Stops playback and resets the buffer.
        /// </summary>
        public void Stop()
        {
            ResetBuffer();
            audioSource.Stop();
        }

        /// <summary>
        /// Starts playback if not already playing.
        /// </summary>
        public void Play()
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            audioSource.Pause();
        }

        /// <summary>
        /// Adds audio data for playback.
        /// </summary>
        /// <param name="audioData">Raw audio data to add.</param>
        public void AddData(byte[] audioData)
        {
            OnAddData(audioData, 0, audioData.Length, Array.Empty<PlaybackEvent>());
        }

        /// <summary>
        /// Adds audio data for playback with an offset and length.
        /// </summary>
        /// <param name="audioData">Raw audio data to add.</param>
        /// <param name="offset">Offset to start reading from.</param>
        /// <param name="length">Length of data to read.</param>
        public void AddData(byte[] audioData, int offset, int length)
        {
            OnAddData(audioData, offset, length, Array.Empty<PlaybackEvent>());
        }

        /// <summary>
        /// Adds audio data for playback with an offset and length.
        /// </summary>
        /// <param name="audioData">Raw audio data to add.</param>
        /// <param name="offset">Offset to start reading from.</param>
        /// <param name="length">Length of data to read.</param>
        public void AddData(byte[] audioData, int offset, int length, PlaybackEvent[] events)
        {
            OnAddData(audioData, offset, length, events);
        }

        /// <summary>
        /// Decodes and enqueues audio data into the buffer.
        /// </summary>
        /// <param name="audioData">Audio data to decode.</param>
        /// <param name="offset">Offset in the audio data array.</param>
        /// <param name="length">Length of audio data to process.</param>
        protected virtual void EnqueueDecodedData(byte[] audioData, int offset, int length, PlaybackEvent[] events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                // Calculate the offset in the buffer for the event based on the ms time
                int eventOffset = (int)(events[i].time * encoding.SampleRate() / 1000);
                events[i].bufferOffset = Math.Min(bufferWritten + eventOffset, bufferWritten + audioBuffer.Length - 1);
                eventQueue.Enqueue(events[i]);
            }
            for (int i = 0; i < length / 2; i++)
            {
                if (bufferWriteIndex >= audioBuffer.Length)
                {
                    bufferWriteIndex = 0;
                }

                short sample = (short)(audioData[offset + i * 2] | (audioData[offset + i * 2 + 1] << 8));
                audioBuffer[bufferWriteIndex++] = sample / 32768f;
                bufferWritten++;
            }
        }

        /// <summary>
        /// Handles adding audio data for custom processing with offset and length. Must be implemented by derived classes.
        /// </summary>
        /// <param name="audioData">Raw audio data.</param>
        /// <param name="offset">Offset to start reading from.</param>
        /// <param name="length">Length of data to read.</param>
        protected abstract void OnAddData(byte[] audioData, int offset, int length, PlaybackEvent[] events);

        /// <summary>
        /// Initializes the audio buffer.
        /// </summary>
        private void InitializeBuffer()
        {
            audioBuffer = new float[encoding.SampleRate() * bufferLength]; // Buffer for specified length and sample rate
        }

        /// <summary>
        /// Initializes the audio clip for playback.
        /// </summary>
        private void InitializeAudioClip()
        {
            audioClip = AudioClip.Create("ElevenLabsTTS", audioBuffer.Length, 1, encoding.SampleRate(), true, PcmReader);
            audioSource.clip = audioClip;
            audioSource.loop = true;
        }
    }

    public class PlaybackEvent
    {
        public int time;
        public long bufferOffset;
        public Action callback;

        public PlaybackEvent(int timeOffset, Action callback)
        {
            this.time = timeOffset;
            this.callback = callback;
        }
    }
}
