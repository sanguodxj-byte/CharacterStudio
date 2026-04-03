# CharacterStudio Architecture Risk Reduction Plan

## 0. Purpose

This plan is the execution blueprint for reducing architectural risk in CharacterStudio without a destabilizing rewrite.

It is written for execution by an ultrawork-style implementation flow: long-running, high-discipline, incrementally verifiable work that must remain safe under partial completion, handoff, and review.

Primary goals:

1. Reduce regression risk across UI, rendering, runtime state, and export.
2. Clarify boundaries between editor state, runtime state, and export state.
3. Make future implementation safer, more diagnosable, and more incremental.
4. Preserve current behavior unless a change is explicitly intended.
5. Make each implementation slice testable before and after refactoring.
6. Ensure every mergeable step can land through small, atomic commits.

Non-goals:

- No broad rewrite of the mod.
- No speculative redesign detached from current code.
- No large feature work mixed into the stabilization effort.

Execution requirements for this plan:

- Plan and execute in English.
- Use TDD-oriented sequencing wherever code seams allow it.
- Prefer characterization tests and golden-path scenario checks before structural edits.
- Keep each implementation batch independently reviewable and revertable.

---

## 1. Current Architecture Summary

Confirmed high-level structure from codebase inspection:

- Entry / bootstrapping:
  - `Source/CharacterStudio/ModEntryPoint.cs`
- Core domain and runtime state:
  - `Source/CharacterStudio/Core/`
- Rendering and Harmony patch integration:
  - `Source/CharacterStudio/Rendering/`
- UI/editor surface:
  - `Source/CharacterStudio/UI/`
- Export pipeline:
  - `Source/CharacterStudio/Exporter/`
- Additional supporting subsystems:
  - `Abilities/`, `Attributes/`, `Items/`, `Performance/`, `Debug/`, `Patches/`, `Introspection/`

Observed system shape:

- Core acts as the logical center.
- Rendering is the highest-risk integration layer.
- UI is the largest complexity concentration.
- Export is a separate output pipeline built on top of core models.
- Public API already exists and must be treated as an external contract.

---

## 2. Risk Model

### 2.1 Primary Risks

#### R1. UI complexity concentration

Signals:

- Large `Dialog_SkinEditor.*` family.
- Large `Dialog_AbilityEditor.*` family.
- Likely mixing of interaction state, orchestration, validation, preview, and persistence triggers.

Why this matters:

- Small UI changes can affect preview, apply, or save behavior.
- State inconsistencies become hard to diagnose.
- Behavior becomes distributed across partial files.

#### R2. Rendering patch coupling and opaque execution

Signals:

- `Patch_PawnRenderTree.*`
- `Patch_WeaponRender.cs`
- `PawnRenderNodeWorker_*`
- `RuntimeAssetLoader.cs`

Why this matters:

- Patch logic is fragile by nature.
- Preview/runtime divergence is likely if paths drift.
- Node hiding/injection/replacement logic is easy to entangle.
- Failures are hard to explain without explicit diagnostics.

#### R3. State boundary ambiguity

The system naturally contains at least three state shapes:

1. Editor/session state
2. Runtime-applied state
3. Export serialization state

Why this matters:

- Temporary editor state can leak into persistence or export.
- Runtime-enriched state can contaminate saved definitions.
- Responsibilities become impossible to reason about cleanly.

#### R4. Public API stability risk

Signals:

- `CharacterStudioAPI`
- runtime events
- animation provider registration surface

Why this matters:

- Internal refactors can break external integrations.
- Public/internal boundaries may be too porous.

#### R5. Lack of regression protection

Why this matters:

- Manual testing cost rises with every feature.
- Rendering/output regressions may only be found late.
- Refactoring is unsafe without narrow checks.

### 2.2 Priority Ranking

- P0:
  - R1 UI complexity concentration
  - R2 Rendering patch coupling
  - R5 Lack of regression protection
- P1:
  - R3 State boundary ambiguity
  - R4 Public API stability risk
- P2:
  - Performance/caching formalization
  - Documentation and contribution guardrails

---

## 3. Execution Strategy

Implementation must follow this principle:

> Stabilize before restructuring deeply.

The order is mandatory:

1. Establish visibility and guardrails.
2. Isolate UI state and orchestration.
3. Isolate rendering rule evaluation from patch application.
4. Separate model responsibilities.
5. Harden external API boundaries.
6. Add performance structure after boundaries are clearer.

No broad opportunistic refactors while fixing these phases.

### 3.1 Ultrawork Execution Rules

Every phase must be executable by an autonomous or semi-autonomous worker without relying on hidden context.

That means each work slice must define:

