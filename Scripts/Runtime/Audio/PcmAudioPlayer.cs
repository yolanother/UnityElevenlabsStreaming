using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEngine;
using UnityEngine.Events;

namespace Doubtech.ElevenLabs.Streaming
{
    public class PcmAudioPlayer : MonoBehaviour, IStreamedPlayer
    {
        [SerializeField] private AudioSource _audioSource;

        private AudioClip streamingClip;
        private List<float> audioBuffer = new List<float>();
        private float elapsedAudioTime = 0;

        private bool clipCompleted = false;
        private bool starting = false;
        private bool streaming = false;

        public event Action OnClipStart;
        public event Action OnClipStop;

        private List<ITimedCallback> timeListeners = new List<ITimedCallback>();
        private int frequency;
        private int channels;
        private float totalDataDuration = 0;

        private ConcurrentQueue <ITimedCallback> _callbackQueue = new ConcurrentQueue<ITimedCallback>(); 

        private void Awake()
        {
            if (!_audioSource) _audioSource = GetComponent<AudioSource>();
        }

        public bool IsPlaying => _audioSource.isPlaying;
        public int StreamBufferSize => audioBuffer.Count;
        public bool IsStreaming => streaming;
        public float Length => totalDataDuration;
        public float CurrentTime => elapsedAudioTime;
        public AudioClip Clip => streamingClip;

        public void StartClip(int channels, int frequency)
        {
            // Initialize or clear the audio buffer
            audioBuffer.Clear();

            // Create a streaming AudioClip
            streamingClip =
                AudioClip.Create("Streaming_PCM_Audio", int.MaxValue, channels, frequency, true, OnAudioRead);
            this.frequency = frequency;
            this.channels = channels;
            _audioSource.clip = streamingClip;
            _audioSource.loop = false;
            _audioSource.Play();
            elapsedAudioTime = 0;
            clipCompleted = false;
            starting = true;
            streaming = true;
            totalDataDuration = 0;
            _callbackQueue.Clear();
            timeListeners.Clear();

            StartCoroutine(TriggerTiming());
        }

        public void CompleteClip()
        {
            clipCompleted = true;
            TryClose();
        }

        public void AddListener(ITimedCallback callback)
        {
            timeListeners.Add(callback);
            // Sort the time listeners by time in reverse order
            timeListeners.Sort((a, b) => b.Time.CompareTo(a.Time));
        }

        public float Duration(int length, int frequency, int channels)
        {
            return length / (float)(frequency * channels);
        }

        public void AddData(byte[] buffer, List<ITimedCallback> callbacks = null)
        {
            float[] audioData = ConvertByteArrayToFloatArray(buffer);
            lock (audioBuffer)
            {
                audioBuffer.AddRange(audioData);
            }

            // Calculate the duration of the added data
            float addedDataDuration = Duration(buffer.Length, frequency, channels);
            totalDataDuration += addedDataDuration;

            if (callbacks != null)
            {
                foreach (var callback in callbacks)
                {
                    // Adjust the trigger time relative to the total data duration
                    float adjustedTriggerTime = totalDataDuration - addedDataDuration + callback.Time;
                    
                    AddListener(new AdjustedTimeCallback(adjustedTriggerTime, callback));
                }
            }
        }

        private void OnAudioRead(float[] data)
        {
            if (starting)
            {
                OnClipStart?.Invoke();
                starting = false;
            }

            lock (audioBuffer)
            {
                int count = Mathf.Min(data.Length, audioBuffer.Count);
                audioBuffer.CopyTo(0, data, 0, count);
                audioBuffer.RemoveRange(0, count);
                // If count < data.Length, fill the rest of the data with zeroes using an effiecient Array fill
                Array.Clear(data, count, data.Length - count);
            }

            // Calculate elapsed time for this chunk
            float chunkDuration = data.Length / (float)frequency;
            elapsedAudioTime += chunkDuration;
            /*for (int i = timeListeners.Count - 1; i >= 0; i--)
            {
                if (elapsedAudioTime >= timeListeners[i].Time)
                {
                    _callbackQueue.Enqueue(timeListeners[i]);
                }
            }*/

            TryClose();
        }

        private IEnumerator TriggerTiming()
        {
            Debug.Log("Tracking playback.");
            
            while (streaming)
            {
                FlushCallbacks();

                yield return null;
            }
            
            FlushCallbacks();
            Debug.Log("Stopped tracking playback at " + _audioSource.time);
            
            OnClipStop?.Invoke();
            _audioSource.Stop();
        }

        private void FlushCallbacks()
        {
            if (_callbackQueue.TryDequeue(out var callback))
            {
                callback.Invoke();
                timeListeners.Remove(callback);
            }

            if (_callbackQueue.Count == 0)
            {
                // Check and invoke listeners
                for (int i = timeListeners.Count - 1; i >= 0; i--)
                {
                    if (_audioSource.time >= timeListeners[i].Time)
                    {
                        timeListeners[i].Invoke();
                        timeListeners.RemoveAt(i);
                    }
                }
            }
        }

        private void TryClose()
        {
            // Check for clip completion
            if (clipCompleted && audioBuffer.Count == 0)
            {
                clipCompleted = false;
                streaming = false;
            }
        }

        public void PlayData(byte[] buffer, int channels, int frequency)
        {
            StartClip(channels, frequency);
            AddData(buffer);
            CompleteClip();
        }

        public void Stop()
        {
            CompleteClip();
            _audioSource.Stop();
        }

        public void Pause()
        {
            _audioSource.Pause();
        }

        public void Resume()
        {
            _audioSource.UnPause();
        }

        private float[] ConvertByteArrayToFloatArray(byte[] audioBytes)
        {
            // Assuming audioBytes represent 16-bit PCM data
            int samples = audioBytes.Length / 2;
            float[] audioFloats = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                short s = (short)((audioBytes[i * 2 + 1] << 8) | audioBytes[i * 2]);
                audioFloats[i] = s / 32768.0F;
            }

            return audioFloats;
        }
    }

    public interface ITimedCallback
    {
        float Time { get; }
        void Invoke();
    }

    public class TimedCallbackUnityEvent : UnityEvent, ITimedCallback
    {
        public float Time { get; set; }
    }

    /// <summary>
    /// Triggered at a different time than the original callback. Used to handle relative offset time
    /// </summary>
    public class AdjustedTimeCallback : ITimedCallback
    {
        public float Time { get; set; }
        public ITimedCallback callback;
        
        public AdjustedTimeCallback(float time, ITimedCallback callback)
        {
            Time = time;
            this.callback = callback;
        }
        
        public void Invoke()
        {
            callback.Invoke();
        }
    }

    public class TimedCallback : ITimedCallback
    {
        public float Time { get; set; }
        public Action OnTimeHit { get; set; }
        
        /// <summary>
        /// Initialize a timed callback
        /// </summary>
        /// <param name="time">In seconds</param>
        /// <param name="onTimeHit"></param>
        public TimedCallback(float time, Action onTimeHit)
        {
            Time = time;
            OnTimeHit = onTimeHit;
        }
        
        public void Invoke()
        {
            OnTimeHit?.Invoke();
        }

        public static implicit operator TimedCallback((float, Action) tuple)
        {
            return new TimedCallback(
                tuple.Item1,
                tuple.Item2
            );
        }
    }
}
