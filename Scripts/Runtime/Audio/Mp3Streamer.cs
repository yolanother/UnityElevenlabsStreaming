using UnityEngine;
#if NAUDIO
using NAudio.Wave;
#endif
using System.IO;
using System.Collections.Generic;
using Doubtech.ElevenLabs.Streaming.Interfaces;

namespace Doubtech.ElevenLabs.Streaming
{
    public class Mp3Streamer : BaseAudioStreamPlayer
    {
        private AudioSource audioSource;
        private Queue<byte[]> mp3Chunks = new Queue<byte[]>();
        private bool isPlaying = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
#if !NAUDIO
            enabled = false;
            Debug.LogError("NAudio is required for Mp3Streamer to work. Please import NAudio.");
#endif
        }

        protected override void OnResetBuffer()
        {
            base.OnResetBuffer();
            mp3Chunks.Clear();
        }

        protected override void OnAddData(byte[] mp3Data)
        {
            AddData(mp3Data, 0, mp3Data.Length);

            // If not already playing, start playback.
            if (!isPlaying)
            {
                PlayNextChunk();
            }
        }

        protected override void OnAddData(byte[] mp3Data, int offset, int length)
        {
            // Copy the data into a new byte array and add it to the queue.
            byte[] chunk = new byte[length];
            System.Array.Copy(mp3Data, offset, chunk, 0, length);
            mp3Chunks.Enqueue(chunk);

            // If not already playing, start playback.
            if (!isPlaying)
            {
                PlayNextChunk();
            }
        }

        private void PlayNextChunk()
        {
            #if NAUDIO
            if (mp3Chunks.Count == 0)
            {
                isPlaying = false;
                return;
            }

            byte[] currentChunk = mp3Chunks.Dequeue();
            MemoryStream mp3Stream = new MemoryStream(currentChunk);
            Mp3FileReader mp3Reader = new Mp3FileReader(mp3Stream);

            var waveBuffer = new WaveBuffer(mp3Reader.Mp3WaveFormat.AverageBytesPerSecond * 4);
            int bytesRead = mp3Reader.Read(waveBuffer, 0, waveBuffer.numberOfBytes);
            EnqueueDecodedData(waveBuffer.ByteBuffer, 0, bytesRead);

            mp3Reader.Close();
            mp3Stream.Close();
            #endif
        }

        void Update()
        {
            // If the AudioSource has finished playing and there are more chunks to play, play the next chunk.
            if (!audioSource.isPlaying && isPlaying)
            {
                PlayNextChunk();
            }
        }
    }
}