1. **Target seam** — the exact boundary being introduced or clarified.
2. **Pre-change safety check** — diagnostics, impact review, and baseline scenario confirmation.
3. **Failing test or explicit characterization check** — evidence of current behavior or intended behavior.
4. **Minimal code movement** — the smallest extraction or isolation needed to pass the check.
5. **Post-change proof** — diagnostics + targeted verification + scenario result.
6. **Commit boundary** — a single coherent reason the slice can be reviewed independently.

### 3.2 TDD Planning Mode

This is not greenfield TDD in the pure sense. Because this codebase contains UI integration, Harmony patches, and runtime behavior, the plan uses a layered TDD model:

1. **Characterization-first TDD** for legacy or opaque behavior.
   - Capture current behavior before extraction.
   - Use narrow tests, fixtures, snapshots, or deterministic dumps.
2. **Red-Green-Refactor** for newly introduced seam classes.
   - Add a failing test for the new evaluator/session/adapter behavior.
   - Implement the minimum logic to pass.
   - Refactor only after the seam is protected.
3. **Scenario TDD** for flows that are difficult to unit test directly.
   - Define scenario inputs and expected outputs.
   - Automate what is feasible.
   - Document remaining manual checks explicitly.

### 3.3 Slice Size Rule

No execution slice should simultaneously:

- introduce a new abstraction,
- migrate multiple unrelated call sites,
- and change user-visible behavior.

If all three are required, split the work into separate commits/phases.

### 3.4 Preferred Slice Order Inside Each Workstream

For each workstream, use this internal order:

1. Baseline discovery
2. Characterization test / scenario capture
3. Introduce seam without behavior change
4. Migrate one caller/path
5. Re-verify
6. Commit
7. Repeat for the next caller/path

---

## 4. Workstreams

The plan is organized into six workstreams.

### Workstream A. Baseline Mapping and Safety Rails

#### Objective

Create enough observability and inventory to refactor safely.

#### Tasks

1. Identify exact core entry points for:
   - editor open
   - preview refresh
   - apply-to-pawn
   - save draft / save skin
   - export flow
2. Identify exact rendering decision points in:
   - render tree patching
   - node injection
   - node hiding
   - weapon rendering override
3. Identify the main public API surfaces and event emitters.
4. Add temporary architecture notes under `.sisyphus/plans/` or `docs/` during execution.
5. Define a small set of representative scenarios to preserve:
   - simple skin
   - multi-layer skin
   - face-configured skin
   - weapon/equipment-influenced skin
   - exportable skin package

#### Deliverables

- Entry-point inventory
- Rendering decision inventory
- Public API inventory
- Scenario list for verification
- Initial testability map describing which flows are unit-testable, snapshot-testable, or manual-only

#### Success Criteria

- We can point to concrete files/methods for the core flows.
- We know which scenarios must not regress during refactoring.
- We know the smallest defensible test asset to create before each later extraction.

#### TDD Notes

- Produce characterization targets before editing behavior-heavy code.
- Prefer documenting missing seams as explicit testability gaps rather than guessing at tests.
- Treat this workstream as the prerequisite for all later Red-Green-Refactor loops.

---

### Workstream B. UI State Extraction

#### Objective

Move editor state ownership out of the UI dialog partials and into a dedicated session model.

#### Proposed New Abstractions

- `SkinEditorSession`
- Optional supporting structures:
  - `SkinEditorSelectionState`
  - `SkinEditorPreviewState`
  - `SkinEditorDirtyState`
  - `SkinEditorUndoState`

#### Scope

The session should own:

- current target skin/draft
- current selection
- current panel or tool mode where relevant
- preview flags/mode
- dirty tracking
- temporary edits awaiting commit
- undo/redo history if already present conceptually

UI should stop owning these directly wherever feasible.

#### Migration Rules

1. Do not rewrite all UI at once.
2. Introduce session class first.
3. Move one cluster of responsibilities at a time:
   - selection
   - dirty tracking
   - preview state
   - save/apply prerequisites
4. Keep behavior identical during migration.
5. If a state field remains in UI temporarily, document why.

#### Target Files

Primary expected impact:

- `Source/CharacterStudio/UI/Dialog_SkinEditor.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties*.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.SaveApply*.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.SelectionUndo.cs`

#### Deliverables

- New session object integrated into skin editor.
- Reduced direct state sprawl across dialog partials.
- Single place to inspect current editing state.
- Characterization checks for the first migrated state clusters

#### Success Criteria

- Core editor state can be inspected through session.
- Changes in one UI panel do not require hidden coordination across many partial files.
- Preview, save, and apply paths consume session state instead of scattered dialog fields.

#### TDD Notes

- Start by characterizing one state cluster at a time.
- Recommended first clusters:
  1. dirty tracking
  2. selection state
  3. preview flags
- For each cluster:
  - capture current behavior,
  - add a failing seam-focused test where feasible,
  - migrate only that cluster,
  - verify UI behavior did not drift.

