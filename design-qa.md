# Controller Lab — Design QA

## Ground truth and implementation

- Source of visual truth: `design-reference.png`
- Implementation screenshot: `implementation.png`
- Combined comparison input: `design-comparison.png`
- Functional live-state screenshot: `implementation-live.png`
- Comparison viewport: 1440 × 1024, demo input state, 100% application scale
- Live verification viewport: 1440 × 936, real XInput state, Windows display at 150% DPI
- Runtime: native C# WPF / .NET Framework, Per-Monitor V2 DPI awareness

## Comparison history

### Pass 1

- P1 layout: controller render was too small relative to the center stage. Increased the controller image slot and moved it slightly right to preserve the reference callout balance.
- P1 capture: the first external screenshot was clipped by 150% DPI virtualization. Replaced it with a DPI-aware `PrintWindow` capture and normalized the comparison to 1440 × 1024.
- P2 window chrome: a visible resize border created a white strip. Added custom WPF `WindowChrome` while retaining resize behavior.

### Pass 2

- P1 content: the right-stick leader line crossed its numeric readout. Shortened the line so it terminates before the text column.
- P1 typography: the trigger chart's vertical `Output` title collided with the 50% label. Moved it into the dedicated axis-title gutter.
- P2 proportions: trigger cards and calibration controls were shorter than the reference. Increased both fixed rows to match the source rhythm.
- P2 accessibility: deadzone controls were keyboard-operable but not exposed as sliders. Added named UI Automation slider peers with a real range-value pattern.

### Pass 3

- Full-page comparison: title bar, device card, center controller stage, paired trigger cards, dual stick plots, calibration controls, footer, borders, radii, and palette all preserve the selected design hierarchy.
- Focused controller region: high-resolution transparent controller asset is sharp at 150% DPI; no chroma fringe, clipping, or stretch distortion was found.
- Focused chart region: ring spacing, deadzone circles, vectors, dots, trails, axes, trigger grids, markers, meters, and labels remain legible without overlap.
- Responsive live state: at the primary monitor's 936-pixel working height, the window is fully on-screen and all controls remain visible and usable.

## Functional and accessibility verification

- Native build completed successfully with the system .NET Framework compiler.
- Real XInput launch detected `Connected · Player 1` through `xinput1_4` and reported a wired controller.
- Calibration interaction verified through UI Automation: `Start Calibration` → `Keep sticks centered…` → `Start Calibration` after the two-second sample and completion state.
- Left deadzone UI Automation range verified by changing the value from `0.08` to `0.12` and reading it back.
- Mouse dragging, keyboard arrows, Home/End, per-stick reset, global reset, close/minimize/maximize, automatic connection scanning, and 125 Hz timer paths are implemented.
- Text and status colors meet practical contrast on the dark surfaces; keyboard focus is visible on the custom deadzone controls.

## Final assessment

No unresolved P0, P1, or P2 findings. The implementation is visually faithful, functional, DPI-sharp, keyboard-usable, and stable in both the reference viewport and the user's live viewport.

previous result: passed

## Chinese Dynamic Feedback Update

### Evidence

- User-provided focused reference: `dynamic-feedback-reference.png`
- Source visual truth: `design-reference.png`
- Updated implementation screenshot: `implementation-cn.png`
- Full-view comparison input: `design-comparison-cn.png`
- Focused controller comparison input: `controller-focus-comparison-cn.png`
- Dynamic state evidence: `implementation-cn-amplitude-a.png`, `implementation-cn-amplitude-b.png`, and `dynamic-feedback-preview.gif`
- Comparison viewport: 1440 × 1024, demo input state, dark theme, 150% Windows DPI captured and normalized to 100%

### Findings and comparison history

