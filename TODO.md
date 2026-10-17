# Brawler — Day One TODO list

Every `TODO:` in `Assets/_MyFiles` appears exactly once below (98 items). Work top to bottom: each phase builds on the one before, and the pure C# phases (1–5) have EditMode tests that go green as you finish them. The comment at each link has the full hints and edge cases.

## Before you start

1. Focus the Unity Editor and let it import and compile the new scripts, shaders and input asset. The Console should show no errors.
2. Run **Brawler > Build Test Scene** once. It creates the data assets, materials, placeholder sprites, the damage-number prefab and `Assets/_MyFiles/Scenes/TestArena.unity`. Running it again never overwrites assets you've tuned.
3. Open **Window > General > Test Runner > EditMode** and click Run All. Most tests fail on purpose: they're the spec for Phases 1–5. (`ComboData_Empty_GetAttack_ReturnsNull` and two `PlayerWiringTests` tests already pass.)
4. Optional: set **Project Settings > Time > Fixed Timestep** to `0.0166667` (1/60) so one FixedUpdate equals one frame of frame data. It isn't required, because `FrameClock` converts any timestep.
5. When you finish a file, delete the `#pragma warning disable CS0169, CS0649, CS0414` line at the top (see ARCHITECTURE.md §10.1).

## Phase 1 — StateMachine (4)

Make these tests green: `Assets/_MyFiles/Tests/EditMode/StateMachineTests.cs` (and `StateMachine_WithPlayerStates_EntersIdle` in `PlayerWiringTests.cs`).