---

### Workstream C. UI Command / Use-Case Layer

#### Objective

Extract business actions from UI event handlers into named commands/use-cases.

#### Proposed New Abstractions

- `SkinEditorCommands` or `SkinEditorUseCases`
- Possibly separate collaborators:
  - `PreviewCurrentSkin`
  - `ApplySkinToPawn`
  - `SaveSkinDraft`
  - `ImportSkinSource`
  - `UpdateLayerProperties`

#### Scope

Actions that should not remain embedded directly in UI control handlers:

- save/apply
- preview rebuild/refresh
- import/apply workflows
- destructive edits
- property updates requiring consistency logic

#### Migration Rules

1. Start with the highest-risk actions:
   - preview
   - save
   - apply
2. Name commands according to intent, not UI widgets.
3. Keep UI as caller, not orchestrator.
4. Preserve current user-visible behavior.

#### Deliverables

- One named orchestration layer for editor actions.
- UI calling commands instead of embedding multi-step logic.
- Focused tests around extracted command/use-case behavior

#### Success Criteria

- Main operations can be traced through explicit command/use-case methods.
- UI partials become thinner and more declarative.

#### TDD Notes

- Begin with preview, save, and apply because they offer the best risk reduction.
- For each command:
  - define expected inputs, side effects, and outputs,
  - write a failing test against the command surface,
  - extract orchestration from the UI handler,
  - keep UI code as a thin adapter.

---

### Workstream D. Rendering Rule Isolation

#### Objective

Separate rendering decision logic from Harmony patch application logic.

#### Proposed New Abstractions

- `RenderContext`
- `RenderEvaluationResult`
- `RenderRuleEvaluator`
- Optional stage split:
  - `RenderInputCollector`
  - `RenderRuleEvaluator`
  - `RenderTreeApplicator`

#### Conceptual Pipeline

1. Collect relevant render inputs.
2. Evaluate what should happen.
3. Apply results through patch-compatible code.

#### RenderContext should centralize

- pawn
- active skin / runtime skin data
- facing
- current tick
- preview/runtime mode
- layer data relevant to rendering
- equipment/weapon relevant state
- any face/eye component state required by rendering

#### RenderEvaluationResult should express

- nodes to hide
- nodes to inject
- node ordering/priorities if applicable
- active custom visual features
- asset lookup requests or resolved references if appropriate

#### Migration Rules

1. Do not try to redesign all rendering at once.
2. Extract logic in the order:
   - node hiding decisions
   - node injection decisions
   - face/eye component activation decisions
   - weapon visual decisions
3. Patch files should become adapters.
4. Preserve runtime behavior before optimizing internals.

#### Target Files

- `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs`
- `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.BaseAppearance.cs`
- `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.Hiding.cs`
- `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.Injection.cs`
- `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.NodeSearch.cs`
- `Source/CharacterStudio/Rendering/Patch_WeaponRender.cs`
- `Source/CharacterStudio/Rendering/PawnRenderNodeWorker_*.cs`

#### Deliverables

- Rendering decision logic located in explicit evaluator classes.
- Patch layer reduced to bridging/application logic.
- Deterministic render-rule verification artifacts for at least the first extracted rule family

#### Success Criteria

- Given a render context, we can explain why a render result happens.
- Preview and runtime can share the same rule evaluation path as much as possible.
- Rendering bugs can be localized to collect/evaluate/apply stages.

#### TDD Notes

- Use characterization snapshots or normalized rule dumps before moving patch logic.
- Extract one rule family at a time in this order:
  1. node hiding
  2. node injection
  3. face/eye activation
  4. weapon visuals
- New evaluator classes should be driven by deterministic tests over `RenderContext` inputs and normalized `RenderEvaluationResult` outputs.

---

### Workstream E. Model Boundary Separation

#### Objective

Separate editor-only, runtime-only, and export-only state from the stable core domain model.

#### Target Model Categories

1. **Domain Model**
   - canonical skin/layer/face definitions
2. **Editor Session Model**
   - selection, dirty state, temporary edits, UI-only metadata
3. **Runtime Projection**
   - runtime-efficient representation for rendering/application
4. **Export DTO / Export Projection**
   - export-ready representation with only relevant serializable fields

#### Migration Rules

1. Do not rename core models casually.
2. Prefer adapters/projections before invasive model surgery.
3. Make each transformation path explicit:
   - domain -> editor session view
   - domain -> runtime projection
   - domain -> export DTO
4. Remove direct dependence of exporter on UI/session state.
5. Remove direct dependence of rendering on UI-only state.

#### Likely Impact Areas

