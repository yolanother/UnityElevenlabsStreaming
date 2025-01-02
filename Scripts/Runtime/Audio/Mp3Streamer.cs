using UnityEngine;
#if NAUDIO
using NAudio.Wave;
#endif
using System.IO;
using System.Collections.Generic;
using Doubtech.ElevenLabs.Streaming.Interfaces;

namespace Doubtech.ElevenLabs.Streaming
{
    /// <summary>
    /// Handles streaming and playback of MP3 data chunks using NAudio for decoding.
    /// </summary>
    public class Mp3Streamer : BaseAudioStreamPlayer
    {
        private readonly Queue<byte[]> mp3Chunks = new Queue<byte[]>();
        private bool isPlaying;

        private void OnEnable()
        {
#if !NAUDIO
            enabled = false;
            Debug.LogError("NAudio is required for Mp3Streamer to work. Please import NAudio.");
#endif
        }

        /// <summary>
        /// Clears the MP3 chunk buffer.
        /// </summary>
        protected override void OnResetBuffer()
        {
            base.OnResetBuffer();
            mp3Chunks.Clear();
        }

        /// <summary>
        /// Adds a chunk of MP3 data to the buffer and starts playback if not already playing.
        /// </summary>
        /// <param name="mp3Data">MP3 data buffer.</param>
        /// <param name="offset">Offset within the buffer.</param>
        /// <param name="length">Length of data to read.</param>
        protected override void OnAddData(byte[] mp3Data, int offset, int length, PlaybackEvent[] events)
        {
            if (length <= 0) return;

            byte[] chunk = new byte[length];
            System.Array.Copy(mp3Data, offset, chunk, 0, length);
            mp3Chunks.Enqueue(chunk);

            if (!isPlaying)
            {
                PlayNextChunk(events);
            }
        }

        /// <summary>
        /// Plays the next MP3 chunk in the queue.
        /// </summary>
        private void PlayNextChunk(PlaybackEvent[] events)
        {
#if NAUDIO
            if (mp3Chunks.Count == 0)
            {
                isPlaying = false;
                return;
            }

            byte[] currentChunk = mp3Chunks.Dequeue();

            using (var mp3Stream = new MemoryStream(currentChunk))
            using (var mp3Reader = new Mp3FileReader(mp3Stream))
            {
                var waveBuffer = new WaveBuffer(mp3Reader.Mp3WaveFormat.AverageBytesPerSecond * 4);
                int bytesRead = mp3Reader.Read(waveBuffer, 0, waveBuffer.numberOfBytes);

                if (bytesRead > 0)
                {
                    EnqueueDecodedData(waveBuffer.ByteBuffer, 0, bytesRead, events);
                }
            }

            isPlaying = mp3Chunks.Count > 0;
#endif
        }
    }
}
