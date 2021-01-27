namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private readonly struct WindowMetrics
        {
            public readonly int ClientLeft;
            public readonly int ClientTop;
            public readonly int ClientWidth;
            public readonly int ClientHeight;
            public readonly uint Dpi;

            public WindowMetrics(int clientLeft, int clientTop, int clientWidth, int clientHeight, uint dpi)
            {
                ClientLeft = clientLeft;
                ClientTop = clientTop;
                ClientWidth = clientWidth;
                ClientHeight = clientHeight;
                Dpi = dpi;
            }
        }
    }
}
