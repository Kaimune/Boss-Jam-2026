# Context for Claude Code

## Project layout
- **This project (`Boss-Jam-2026/Boss Jam Project/`) is the active Unity project.** Unity Editor is opened on this one.
- The "Boss GameJam" sibling referenced in earlier notes no longer exists on disk; this directory absorbed all the active work.

## Unity MCP setup
- Unity MCP for Unity (CoplayDev) v9.6.8 is running, serving HTTP on `http://127.0.0.1:8080/mcp`.
- The Unity Editor instance is `Boss GameJam@<hash>` (Unity 6000.0.75f1).
- To enable the MCP tools in Claude Code, this project needs `.mcp.json` at its root:
  ```json
  { "mcpServers": { "unity": { "type": "http", "url": "http://127.0.0.1:8080/mcp" } } }
  ```
  After creating it, restart Claude Code and approve the project-scoped server when prompted.

## Asset status
- Single source of truth lives in this directory. No "old" / "new" split anymore.

## Known console state
- 5 "missing script" warnings present. Likely stale references — investigate before assuming they're harmless if they affect gameplay.

## Conventions
- Unity 6 with URP. Render pipeline assets live in `Assets/Settings/`.
- New Input System asset: `Assets/InputSystem_Actions.inputactions`.
- Pixel art via `ProPixelizer` (third-party, do not modify).
- Scenes: `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/BossScene.unity` (boss work-in-progress).
- User scripts live under `Assets/Scripts/` (currently has `Player/`).

## Workflow notes
- After editing/creating scripts via MCP, call `read_console` to check for compile errors before using new types.
- Path conventions for MCP tools: relative to `Assets/`, forward slashes.
