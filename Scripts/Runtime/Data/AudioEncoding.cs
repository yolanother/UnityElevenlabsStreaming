namespace Doubtech.ElevenLabs.Streaming.Data
{
    /// <summary>
    /// Defines various audio encoding formats supported by the system.
    /// </summary>
    public enum AudioEncoding
    {
        pcm_16000,
        pcm_22050,
        pcm_24000,
        pcm_44100,
        ulaw_8000,
        mp3_44100
    }

    /// <summary>
    /// Provides utility methods for working with AudioEncoding enums.
    /// </summary>
    public static class AudioEncodingUtils
    {
        /// <summary>
        /// Gets the sample rate associated with the given audio encoding.
        /// </summary>
        /// <param name="encoding">The audio encoding format.</param>
        /// <returns>The sample rate in Hz.</returns>
        public static int SampleRate(this AudioEncoding encoding)
        {
            return encoding switch
            {
                AudioEncoding.pcm_16000 => 16000,
                AudioEncoding.pcm_22050 => 22050,
                AudioEncoding.pcm_24000 => 24000,
                AudioEncoding.pcm_44100 => 44100,
                AudioEncoding.mp3_44100 => 44100,
                AudioEncoding.ulaw_8000 => 8000,
                _ => 24000,
            };
        }
    }
}
