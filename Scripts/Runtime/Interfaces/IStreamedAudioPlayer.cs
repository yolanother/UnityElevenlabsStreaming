namespace Doubtech.ElevenLabs.Streaming.Interfaces
{
    public interface IStreamedAudioPlayer
    {
        void Stop();
        void Pause();
        void AddData(byte[] audioData);
        void AddData(byte[] audioData, int offset, int length);
        void Play();
    }
}