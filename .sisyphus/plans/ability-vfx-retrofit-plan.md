# Ability VFX Spatial Retrofit Plan

## 1. Objective

Retrofitting the ability VFX pipeline so the existing point-oriented system can also express **line** and **wall** effects, while preserving:

- backward compatibility for existing saved abilities
- current editor workflow and mental model
- current runtime ability execution pipeline
- current preview usefulness for authoring and validation

This plan is written for **ultrawork execution**: tightly staged, test-first where practical, low-risk, and suitable for a sequence of small verifiable changes.

---

## 2. Success Criteria

The work is complete only when all of the following are true:

1. Existing saved abilities still load without migration.
2. Existing point/preset/custom-texture VFX still preview and play correctly.
3. Runtime supports all exposed VFX triggers:
   - `OnCastStart`
   - `OnWarmup`
   - `OnCastFinish`
   - `OnTargetApply`
   - `OnDurationTick`
   - `OnExpire`
4. The editor can configure spatial VFX in three modes:
   - `Point`
   - `Line`
   - `Wall`
5. Runtime can play line and wall effects using current engine-compatible primitives.
6. Preview can display line/wall geometry while preserving current point-preview interaction.
7. Validation rejects invalid spatial VFX configs early.
8. The implementation lands through atomic commits that are individually understandable and verifiable.

---

## 3. Confirmed Baseline

### 3.1 Relevant files

- `Source/CharacterStudio/Abilities/ModularAbilityDef.cs`
- `Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs`
- `Source/CharacterStudio/Abilities/VisualEffectWorker.cs`
- `Source/CharacterStudio/UI/Dialog_AbilityEditor.Panels.Vfx.cs`
- `Source/CharacterStudio/UI/Dialog_AbilityEditor.Preview.cs`
- `Source/CharacterStudio/Abilities/AbilityGrantUtility.cs`

### 3.2 Current limitations

1. Runtime VFX dispatch is effectively centered on `OnTargetApply` / `OnCastFinish` behavior.
2. Position resolution is point-based (`Caster`, `Target`, `Both`) with offsets.
3. Existing workers are fleck/mote oriented rather than spatial-path oriented.
4. Custom texture VFX resolve to a single runtime point.
5. Preview is good for point textures but has no line/wall visualization layer.

---

## 4. Non-Goals

The retrofit must **not** expand into any of the following:

- node-graph VFX authoring
- spline/path authoring UI
- projectile trail framework
- new standalone effect asset system
- broad rewrite of the ability effect system
- speculative rendering framework beyond current RimWorld-friendly primitives

---

## 5. Architectural Direction

Preserve the current execution chain:

`ModularAbilityDef -> AbilityGrantUtility -> CompProperties_AbilityModular -> CompAbilityEffect_Modular -> VisualEffectWorkerFactory -> worker.Play(...)`

The retrofit adds a **spatial interpretation layer** to `AbilityVisualEffectConfig` and the worker/resolver path. It does **not** replace the surrounding pipeline.

Key rule: **new functionality must compose with the current model, not fork the model.**

---

## 6. Design Decisions

### 6.1 Data model direction

Add explicit spatial metadata rather than overloading current point-only fields.

#### New enums

- `AbilityVisualSpatialMode`
  - `Point`
  - `Line`
  - `Wall`
- `AbilityVisualAnchorMode`
  - `Caster`
  - `Target`
  - `TargetCell`
  - `AreaCenter`
- `AbilityVisualPathMode`
  - `None`
  - `DirectLineCasterToTarget`

#### Extend `AbilityVisualEffectType`

Add runtime/editor-visible types:

- `LineTexture`
- `WallTexture`

#### Extend `AbilityVisualEffectConfig`

Add grouped fields.

**Spatial fields**

- `spatialMode = AbilityVisualSpatialMode.Point`
- `anchorMode = AbilityVisualAnchorMode.Target`
- `secondaryAnchorMode = AbilityVisualAnchorMode.Target`
- `pathMode = AbilityVisualPathMode.None`

**Geometry fields**

- `lineWidth = 0.35f`
- `wallHeight = 2.5f`
- `wallThickness = 0.2f`
- `tileByLength = true`
- `followGround = false`
- `segmentCount = 1`
- `revealBySegments = false`
- `segmentRevealIntervalTicks = 3`

