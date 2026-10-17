// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using Brawler.Combat;
using Brawler.Core;

namespace Brawler.Player
{
    /// <summary>
    /// Plays the combo link at <c>ComboTracker.CurrentIndex</c>. Re-entered (same-state ChangeState) for each next link.
    /// Frame timeline runs on a private <see cref="FrameClock"/> in FixedTick; hitbox sweeps on Active frames.
    /// </summary>
    /// <remarks>
    /// Transitions (§4.1): Tick — 1. cancel window &amp;&amp; consume Dash -&gt; Dash (ComboTracker.Reset);
    /// 2. cancel window &amp;&amp; HasNext &amp;&amp; consume Attack -&gt; TryAdvance, re-enter Attack.
    /// FixedTick — 3. IsFinished -&gt; ComboTracker.Reset; Move if input else Idle. Hitstun external (priority 0).
    /// Facing is LOCKED for the whole attack.
    /// </remarks>
    public sealed class AttackState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private AttackData _attack;

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // TODO: grab the attack for the current combo index, reset the frame clock, arm the hitbox with the current
            // facing (facing is locked for the rest of the swing), and process frame 0 right away so 0-startup attacks
            // actually hit on frame 0.
            // - Missing attack (bad index / unassigned asset): reset the combo and bail to Idle.
            // - Chaining re-enters this same state, so Exit/Enter clears the hit set and the next link can hit the same target.
            //   Don't reset the ComboTracker in Exit or chaining breaks.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // TODO: cancels, lunge, move.
            // - Inside the cancel window: buffered dash -> reset combo, Dash. Otherwise if there IS a next link and attack is
            //   buffered -> advance the combo and re-enter Attack.
            //   Check HasNext before consuming, so an attack press during the finisher stays buffered (and expires) instead of
            //   being eaten.
            // - A press made during startup only chains if it's still in the buffer when the window opens, so cancel start and
            //   the buffer window need tuning together. This window is basically the DMC / fighting game "link" timing.
            // - Lunge forward during startup/active, decelerate during recovery. Always call Move (gravity).
            // - No facing changes mid-swing.
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            // TODO: advance the clock, process every frame we stepped over (so a 1-frame active window is never skipped when
            // one FixedUpdate covers two sim frames), then if the attack is finished: reset the combo and go to Move/Idle.
            // Anything still buffered then gets used by Idle/Move on their next tick (fresh combo or dash).
            // No special hitstop handling needed - timeScale 0 stops FixedUpdate.
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // TODO: disarm the hitbox, drop the attack ref. Runs on every exit path (finished, dash cancel, next link,
            // got hit) so keep it idempotent. Resetting the combo is the job of whichever transition ends it.
        }

        // ============ Helpers ============

        private void ProcessFrame(PlayerContext context, int frame)
        {
            // TODO: sweep the hitbox on active frames. Sweep dedupes per activation, so calling it every active frame is right -
            // something that walks into the box late still gets hit exactly once.
            // If hits feel a step late, a single Physics.SyncTransforms() before the first sweep is worth trying.
        }
    }
}