- [P1] The first dynamic pass covered the stick-cap texture with a strong green/blue radial fill. This made the cap look painted instead of physically displaced. The fill was moved behind the real cropped stick-cap asset and replaced with a restrained outer light ring. Post-fix evidence in `implementation-cn-amplitude-a.png` and `implementation-cn-amplitude-b.png` shows the original cap texture remains visible.
- [P1] The initial stick travel was too subtle for the requested visual feedback. Travel was increased from 1.75% to 2.5% of the rendered controller width, with a small magnitude-based cap scale adjustment to keep the motion visually seated in the ring. The two amplitude captures show distinct stick positions while the numeric plots remain 1:1.
- [P2] The demo state only exercised A, X, and RB. It now cycles A, B, X, RB, D-pad Up, and left-stick press while driving both triggers, so all feedback families are visually testable.
- [P2] English copy remained in the title, device card, stick cards, calibration flow, chart axes, callouts, connection states, battery states, tooltips, automation names, and footer. All user-facing copy is now Chinese; Xbox, LT/RT, X/Y, XInput, and Hz remain unchanged because they are product or input-standard identifiers.

### Required fidelity surfaces

- Fonts and typography: switched UI and custom-drawn labels to Microsoft YaHei UI; Chinese headings, numeric columns, chart labels, and status text remain legible with no clipping or broken wrapping.
- Spacing and layout rhythm: full-view comparison preserves the original two-column hierarchy, device-card height, controller stage, paired trigger cards, dual stick plots, calibration controls, radii, and footer spacing.
- Colors and visual tokens: graphite surfaces, green left-side semantics, blue right-side semantics, muted labels, borders, and contrast remain aligned with the source. Dynamic overlays reuse those semantic colors.
- Image quality and asset fidelity: the high-resolution controller raster remains the visual base. Moving stick caps are cropped from the same source asset, retain texture, and show no chroma fringe or stretching at 150% DPI.
- Copy and content: terminology is coherent Chinese for an Xbox input monitor. Dynamic values and standard input abbreviations remain precise.
- Interaction states: real-time stick displacement, button press/release easing, directional D-pad feedback, shoulder feedback, trigger-strength feedback, calibration, deadzone range values, resets, connection, and disconnected states are implemented.
- Accessibility and responsiveness: deadzone controls retain Slider semantics and keyboard range control. Chinese calibration button states were verified through UI Automation. The 1440 × 936 live viewport remains fully visible on the primary monitor.

### Functional verification

- Native C# WPF build completed successfully.
- Deadzone range changed from 0.08 to 0.16 and read back through UI Automation, then reset to 0.08.
- Calibration completed the Chinese sequence `开始校准` → `请保持两个摇杆居中…` → `开始校准`.
- Two controller-region captures taken 1.6 seconds apart changed 13,931 of 130,500 pixels (10.68%), confirming visible dynamic feedback rather than text-only updates.
- Animated preview contains 34 captured frames.

No actionable P0, P1, or P2 findings remain. The intentional Chinese copy difference is the requested localization, and the larger stick travel is the requested behavior change.

previous result: passed

## D-pad Dynamic Feedback Update

### Evidence

- Focused source reference: `dpad-reference.png`
- Updated implementation screenshot: `implementation-dpad.png`
- Same-region comparison input: `dpad-focus-comparison.png`
- Multi-state contact sheet: `dpad-contact-sheet.png`
- Animated state preview: `dpad-dynamic-preview.gif`
- Viewport: 1440 × 1024 demo state, dark theme, 150% Windows DPI normalized to 100%

### Findings and fixes

- [P1] The previous implementation only rendered a direction marker while a direction was pressed, so the four-way feedback area was not discoverable at rest. Added four persistent low-luminance direction rings aligned to the physical D-pad segments.
- [P1] The demo only exercised D-pad Up, which did not prove left/right/down or diagonal combinations. Expanded the demo sequence to Up, Right, Down, Left, Up+Right, and Down+Left.
- [P2] Reusing the generic face-button glow made the D-pad feedback feel detached from the cross shape. Added a dedicated D-pad renderer with smaller segment-aligned rings, press-depth movement, restrained resting opacity, and stronger active fill.