### 6.2 Save normalization

Update `NormalizeForSave()` so that it:

1. preserves all valid VFX triggers instead of collapsing most of them
2. clamps geometry values into safe ranges
3. falls back invalid enum values to safe defaults
4. keeps old point-based abilities semantically unchanged

### 6.3 Validation

Extend validation so each visual effect config is checked individually.

Required rules:

- `LineTexture` requires `lineWidth > 0`
- `WallTexture` requires `wallHeight > 0` and `wallThickness > 0`
- `segmentCount >= 1`
- `segmentRevealIntervalTicks >= 0`
- custom-texture-based effects still require a non-empty texture path where applicable

---

## 7. Runtime Retrofit Plan

### 7.1 Trigger dispatch

Replace the current partial VFX scheduling with centralized trigger dispatch in `CompAbilityEffect_Modular`.

Planned helper methods:

- `TriggerVisualEffects(AbilityVisualEffectTrigger trigger, LocalTargetInfo target)`
- `QueueVisualEffectsForTrigger(AbilityVisualEffectTrigger trigger, LocalTargetInfo target)`

Planned dispatch points:

1. Ability application start
   - dispatch `OnCastStart`
2. Warmup path when applicable
   - dispatch `OnWarmup`
3. Immediately before resolved effect payload application
   - dispatch `OnCastFinish`
4. Immediately after resolved effect payload application
   - dispatch `OnTargetApply`
5. Periodic pulse/tick path
   - dispatch `OnDurationTick`
6. Expiration path for timed/shield/periodic state
   - dispatch `OnExpire`

Important constraint: keep the delayed queue model if possible; do not rewrite scheduling infrastructure unless required by evidence.

### 7.2 Spatial resolver

Add a small spatial resolution helper in `VisualEffectWorker.cs` or a sibling internal helper.

The helper must resolve:

- anchor positions from caster/target/cell/area center
- direct line start/end positions
- repeated sample points for line/wall segment placement

Minimum viable behavior:

- `Point` -> current point logic
- `Line` -> direct start/end line
- `Wall` -> same line, rendered as repeated upright segments

### 7.3 Worker strategy

Add:

- `VisualEffectWorker_LineTexture`
- `VisualEffectWorker_WallTexture`

Do **not** introduce continuous mesh generation.

Use the current custom-texture/mote-compatible strategy:

- `LineTexture`
  - resolve line start/end
  - compute direction and distance
  - spawn repeated textured motes along the line
  - align rotation to direction
  - scale width from `lineWidth`
  - tile by length or segment count

- `WallTexture`
  - reuse the same line basis
  - spawn repeated upright segments along the line
  - derive apparent width/thickness from config
  - derive vertical size from `wallHeight`
  - optionally reveal by segment scheduling

### 7.4 Factory registration

Register new types in the worker factory and wire any preset maps only if they already fit the existing pattern cleanly.

---

## 8. Editor Retrofit Plan

### 8.1 UI approach

Preserve the current list-item editor pattern and add conditional spatial controls instead of building a separate VFX editor.

### 8.2 New controls

Add editor controls for:

1. spatial mode
2. anchor mode
3. secondary anchor mode (line/wall only)
4. path mode (line/wall only)
5. geometry fields
   - line width
   - wall height
   - wall thickness
   - segment count
   - reveal by segments
   - segment reveal interval

### 8.3 Trigger exposure

Once runtime support exists, the editor must expose the full trigger set rather than filtering down to the currently narrow runtime-safe subset.

### 8.4 Layout sizing

Update item height calculation to account for the added rows only when they are relevant to the currently selected VFX type/spatial mode.

---

## 9. Preview Retrofit Plan

### 9.1 Preview model

Keep current event-plan generation and extend preview metadata with:

- spatial mode
- optional resolved line endpoints
- wall height metadata
- segment count / reveal metadata if useful for display

### 9.2 Rendering approach

Preserve current point texture preview behavior.

Add lightweight geometry overlays:

- line -> draw a strip/segment approximation between start and end
- wall -> draw repeated upright segment rectangles along the same basis

### 9.3 Interaction scope

For v1, keep existing point manipulation behavior unchanged.

Do **not** add new interactive handles for line/wall editing in this retrofit. Use current caster/target anchor semantics for the first complete landing.

---

