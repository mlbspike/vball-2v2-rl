# Volleyball 2v2 ML-Agents Environment

A Unity ML-Agents 4.x environment for training 2v2 volleyball agents using self-play.

## Requirements

- Unity 2022 LTS
- ML-Agents 4.x package
- Unity Universal Render Pipeline (URP)

## Setup

### 1. Create or Open Scene

- **Option A**: Open the existing `Volleyball2v2` scene (if it exists)
- **Option B**: Create a new empty scene:
  - Go to `File > New Scene`
  - Select `Basic (Built-in)` or your preferred template
  - Save the scene as `Volleyball2v2`

### 2. Add VolleyballBootstrap Component

1. In the Hierarchy, create an empty GameObject:
   - Right-click in Hierarchy → `Create Empty`
   - Rename it to `VolleyballBootstrap`

2. Add the `VolleyballBootstrap` component:
   - Select the `VolleyballBootstrap` GameObject
   - In the Inspector, click `Add Component`
   - Search for and add `VolleyballBootstrap`

3. **Setup the scene** using one of these methods:
   - **Method 1**: Right-click the `VolleyballBootstrap` component in the Inspector and select `Setup Scene`
   - **Method 2**: In the Unity menu bar, go to `Volleyball > Setup Scene`
   - **Method 3**: Enter Play mode - the scene will be set up automatically

This will programmatically create:
- A court plane (green surface)
- A center net with collider
- 4 capsule agents (2 per team) with:
  - Rigidbody components
  - BehaviorParameters (configured for `VBall2v2` behavior)
  - VolleyballAgent scripts
  - Team IDs correctly assigned (0 for near side, 1 for far side)
- 1 ball (orange sphere with Rigidbody)
- 1 VolleyballGameManager with all references automatically assigned

### 3. Verify Setup

After running the setup, you should see:
- A green court plane at the origin
- A white net in the center
- 4 colored capsule agents (2 blue on one side, 2 red on the other)
- 1 orange ball
- A GameManager GameObject

All components should be automatically wired up - no manual drag-and-drop required!

## Training

### Start Training Run

Open a terminal/command prompt and navigate to your Unity project root directory. Run:

```bash
mlagents-learn config/vball_selfplay.yaml --run-id=vball_v1 --no-graphics --time-scale=80 --num-envs=16
```

**Command Parameters:**
- `config/vball_selfplay.yaml`: The training configuration file
- `--run-id=vball_v1`: Unique identifier for this training run
- `--no-graphics`: Disable graphics for faster training (use `--force` if needed)
- `--time-scale=80`: Speed up simulation 80x for faster training
- `--num-envs=16`: Run 16 parallel environments

### Training Configuration

The training configuration (`config/vball_selfplay.yaml`) uses:
- **Trainer**: PPO (Proximal Policy Optimization)
- **Behavior Name**: `VBall2v2`
- **Self-Play**: Enabled (agents learn by playing against each other)
- **Observation Size**: 21 floats (team ID, self position/velocity, ball position/velocity, teammate/opponent positions, touch counts)
- **Action Size**: 6 continuous actions (movement X/Z, jump, hit direction X/Z, hit power)

**Note**: In ML-Agents 4.x, the action specification is automatically determined from the Agent's `OnActionReceived` implementation, so no manual configuration is needed.

## Scene Structure

After setup, the scene hierarchy will look like:

```
Scene
├── Court (Plane)
├── Net
│   ├── NetVisual (Cube with collider)
│   └── NetCenter (Transform reference)
├── Ball (Sphere with Rigidbody)
├── Agent_Team0_PlayerA (Capsule with VolleyballAgent)
├── Agent_Team0_PlayerB (Capsule with VolleyballAgent)
├── Agent_Team1_PlayerA (Capsule with VolleyballAgent)
├── Agent_Team1_PlayerB (Capsule with VolleyballAgent)
└── GameManager (VolleyballGameManager)
```

## Notes

- The `VolleyballBootstrap` component can be run multiple times - it will clean up existing objects before creating new ones
- All agent positions, physics settings, and references are set programmatically
- Team 0 (blue) is on the near side (negative Z), Team 1 (red) is on the far side (positive Z)
- The net is positioned at Z=0 with a height of 2.43m (standard volleyball net height)

## Troubleshooting

- **Scene not setting up**: Make sure the `VolleyballBootstrap` component is attached to a GameObject in the scene
- **Agents not training**: Verify that the BehaviorParameters on each agent have `BehaviorName` set to `VBall2v2` and `TeamId` matches the agent's team (0 or 1)
- **Config file not found**: Ensure `config/vball_selfplay.yaml` exists in the project root (not in Assets)

