# Deeper game analysis

Inspected 2026-10-12. This extends [the installation baseline](installation-analysis.md). Game files and user saves were read without modification. No game process, debugger, injected DLL, or mod loader was started.

## What is now established

- The serialized assets are readable even though the IL2CPP metadata is protected-looking.
- The main archive contains 30 internal files, representing 4,509,307,744 uncompressed bytes in 34,404 blocks.
- A bounded scan inspected 16 serialized files and 26,292 objects. It recovered 2,684 MonoScript records representing 2,627 distinct assembly/namespace/class combinations.
- 444 distinct classes belong to Assembly-CSharp with an empty namespace. This is a useful game-code candidate list, not a guarantee that all 444 are original gameplay code. See [the class index](game-class-index.csv).
- All 10 localization bundles decoded. There are 11 table collections, 539 shared keys, and 538 nonempty English entries.
- Existing user data uses readable JSON for saves and settings.
- Debug console GameObjects are saved as inactive in both inspected scenes that contain them.

## Scene structure

The BuildSettings object gives the actual scene list in this order:

| Build index | Archive file | Scene |
| --- | --- | --- |
| 0 | level0 | Assets/Scenes/Bootstrap.unity |
| 1 | level1 | Assets/Scenes/MainMenu.unity |
| 2 | level2 | Assets/Scenes/Core.unity |
| 3 | level3 | Assets/Scenes/BlankSystems.unity |
| 4 | level4 | Assets/Scenes/GameWorld.unity |
| 5 | level5 | Assets/Scenes/Interiors.unity |
| 6 | level6 | Assets/Scenes/Interor_HUB02.unity |

The `Interor_HUB02` spelling is preserved from the build. Scene order does not establish the runtime load sequence. `Bootstrap`, `SceneLoadingSystem`, and `SubSceneLoader` are useful candidates for researching that sequence.

Core contains GameObjects named GameManager, SaveManager, SettingsMaster, CheckpointMaster, DebugMaster, FontLocalizationManager, and Rewired Input Manager. BlankSystems contains vehicle/player components, UI, missions/navigation-related objects, and world-system managers. Its name should not be interpreted as an empty scene.

The initial bounded object scan deliberately skips `level4` (194,512,580 bytes) and `sharedassets4.assets` (670,150,576 bytes). It also skips bulk resource streams. Their archive offsets and sizes are recorded, but this is not a complete world or terrain analysis.

## Gameplay architecture and likely investigation points

These names come from serialized MonoScript records. They identify classes, not verified public APIs or hook signatures.

| Area | Observed class candidates |
| --- | --- |
| Vehicle physics | CarController, Engine, Gearbox, Fuel, Brakes, Steering, Suspension, TerrainWheelFriction, WheelInflation |
| Damage / repair | Damageable, Repairable, ImpactDamageHandler, TruckComponentSO, RepairKitUsable |
| Upgrades / customization | UpgradeManager, UpgradeSO, UpgradeData, CustomizationMaster, CustomizationSO |
| Player / interaction | PlayerCore, PlayerController, FPSCharacterController, PlayerVehicleConnection, PlayerInteraction, Pickupable |
| Cargo / supplies | TruckBed, CargoSpawner, CargoRopeWrapper, ItemStash, ItemUsable, FuelCanInteractable |
| Missions / progression | MissionManager, Mission, MissionData, MissionLogicSO, MissionCompleteManager, ArtifactData, AnomalyData |
| Navigation | MapController, NavMast, WaystoneManager, Waystone, RadiationDetector, RadarSweep |
| Threats / world | StalkerAI, StalkerDirector, ShamblerAI, ShadowPerson, DreamCorruptionZone, ThreatLevel, TimeManager, WeatherForecast |
| Save / configuration | SaveManager, SaveData, PlayerSaveData, PlayerSaveSO, JsonPrefs, SettingsMaster |
| Debug / diagnostics | ConsoleCommands, DebugMaster, DebugGameLoader, DebugInitializer, FreeCam, DebugCamera |

Selected components were tied to their MonoScript references through the serialized file's external-reference table:

| Scene file | Object path ID | Class | GameObject |
| --- | --- | --- | --- |
| level2 | 252 | SaveManager | SaveManager |
| level2 | 254 | SettingsMaster | SettingsMaster |
| level3 | 10436 | Gearbox | Car |
| level3 | 10437 | CarController | Car |
| level3 | 10440 | Fuel | Car |
| level3 | 10441 | Engine | Car |
| level3 | 10611 | PlayerCore | PlayerCORE |
| level3 | 11574 | PlayerController | ---PLAYER--- |
| level3 | 11576 | UpgradeManager | ---PLAYER--- |

