# Combat v3 implementation ledger

Audit baseline: HEAD `07472f3e71f68867baa4acca6c9d4b8660a236fd`; Unity `6000.6.6f1`; 2D Animation `16.0.1`; PSD Importer `15.0.1`. The audited working tree was already dirty, so existing scene, script, log, and user-setting changes must be preserved and excluded from unrelated commits.

| Gate | Status | Audit evidence / next verification |
| --- | --- | --- |
| Audit | VERIFIED | Repository, HEAD, dirty working tree, Unity/package versions, build settings, validators, logs, and available tests inspected without launching Unity. |
| P0 | IN PROGRESS | Historical `Editor-prev.log` contains `SWORD_IK_REPAIR_OK` and `SWORD_IK_VALIDATION_OK`; this is pending fresh compile and `SwordIKUpgradeValidation.Validate` verification on the feature branch. Preserve the PSB rig and repaired chains. |
| P1 | NOT STARTED | Runtime verification pending: coincident targets within 0.01, reach clamp, calibrated monotonic turn response, 180-degree sign stability, dead zone, acceleration, and frame-rate independence. |
| P2 | NOT STARTED | Runtime verification pending: independent hilt/blade, scale-based ignition, collision gating, drop/retrieve, owner exclusions, and installer idempotency. |
| P3 | NOT STARTED | Runtime verification pending: windup, forward spring, alternating committed swings, immutable attack direction, input priority, and recovery/spam behavior. |
| P4 | NOT STARTED | Runtime verification pending: combo timing/reset rules, 120/155/190/225/260/280 ms windows, rearm, real blade contact/elevation, inactive/static-guard rejection, and contact deduplication. |
| P5 | NOT STARTED | Runtime verification pending: weak recoil, physical guard break from collision impulse, IK/physics authority handoff, repeated stability, and smooth shared-grip reacquisition. |
| P6 | NOT STARTED | No project NUnit/EditMode/PlayMode tests or duel scene were found during audit. Add targeted test assemblies and a two-combatant physics scenario, then run compile, both validators, EditMode, PlayMode, and scene/build validation. |

Validation commands use `C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe` with `-batchmode -nographics -quit -projectPath D:\Library\Development\2D-Sword-IK-Prototype`. Run `SwordIKUpgradeValidation.Validate` and `SwordIKCombatValidation.ValidateBatch` serially while the interactive editor is closed. Once test assemblies exist, run targeted `-runTests -testPlatform EditMode` and `-runTests -testPlatform PlayMode` commands with results and logs under `Temp/`.

Historical validator success is evidence only; it does not establish current compilation, Play Mode behavior, collision physics, a two-combatant scenario, or final acceptance.
