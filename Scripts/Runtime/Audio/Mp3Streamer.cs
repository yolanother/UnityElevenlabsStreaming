using UnityEngine;
#if NAUDIO
using NAudio.Wave;
#endif
using System.IO;
using System.Collections.Generic;

namespace Doubtech.ElevenLabs.Streaming
{
    public class Mp3Streamer : MonoBehaviour
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
#endif
        }

        public void AddMp3Chunk(byte[] mp3Data)
        {
            mp3Chunks.Enqueue(mp3Data);

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
            if (bytesRead > 0)
            {
                audioSource.clip = AudioClip.Create("StreamedMp3", bytesRead / 2, 2, mp3Reader.Mp3WaveFormat.SampleRate, false);
                audioSource.clip.SetData(waveBuffer.FloatBuffer, 0);
                audioSource.Play();
                isPlaying = true;
            }

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