- `Source/CharacterStudio/Core/PawnSkinDef.cs`
- `Source/CharacterStudio/Core/PawnLayerConfig.cs`
- `Source/CharacterStudio/Core/PawnFaceConfig.cs`
- `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`
- `Source/CharacterStudio/Core/PawnSkinRuntimeValidator.cs`
- `Source/CharacterStudio/Exporter/*.cs`
- UI save/apply/import integration files

#### Deliverables

- Explicit model boundary map.
- At least one runtime projection structure.
- At least one export projection/DTO structure where needed.
- Tests or structured assertions proving the projection boundaries for the first migrated path

#### Success Criteria

- Editor-only state no longer contaminates export/runtime logic.
- Runtime-specific structures are not used as editor scratch state.
- Export pipeline consumes export-safe data.

#### TDD Notes

- First create assertions around the current export/runtime shape for one narrow path.
- Then introduce one projection at a time.
- Only after the projection is verified should callers be migrated to consume it.

---

### Workstream F. Public API Hardening

#### Objective

Protect external integrations from internal refactor churn.

#### Scope

- `CharacterStudioAPI`
- public events
- animation provider registration surface

#### Tasks

1. Inventory all currently public members that external mods may use.
2. Classify them as:
   - stable public API
   - provisional/experimental API
   - accidental exposure/internal leakage
3. Introduce facades/wrappers if internals are currently exposed too directly.
4. Clarify event semantics:
   - when fired
   - ordering
   - repeatability
   - expected consumer assumptions
5. Add documentation comments where absent.

#### Deliverables

- Public API inventory
- Stable API surface definition
- Reduced direct dependence on mutable internals
- API compatibility checks or documented consumer contract examples for critical entry points

#### Success Criteria

- Internal refactors in core/rendering can proceed with lower compatibility risk.
- External consumers have a smaller, clearer surface to rely on.

#### TDD Notes

- Treat public API behaviors as contract tests wherever feasible.
- Before narrowing or wrapping a public surface, define the contract explicitly:
  - inputs,
  - outputs,
  - event timing,
  - compatibility expectations.

---

## 5. Regression Protection Plan

This work must not proceed without verification.

Verification must be planned before implementation, not after it.

### 5.1 Verification Layers

#### Layer 1: Diagnostics

- Run `lsp_diagnostics` on every changed file after each logical unit.

#### Layer 2: Characterization and seam tests

- Add or update tests for extracted seam classes before migrating broad call paths.
- Prefer deterministic unit tests for session objects, command layers, evaluators, and projections.

#### Layer 3: Scenario validation

Validate against representative scenarios captured in Workstream A.

#### Layer 4: Export/output verification

For export-related work, compare structured output against expected shape.

#### Layer 5: Rendering reasoning verification

When rendering logic changes, verify that rule output explains the observed behavior.

### 5.2 Minimum Safety Assets to Introduce

If feasible in this repo, introduce one or more of:

- lightweight scenario fixtures
- export snapshots
- normalized rule dumps for rendering decisions
- validator checks for core invariants
- command/use-case tests for extracted editor actions
- contract tests or compatibility examples for public API surfaces

### 5.3 Verification Rule

No phase is complete without evidence that:

- diagnostics are clean on changed files
- new or updated tests for the touched seam pass
- intended user-facing behavior is preserved in the targeted scenarios
- no unrelated architectural sprawl was introduced

### 5.4 TDD Decision Matrix

Use this rule when deciding what kind of test/check to add first:

| Change Type | First Safety Asset | Secondary Check |
|---|---|---|
| Extracting pure/session logic | Unit test | Scenario check |
| Extracting orchestration/use-case logic | Command/use-case test | UI scenario check |
| Extracting rendering rule logic | Rule snapshot / normalized output test | In-game preview/runtime scenario |
| Introducing export projection | DTO/output snapshot | Export scenario validation |
| Hardening public API | Contract test / compatibility example | Integration scenario |

---

## 6. Execution Phases

### Phase 0. Discovery and Safeguards

Goal:

- establish concrete inventories and representative scenarios

Completion conditions:

- entry points mapped
- rendering decision points mapped
- public API mapped
- representative verification scenarios documented
- first-pass testability map documented
- first batch of characterization targets selected

### Phase 1. Editor Session Extraction

Goal:

- centralize skin editor state

Completion conditions:

- session object introduced
- major state clusters migrated
- UI no longer owns most core edit state directly
- characterization/tests exist for the migrated state clusters

### Phase 2. Editor Action Extraction

Goal:

- move save/apply/preview/import orchestration out of UI handlers

Completion conditions:

- command/use-case layer introduced
- main editor flows routed through it
- extracted commands have focused seam tests

### Phase 3. Rendering Evaluation Extraction

Goal:

- separate render decisions from patch application

Completion conditions:

- render context/evaluator introduced
- patch files narrowed to bridge/apply behavior
- normalized render-rule verification assets exist for extracted logic

### Phase 4. Model Boundary Cleanup

