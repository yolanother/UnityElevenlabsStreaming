using Doubtech.ElevenLabs.Streaming.Data;
using Doubtech.ElevenLabs.Streaming.Interfaces;
using UnityEngine;

namespace Doubtech.ElevenLabs.Streaming
{
    public class PcmAudioPlayer : BaseAudioStreamPlayer
    {
        protected override void OnAddData(byte[] audioData)
        {
            AddData(audioData, 0, audioData.Length);
        }

        protected override void OnAddData(byte[] audioData, int offset, int length)
        {
            EnqueueDecodedData(audioData, offset, length);
        }
    }
}