Path IDs are serialized asset identifiers for this build, not native memory addresses. Most selected custom components expose only their base MonoBehaviour fields to the available reader. For example, Engine's object contains 452 bytes while the available type tree reads only 32 bytes. This means field layouts and numeric tuning values have not been recovered; guessing float offsets would be premature.

## Mechanics evidenced by localization

The bundled manual and UI text describe:

- An expedition loop involving truck travel, on-foot exploration, recovering cargo/artifacts, and returning them to outposts for XP and SP.
- Automatic, low, and reverse gearbox modes; selectable AWD; fuel usage; collision and environmental damage; maintenance and consumable repair kits.
- Cargo fastening when entering the truck, with extreme bed tilt called out as a hazard.
- A deployable winch anchor and a winch upgrade described as extending the cable to 16 meters.
- Navigation based on nearby radio masts, deployable waystones/NavSpires, an RF scanner, and anomaly photography.
- Missions categorized as delivery, pickup/delivery, search/delivery, photography, and construction.
- Upgrades named AccelerationAssist, DiffLock, EngineUpgrade, FuelUpgrade, Lightbar, Receiver, Roofrack, Suspension, UVLights, and WinchUpgrade, plus a DummyUpgrade placeholder.
- Separate settings for camera sway, head tilt, screen noise, fisheye, vignette, vertical FOV, compression effect, rendering scale, terrain, grass, reflections, draw distance, volumetric fog, and ambient occlusion.

Text can be stale, unused, or apply beyond the demo. These are descriptions of bundled content, not runtime verification of every feature. In particular, UI descriptions do not establish actual torque, capacity, price, or damage formulas.

## Localization findings

| Collection | Shared keys | Nonempty English entries |
| --- | ---: | ---: |
| Cargo | 14 | 14 |
| Codex | 70 | 69 |
| Dialogue | 42 | 42 |
| HUD | 16 | 16 |
| InputPrompts | 45 | 45 |
| Manual | 30 | 30 |
| Menus | 185 | 185 |
| Missions | 42 | 42 |
| Prompts | 14 | 14 |
| Settings | 59 | 59 |
| Tutorial | 22 | 22 |

`anomaly.treehead.text` is missing in seven language tables and present but empty in Portuguese. No other missing IDs, unknown IDs, or blank values were found in the decoded table entries. This checks structural coverage only; it does not evaluate translation quality or whether all text appears in play.

There are eight ordinary locale assets plus a pseudo-locale used for localization testing. Successful decoding confirms that these localization bundles are accessible without decoding global-metadata.dat. Changing them would still require preserving IDs, dependencies, serialization, and any catalog integrity information; no asset write test was performed.

## Native code and metadata

GameAssembly.dll is an AMD64 PE with 386 exports, including 241 names beginning with `il2cpp_`. Exported functions include:

- il2cpp_domain_get / il2cpp_domain_get_assemblies
- il2cpp_assembly_get_image
- il2cpp_class_from_name
- il2cpp_runtime_invoke
- il2cpp_thread_attach

These are promising runtime inspection entry points, but exports alone do not prove that a particular loader or generated binding will work. `mono_`-prefixed compatibility exports are also present; the build still uses IL2CPP.

The metadata starts with `GMEV` and has measured byte entropy of 7.99998886 bits/byte. GameAssembly contains source-path strings under `library/com.gups.obfuscator/il2cpp/libil2cpp`. Together these are strong evidence of a modified runtime and protected metadata. High entropy alone cannot distinguish encryption from compression, and the exact format, cipher, and decoding path remain unverified.

GuardingPear's own [metadata-protection documentation](https://www.guardingpearsoftware.com/product/obfuscator/feature/global-metadata-encryption) describes encryption of this file. That supports the interpretation, but does not independently prove which settings this game used. Existing log stacks retain class names while showing short method names such as SaveManager:bma(), which provides additional evidence of method-name obfuscation.

Do not assume that changing the metadata magic or renaming files will make ordinary IL2CPP tools compatible.

## Debug console