Goal:

- separate editor/runtime/export state responsibilities

Completion conditions:

- explicit projections/adapters in place
- exporter and renderer no longer depend on editor scratch state
- projection behavior is covered by targeted assertions/tests

### Phase 5. API Hardening and Documentation

Goal:

- protect external integrations

Completion conditions:

- public API inventory completed
- accidental exposure reduced
- semantics documented
- compatibility expectations are captured as tests or explicit contract examples

### Phase 6. Performance Structure and Follow-through

Goal:

- formalize caches/metrics after boundaries are clearer

Completion conditions:

- cache points identified and implemented carefully
- performance metrics made observable where useful
- no performance change is merged without preserving the safety assets from earlier phases

### 6.1 Phase Exit Template

Every phase exit report should answer:

1. What seam was introduced or clarified?
2. What tests/checks were added first?
3. What call sites were migrated?
4. What scenarios were rerun?
5. What remains intentionally deferred?

---

## 7. Constraints During Execution

These rules are binding while implementing this plan.

1. No broad rewrites.
2. No mixing feature work into stabilization phases unless explicitly requested.
3. No type-safety suppression.
4. No refactor without a clear before/after responsibility change.
5. No rendering changes without preserving explainability.
6. No export changes without output verification.
7. If impact analysis on a symbol returns HIGH or CRITICAL, warn before editing.
8. No batch migration without an explicit passing verification step in between.
9. No commit that mixes seam introduction and broad downstream adoption unless the scope is provably tiny.

---

## 8. GitNexus Usage Requirements During Execution

Before editing any function/class/method symbol:

1. Run impact analysis upstream on the target symbol.
2. Record blast radius and note risk level.
3. If risk is HIGH/CRITICAL, explicitly acknowledge and narrow the change.

During refactors:

- use context analysis for target symbols
- use detect_changes after meaningful batches

Before any future commit related to this plan:

- run change detection to verify affected scope is expected

---

## 9. Atomic Commit Strategy

All execution under this plan must use small, reviewable, single-reason commits.

### 9.1 Commit Principles

Each commit should ideally do exactly one of the following:

1. add a failing or characterization safety asset,
2. introduce a new seam without broad adoption,
3. migrate one narrow caller/path to the new seam,
4. remove obsolete code after migration is proven safe,
5. add documentation/contract clarification tied to a completed structural change.

Do not combine multiple categories unless the slice is too small to justify separation.

### 9.2 Preferred Commit Pattern Per Slice

For medium-risk refactors, prefer this commit sequence:

1. **Baseline commit**
   - add characterization test, fixture, snapshot, or scenario note
2. **Seam-introduction commit**
   - add session/evaluator/use-case/projection class with minimal integration
3. **Single-path migration commit**
   - route one path/caller through the seam
4. **Cleanup commit**
   - remove dead fields/branches/helpers once proven unnecessary

For very small slices, steps 2 and 3 may be combined.

### 9.3 Commit Message Shape

Use commit messages that describe why the slice exists, not just what moved.

Recommended formats:

- `test: characterize skin editor dirty-state behavior before extraction`
- `refactor: introduce SkinEditorSession for isolated editor state ownership`
- `refactor: route preview refresh through SkinEditorUseCases`
- `test: normalize render hiding results for evaluator extraction`
- `refactor: add export projection for cosmetic-pack output boundary`
- `docs: define CharacterStudioAPI event contract after surface hardening`

### 9.4 Commit Rejection Rules

Reject a commit if it:

- changes multiple workstreams at once without a compelling seam,
- adds a new abstraction and migrates many callers in the same batch,
- changes behavior without first preserving or defining expected behavior,
- cannot be explained as a single reviewable reason.

---

## 10. How to Execute This Plan Incrementally

Execution should proceed as a sequence of small, reviewable slices.

Recommended slice pattern:

1. Choose one narrow target.
2. Map exact affected symbols/files.
3. Run GitNexus impact on edited symbols.
4. Add the smallest failing test, characterization check, or scenario artifact.
5. Implement minimal structural extraction.
6. Run diagnostics.
7. Run the new seam-focused verification.
8. Validate against one or more representative scenarios.
9. Commit the slice atomically.
10. Document what changed in the plan or follow-up notes.

Good first slices:

1. Characterize dirty/selection behavior, then extract the first state cluster into `SkinEditorSession`.
2. Characterize preview refresh, then route it through a named use-case.
3. Capture normalized node-hiding outputs, then extract node hiding rules into a render evaluator.
4. Snapshot one export path, then introduce an export projection for that path.

### 10.1 Ultrawork Work Packet Template

Every execution packet under this plan should be written in this format:

1. **Objective**
2. **Exact files/symbols in scope**
3. **Risk level from impact analysis**
4. **First failing test or characterization artifact**
5. **Minimal implementation step**
6. **Verification to rerun**
7. **Expected commit boundary**
8. **Explicit out-of-scope items**