### Post-fix verification

- `dpad-focus-comparison.png` confirms the four markers align with the physical D-pad positions in the supplied reference.
- `dpad-contact-sheet.png` confirms persistent rest rings, single-direction activation, and simultaneous diagonal activation without clipping adjacent controls.
- XInput masks map directly to Up `0x0001`, Down `0x0002`, Left `0x0004`, and Right `0x0008`; combined bitmasks illuminate multiple directions.
- The new indicators preserve the existing controller asset, green semantic feedback color, Chinese UI, trigger overlays, and enlarged stick motion.

No actionable P0, P1, or P2 findings remain for the D-pad feedback update.

previous result: passed

## D-pad Shape and Face-button Alignment Update

### Evidence

- User issue reference: `button-alignment-reference.png`
- Controller asset used for coordinate measurement: `Assets/controller.png`
- Updated full implementation state: `alignment-pass-full.png`
- Multi-state alignment evidence: `alignment-contact-sheet.png`
- Animated A/B/X/Y and D-pad preview: `aligned-button-preview.gif`
- Viewport: 1440 × 1024 demo state, 150% Windows DPI normalized to 100%

### Findings and fixes

- [P1] Circular D-pad indicators were centered near each arm but did not match the cross-shaped physical control, so their glow extended into the gaps. Replaced them with vertical and horizontal rounded segments sized from the controller raster.
- [P1] D-pad Up was approximately 10 source pixels too high, while Left and Right were approximately 20 source pixels too far outward. Re-measured the source centers and moved Up to `(0.394, 0.443)`, Left to `(0.362, 0.496)`, and Right to `(0.426, 0.496)`; Down remains at `(0.394, 0.552)`.
- [P1] Face-button feedback used a broader 2.6%-width radius and 1.7× glow, making active states appear larger than the physical buttons. Reduced the core radius to 2.35%, reduced the glow to 1.45×, and limited press displacement to 0.8 pixels.
- [P2] A and Y visual centers were slightly offset from the raster. Re-aligned A to `(0.698, 0.389)` and Y to `(0.700, 0.232)`; B and X were also checked against the source centers.
- [P2] The demo sequence did not visibly exercise Y. Added a Y phase so all A/B/X/Y alignment states are included in the animated preview.

### Post-fix verification

- `alignment-contact-sheet.png` shows active and resting D-pad segments contained inside the physical cross arms.
- A, B, X, and Y glows remain centered on the corresponding button faces and no longer spill across neighboring controls.
- Single directions, diagonal D-pad combinations, face buttons, triggers, and stick motion continue to animate independently.
- Native build completed successfully and the normal XInput launch remained responsive.

No actionable P0, P1, or P2 alignment findings remain.

previous result: passed

## Exact D-pad Silhouette Update

### Evidence

- Source visual truth: `Assets/controller.png`, using the physical D-pad crop at source coordinates `(495, 365, 260, 260)`.
- Implementation screenshot: `implementation-exact-dpad.png`.
- Same-scale focused comparison input: `exact-dpad-comparison.png`.
- Multi-state evidence: `exact-dpad-contact-sheet.png`.
- Animated evidence: `exact-dpad-preview.gif`.
- Viewport and state: 1440 × 1024 demo capture, dark theme, Up, Down, Left, Right, Up+Right, and Down+Left states.

### Findings and comparison history

- [P1] The prior rounded rectangles matched the approximate arm centers but not the controller asset's curved outer edges, so the active outline still read as an overlay placed on top of the D-pad. Replaced all four rectangles with source-measured closed geometries that follow the physical segment seams and outer circular silhouette.
- [P2] The generic glow stroke extended beyond the intended control footprint. The revised active fill, inner border, and restrained glow now share the exact same per-direction geometry, keeping the dominant light inside each physical segment.
- [P2] Face-button feedback retained a small vertical press offset, which could visually pull the glow below the raster button. Removed the displacement and re-measured A/B/X/Y centers against the same controller asset.