The serialized DebugLogManager component is enabled, but its `IngameDebugConsole` GameObject has `m_IsActive = false` in both Core and BlankSystems:

- level2, component path ID 189.
- level3, component path ID 9339.

DebugMaster GameObjects are saved active. This does not establish whether they activate the console at runtime. No console key, command syntax, or availability has been verified. The custom console fields could not be decoded with the available type tree.

## Existing save and settings data

A real user-data directory was found at:

`%USERPROFILE%\AppData\LocalLow\CodeWakers\Project Prospice`

It contains `demo.json`, `settings.json`, `Player.log`, `Player-prev.log`, and a Photos directory with JPEG files and JSON sidecars. Only schema and relevant diagnostics are documented here; user progress and pictures are not copied into Git.

`demo.json` parses as ordinary JSON with these major sections:

| Section | Observed structure |
| --- | --- |
| version | Integer format/version field |
| playerSaveData | Spawn, XP, level, money, time, tutorial flags, cosmetics, world seed, and progression counters |
| sessionStash / saveStash | Navigation consumables, small/large repair boxes, and fuel |
| missions | ID-keyed objects containing uid and missionStatus |
| artifacts / anomalies | ID-keyed objects containing uid and collected |
| upgrades | ID-keyed objects containing uid, purchased, and equipped |
| parts | ID-keyed objects containing numeric hp |
| waystones | Array of numeric x/y/z coordinates |

Fields named `salt`, `ash`, and `theta` also exist in playerSaveData. Their role is unknown. Plain JSON readability does not prove that edits will be accepted or that there are no consistency/integrity checks. Mission-status integer meanings, valid ranges, migration rules, and cross-field constraints remain unverified.

`settings.json` has direct settings keys such as fpFOV, CameraSway, HeadTilt, Fisheye, Vignette, Framelimit, RenderingScale, and AA. It also stores Rewired configuration as serialized strings. Numeric option values must not be assumed to be physical values or enum labels without checking the corresponding Settings classes.

This is the clearest starting point for a read-only save inspector, backup tool, or settings comparison tool. Any later save editor should first establish constraints, create backups, preserve unknown fields, and avoid writes while the game may save over the same file.

## Baseline diagnostics

The existing Player.log was last modified on 2026-10-12 at 00:12 Asia/Dubai. It records Unity 2022.3.62f2 and Direct3D 11. No fresh session was started for this analysis.

The log contains 17 NullReferenceException entries:

- 2 with the first stack frame IndoorsTrigger.OnEnable.
- 15 with the first stack frame GameManualButton.OnEnable.

These are historical observations, not reproduction results or diagnosed causes. Record them as pre-existing issues when comparing future modded sessions. PlayerSettings reports bundleVersion 0.1.0 and usePlayerLog enabled; the bundle version may be a generic build setting rather than a public demo revision.

## Recommended sequence for mod development

1. Choose one concrete feature. A save/settings inspector or localization investigation has the fewest currently demonstrated dependencies.
2. For gameplay changes, investigate the named component and obtain verified field/method information through a compatible IL2CPP workflow. Keep the protected metadata and obfuscated method names in scope.
3. Establish a minimal plugin load/unload test before adding behavior. Loader compatibility remains untested.
4. Validate against the baseline logs, then exercise saving/loading and relevant scene transitions.

## Reproducing the static inspection

The reader uses [UnityPy](https://github.com/K0lb3/UnityPy), pefile, and UnityPy's LZ4 dependency. Tool versions are pinned in `tools/requirements-analysis.txt`. The archive reader decompresses only blocks overlapping selected serialized files; it does not load all 4.51 GB into memory or rewrite the archive.

From PowerShell in `C:\git\prospicemod`:

```powershell
python -m pip install --target .\local\python-deps -r .\tools\requirements-analysis.txt
python -B .\tools\inspect_game.py --game-dir 'E:\SteamLibrary\steamapps\common\Prospice Demo' --output .\local\analysis
```

Generated JSON is ignored under `local/analysis/`. It includes native exports, metadata measurements, archive index, asset counts/names, script records, selected component observations, joined localization, and localization coverage. The user-data/log inspection was a separate read-only step and is not run automatically by this script.

Validation: the final reader completed successfully on this installation; all 16 selected serialized files were inventoried without object-inventory errors. Custom field-read limitations are explicitly recorded in components.json. Full GameWorld analysis, decryption, method recovery, loader installation, and runtime mod tests remain outstanding.
