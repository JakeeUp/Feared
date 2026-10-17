// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Fixed-length burst along input direction (or facing when neutral) with i-frames in
    /// [DashIFrameStart, DashIFrameEnd). Timeline on a private <see cref="FrameClock"/>.
    /// </summary>
    /// <remarks>
    /// Transitions (FixedTick, after advancing — §4.1): when frame &gt;= DashFrames: 1. consume Attack -&gt; Attack
    /// (ComboTracker.Begin; dash-attack buffering); 2. else Move if input, else Idle.
    /// Hitstun: external, only reachable outside i-frames (Hitbox skips invulnerable hurtboxes).
    /// </remarks>
    public sealed class DashState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private Vector3 _direction; // unit planar direction, locked at Enter

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // TODO: pick a direction (stick if held, otherwise facing), update facing only if there's horizontal input, reset
            // the clock, process frame 0 so i-frames are on immediately, and set velocity to DashSpeed.
            // Normalizing after the depth scaling makes diagonal dashes full speed; not normalizing keeps depth dashes shorter.
            // Try both, it's a feel call.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // TODO: hold dash velocity and move. Constant speed is fine to start; easing out over the last few frames usually
            // feels better. No voluntary exits from here - the dash commits for its full length.
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            // TODO: advance the clock, process every frame we stepped over (i-frames), and once DashFrames is reached:
            // buffered Attack -> start a combo, otherwise Move/Idle depending on input.
            // Might want to drop velocity to around MoveSpeed on the way out so the run doesn't start at dash speed.
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // TODO: always clear i-frames here. A dash that gets interrupted must never leave the player invulnerable.
        }

        // ============ Helpers ============

        private void ProcessFrame(PlayerContext context, int frame)
        {
            // TODO: invulnerable while frame is in [IFrameStart, IFrameEnd). Write it every processed frame so it switches off
            // exactly on time. DMC/Souls-style dodges live and die by this window being tight and predictable.
        }
    }
}