## 10. Localization Plan

Update:

- `Languages/ChineseSimplified/Keyed/CS_Keys_AbilityEditor.xml`
- `Languages/English/Keyed/CS_Keys_AbilityEditor.xml`

Add labels for:

- new VFX types
- spatial mode values
- anchor mode values
- path mode values
- geometry labels
- validation/error text if surfaced in UI

---

## 11. Compatibility Rules

Backward compatibility is a hard requirement.

Rules:

1. Existing abilities default to `Point` spatial mode.
2. Existing custom texture VFX remain point-based unless explicitly edited.
3. Existing trigger values stay valid and are no longer collapsed away on save.
4. Legacy fields such as `sourceMode` / `useCasterFacing` must remain synchronized where current code still depends on them.
5. No forced migration pass is introduced.

---

## 12. TDD-Oriented Execution Plan

This project area is UI + runtime heavy, so “pure unit tests first” may not cover everything. The correct TDD posture here is:

- add or extend low-level tests where logic is isolatable
- add regression tests before changing behavior when a seam exists
- use build + targeted manual scenarios as the acceptance layer for engine-bound rendering behavior

### 12.1 TDD rules for this work

For each stage:

1. **Write or identify a failing test first** for normalization, validation, resolver logic, or trigger dispatch where the code can be isolated.
2. **Make the smallest code change** required to pass that test.
3. **Refactor only after green** and only within the stage boundary.
4. **Run targeted regression verification** after each stage.

### 12.2 Candidate test seams

Prefer tests around:

- `AbilityVisualEffectConfig.NormalizeForSave()`
- validation helpers / `ModularAbilityDefExtensions.Validate()`
- trigger-to-runtime dispatch selection logic
- spatial resolver functions that convert anchors into positions and sampled segments

Where direct automated tests are not practical, define manual acceptance checks before implementation.

### 12.3 TDD stage breakdown

#### Stage A — Data model and normalization

Write failing tests for:

- default spatial values on legacy-compatible configs
- trigger preservation during normalize/save
- geometry clamping and enum fallback behavior

Then implement only enough data model and normalization changes to turn those tests green.

#### Stage B — Validation

Write failing tests for invalid line/wall configs.

Then implement validation rules and minimal user-facing error text hooks.

#### Stage C — Runtime trigger dispatch

Add tests or isolated assertions for dispatch selection if seams exist.

If full automated runtime testing is impractical, create a pre-declared manual checklist mapping each trigger to an expected gameplay event, then implement the dispatch hooks.

#### Stage D — Spatial resolver and worker behavior

Write tests for resolver outputs:

- start/end selection
- segment count behavior
- fallback behavior when target/cell info is incomplete

Then implement line/wall workers using the resolver.

#### Stage E — Editor UI

Define explicit manual acceptance cases before editing UI:

- relevant fields appear only when needed
- existing point workflows remain uncluttered
- full trigger set is selectable after runtime support lands

Then implement the UI.

#### Stage F — Preview

Define preview acceptance examples before implementation:

- point preview unchanged
- line preview matches expected spatial relationship
- wall preview aligns with line basis and segment density

Then implement the preview overlay logic.

#### Stage G — Localization and final regression

Add/update strings last, then run final regression and documentation pass.

---

## 13. Ultrawork Delivery Sequence

This is the execution order intended for high-discipline delivery.

### Wave 1 — Safe schema foundation

1. Extend enums and config fields.
2. Extend normalize/save behavior.
3. Extend validation.
4. Keep runtime/editor behavior unchanged except where required for compatibility.

**Exit criteria:** build green; tests for normalization/validation green; existing abilities still deserialize.

### Wave 2 — Runtime trigger correctness

1. Centralize VFX trigger dispatch.
2. Hook all currently exposed trigger points.
3. Keep point effects behaving exactly as before for equivalent configs.

**Exit criteria:** build green; trigger checklist passes; no regression in existing point effects.

### Wave 3 — Spatial runtime playback

1. Add spatial resolver.
2. Add line worker.
3. Add wall worker.
4. Register factory entries.

**Exit criteria:** line/wall configs can play in runtime; point effects unchanged.

### Wave 4 — Authoring surface

1. Extend VFX editor UI.
2. Expose full trigger list.
3. Adjust dynamic layout height.