### Post-fix visual verification

- `exact-dpad-comparison.png` places the source crop and the active Up+Right implementation in one normalized comparison input. The top and right highlights follow the source's central seams, straight arm walls, and rounded outer caps.
- `exact-dpad-contact-sheet.png` shows all single-direction and diagonal states. No active segment enters the neighboring arm, center square, outer mounting ring, stick, or menu button.
- `implementation-exact-dpad.png` confirms the change preserves the controller scale, callouts, trigger feedback, stick motion, Chinese copy, and semantic green/blue color system.

### Required fidelity surfaces

- Fonts and typography: unchanged from the previously passed Chinese UI; no text or hierarchy was affected by the focused control update.
- Spacing and layout rhythm: controller placement, callouts, cards, and control spacing remain unchanged.
- Colors and visual tokens: D-pad feedback continues to use the left-side green semantic token with lower resting opacity and stronger active contrast.
- Image quality and asset fidelity: the supplied high-resolution controller raster remains the source asset. The new geometry was measured directly from it and introduces no replacement art, stretching, or raster degradation.
- Copy and content: unchanged and fully Chinese except standard Xbox/XInput input identifiers.
- Interaction states: all four D-pad directions and both diagonal combinations were captured; each mask illuminates only its corresponding physical segment.

The focused source-to-implementation comparison found no remaining actionable P0, P1, or P2 D-pad alignment issues.

previous result: passed

## Measurement Trust, Performance, and Calibration Update

### Evidence

- Audit baseline: `audit-2026-07-16/01-connected-dashboard.png` and `xbox-controller-lab-analysis.md`.
- Updated implementation: `implementation-optimized.png`.
- Viewport: 1440 × 936 logical pixels, Windows 150% DPI, connected Xbox controller, dark theme.
- Live verification: UI Automation, XInput sampling, process metrics, settings restart test, demo movement-rejection test, and real-controller stable calibration.

### Findings and fixes

- [P1] The fixed `125 Hz` badge implied a measured rate. It now separates actual UI display frequency from background XInput sampling frequency. The verified connected run displayed approximately 63–64 Hz and 221–222 Hz respectively.
- [P1] The right-column label `漂移` represented current stick coordinates, not a drift statistic. It is now `实时位置`. `死区` is now explicitly `参考死区`, with UI, tooltip, footer, automation name, and reset copy explaining that it affects diagnostic display only.
- [P1] Calibration accepted movement and lost all values after restart. It now rejects excessive motion, reports sampled noise, recommends reference deadzones, and persists offsets/deadzones under LocalAppData.
- [P1] Custom title-bar controls exposed private-font glyphs and were excluded from keyboard focus. They now expose Chinese names, help text, tooltips, focusability, and normal Invoke patterns.
- [P2] UI input, battery calls, text updates, and all custom visual redraws ran on every 8 ms tick. XInput sampling now runs on a background thread, battery queries are cached for two seconds, unchanged text and plot values are skipped, and visual easing invalidates only while values or animation states change.
- [P2] The battery mapper treated unknown battery type as disconnected. Disconnected, wired, unknown, and charge-level states are now distinct.
- [P2] Multiple normal instances could poll and render the same controller. A named mutex now keeps normal and demo sessions single-instance and focuses the existing window on duplicate launch.

### Functional verification

- Native x64 WPF build completed successfully.
- Connected state read back as `已连接 · 玩家 1`; measured labels read back as `显示 64 Hz` and `采样 221–222 Hz`.
- Idle process sampling dropped from roughly 21–22% of one logical core per instance in the audit to 1.6% in the optimized verification run.
- Left reference deadzone changed to 0.12 through RangeValue, survived a graceful restart, then reset to and persisted as 0.08.
- A duplicate normal launch exited automatically while the original responsive process remained.
- Demo calibration with moving sticks produced `检测到移动 · 重试` and did not save offsets.
- Real connected calibration completed with `校准完成 · 建议 4% / 4%`; saved settings reloaded as valid defaults on the current centered controller.
- UI Automation reported readable and keyboard-focusable `最小化`, `最大化或还原`, and `关闭` buttons plus both reference-deadzone sliders.

