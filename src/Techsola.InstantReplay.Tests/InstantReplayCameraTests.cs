using NUnit.Framework;
using Shouldly;

namespace Techsola.InstantReplay.Tests
{
    public static class InstantReplayCameraTests
    {
        [Test]
        public static void SaveGif_with_no_frames_collected_should_return_null()
        {
            InstantReplayCamera.SaveGif().ShouldBeNull();
        }
    }
}
