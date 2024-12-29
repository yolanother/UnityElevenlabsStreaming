using Doubtech.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public abstract class BaseAudioStreamPlayer : MonoBehaviour, IStreamedAudioPlayer
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioEncoding encoding = AudioEncoding.pcm_24000;
        [Tooltip("The length of the buffer in seconds")]
        [SerializeField] private int bufferLength = 10;
        
        private AudioClip audioClip;
        private float[] audioBuffer;
        private long bufferRead;
        private long bufferWritten;
        private int bufferWriteIndex = 0;
        private int bufferReadIndex = 0;

        async void Start()
        {
            audioBuffer = new float[encoding.SampleRate() * bufferLength]; // 10 seconds of buffer at 24kHz
            // Initialize the audio clip
            audioClip = AudioClip.Create("ElevenLabsTTS", audioBuffer.Length, 1, encoding.SampleRate(), true, PcmReader);

            audioSource.clip = audioClip;
            audioSource.loop = true;
        }

        private void ResetBuffer()
        {
            audioSource.time = 0;
            bufferWriteIndex = 0;
            bufferReadIndex = 0;
            bufferWritten = 0;
            bufferRead = 0;
            OnResetBuffer();
        }
        
        protected virtual void OnResetBuffer() { }

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
            }
        }

        public void Stop()
        {
            ResetBuffer();
            audioSource.Stop();
        }

        public void Play()
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        public void Pause()
        {
            audioSource.Pause();
        }

        public void AddData(byte[] audioData)
        {
            OnAddData(audioData, 0, audioData.Length);
        }

        public void AddData(byte[] audioData, int offset, int length)
        {
            OnAddData(audioData, offset, length);
        }
        
        protected virtual void EnqueueDecodedData(byte[] audioData, int offset, int length)
        {
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

        protected abstract void OnAddData(byte[] audioData);
        protected abstract void OnAddData(byte[] audioData, int offset, int length);
    }
}