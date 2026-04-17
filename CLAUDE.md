# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Character Studio is a RimWorld 1.6 mod for in-game character appearance editing. It provides a full pipeline: edit appearance layers, face animations, equipment, abilities → preview in real-time → export as a standalone mod. Written in C# targeting .NET Framework 4.7.2 with Harmony patching.

## Build & Deploy

```bash
# Build (outputs to 1.6/Assemblies/CharacterStudio.dll)
cd Source/CharacterStudio && dotnet build -c Release --nologo

# Quick deploy to RimWorld Mods folder (build + copy)
powershell -File build_and_deploy.ps1
# or
quick_deploy.bat

# RimWorld assembly path is resolved via RIMWORLD_PATH env var, defaulting to:
# D:\steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed
```

No test suite exists. Testing is done in-game.

## Architecture

**Entry point:** `ModEntryPoint.cs` — `[StaticConstructorOnStartup]` class that initializes Harmony patches and loads runtime definitions (`PawnSkinDefRegistry`, `CharacterSpawnProfileRegistry`, `CharacterRuntimeTriggerRegistry`). `CharacterStudioMod.cs` is the `Mod` subclass handling settings.

**Core modules under `Source/CharacterStudio/`:**

| Directory | Responsibility |
|-----------|---------------|
| `Core/` | Central data types (`PawnSkinDef`, `CompPawnSkin`, `PawnLayerConfig`, `PawnFaceConfig`, `CharacterDefinition`), runtime registries, `CharacterStudioAPI` (public facade for external mods), spawn utility, equipment defs |
| `Core/RuntimeFace/` | Runtime face animation subsystem |
| `Rendering/` | Harmony patches on RimWorld's `PawnRenderTree` and `PawnRenderer`; custom render node workers (`PawnRenderNodeWorker_CustomLayer`, `_EyeDirection`, `_FaceComponent`, `_WeaponCarryVisual`); `Graphic_Runtime` and `GraphicRuntimePool` |
| `UI/` | All dialog windows. `Dialog_SkinEditor` (partial classes split by concern: LayerTree, Properties, Preview, Face, Animation, etc.), `Dialog_AbilityEditor` (similarly split), `Dialog_ExportMod`, `MainTabWindow_CharacterStudio` |
| `Abilities/` | Modular ability system: `ModularAbilityDef`, `CompAbilityEffect_Modular`, effect workers, VFX player, hotkey runtime, time-stop controller |
| `Abilities/RuntimeComponents/` | Pluggable runtime component handlers for ability effects |
| `Exporter/` | Skin saving, XML export, mod builder (`ModBuilder`), ability/equipment/unit XML writers |
| `Patches/` | Additional Harmony patches (flight state, game component bootstrap, race label, attribute buff stats) |
| `AI/` | Custom AI behavior, LLM-based character generation |
| `Design/` | Character design document compilation and node rules |
| `Items/` | `CompSummonCharacter` for spawning characters via items |
| `Attributes/` | Character attribute buff definitions and service |
| `Introspection/` | Render tree parser for debugging/inspection |
| `Performance/` | Performance stats tracking |

**Key patterns:**
- **Partial classes for large dialogs:** `Dialog_SkinEditor` and `Dialog_AbilityEditor` are split across many files by feature area (e.g., `Dialog_SkinEditor.LayerTree.cs`, `Dialog_SkinEditor.Preview.cs`).
- **Harmony patching:** All RimWorld behavior modifications use Harmony. Each patch group has a static `Apply(Harmony)` method called from `ModEntryPoint.ApplyPatches()`. Harmony ID: `"CharacterStudio.Main"`.
- **Def registries:** Custom defs (`PawnSkinDef`, `CharacterSpawnProfileDef`, `CharacterRuntimeTriggerDef`) use config-file-backed registries loaded at startup.
- **CompPawnSkin partial classes:** The main pawn component is split by subsystem (Animation, FacePreview, FaceRuntime, FlightState, etc.).
- **Public API:** `CharacterStudioAPI` is the stable facade for external mod integration. Events use static C# events (`SkinChangedGlobal`, `AbilitiesGrantedGlobal`, etc.).

## File Layout

```
Source/CharacterStudio/    # All C# source code
1.6/Assemblies/            # Build output DLL
1.5/Assemblies/            # Legacy 1.5 DLL (copy of 1.6 build)
About/                     # RimWorld mod metadata (About.xml)
Defs/                      # RimWorld XML definitions
Languages/                 # ChineseSimplified + English translations
  └─ {lang}/Keyed/         # Translation keys (CS_Keys_*.xml)
docs/                      # Documentation and special character references
```

## Language & Conventions

- Code comments and XML translations are primarily in Chinese (Simplified) with English alongside
- Translation keys follow `CS_Keys_*.xml` pattern under `Languages/{lang}/Keyed/`
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`)
- Log messages use `[CharacterStudio]` prefix
