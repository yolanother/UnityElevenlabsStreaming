using System;
using System.Threading.Tasks;
using Doubtech.ElevenLabs.Streaming.Data;

namespace DoubTech.AI.ThirdParty.ElevenLabs.Streaming.Data
{
    public class AudioRequest
    {
        /// <summary>
        /// The original request text
        /// </summary>
        public string message;
        
        /// <summary>
        /// The total number of characters that haven't been played from this request
        /// </summary>
        public int charsRemaining;

        internal TaskCompletionSource<AudioRequest> taskCompletionSource = new();
        
        /// <summary>
        /// A task that will be completed when the request is finished
        /// </summary>
        public Task Task => taskCompletionSource.Task;
        
        /// <summary>
        /// Called when a character at the given index is played
        /// </summary>
        public Action<AudioRequest, int> onCharacterPlayed;

        /// <summary>
        /// Called when an error occurs during audio playback
        /// </summary>
        public Action<AudioRequest, string> onError;
        
        /// <summary>
        /// Called when all of the audio data has been received and we are just waiting for playback to complete
        /// </summary>
        public Action<AudioRequest> onDataProcessingComplete;
        
        /// <summary>
        /// Called when audio playback of this audio request has begun
        /// </summary>
        public Action<AudioRequest> onPlaybackStarted;
        
        /// <summary>
        /// Called when audio playback of this audio request has completed.
        /// </summary>
        public Action<AudioRequest> onPlaybackComplete;
        
        /// <summary>
        /// Called after data has been processed, an error was received, or playback completed. This indicates
        /// no more events will be triggered by this request.
        /// </summary>
        public Action<AudioRequest> onComplete;
    }
}