---

## 11. Stop Conditions / Escalation Rules

Pause and reassess if any of the following happen:

1. A change requires touching both UI orchestration and rendering internals at once without a clear seam.
2. Preview behavior and runtime behavior diverge after extraction.
3. Core models appear to be serving conflicting responsibilities that cannot be separated incrementally.
4. Public API consumers depend directly on mutable internal types that cannot be stabilized cheaply.

If repeated implementation attempts fail:

- stop further edits
- document attempted approaches
- consult Oracle before continuing

---

## 12. Definition of Success

This plan is successful when the codebase reaches all of the following:

1. Editor state is centrally inspectable.
2. Main editor actions are routed through explicit orchestration.
3. Rendering decisions are explainable outside patch code.
4. Export/runtime/editor state responsibilities are more clearly separated.
5. Public API is smaller, clearer, and less coupled to internals.
6. Refactoring can proceed with lower regression risk because validation assets and diagnostics exist.
7. The implementation history reflects small atomic commits with clear intent boundaries.

---

## 13. Immediate Next Execution Step

When beginning actual implementation, start with:

1. discovery of concrete editor-state fields and main action entry points
2. GitNexus impact/context analysis for the first symbol batch
3. creation of the first characterization assets for dirty/selection state and preview refresh
4. introduction of `SkinEditorSession` with the smallest viable state migration

Do not start from rendering first unless discovery shows the editor state is already reasonably isolated.

---

## 14. Phase 0 Baseline Inventory (Captured)

This section records the concrete seams already verified directly from the codebase. It is the working baseline for the first implementation slices.

### 14.1 Editor Open Entry Points

Confirmed entry points:

1. `Source/CharacterStudio/UI/MainTabWindow_CharacterStudio.cs`
   - `PreOpen()` -> `Find.WindowStack.Add(new Dialog_SkinEditor())`
   - `DoWindowContents(...)` editor button -> `Find.WindowStack.Add(new Dialog_SkinEditor())`
2. `Source/CharacterStudio/UI/Gizmo_ChangeAppearance.cs`
   - right-click menu option "Open Editor" -> `Find.WindowStack.Add(new Dialog_SkinEditor(pawn))`

Implication:

- There are at least two user-facing editor entry contexts:
  - generic editor launch
  - pawn-targeted editor launch

These must remain behavior-compatible during session extraction.

### 14.2 Preview Refresh Chain

Confirmed symbols:

1. `Source/CharacterStudio/UI/Dialog_SkinEditor.PreviewLifecycle.cs`
   - `EnsureMannequinReady()`
   - `InitializeMannequin()`
   - `RefreshPreview()`
2. `RefreshPreview()` currently performs all of the following:
   - builds a preview application plan via `BuildApplicationPlan(null, true, "EditorPreview")`
   - applies that plan to `mannequin.ApplyPlan(previewPlan)`
   - rebuilds render tree via `Patch_PawnRenderTree.ForceRebuildRenderTree(previewPawn)`
   - synchronizes preview override state into `CompPawnSkin`
   - requests render refresh via `skinComp.RequestRenderRefresh()`
3. `Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs`
   - multiple preview override setters call `RefreshPreview()` directly

Implication:

- `RefreshPreview()` is already a high-value extraction seam because it mixes orchestration, preview-plan execution, render refresh, and preview-state synchronization.

### 14.3 Save / Apply Chain

Confirmed symbols:

1. `Source/CharacterStudio/UI/Dialog_SkinEditor.SaveApply.cs`
   - `OnSaveSkin()`
   - `OnApplyToTargetPawn()`
   - `OnApplySkinToTargetPawn()`
2. `OnSaveSkin()` currently performs all of the following:
   - validates `workingSkin`
   - normalizes identity fields
   - constructs a runtime skin via `BuildRuntimeSkinForExecution()`
   - saves XML through `SkinSaver.SaveSkinDef(...)`
   - registers the runtime skin through `PawnSkinDefRegistry.RegisterOrReplace(...)`
   - may auto-apply to `targetPawn` via `CharacterApplicationExecutor.Execute(applyPlan)`
   - resets dirty state (`isDirty = false`)
3. `OnApplySkinToTargetPawn()` currently performs all of the following:
   - builds an application plan via `BuildApplicationPlan(...)`
   - mutates runtime skin data before execution
   - executes through `CharacterApplicationExecutor.Execute(applyPlan)`
   - refreshes render tree and updates status/messages

Implication:

- Save/apply are prime use-case extraction targets after session extraction because they already behave like orchestration commands embedded in UI.

### 14.4 Export Chain

Confirmed symbols:

1. `Source/CharacterStudio/UI/Dialog_SkinEditor.Workflows.cs`
   - `OnExportMod()`
