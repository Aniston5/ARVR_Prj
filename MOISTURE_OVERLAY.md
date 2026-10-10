# Underground moisture visualization

## Preview

1. Exit Play mode and let Unity finish compiling. Use Assets > Refresh if needed.
2. Choose AR Planter > Open Moisture Preview from the top menu bar. Save your existing scene if prompted.
3. Press Play. One continuous rectangular 3D moisture volume fills the entire planter, visible through the wall.
4. Select Plant 1–4 and press Water selected plant (Water plant on a portrait screen). Watch the shallow reading rise, then the middle and deep readings. Each plant is independent.
5. Use Pause, the 1x/5x/10x control, and Reset to examine the demonstration. Normal hides underground graphics; Underground shows them again.

The reference image guides the underground moisture view only. Pest simulation and environment controls are not part of this change.

## What is shown

- One full soil volume, initially 1.1 m wide by 0.3 m front-to-back by 0.32 m deep. Sensor columns follow Plants/Plant_1 through Plant_4.
- Three sensor positions per plant: initially 4, 14, and 26 cm below a soil surface at local Y=0.43. These are editable demo assumptions, not confirmed hardware positions.
- Twelve independently stored sensor readings, with values for the selected plant in the panel.
- A continuous volume estimated from all fresh sensor readings using inverse-distance-squared weighting in three dimensions. Nearby sensors influence a point more than distant sensors. A 32 x 16 x 12 grid is refreshed at 5 Hz and sampled by the volume shader. The whole rectangular bed is filled; there are no cylindrical per-plant envelopes. Sparse readings produce estimates, including extrapolation near the edges, not a measured water map.
- Blue indicates low moisture, cyan/green intermediate levels, and yellow/orange/red high moisture. This is a magnitude legend, not a dry/optimal/wet recommendation.
- Illustrative branching roots and sensor markers. Actual roots have not been scanned.

All readings start in Demo mode and are explicitly labelled simulated. Watering is a delayed illustrative wetting response; it is not a hydrological model or a pump command. Re-watering restarts that plant's demo response. Live sensor percentages will be relative calibrated readings, not necessarily volumetric water content.

## Position and appearance

Select CropBedRoot's Planter Moisture Overlay component. Soil Surface Y, Root Depth, Planter Size, Planter Center, and Sensor Depths control the geometry. Root Zone Size only sizes the illustrative roots and sensor offsets. Values are in local metres at root scale 1; changing root scale also scales the displayed depths. Change Volume Opacity to adjust visibility.

Xray Overlay intentionally draws the volume, roots, and markers through the planter wall. Turn it off for ordinary depth testing. Volume Offset can move the whole underground illustration forward for an exploded view; zero keeps it under the actual plant positions. The plant model stays in place.

Stop Play before saving permanent Inspector changes to the prefab. Reopen the preview if you were already using the older single-sensor preview.

## AR placement

MainAR still uses tap-to-place. The CropBedRoot prefab contains this visualization, so all root zones move with the placed planter. The scene preview is hidden on startup; the legacy stacked MoistureZones objects remain disabled. Image-marker alignment to a physical planter is not implemented.

## Future sensor connection

Call from Unity's main thread after placement:

    var overlay = placement.PlacedCropBed.GetComponent<PlanterMoistureOverlay>();
    overlay.SetSensorMoisture(0, 0, 63f); // Plant 1, shallow sensor
    overlay.SetSensorMoisture(0, 1, 48f); // Plant 1, middle sensor
    overlay.SetSensorMoisture(0, 2, 29f); // Plant 1, deep sensor

Plant indexes are 0..3; depth indexes are 0..2. Accepted readings switch to Live mode. Convert ADC readings to calibrated percentages before calling. Invalid indexes, NaN, infinity, and values outside 0..100 are rejected. Each sensor expires independently after Stale After Seconds. Missing/stale markers turn grey and are excluded from interpolation. The panel reports how many of the twelve sensors are fresh. The field is hidden when no readings are fresh; fewer sensors mean less spatial information. ClearReadings invalidates all sensors. There is no ESP32 transport yet.

## Verification

Runtime and editor C# compiled against the installed Unity references. Tests/SoilMoistureProfileChecks.cs checks sensor independence, input bounds, stale readings, reset, wetting order and demo value ranges. The continuous-volume shader and watering controls were visually checked in Unity. Tests also cover exact sensor positions, distance weighting, missing sensors, and uniform fields. Android device verification is tracked separately.

## Android build

Use AR Planter > Build Android APK while Play mode is stopped. This builds Assets/MainAR.unity to Builds/Android/ARPlanter.apk using the existing Android configuration and package com.mte4442.arcrop. Builds/Android/build-status.txt records the result. Connect an Android phone with USB debugging enabled to install using Android Debug Bridge. On the phone, allow camera access, scan a well-lit textured floor, then tap a detected plane to place the planter.
