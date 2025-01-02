namespace Doubtech.ElevenLabs.Streaming
{
    /// <summary>
    /// A player for PCM audio streams, handling audio data addition and decoding.
    /// </summary>
    public class PcmAudioPlayer : BaseAudioStreamPlayer
    {
        /// <summary>
        /// Adds a subset of audio data to the player from a byte array.
        /// </summary>
        /// <param name="audioData">The audio data to add.</param>
        /// <param name="offset">The starting offset in the audio data array.</param>
        /// <param name="length">The number of bytes to add.</param>
        protected override void OnAddData(byte[] audioData, int offset, int length, PlaybackEvent[] events)
        {
            EnqueueDecodedData(audioData, offset, length, events);
        }
    }
}