2. `OnExportMod()` currently:
   - validates whether there are layers/base slots
   - warns for missing textures
   - calls `SyncAbilitiesToSkin()`
   - opens `new Dialog_ExportMod(workingSkin, workingAbilities)`
3. `Source/CharacterStudio/UI/Dialog_ExportMod.cs`
   - export UI collects config and eventually triggers `OnExport()`
4. `Source/CharacterStudio/Exporter/ModBuilder.cs`
   - `Export(ModExportConfig config)` -> `ExecuteExportPipeline(...)`
   - pipeline includes rights validation, directory creation, optional texture copy, definition generation, and manifest generation
5. `Source/CharacterStudio/Exporter/SkinSaver.cs`
   - `SaveSkinDef(PawnSkinDef skinDef, string filePath)` handles XML save for config skin storage

Implication:

- Export path is already split into UI dialog + exporter pipeline. This likely needs less early restructuring than preview/save/apply.

### 14.5 Runtime Registration / Application Chain

Confirmed symbols:

1. `Source/CharacterStudio/Core/PawnSkinDefRegistry.cs`
   - `RegisterOrReplace(PawnSkinDef? def)`
   - `LoadFromConfig()`
2. `RegisterOrReplace(...)`:
   - ensures identity
   - loads config defs lazily if needed
   - overwrites existing runtime def or adds new one to `DefDatabase<PawnSkinDef>`
   - fires `RuntimeSkinRegisteredGlobal`
3. `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`
   - `ApplySkinToPawn(...)`
   - `ClearSkinFromPawn(...)`
4. `ApplySkinToPawn(...)` currently:
   - ensures `CompPawnSkin`
   - prepares runtime skin
   - prewarms external assets
   - writes active skin through component API
   - clears rendering caches
   - calls `Patch_PawnRenderTree.RefreshHiddenNodes(...)`
   - calls `Patch_PawnRenderTree.ForceRebuildRenderTree(...)`
   - syncs abilities and attribute buffs

Implication:

- Runtime application already mixes domain preparation, asset prewarming, patch refresh, and runtime side effects. This will matter later when separating runtime projections from UI/editor state.

### 14.6 Rendering Decision Baseline

Confirmed symbols:

1. `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs`
   - `Apply(...)`
   - `GetFinalizedMaterial_Postfix(...)`
   - `TrySetupGraphIfNeeded_Postfix(...)`
2. `TrySetupGraphIfNeeded_Postfix(...)` currently mixes:
   - active skin resolution (gene skin vs comp skin)
   - render-fix applicability check
   - duplicate custom-node prevention
   - vanilla hiding decision
   - custom layer injection trigger
3. `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.Injection.cs`
   - `RefreshHiddenNodes(Pawn pawn)`
   - `ForceRebuildRenderTree(Pawn pawn)`
   - `InjectCustomLayers(...)`
   - `HasPotentialInjectedOverrides(...)`
4. `Source/CharacterStudio/Rendering/Patch_PawnRenderTree.Hiding.cs`
   - `HideNode(...)`
   - `HideVanillaBodyFallback(...)`
   - `HideVanillaNodesByImportedTexPaths(...)`
   - hidden-node state is tracked through conditional weak tables

Implication:

- Rendering rules are currently spread across patch entrypoints and helper files, but the main seam is clear:
  - collect active skin/runtime conditions
  - decide hide/inject behavior
  - mutate render tree / graphics cache

This validates `RenderContext` + `RenderRuleEvaluator` as the correct extraction direction.

### 14.7 Public API Baseline

Confirmed symbol:

1. `Source/CharacterStudio/Core/CharacterStudioAPI.cs`

Currently exposed stable-looking surface includes:

- query methods:
  - `IsCustomized(...)`
  - `GetActiveSkin(...)`
  - `GetExplicitAbilityLoadout(...)`
  - `GetEffectiveAbilityLoadout(...)`
  - `GetGrantedAbilityNames(...)`
- mutation/registration methods:
  - `RegisterOrReplaceSkin(...)`
  - `GrantAbilities(...)`
  - `GrantEffectiveLoadout(...)`
  - `RevokeGrantedAbilities(...)`
  - `RegisterLayerAnimationProvider(...)`
  - `UnregisterLayerAnimationProvider(...)`
- global events:
  - `SkinChangedGlobal`
  - `AbilitiesGrantedGlobal`
  - `AbilitiesRevokedGlobal`
  - `RuntimeSkinRegisteredGlobal`

Implication:

- API hardening later must preserve this file as the main facade layer. Internal refactors should prefer adapting beneath this surface rather than widening it.

### 14.8 First Implementation Slice Decision

Based on the captured baseline, the correct first implementation slice is:

