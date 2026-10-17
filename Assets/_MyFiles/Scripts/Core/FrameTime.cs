using UnityEngine;

namespace Brawler.Core
{
    /// <summary>
    /// The project's frame-data time base. All combat timelines are authored in frames of a fixed
    /// 60 Hz simulation, independent of the render rate and of <c>Time.fixedDeltaTime</c>
    /// (see <c>FrameClock</c>, which converts accumulated seconds into whole simulation frames).
    /// </summary>
    /// <remarks>Pure conversion plumbing (not gameplay), so it is implemented.</remarks>
    public static class FrameTime
    {
        // ============ Constants ============

        /// <summary>Simulation frames per second that frame data is authored against.</summary>
        public const int FramesPerSecond = 60;

        /// <summary>Duration of one simulation frame in seconds (1/60).</summary>
        public const float SecondsPerFrame = 1f / FramesPerSecond;

        // ============ Conversions ============

        /// <summary>Converts a frame count to seconds.</summary>
        /// <param name="frames">Frame count (may be 0).</param>
        /// <returns><paramref name="frames"/> * <see cref="SecondsPerFrame"/>.</returns>
        public static float ToSeconds(int frames)
        {
            return frames * SecondsPerFrame;
        }

        /// <summary>Converts seconds to the nearest whole frame count.</summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>Rounded frame count.</returns>
        public static int ToFrames(float seconds)
        {
            return Mathf.RoundToInt(seconds * FramesPerSecond);
        }
    }
}
