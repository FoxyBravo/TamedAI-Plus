# PetAI Mod — Project Knowledge

## Build
- Project: `PetAI\PetAI.csproj`
- Framework: `net10.0`
- Build command: `dotnet build -c Release` (run from `PetAI\` directory)
- Output: `PetAI\bin\Release\Mods\mod\`

## Deploy
The game loads mods from **`%appdata%\VintagestoryData\Mods\`**, NOT `%appdata%\Vintagestory\Mods\`.

Deploy steps:
1. `dotnet build -c Release` in `PetAI\`
2. Delete old version folder: `Remove-Item "%appdata%\VintagestoryData\Mods\petai-vX.Y.Z" -Recurse -Force`
3. Copy build output: `robocopy "PetAI\bin\Release\Mods\mod" "%appdata%\VintagestoryData\Mods\petai-vX.Y.Z" /E /IS`

The folder name includes the version (e.g., `petai-v5.1.3`). Use `robocopy` instead of `Copy-Item` for reliable file copying.

## Version
- Update in `PetAI\modinfo.json` before building
- Keep folder name in sync with version

## Key Files
- `PetAI\src\Entity\AITask\AiTaskRestrictedRoam.cs` — merged stay/guard AI task
- `PetAI\src\Systems\PetAI.cs` — task registration
- `PetAI\assets\petai\patches\entities\dog-guard.json` — guard task config (maxdistance: 50)

## Feature Branch
- `feature/stay-guard-restricted-roam` — incremental commits for stay/guard refactor
  1. `chore: add build environment config`
  2. `feat: merge AiTaskStay and AiTaskGuard into AiTaskRestrictedRoam`
  3. `feat: add roam/guard commands, downed check, wound healing`
  4. `feat: explicit walk animation and forward controls in AiTaskRestrictedRoam`
  5. `chore: bump version to 5.1.3`
- Test each commit individually to isolate regressions

## Known Issues / Investigations
- Walk animation for returning to guard/stay center: animation name is `"Walk"` (capital W), not `"walk"`
- If a commit breaks wolf item placement, checkout the previous commit and verify — narrow down which feature introduced the regression
