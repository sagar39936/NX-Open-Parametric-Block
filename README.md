# Optellix Parametric Block

An NX Open C# application that creates and updates a centered parametric block, a rectangular through-hole pattern, and a top-edge chamfer.

## NX version used

- Siemens NX **2412**
- .NET Framework **4.8**
- NX Open managed assemblies supplied in `NXDlls/`
- Build platform: **x64**

## Build steps

1. Open `OptellixParametricBlock.csproj` in Visual Studio 2022.
2. Confirm the project targets **.NET Framework 4.8** and the platform target is **x64**.
3. Confirm these references point to the local `NXDlls` folder:

   - `NXOpen.dll`
   - `NXOpen.UF.dll`
   - `NXOpen.Utilities.dll`
   - `NXOpenUI.dll`

4. Select **Debug | Any CPU** (the project sets the actual platform target to x64).
5. Build the project with **Build > Build Solution**.
6. The deployment files are created in `bin\Debug`:

   - `OptellixParametricBlock.dll`
   - `OptellixParametricBlock.dlx`

To run it in NX, open a new millimetre model part, press `Ctrl+U` (**File > Execute > NX Open**), and select `bin\Debug\OptellixParametricBlock.dll`.

When rebuilding while NX is open, unload the previously executed library before running the new DLL.

## Architecture

```text
EntryPoint
  |
  +-- validates work-part context
  |
  +-- ParametricBlockDialog (.dlx / Block UI Styler)
        |
        +-- ParametricValidator
        |     validates dimensions, spacing, hole overlap, and pattern bounds
        |
        +-- geometry Apply transaction (Undo Mark)
              |
              +-- BaseBlockService
              |     XY sketch -> centered rectangle -> +Z extrude
              |     OPTELLIX_BASE_BLOCK
              |
              +-- SeedHoleService
              |     expression-driven seed position -> through-hole cut
              |     OPTELLIX_SEED_HOLE
              |
              +-- HolePatternService
              |     native NX rectangular pattern
              |     OPTELLIX_HOLE_PATTERN
              |
              +-- TopChamferService
                    outer top edges of the base block
                    OPTELLIX_TOP_CHAMFER
```

Every Apply creates an NX Undo Mark. The application creates or updates the dependent features, performs one final model update, and calls `UndoToMark` if a feature or update fails.

## Pattern strategy

The seed-hole location is calculated from named NX expressions so the rectangular pattern stays centered when the block size, counts, or pitches change.

```text
patternMarginX = (Length - ((CountX - 1) * PitchX)) / 2
patternMarginY = (Width  - ((CountY - 1) * PitchY)) / 2

seedX = -Length / 2 + patternMarginX
seedY = -Width  / 2 + patternMarginY
```

The application creates one seed through-hole and uses NX's native `PatternFeatureBuilder` to make the X-Y rectangular pattern. Before any geometry is changed, validation checks that:

- all block and hole dimensions are positive;
- counts are at least one;
- pitch is greater than hole diameter when more than one hole is requested;
- complete pattern extents, including hole diameter, fit inside the block.

An invalid pattern is rejected before the geometry transaction begins, so the existing model is unchanged.

## Block UI Styler (`.dlx`) setup

The dialog file is `OptellixParametricBlock.dlx`. It must be beside the built DLL because the code loads it from the assembly directory.

1. In NX, open **Tools > Block UI Styler**.
2. Open `OptellixParametricBlock.dlx` if you need to edit the dialog.
3. Keep these block IDs unchanged; they are referenced by `ParametricBlockDialog.cs`:

   | Dialog field | Block ID | Expected type |
   | --- | --- | --- |
   | Length | `doubleLength` | Linear Dimension |
   | Width | `doubleWidth` | Linear Dimension |
   | Height | `doubleHeight` | Linear Dimension |
   | Hole diameter | `doubleHoleDiameter` | Linear Dimension |
   | Holes in X | `integerHolesX` | Integer |
   | Holes in Y | `integerHolesY` | Integer |
   | Spacing in X | `doubleSpacingX` | Linear Dimension |
   | Spacing in Y | `doubleSpacingY` | Linear Dimension |

4. Ensure the dialog enables the **Initialize**, **Dialog Shown**, **Apply**, **OK**, and **Update** callbacks.
5. Save the `.dlx` file and rebuild the project, or copy the updated `.dlx` beside the DLL in the folder used by NX.

If a block ID or its datatype changes, update the matching lookup and value access in `UI/ParametricBlockDialog.cs` at the same time.

## Quick verification in NX

1. Apply the default values and confirm the named sketch, extrude, seed hole, pattern, and chamfer appear in the Part Navigator.
2. Change Length, Width, or Height and apply again; confirm the base block updates.
3. Change hole count or pitch and confirm the pattern remains centered.
4. Enter an over-sized pattern; confirm validation rejects it and the existing geometry remains unchanged.
5. Confirm the top chamfer remains on the four outer top edges after an update.


