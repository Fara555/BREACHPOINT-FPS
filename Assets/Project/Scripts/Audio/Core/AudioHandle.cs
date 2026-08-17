namespace Breachpoint.Audio
{
    public readonly struct AudioHandle
    {
        private readonly AudioService _service;
        private readonly int _playbackId;

        internal AudioHandle(AudioService service, int playbackId)
        {
            _service = service;
            _playbackId = playbackId;
        }

        public bool IsPlaying =>
            _service != null &&
            _service.IsPlaybackActive(_playbackId);

        public void Stop()
        {
            if (_service == null)
            {
                return;
            }

            _service.StopPlayback(_playbackId);
        }
    }
}