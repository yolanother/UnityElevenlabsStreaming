using System;
using System.IO;
#if NAUDIO
using NAudio.Wave;
#endif
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public class Mp3Player : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        
        private MemoryStream mp3Stream;
#if NAUDIO
        private Mp3FileReader mp3Reader;
#endif

        void Awake()
        {
            if(!audioSource) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            #if !NAUDIO
            enabled = false;
            #endif
        }


        public void StreamMp3(byte[] mp3Data)
        {
#if NAUDIO
            if (mp3Stream != null)
            {
                mp3Stream.Close();
            }
        
            mp3Stream = new MemoryStream(mp3Data);
            mp3Reader = new Mp3FileReader(mp3Stream);

            var waveBuffer = new WaveBuffer(mp3Reader.Mp3WaveFormat.AverageBytesPerSecond * 4);
            int bytesRead;

            while ((bytesRead = mp3Reader.Read(waveBuffer, 0, waveBuffer.numberOfBytes)) > 0)
            {
                audioSource.clip = AudioClip.Create("StreamedMp3", bytesRead / 2, 2, mp3Reader.Mp3WaveFormat.SampleRate, false);
                audioSource.clip.SetData(waveBuffer.FloatBuffer, 0);
                audioSource.Play();
                while (audioSource.isPlaying)
                {
                    // Wait until clip finishes playing
                }
            }

            mp3Reader.Close();
            mp3Stream.Close();
#else
            Debug.LogWarning("Playback with Mp3Player is disabled. NAudio is not enabled for this project.");
#endif
        }
    }
}
