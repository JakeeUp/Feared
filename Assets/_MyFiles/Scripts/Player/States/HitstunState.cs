using Brawler.Core;

namespace Brawler.Player
{
    /// <summary>
    /// Hit reaction: no control for <c>LastHit.HitstunFrames</c>, sliding with knockback that decelerates.
    /// Entered from any state by <c>PlayerController.TryApplyDamage</c>; a re-hit re-enters this same state
    /// (StateMachine performs Exit then Enter), which refreshes the timeline with the new hit's hitstun.
    /// </summary>
    /// <remarks>Transition (FixedTick, §4.1): frame &gt;= LastHit.HitstunFrames -&gt; Move if input, else Idle.</remarks>
    public sealed class HitstunState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // TODO: clear the buffer (no mashing out), disarm the hitbox, reset the combo, drop i-frames, zero velocity, reset
            // the clock. Zeroing velocity is safe because Hitbox applies knockback right after the damage call.
            // Re-hit re-enters this state, so this also refreshes the stun with the new LastHit.
            // Optional: turn to face whoever hit us (if LastHit.Source is set).
            // HitstunFrames 0 still gives one frame of flinch - acceptable.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // TODO: decelerate the knockback slide (KnockbackDeceleration) and move. No input-driven exits.
            // Presses made during hitstun still get buffered, so anything still live when the stun ends gets used on the first
            // Idle/Move tick - lets you buffer a wake-up dash or attack, which is intended.
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            // TODO: advance the clock; once it reaches LastHit.HitstunFrames go to Move or Idle depending on input.
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // TODO: nothing needed. Keep whatever knockback velocity is left - Idle/Move ease out of it.
        }
    }
}