1. introduce a minimal `SkinEditorSession`
2. migrate the smallest state cluster first
3. recommended first cluster: dirty state + selection state

Reason:

- those fields are visibly concentrated in `Dialog_SkinEditor.cs`
- they are simpler than preview orchestration
- they create the seam needed before extracting preview/save/apply commands

### 14.9 Baseline QA Criteria for Phase 1

Before and after the first session extraction slice, verify at minimum:

1. Editor can still open from both:
   - main tab launcher
   - pawn gizmo context
2. Creating a new skin still:
   - resets editor state
   - updates preview
3. Saving a skin still:
   - writes XML under config skin path
   - clears dirty state on success
4. Applying to target pawn still:
   - updates appearance
   - triggers render refresh path
5. Export dialog still opens from editor workflow

Where feasible, preserve these through characterization checks before moving to deeper refactors.

### 14.10 Representative Scenario Characterization Checklist

The following scenarios are the minimum regression checklist for the stabilization work. They are intentionally phrased as concrete pass/fail checks.

#### Scenario A. Simple Skin Editing

Goal:

- confirm that the editor still supports a basic single-skin workflow after state/session extraction.

QA steps:

1. Open the editor from the main tab.
2. Create a new standard skin.
3. Add one layer.
4. Change one visible property that marks the editor dirty.
5. Save the skin.

Pass conditions:

- editor opens successfully
- one layer can be added
- dirty indicator changes after modification
- save completes without error
- saved XML exists under `Config/CharacterStudio/Skins`
- dirty indicator clears after successful save

#### Scenario B. Multi-Layer Selection and Ordering

Goal:

- protect the first extracted state cluster: dirty flag + selection state.

QA steps:

1. Open a skin with multiple layers or create several layers.
2. Select a single layer.
3. Use Ctrl/Shift selection to build multi-selection.
4. Reorder or duplicate a selected layer.
5. Delete one or more selected layers.

Pass conditions:

- primary selection remains valid
- multi-selection set behaves consistently
- reorder/duplicate/delete operations keep selection in a sane state
- dirty indicator remains correct throughout

#### Scenario C. Face / Preview Synchronization

Goal:

- ensure preview logic still responds correctly after future use-case extraction.

QA steps:

1. Open a skin with face configuration or enable face-related preview controls.
2. Toggle one preview override (expression, mouth, brow, lid, emotion, or eye direction).
3. Trigger preview refresh.
4. Disable the override again.

Pass conditions:

- preview refresh executes without errors
- preview override visibly applies
- clearing the override returns preview to the expected state

#### Scenario D. Weapon / Equipment Visual Path

Goal:

- protect runtime/render integration paths that depend on skin application.

QA steps:

1. Open a pawn-targeted editor session.
2. Modify a weapon or equipment-related visual setting if present.
3. Apply the skin to the target pawn.

Pass conditions:

- apply action completes without error
- target pawn receives the updated appearance state
- render refresh path executes successfully

#### Scenario E. Export Flow

Goal:

- preserve the editor-to-export handoff and exporter pipeline.

QA steps:

1. Open export from the editor.
2. Confirm the export dialog opens with populated defaults.
3. Execute export with valid output path and rights confirmations.

Pass conditions:

- export dialog opens successfully
- export config fields are populated
- export completes without pipeline failure
- output mod directory is created with expected basic structure

#### Scenario F. Pawn Gizmo Entry Path

Goal:

- ensure alternate editor entry survives session extraction.

QA steps:

1. Open the editor from the pawn gizmo context menu.
2. Verify the target pawn context is present.
3. Attempt apply-to-target.

Pass conditions:

- pawn-context editor opens successfully
- target pawn-dependent actions remain enabled/valid
- apply-to-target path completes without regression

### 14.11 Current Verification Notes for Slice 1

For the first `SkinEditorSession` extraction slice, the available evidence is:

1. Code seam introduced without downstream behavioral rewrite:
   - new file: `Source/CharacterStudio/UI/SkinEditorSession.cs`
   - state routing added in `Source/CharacterStudio/UI/Dialog_SkinEditor.cs`
2. Build verification passed:
   - `dotnet build "Source/CharacterStudio/CharacterStudio.csproj"`
   - result: success, 0 warnings, 0 errors
3. Deploy-script verification passed:
   - `powershell -ExecutionPolicy Bypass -File "build_and_deploy.ps1"`
   - result: build succeeded, DLL copied to 1.5/1.6 assemblies, mod deployed to RimWorld Mods folder
4. Environment limitation:
   - `lsp_diagnostics` for `.cs` is currently blocked because `csharp-ls` is not installed in this environment
5. GitNexus limitation observed during this slice:
   - `gitnexus status` works
   - `gitnexus impact/context` intermittently fail due local index lock contention, so blast radius evidence for this slice is partially blocked by tool concurrency state