### Visual verification

- Typography, spacing, controller proportions, semantic left/right colors, exact D-pad geometry, and all prior dynamic feedback remain visually unchanged.
- The device card now communicates `显示` and `采样` as separate concepts without increasing card height or causing wrapping.
- The right column reads `实时位置` and `参考死区`; the footer clarifies the reference-line behavior. No clipping or collision is visible at the verified viewport.

No actionable P0, P1, or P2 issue remains in this first optimization batch. Trigger-history redesign, diagnostic scoring, reduced-motion controls, and multi-controller selection remain intentionally deferred to a later feature batch.

final result: passed

## Second Feature Batch: History, Diagnostics, Motion, and Device Selection

### Evidence

- Updated demo capture: `implementation-second-batch.png` at 1440 × 936 logical pixels.
- Connected-controller verification: UI Automation readback, process metrics, keyboard device-selection test, and settings restart test on Windows at 150% DPI.
- Final build: native x64 WPF `ControllerLab.exe` compiled with the system .NET Framework compiler.

### Implemented changes

- [P1] Replaced the static LT/RT diagonal response graphic with a rolling near-5-second history waveform. Each card now distinguishes current value from rolling peak and keeps the right edge anchored to “现在”.
- [P1] Added an automatic diagnostic score based on connection, measured XInput sampling rate, and observed center stability. A separate 0/6 coverage counter records whether both sticks, both triggers, face buttons, and D-pad have been exercised; incomplete coverage does not lower the health score.
- [P1] Added a reduced-motion preference. It removes stick trails, broad glow diffusion, and elastic interpolation while retaining direct position, button, D-pad, shoulder, and trigger feedback.
- [P1] Added explicit controller routing for Auto and Player 1–4. The selector changes the background XInput slot instead of merely filtering the label.
- [P2] Persisted controller routing and reduced-motion state alongside calibration and reference deadzones in settings schema version 2.
- [P2] Replaced the occlusion-sensitive 4 ms sleep loop with a high-resolution waitable timer and retained the previous timer-period fallback.

### Functional verification

- Connected controller read back as `已连接 · 玩家 1`.
- Foreground and background runs displayed approximately 63–64 Hz UI refresh and 235–242 Hz XInput sampling after the high-resolution timer fix.
- The automatic diagnostic reached `诊断 100 · 状态良好`; interaction coverage progressed from 0/6 to 6/6 without changing the score merely for incomplete testing.
- Keyboard selection of `玩家 2` changed the live state to `玩家 2 未连接` and persisted `controllerIndex=1`; returning to `自动选择` persisted `controllerIndex=-1` and reconnected Player 1.
- UI Automation toggled reduced motion on and persisted `reducedMotion=True`; a graceful restart restored the checked state. Toggling it off persisted the final `reducedMotion=False` state.
- Settings readback ended at schema version 2 with offsets 0, both reference deadzones 0.08, automatic controller selection, and standard motion.
- A five-second process sample during idle history rendering used approximately 5.3% of one logical core; active stick/trail rendering rose proportionally while the UI remained responsive.

### Visual verification

- The new trigger plots preserve the green-left/blue-right semantics and fit the existing card height without clipping labels, waveform, current marker, or peak value.
- The compact device-and-diagnostics card preserves the right-column plot size and presents score, coverage, selector, motion control, calibration, and reset actions as one aligned control group.
- The controller raster, exact D-pad silhouette, face-button alignment, stick scale, callouts, and high-DPI typography remain unchanged.

No actionable P0, P1, or P2 issue remains in the second feature batch.

final result: passed