**Exit criteria:** author can configure new spatial effects without breaking old workflows.

### Wave 5 — Preview and usability finish

1. Add preview metadata.
2. Add line/wall preview overlay.
3. Add localization.
4. Run final QA.

**Exit criteria:** preview is useful and coherent with runtime behavior; strings are complete.

---

## 14. Atomic Commit Strategy

Every commit must be small enough to review, revert, and verify independently. Avoid mixed-purpose commits.

### Planned commit series

1. **`extend ability vfx config for spatial modes`**
   - enums
   - config fields
   - normalization defaults
   - no editor/runtime feature exposure yet

2. **`add validation for spatial ability vfx configs`**
   - validation rules
   - tests for invalid configs
   - optional validation messages

3. **`dispatch modular ability vfx across all triggers`**
   - centralized runtime dispatch
   - trigger hook wiring
   - regression verification for existing point effects

4. **`add line and wall texture vfx workers`**
   - spatial resolver
   - line/wall workers
   - factory registration

5. **`expose spatial vfx controls in ability editor`**
   - UI controls
   - trigger selection expansion
   - dynamic layout updates

6. **`preview line and wall ability vfx`**
   - preview metadata
   - geometry overlay rendering

7. **`localize spatial ability vfx editor strings`**
   - English + ChineseSimplified keys

8. **`add final retrofit regression coverage`**
   - test cleanup/additions
   - any non-feature documentation updates tightly tied to verification

### Commit rules

- one purpose per commit
- tests or explicit verification evidence must accompany each behavioral commit
- no opportunistic refactors in bugfix/feature commits
- if a stage needs a preparatory rename/extraction, make that its own commit only if it reduces risk materially

---

## 15. Verification Matrix

### 15.1 Automated verification

Run when available after each relevant stage:

- compile/build
- changed-file diagnostics
- targeted tests for normalization/validation/resolver/dispatch seams

### 15.2 Manual verification scenarios

#### Compatibility regression

1. Existing point dust/glow VFX still plays.
2. Existing custom texture VFX still previews and plays.
3. Saving an old ability does not silently rewrite it into broken trigger semantics.

#### Trigger coverage

4. `OnCastStart` plays at cast start.
5. `OnWarmup` plays during warmup.
6. `OnCastFinish` plays immediately before payload application.
7. `OnTargetApply` plays immediately after payload application.
8. `OnDurationTick` plays on periodic pulse.
9. `OnExpire` plays on timed/shield/periodic expiry.

#### Spatial runtime

10. Line texture VFX appears from the expected start anchor to the expected end anchor.
11. Wall texture VFX appears along the same spatial basis with visible segmentation.
12. Invalid configs are rejected before runtime playback.

#### Editor and preview

13. Point-mode editing remains straightforward.
14. Spatial-only controls appear only when relevant.
15. Preview line matches expected caster/target relationship.
16. Preview wall segment density is coherent with runtime intent.

---

## 16. Main Risks and Controls

### Risk 1 — Config breadth increases

`AbilityVisualEffectConfig` becomes wider.

**Control:** keep fields grouped, default-safe, and conditionally rendered in UI.

### Risk 2 — Trigger semantics drift

New runtime hook points may accidentally change timing for existing effects.

**Control:** isolate dispatch logic, preserve old point behavior for equivalent configs, and regression-test existing triggers.

### Risk 3 — Preview/runtime mismatch

Preview may not perfectly mirror runtime rendering.

**Control:** preview for spatial relationship and authoring confidence, not pixel-perfect rendering; document that constraint in implementation review.

### Risk 4 — Scope creep into rendering tech

There will be temptation to build a richer rendering system.

**Control:** explicitly stay with repeated mote/segment rendering for this retrofit.

---

## 17. Definition of Done

Done means all of the following are true:

1. The code matches this plan’s compatibility and scope rules.
2. Authors can configure point, line, and wall VFX in the existing ability editor.
3. Runtime plays those effects through the existing ability execution pipeline.
4. Preview visualizes the new spatial modes sufficiently for authoring.
5. Existing abilities remain compatible without migration.
6. TDD-oriented checks were used where seams existed, with explicit manual acceptance where they did not.
7. The work landed in atomic commits with clear verification evidence.
8. Final build/regression verification is green or any pre-existing unrelated failures are explicitly called out.