- [ ] `StateMachine.cs` — ChangeState: ignore null; Exit old → set Previous/Current → Enter new; same-state re-enters (no short-circuit). — [Assets/_MyFiles/Scripts/Core/StateMachine.cs:53](Assets/_MyFiles/Scripts/Core/StateMachine.cs#L53)
- [ ] `StateMachine.cs` — Tick: forward to `_currentState?.Tick(_context, dt)`; no re-dispatch after a mid-tick change. — [Assets/_MyFiles/Scripts/Core/StateMachine.cs:66](Assets/_MyFiles/Scripts/Core/StateMachine.cs#L66)
- [ ] `StateMachine.cs` — FixedTick: forward to `_currentState?.FixedTick(_context, dt)`; same rules as Tick. — [Assets/_MyFiles/Scripts/Core/StateMachine.cs:74](Assets/_MyFiles/Scripts/Core/StateMachine.cs#L74)
- [ ] `StateMachine.cs` — IsInState: `ReferenceEquals(_currentState, state)`. — [Assets/_MyFiles/Scripts/Core/StateMachine.cs:82](Assets/_MyFiles/Scripts/Core/StateMachine.cs#L82)

## Phase 2 — InputBuffer (7)

Make these tests green: `Assets/_MyFiles/Tests/EditMode/InputBufferTests.cs`.

- [ ] `InputBuffer.cs` — IsLive (helper): add `now - pressTime <= _windowSeconds` (inclusive; future-stamped presses are live). Do this first. — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:125](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L125)
- [ ] `InputBuffer.cs` — WindowSeconds (setter): store the value (optionally clamp negatives to 0). — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:62](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L62)
- [ ] `InputBuffer.cs` — Count: return `_count`. — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:74](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L74)
- [ ] `InputBuffer.cs` — Enqueue: ignore None; when full drop the oldest; write at the tail; `_count++`. — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:86](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L86)
- [ ] `InputBuffer.cs` — TryConsume: remove the OLDEST live matching entry, shifting later entries to keep order. — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:98](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L98)
- [ ] `InputBuffer.cs` — Peek: same scan as TryConsume, without removing. — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:113](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L113)
- [ ] `InputBuffer.cs` — Clear: `_head = 0; _count = 0`. — [Assets/_MyFiles/Scripts/Core/InputBuffer.cs:120](Assets/_MyFiles/Scripts/Core/InputBuffer.cs#L120)

## Phase 3 — FrameClock (3)

Make these tests green: `Assets/_MyFiles/Tests/EditMode/FrameClockTests.cs`.

- [ ] `FrameClock.cs` — Frame: return `_frame`. — [Assets/_MyFiles/Scripts/Core/FrameClock.cs:33](Assets/_MyFiles/Scripts/Core/FrameClock.cs#L33)
- [ ] `FrameClock.cs` — Advance: accumulate dt (ignore <= 0), step one frame per `SecondsPerFrame` with a small epsilon, return steps. — [Assets/_MyFiles/Scripts/Core/FrameClock.cs:45](Assets/_MyFiles/Scripts/Core/FrameClock.cs#L45)
- [ ] `FrameClock.cs` — Reset: `_frame = 0; _accumulator = 0`. — [Assets/_MyFiles/Scripts/Core/FrameClock.cs:57](Assets/_MyFiles/Scripts/Core/FrameClock.cs#L57)

## Phase 4 — EventBus (6)

Make these tests green: `Assets/_MyFiles/Tests/EditMode/EventBusTests.cs`.

- [ ] `EventBus.cs` — Subscribe<T>: get/create the `List<Action<T>>` for `typeof(T)`, skip null and duplicates, append. — [Assets/_MyFiles/Scripts/Core/EventBus.cs:59](Assets/_MyFiles/Scripts/Core/EventBus.cs#L59)
- [ ] `EventBus.cs` — Unsubscribe<T>: during a publish null the slot (+ flag compaction); otherwise `RemoveAt`, keeping order. — [Assets/_MyFiles/Scripts/Core/EventBus.cs:69](Assets/_MyFiles/Scripts/Core/EventBus.cs#L69)
- [ ] `EventBus.cs` — Publish<T>: snapshot count, `_publishDepth++`, invoke non-null listeners in order inside try/catch → `Debug.LogException`, compact at depth 0. — [Assets/_MyFiles/Scripts/Core/EventBus.cs:78](Assets/_MyFiles/Scripts/Core/EventBus.cs#L78)
- [ ] `EventBus.cs` — Clear: `_listenersByType.Clear()` and reset the compaction flag. — [Assets/_MyFiles/Scripts/Core/EventBus.cs:92](Assets/_MyFiles/Scripts/Core/EventBus.cs#L92)
- [ ] `EventBus.cs` — GetListenerCount<T>: count non-null entries for T (0 if there is no list). — [Assets/_MyFiles/Scripts/Core/EventBus.cs:103](Assets/_MyFiles/Scripts/Core/EventBus.cs#L103)
- [ ] `EventBus.cs` — Compact (helper): allocation-free two-index compaction of a list after the outermost publish. — [Assets/_MyFiles/Scripts/Core/EventBus.cs:109](Assets/_MyFiles/Scripts/Core/EventBus.cs#L109)

## Phase 5 — AttackFrames / ComboTracker / AttackData (13)

Make these tests green: `Assets/_MyFiles/Tests/EditMode/AttackFramesTests.cs`, `Assets/_MyFiles/Tests/EditMode/ComboTrackerTests.cs`, `Assets/_MyFiles/Tests/EditMode/AttackDataTests.cs`.

- [ ] `AttackFrames.cs` — TotalFrames: `_startup + _active + _recovery`. — [Assets/_MyFiles/Scripts/Combat/AttackFrames.cs:74](Assets/_MyFiles/Scripts/Combat/AttackFrames.cs#L74)
- [ ] `AttackFrames.cs` — GetPhase: half-open Startup/Active/Recovery/Finished ranges; frame < 0 → Startup. — [Assets/_MyFiles/Scripts/Combat/AttackFrames.cs:86](Assets/_MyFiles/Scripts/Combat/AttackFrames.cs#L86)
- [ ] `AttackFrames.cs` — IsActive: `GetPhase(frame) == AttackPhase.Active`. — [Assets/_MyFiles/Scripts/Combat/AttackFrames.cs:97](Assets/_MyFiles/Scripts/Combat/AttackFrames.cs#L97)
- [ ] `AttackFrames.cs` — IsInCancelWindow: `start <= frame < end`; start >= end → never; no clamping. — [Assets/_MyFiles/Scripts/Combat/AttackFrames.cs:106](Assets/_MyFiles/Scripts/Combat/AttackFrames.cs#L106)
- [ ] `AttackFrames.cs` — IsFinished: `frame >= TotalFrames`. — [Assets/_MyFiles/Scripts/Combat/AttackFrames.cs:116](Assets/_MyFiles/Scripts/Combat/AttackFrames.cs#L116)
- [ ] `ComboTracker.cs` — IsInCombo: `_currentIndex >= 0`. — [Assets/_MyFiles/Scripts/Combat/ComboTracker.cs:44](Assets/_MyFiles/Scripts/Combat/ComboTracker.cs#L44)
- [ ] `ComboTracker.cs` — HasNext: in a combo and another link follows. — [Assets/_MyFiles/Scripts/Combat/ComboTracker.cs:54](Assets/_MyFiles/Scripts/Combat/ComboTracker.cs#L54)
- [ ] `ComboTracker.cs` — IsFinisher: in a combo and at the last index. — [Assets/_MyFiles/Scripts/Combat/ComboTracker.cs:66](Assets/_MyFiles/Scripts/Combat/ComboTracker.cs#L66)
- [ ] `ComboTracker.cs` — Begin: index 0 and return 0 (restarts mid-combo); empty chain → -1. — [Assets/_MyFiles/Scripts/Combat/ComboTracker.cs:77](Assets/_MyFiles/Scripts/Combat/ComboTracker.cs#L77)
- [ ] `ComboTracker.cs` — TryAdvance: if HasNext advance and return true; else no change and `nextIndex = CurrentIndex`. — [Assets/_MyFiles/Scripts/Combat/ComboTracker.cs:87](Assets/_MyFiles/Scripts/Combat/ComboTracker.cs#L87)
- [ ] `ComboTracker.cs` — Reset: `_currentIndex = -1` (idempotent). — [Assets/_MyFiles/Scripts/Combat/ComboTracker.cs:96](Assets/_MyFiles/Scripts/Combat/ComboTracker.cs#L96)
- [ ] `AttackData.cs` — CreateDamageInfo: knockback X mirrored by facing (0 → +1), Y kept, Z 0; build the DamageInfo. — [Assets/_MyFiles/Scripts/Combat/AttackData.cs:84](Assets/_MyFiles/Scripts/Combat/AttackData.cs#L84)
- [ ] `AttackData.cs` — OnValidate: keep frame data sane; warn when the cancel window runs past TotalFrames or opens before the active frames end. — [Assets/_MyFiles/Scripts/Combat/AttackData.cs:93](Assets/_MyFiles/Scripts/Combat/AttackData.cs#L93)

## Phase 6 — Combat (Hurtbox / Hitbox / TrainingDummy) (9)

`Hurtbox` is already fully wired (no TODOs). After this phase hits land on the dummies once the player can attack.

- [ ] `Hitbox.cs` — BeginAttack: store the attack + facing sign (never 0), clear the per-activation hit set, arm. — [Assets/_MyFiles/Scripts/Combat/Hitbox.cs:80](Assets/_MyFiles/Scripts/Combat/Hitbox.cs#L80)
- [ ] `Hitbox.cs` — Sweep: `OverlapBoxNonAlloc` → Hurtbox filter (self, already hit, invulnerable) → damage → knockback → mark → publish HitEvent. — [Assets/_MyFiles/Scripts/Combat/Hitbox.cs:89](Assets/_MyFiles/Scripts/Combat/Hitbox.cs#L89)
- [ ] `Hitbox.cs` — EndAttack: disarm, clear the attack and hit set (idempotent). — [Assets/_MyFiles/Scripts/Combat/Hitbox.cs:105](Assets/_MyFiles/Scripts/Combat/Hitbox.cs#L105)
- [ ] `Hitbox.cs` — GetWorldCenter: `transform.position + (offset.x * facing, offset.y, 0)`. — [Assets/_MyFiles/Scripts/Combat/Hitbox.cs:113](Assets/_MyFiles/Scripts/Combat/Hitbox.cs#L113)
- [ ] `Hitbox.cs` — GetHalfExtents: `(size.x / 2, size.y / 2, DepthTolerance)`. — [Assets/_MyFiles/Scripts/Combat/Hitbox.cs:122](Assets/_MyFiles/Scripts/Combat/Hitbox.cs#L122)
- [ ] `TrainingDummy.cs` — Awake: start at MaxHealth; timers and velocities at zero. — [Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs:52](Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs#L52)
- [ ] `TrainingDummy.cs` — Update: health-reset timer, knockback deceleration, gravity, `CharacterController.Move` (scaled dt). — [Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs:57](Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs#L57)
- [ ] `TrainingDummy.cs` — TryApplyDamage: subtract (clamp at 0), restart the reset timer, return true. — [Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs:72](Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs#L72)
- [ ] `TrainingDummy.cs` — ApplyKnockback: SET planar knockback × KnockbackScale; positive Y launches. — [Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs:84](Assets/_MyFiles/Scripts/Combat/TrainingDummy.cs#L84)

## Phase 7 — Player (33)

Order: input → controller/context → states Idle → Move → Dash → Attack → Hitstun. Play-test in `TestArena` after each state.

- [ ] `PlayerInputReader.cs` — Update: read the raw `Move` vector (execution order is already -50 via the attribute). — [Assets/_MyFiles/Scripts/Player/PlayerInputReader.cs:110](Assets/_MyFiles/Scripts/Player/PlayerInputReader.cs#L110)
- [ ] `PlayerInputReader.cs` — OnAttackPerformed: raise `CommandPressed(Attack)`. — [Assets/_MyFiles/Scripts/Player/PlayerInputReader.cs:119](Assets/_MyFiles/Scripts/Player/PlayerInputReader.cs#L119)
- [ ] `PlayerInputReader.cs` — OnDashPerformed: raise `CommandPressed(Dash)`. — [Assets/_MyFiles/Scripts/Player/PlayerInputReader.cs:125](Assets/_MyFiles/Scripts/Player/PlayerInputReader.cs#L125)
- [ ] `PlayerController.cs` — OnCommandPressed: `Buffer.Enqueue(command, Time.time)` (scaled time). — [Assets/_MyFiles/Scripts/Player/PlayerController.cs:124](Assets/_MyFiles/Scripts/Player/PlayerController.cs#L124)
- [ ] `PlayerController.cs` — TryApplyDamage: reject if invulnerable; store LastHit; `ChangeState(Hitstun)`; return true. — [Assets/_MyFiles/Scripts/Player/PlayerController.cs:137](Assets/_MyFiles/Scripts/Player/PlayerController.cs#L137)
- [ ] `PlayerController.cs` — ApplyKnockback: SET planar `Velocity` from knockback X/Z. — [Assets/_MyFiles/Scripts/Player/PlayerController.cs:149](Assets/_MyFiles/Scripts/Player/PlayerController.cs#L149)
- [ ] `PlayerController.cs` — OnDisable: clear i-frames, the armed hitbox and the buffer so re-enabling never resumes stale. — [Assets/_MyFiles/Scripts/Player/PlayerController.cs:102](Assets/_MyFiles/Scripts/Player/PlayerController.cs#L102)
- [ ] `PlayerContext.cs` — GetMoveInputWorld: radial deadzone, clamp to 1, map to (x, 0, y × DepthSpeedMultiplier). — [Assets/_MyFiles/Scripts/Player/PlayerContext.cs:99](Assets/_MyFiles/Scripts/Player/PlayerContext.cs#L99)
- [ ] `PlayerContext.cs` — HasMoveInput: same deadzone test as GetMoveInputWorld. — [Assets/_MyFiles/Scripts/Player/PlayerContext.cs:112](Assets/_MyFiles/Scripts/Player/PlayerContext.cs#L112)
- [ ] `PlayerContext.cs` — Move: gravity/ground-stick on `_verticalVelocity`, then `CharacterController.Move` (skip when dt <= 0). — [Assets/_MyFiles/Scripts/Player/PlayerContext.cs:121](Assets/_MyFiles/Scripts/Player/PlayerContext.cs#L121)
- [ ] `PlayerMovementSettings.cs` — OnValidate: clamp `_dashIFrameEnd` into `[_dashIFrameStart, _dashFrames]`. — [Assets/_MyFiles/Scripts/Player/PlayerMovementSettings.cs:86](Assets/_MyFiles/Scripts/Player/PlayerMovementSettings.cs#L86)
- [ ] `IdleState.cs` — Enter: nothing mandatory; don't zero velocity. — [Assets/_MyFiles/Scripts/Player/States/IdleState.cs:16](Assets/_MyFiles/Scripts/Player/States/IdleState.cs#L16)
- [ ] `IdleState.cs` — Tick: consume Dash → Dash; consume Attack → Begin → Attack; move input → Move; then decelerate + Move. — [Assets/_MyFiles/Scripts/Player/States/IdleState.cs:23](Assets/_MyFiles/Scripts/Player/States/IdleState.cs#L23)
- [ ] `IdleState.cs` — FixedTick: nothing (no timeline). — [Assets/_MyFiles/Scripts/Player/States/IdleState.cs:33](Assets/_MyFiles/Scripts/Player/States/IdleState.cs#L33)
- [ ] `IdleState.cs` — Exit: nothing. — [Assets/_MyFiles/Scripts/Player/States/IdleState.cs:39](Assets/_MyFiles/Scripts/Player/States/IdleState.cs#L39)
- [ ] `MoveState.cs` — Enter: nothing mandatory; keep momentum. — [Assets/_MyFiles/Scripts/Player/States/MoveState.cs:15](Assets/_MyFiles/Scripts/Player/States/MoveState.cs#L15)
- [ ] `MoveState.cs` — Tick: Idle's transitions (→ Idle when neutral); facing from input X; accelerate toward input × MoveSpeed; Move. — [Assets/_MyFiles/Scripts/Player/States/MoveState.cs:21](Assets/_MyFiles/Scripts/Player/States/MoveState.cs#L21)
- [ ] `MoveState.cs` — FixedTick: nothing (locomotion lives in Tick). — [Assets/_MyFiles/Scripts/Player/States/MoveState.cs:31](Assets/_MyFiles/Scripts/Player/States/MoveState.cs#L31)
- [ ] `MoveState.cs` — Exit: nothing; don't zero velocity. — [Assets/_MyFiles/Scripts/Player/States/MoveState.cs:37](Assets/_MyFiles/Scripts/Player/States/MoveState.cs#L37)
- [ ] `DashState.cs` — Enter: lock direction (input or facing), maybe update facing, reset clock, process frame 0, set dash velocity. — [Assets/_MyFiles/Scripts/Player/States/DashState.cs:30](Assets/_MyFiles/Scripts/Player/States/DashState.cs#L30)
- [ ] `DashState.cs` — Tick: constant dash velocity + `context.Move`. — [Assets/_MyFiles/Scripts/Player/States/DashState.cs:39](Assets/_MyFiles/Scripts/Player/States/DashState.cs#L39)
- [ ] `DashState.cs` — FixedTick: advance the clock, process new frames; at DashFrames consume Attack → Attack, else Move/Idle. — [Assets/_MyFiles/Scripts/Player/States/DashState.cs:46](Assets/_MyFiles/Scripts/Player/States/DashState.cs#L46)
- [ ] `DashState.cs` — Exit: ALWAYS clear `IsInvulnerable`. — [Assets/_MyFiles/Scripts/Player/States/DashState.cs:54](Assets/_MyFiles/Scripts/Player/States/DashState.cs#L54)
- [ ] `DashState.cs` — ProcessFrame: i-frames = `DashIFrameStart <= frame < DashIFrameEnd`. — [Assets/_MyFiles/Scripts/Player/States/DashState.cs:61](Assets/_MyFiles/Scripts/Player/States/DashState.cs#L61)
- [ ] `AttackState.cs` — Enter: fetch the attack for CurrentIndex (null → reset + Idle), reset clock, `Hitbox.BeginAttack`, process frame 0. — [Assets/_MyFiles/Scripts/Player/States/AttackState.cs:31](Assets/_MyFiles/Scripts/Player/States/AttackState.cs#L31)
- [ ] `AttackState.cs` — Tick: in the cancel window: Dash cancel, then (HasNext) chain Attack; lunge during Startup/Active; Move. — [Assets/_MyFiles/Scripts/Player/States/AttackState.cs:42](Assets/_MyFiles/Scripts/Player/States/AttackState.cs#L42)
- [ ] `AttackState.cs` — FixedTick: advance the clock, process every new frame; when finished reset the combo → Move/Idle. — [Assets/_MyFiles/Scripts/Player/States/AttackState.cs:56](Assets/_MyFiles/Scripts/Player/States/AttackState.cs#L56)
- [ ] `AttackState.cs` — Exit: `Hitbox.EndAttack()`, clear the attack (don't reset the combo here). — [Assets/_MyFiles/Scripts/Player/States/AttackState.cs:65](Assets/_MyFiles/Scripts/Player/States/AttackState.cs#L65)
- [ ] `AttackState.cs` — ProcessFrame: `Hitbox.Sweep()` on active frames. — [Assets/_MyFiles/Scripts/Player/States/AttackState.cs:73](Assets/_MyFiles/Scripts/Player/States/AttackState.cs#L73)
- [ ] `HitstunState.cs` — Enter: clear buffer, end hitbox, reset combo, clear i-frames, zero velocity, reset clock. — [Assets/_MyFiles/Scripts/Player/States/HitstunState.cs:22](Assets/_MyFiles/Scripts/Player/States/HitstunState.cs#L22)
- [ ] `HitstunState.cs` — Tick: decelerate knockback velocity + `context.Move`. — [Assets/_MyFiles/Scripts/Player/States/HitstunState.cs:32](Assets/_MyFiles/Scripts/Player/States/HitstunState.cs#L32)
- [ ] `HitstunState.cs` — FixedTick: advance the clock; at `LastHit.HitstunFrames` → Move/Idle. — [Assets/_MyFiles/Scripts/Player/States/HitstunState.cs:40](Assets/_MyFiles/Scripts/Player/States/HitstunState.cs#L40)
- [ ] `HitstunState.cs` — Exit: nothing; keep residual knockback. — [Assets/_MyFiles/Scripts/Player/States/HitstunState.cs:46](Assets/_MyFiles/Scripts/Player/States/HitstunState.cs#L46)

## Phase 8 — Presentation basics (7)

- [ ] `CameraRig.cs` — ApplySettings: pivot rotation (pitch, yaw), camera local (0, 0, -Distance), FOV; null-check each reference. — [Assets/_MyFiles/Scripts/Presentation/CameraRig.cs:96](Assets/_MyFiles/Scripts/Presentation/CameraRig.cs#L96)
- [ ] `CameraRig.cs` — SnapToTarget: teleport to target + offset, zero the follow velocity. — [Assets/_MyFiles/Scripts/Presentation/CameraRig.cs:87](Assets/_MyFiles/Scripts/Presentation/CameraRig.cs#L87)
- [ ] `CameraRig.cs` — Start: call `SnapToTarget()`. — [Assets/_MyFiles/Scripts/Presentation/CameraRig.cs:63](Assets/_MyFiles/Scripts/Presentation/CameraRig.cs#L63)
- [ ] `CameraRig.cs` — LateUpdate: SmoothDamp toward target + offset with scaled dt (smooth time 0 = rigid). — [Assets/_MyFiles/Scripts/Presentation/CameraRig.cs:68](Assets/_MyFiles/Scripts/Presentation/CameraRig.cs#L68)
- [ ] `BillboardSprite.cs` — LateUpdate: copy the camera rotation (or yaw only); `flipX = FacingSign < 0`. — [Assets/_MyFiles/Scripts/Presentation/BillboardSprite.cs:62](Assets/_MyFiles/Scripts/Presentation/BillboardSprite.cs#L62)
- [ ] `BlobShadow.cs` — LateUpdate: raycast down (ignore triggers), place/scale the shadow by height, hide on a miss. — [Assets/_MyFiles/Scripts/Presentation/BlobShadow.cs:58](Assets/_MyFiles/Scripts/Presentation/BlobShadow.cs#L58)
- [ ] `BlobShadow.cs` — SetVisible: toggle the shadow only when visibility changes. — [Assets/_MyFiles/Scripts/Presentation/BlobShadow.cs:76](Assets/_MyFiles/Scripts/Presentation/BlobShadow.cs#L76)

## Phase 9 — Game feel (last) (16)

Needs Phase 4 (EventBus) and Phase 6 (Hitbox publishing `HitEvent`).

- [ ] `HitStop.cs` — Trigger: capture timeScale on the first trigger; remaining = max(remaining, min(seconds, Max)); set HitStopTimeScale. — [Assets/_MyFiles/Scripts/Presentation/HitStop.cs:99](Assets/_MyFiles/Scripts/Presentation/HitStop.cs#L99)
- [ ] `HitStop.cs` — Update: count down with `Time.unscaledDeltaTime`; restore timeScale at 0. — [Assets/_MyFiles/Scripts/Presentation/HitStop.cs:85](Assets/_MyFiles/Scripts/Presentation/HitStop.cs#L85)
- [ ] `HitStop.cs` — OnHit: `Trigger(FrameTime.ToSeconds(evt.HitStopFrames))`. — [Assets/_MyFiles/Scripts/Presentation/HitStop.cs:116](Assets/_MyFiles/Scripts/Presentation/HitStop.cs#L116)
- [ ] `HitStop.cs` — OnDisable: CRITICAL: restore timeScale if a freeze is active. — [Assets/_MyFiles/Scripts/Presentation/HitStop.cs:79](Assets/_MyFiles/Scripts/Presentation/HitStop.cs#L79)
- [ ] `ScreenShake.cs` — AddTrauma: ignore <= 0; `Clamp01(trauma + amount)`. — [Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs:97](Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs#L97)
- [ ] `ScreenShake.cs` — LateUpdate: Perlin × trauma² × MaxOffset on ShakeRoot local X/Y; decay on unscaled time. — [Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs:78](Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs#L78)
- [ ] `ScreenShake.cs` — OnHit: `AddTrauma(evt.ScreenShake)`. — [Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs:106](Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs#L106)
- [ ] `ScreenShake.cs` — OnDisable: zero the trauma and the ShakeRoot offset. — [Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs:73](Assets/_MyFiles/Scripts/Presentation/ScreenShake.cs#L73)
- [ ] `DamageNumberSpawner.cs` — OnGetNumber: `SetActive(true)`. — [Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs:123](Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs#L123)
- [ ] `DamageNumberSpawner.cs` — OnReleaseNumber: `SetActive(false)`. — [Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs:130](Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs#L130)
- [ ] `DamageNumberSpawner.cs` — Spawn: offset + camera-right spread, camera rotation, `_pool.Get().Play(...)`. — [Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs:100](Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs#L100)
- [ ] `DamageNumberSpawner.cs` — OnHit: `Spawn(evt.Damage.Amount, evt.Damage.HitPoint)`. — [Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs:150](Assets/_MyFiles/Scripts/Presentation/DamageNumberSpawner.cs#L150)
- [ ] `DamageNumber.cs` — Play: set the text, position/rotation, reset elapsed/colour, mark playing. — [Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs:100](Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs#L100)
- [ ] `DamageNumber.cs` — Update: unscaled rise (ease-out) + fade; release when the lifetime ends. — [Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs:71](Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs#L71)
- [ ] `DamageNumber.cs` — ReleaseToPool: set the guard flag first, then `_pool.Release(this)` (or Destroy if un-pooled). — [Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs:110](Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs#L110)
- [ ] `DamageNumber.cs` — OnDisable: only mark released; never Release from OnDisable. — [Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs:78](Assets/_MyFiles/Scripts/Presentation/DamageNumber.cs#L78)
