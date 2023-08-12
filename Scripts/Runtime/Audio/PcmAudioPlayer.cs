using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public class PcmAudioPlayer : MonoBehaviour, IPcmPlayer
    {
        [SerializeField] private AudioSource _audioSource;

        public class PcmAudio
        {
            public float[] data;
            public int channels;
            public int frequency;
        }
        
        private ConcurrentQueue<AudioClip> _pcmChunks = new ConcurrentQueue<AudioClip>();

        private void Awake()
        {
            if(!_audioSource) _audioSource = GetComponent<AudioSource>();
        }
        
        public void PlayPcm(byte[] buffer, int channels, int frequency)
        {
            var audioData = new PcmAudio()
            {
                data = ConvertByteArrayToFloatArray(buffer),
                channels = channels,
                frequency = frequency
            };
            AudioClip clip = AudioClip.Create("PCM_Audio", audioData.data.Length, audioData.channels, audioData.frequency, false);
            clip.SetData(audioData.data, 0);
            _pcmChunks.Enqueue(clip);
            
            StopAllCoroutines();
            StartCoroutine(FlushQueue());
        }

        public IEnumerator FlushQueue()
        {
            while (_pcmChunks.Count > 0)
            {
                yield return new WaitUntil(() => !_audioSource.isPlaying);
                if (_pcmChunks.TryDequeue(out var pcm))
                {
                    _audioSource.clip = pcm;
                    _audioSource.Play();
                }
                yield return null;
            }
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

    public interface IPcmPlayer
    {
        public void PlayPcm(byte[] pcmData, int channels, int frequency);
    }
}