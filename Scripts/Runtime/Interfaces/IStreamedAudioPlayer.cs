namespace Doubtech.ElevenLabs.Streaming.Interfaces
{
    /// <summary>
    /// Interface defining the operations for a streamed audio player
    /// capable of handling real-time audio data playback.
    /// </summary>
    public interface IStreamedAudioPlayer
    {
        /// <summary>
        /// Stops audio playback and clears any buffered audio data.
        /// </summary>
        void Stop();

        /// <summary>
        /// Pauses the audio playback while retaining buffered data.
        /// </summary>
        void Pause();

        /// <summary>
        /// Adds audio data to the playback buffer.
        /// </summary>
        /// <param name="audioData">The audio data to add to the buffer.</param>
        void AddData(byte[] audioData);

        /// <summary>
        /// Adds a subset of audio data to the playback buffer.
        /// </summary>
        /// <param name="audioData">The audio data to add to the buffer.</param>
        /// <param name="offset">The offset in the audio data array to start reading from.</param>
        /// <param name="length">The number of bytes to read from the audio data array.</param>
        void AddData(byte[] audioData, int offset, int length);

        /// <summary>
        /// Starts or resumes audio playback from the buffered data.
        /// </summary>
        void Play();
        
        /// <summary>
        /// Returns true if the streamer is currently playing streamed content
        /// </summary>
        bool IsPlaying { get; }
    }
}