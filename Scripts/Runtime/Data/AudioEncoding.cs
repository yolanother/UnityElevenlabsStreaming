namespace Doubtech.ElevenLabs.Streaming.Data
{
    public enum AudioEncoding
    {
        pcm_16000,
        pcm_22050,
        pcm_24000,
        pcm_44100,
        ulaw_8000,
        mp3_44100
    }

    public static class AudioEncodingUtils
    {
        public static int SampleRate(this AudioEncoding encoding)
        {
            switch (encoding)
            {
                case AudioEncoding.pcm_16000:
                    return 16000;
                case AudioEncoding.pcm_22050:
                    return 22050;
                case AudioEncoding.pcm_24000:
                    return 24000;
                case AudioEncoding.pcm_44100:
                case AudioEncoding.mp3_44100:
                    return 44100;
                case AudioEncoding.ulaw_8000:
                    return 8000;
                default:
                    return 24000;
            }
        }
    }
}