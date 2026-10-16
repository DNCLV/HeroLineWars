# Floor rendering repair

## Confirmed cause

The saved `HLW_LanePrototype` scene contained two active generators at the origin:

- `HLW_LanePrototype` (component fileID `1000000000000000002`): 256 Kenney floor planes, each at Y=0.
- `LaneGenerator` (component fileID `1831758360`): 256 old primitive floor cubes with centers at Y=-0.075 and thickness 0.15, placing their top surfaces at exactly Y=0.

These surfaces cover the same XZ coordinates. They compete in the depth buffer. At the existing 12-degree tilt from vertical, the competing surfaces produce the repeated horizontal bands. A Unity render with the saved legacy geometry re-enabled reproduced those bands; disabling that hierarchy removed them. The first diagnostic only counted one generator's children, missing the other 256 surfaces.

The debug-material checkbox was enabled on the legacy generator, which had no Kenney floor reference. The previous override only ran in the Kenney branch. The actual Kenney generator's debug checkbox was false. Consequently, the earlier test did not isolate the visible floor's material.

## Mesh and material evidence

Both direct binary FBX inspection and Unity 6000.6.0f1 inspection confirmed:

- One floor mesh, four vertices, one submesh, two triangles.
- Unity local bounds: X and Z from -0.5 to +0.5; Y=0.
- Vertices: (-0.5,0,-0.5), (-0.5,0,+0.5), (+0.5,0,-0.5), (+0.5,0,+0.5).
- Triangle indices: 0,1,2 and 3,2,1. Upward winding and normals (0,1,0).
- Identity rotation and unit imported transform scale; no orientation correction needed.
- All four UVs use the same palette sample (0.96875,0.125). The source texture cannot create horizontal or rectangular patterns within this mesh.
- Material `colormap`, shader `Universal Render Pipeline/Lit`, original colormap base texture, white base tint, opaque surface, queue 2000, depth writing enabled.

The FBX has no duplicate triangles. Its OBJ counterpart repeats faces, but the generator references the FBX, not that OBJ.

## Changes

- `Assets/Scenes/HLW_LanePrototype.unity`: disabled only the redundant `LaneGenerator` root. Its contents remain stored for recovery. The camera still references `HLW_LanePrototype`. No scene objects were deleted.
- `Assets/Scripts/LaneGenerator.cs`: floor footprints now match the full cell size rather than leaving artificial gaps. Floor meshes are centered by their visible bounds. The gray URP/Unlit override now covers every material slot in both generation paths. Cached materials are serialized for reuse. Regeneration clears only the six known generated groups, preserving unrelated children. Updated the light lookup to the supported Unity 6.6 API without changing its lighting values.
- `Assets/Editor/LaneGeneratorEditor.cs`: setup/generation tools require one active generator in the active scene rather than choosing an arbitrary instance. Diagnostics inspect all generator hierarchies, report vertices/indices/normals/UVs and actual material settings, and check for scene-wide overlapping top surfaces. Disabled backup renderers and edge-touching tiles are excluded from active overlap counts. The isolated 2x2 test uses measured floor spacing.

`HlwCameraController.cs` is byte-for-byte unchanged. Imported FBX files, textures, materials, lane dimensions, and spawn/goal coordinate calculations were not edited.

## Unity validation

Validation ran in an isolated project copy under `Temp/HLWRepairValidation`, using the installed Unity 6000.6.0f1 Editor, URP, and the project's graphics settings. Comparison cameras used the existing 12-degree tilt from vertical, with a wider diagnostic view to inspect the whole floor. The production camera was not changed.

- Updated scripts compiled with no C# errors or deprecation warnings.
- One active generator; 256 floor tiles; full X=0..8, Z=0..32 footprint at Y=0.
- Zero positive-area tile overlaps and zero active scene coplanar-surface candidates.
- Repeated regeneration retained the expected count and preserved an unrelated test child.
- All floor material slots received the shared gray URP/Unlit material; subsequent regeneration reused that material.
- Turning debug mode off restored the original Kenney URP/Lit materials.
- Normal and debug floor renders were inspected and showed no repeated stripe bands or irregular patches.
- In Play Mode, injected Input System events verified W movement, down-arrow movement, right boundary clamping, smooth wheel zoom-in, and orthographic projection.

An unrelated `ArgumentOutOfRangeException` from `UnityEditor.Search.SearchDatabase` occurred during batch startup/indexing. The validation assertions completed successfully despite that exception. It was not attributed to the lane scripts or fixed by changing unrelated project settings.

Physical keyboard/mouse input with the Game window focused, mobile-device rendering, prolonged navigation/flicker checks, and every individual movement key have not been manually tested. The batch input checks used virtual devices.

## Apply in the existing Editor

1. Reopen `Assets/Scenes/HLW_LanePrototype.unity` from disk so the saved legacy-root deactivation is loaded. If the scene is already open with unsaved work, preserve that work first; the root named `LaneGenerator` should be inactive, while `HLW_LanePrototype` stays active.
2. Run **Tools > Hero Line Wars > Generate Lane**, then save the scene. FBX references are already assigned on the retained generator.
3. Run **Tools > Hero Line Wars > Report Floor Mesh Geometry**. Expect 256 active floor renderers and zero possible coplanar surface pairs. The inactive legacy hierarchy is reported separately for visibility.
4. Enter Play Mode, click the Game view, move with WASD/arrows, and scroll to zoom. Inspect the floor while moving.
5. For a material comparison, toggle **Use Debug Floor Material** on `HLW_LanePrototype`, regenerate, then toggle it off and regenerate again.

The imported floor is a palette-colored flat plane, so the clean surface is simple and uniform. Adding decorative paving detail is a separate art task. Existing endpoint architecture and scale capsules remain as before; this repair adds no decoration or gameplay.

## Render evidence

- [Bands reproduced with saved legacy surfaces enabled](FloorRenderingRepair/Before.png)
- [Repaired floor with original Kenney material](FloorRenderingRepair/After.png)
- [Repaired floor with gray debug material](FloorRenderingRepair/Debug.png)
- [Unity validation log](FloorRenderingRepair/UnityValidation.log)
