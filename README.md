# TrustHumanHuman

A Unity-based dyadic motor experiment investigating human-human trust during a haptically-coupled tracking task, using two **HRX-1** haptic robotic devices.

## Overview

Two participants each control an HRX-1 robot to track a descending trajectory. Depending on the experimental block, the two robots may be coupled through a **virtual spring**, so that each participant feels the partner's movements through haptic force feedback.

At specific points along the trajectory, the path **splits into two branches**. One of the branches is sometimes blocked by an obstacle. When a participant can see the obstacle, they must communicate its location to their partner **through haptic feedback** (rather than verbally or visually), and the pair must jointly decide which branch to take.

## Task Design

### Split / obstacle structure
- At each split, the obstacle location is determined probabilistically:
  - **1/3** chance: no obstacle (both branches open)
  - **1/3** chance: obstacle on the **left** branch
  - **1/3** chance: obstacle on the **right** branch

### Obstacle visibility modes
Visibility of the obstacle to each subject depends on the block's visibility mode:
- **1-sided (1s):** Only one subject can see the obstacle, consistently, for the entire trial.
- **2-sided (2s):** Both subjects are able to see the obstacle, but visibility occurs with a probability of **1/5** per split.

### Haptic coupling
- **KL** — low spring stiffness, *k* = 0.5
- **KH** — high spring stiffness, *k* = 1.6

## Experimental Blocks

The experiment consists of **6 blocks**, each containing **4 trials**, with trial order randomized within each block. **Solo** is run first as the uncoupled baseline; the order of the remaining **5 connected blocks** (KL, KH, 1-sided-1, 1-sided-2, 2-sided) is randomized:

| Block | Description |
|---|---|
| Solo | No haptic connection between subjects |
| KL | Connected via virtual spring, low stiffness (k = 0.5) |
| KH | Connected via virtual spring, high stiffness (k = 1.6) |
| 1-sided-1 | One-sided obstacle visibility, condition 1 |
| 1-sided-2 | One-sided obstacle visibility, condition 2 |
| 2-sided | Both subjects can see the obstacle (p = 1/5 per split) |

## Configuration (Master Canvas)

The experimenter ("master") configures and runs each trial directly from input fields on the **Master Canvas** in the Unity scene — no code changes or rebuilding are required between trials. The available fields (read at runtime by `HandleExperiment.cs`) are:

- **Control mode** — selects Solo / uncoupled vs. spring-coupled behavior
- **Connection stiffness (Kp) / damping (Kd)** — sets the virtual spring parameters (e.g. KL = 0.5, KH = 1.6)
- **Viscosity (mu_v)** — additional viscous damping sent to the robot controllers
- **Subject 1 / Subject 2 ID** — used to name the output data folder/files
- **Obstacle visibility setting** — selects which visibility mode is active for the block (1-sided-1 / 1-sided-2 / 2-sided)
- **Trial number** — current trial index, included in the saved filename

After setting these fields, the master presses **Start** to begin a trial (triggers a 3-2-1-GO countdown) and **Stop** to end it.

## Data Acquisition

- **Robot state (HRX-1 angles, angular velocities, interaction torque):** read continuously over CAN bus from the robot controllers and **saved automatically** to CSV in the **Experiment Data** folder (per-pair subfolder, named from the Subject 1/2 IDs entered on the Master Canvas), with no manual step required.
- **EMG (wrist flexor/extensor)** and **gaze (Subject 1)**: recorded by the third-party **Cometa** system and saved into the **Cometa** folder. Acquisition settings (channels, sampling rate, device selection, save path, etc.) depend on your specific EMG/gaze hardware and may need to be configured in the Cometa software to match your setup.
- **Recording start/stop trigger:** rather than starting EMG/gaze recording manually, the Unity build triggers it automatically by **simulating an F5 key press** (via the Windows `keybd_event` API) at the moment a trial starts, and an **F6 key press** when the master presses Stop, stopping the recording. This keeps the Cometa recording in sync with the Unity trial timeline without the experimenter needing to switch windows.

## Repository Structure

```
TrustHumanHuman/
├── Assets/
│   ├── HandleExperiment.cs              # Main experiment/trial controller (class HandleExpe)
│   ├── CANRecorder.cs                   # Reads robot state & sends control config over CAN
│   ├── Cursor.cs / CursorHRX2.cs        # Drives Subject 1 / Subject 2 on-screen cursor from HRX-1 (or keyboard, for testing)
│   ├── WavesSpawner.cs                  # Generates descending trajectory, splits, and obstacles (class WaveSpawner)
│   ├── Subject1ScreenFlash.cs           # Flashes Subject 1's screen on an obstacle event
│   ├── Subject2ScreenFlash.cs           # Flashes Subject 2's screen on an obstacle event
│   ├── DisplayLog.cs                    # Mirrors Unity console log onto an in-scene text element
│   ├── TrajectoryMover.cs               # Generic downward-moving/self-destroying object utility (class WaveMover)
│   └── UnityCanController/
│       ├── CANController.cs             # Unity-side CAN bus interface (send/receive queues, read thread)
│       └── PCANBasic.cs                 # Third-party PEAK-System PCAN-Basic API bindings
├── Packages/                            # Unity package manifest/dependencies
├── ProjectSettings/                     # Unity project configuration
├── UnityGame/                           # Built standalone executable
├── TrustExperimentWindowsFormNames.txt  # Reference list of experiment UI form names
├── visualisation.m                      # MATLAB script for visualizing experiment/results data
├── .vscode/                             # Editor configuration
└── .vsconfig                            # Visual Studio component configuration
```

## Scripts in Detail

### `HandleExperiment.cs` (class `HandleExpe`)
The central controller for a trial. On `Start()`, it reads all configuration values from the Master Canvas input fields (control mode, Kp/Kd, viscosity, subject IDs, obstacle visibility, trial number) and builds the output CSV path/folder for that trial. When the master presses **Start**, it runs a 3‑2‑1‑GO countdown coroutine, then begins data logging and simulates an **F5** key press to start EMG/gaze recording. Every frame while running, it:
- Pulls the latest robot state (position, velocity, torque for both HRX-1 units) from `CANRecorder`.
- Tracks each cursor's position relative to the desired left/right trajectory (`WaveSpawner.xdL` / `xdR`) and flags if a subject strays outside the allowed margin.
- Checks whether either subject's cursor is near a currently visible obstacle and triggers the corresponding `Subject1ScreenFlash` / `Subject2ScreenFlash` feedback.
- Writes a row of trial data (time, both cursor/robot positions, velocities, torques, trajectory bounds, obstacle visibility/location, obstacle-hit flags) to the trial's CSV file every frame.
- Continuously sends the current control configuration (mode, viscosity, stiffness, obstacle/margin flags) to the robots over CAN via `CANRecorder.SendCtrlConfig`.

When the master presses **Stop**, it simulates an **F6** key press (stopping EMG/gaze recording), sends a neutral control config to the robots, and closes the application.

### `CANRecorder.cs`
Handles all CAN-bus communication with the HRX-1 controllers. In `FixedUpdate()`, it reads incoming CAN messages by ID (position, velocity, and torque for each robot) and converts the raw fixed-point CAN payloads into engineering units (radians, rad/s, Nm). `GetCurrentHRXData()` exposes the latest values to other scripts, and `SendCtrlConfig()` packages the control mode, viscosity, spring stiffness/damping, and obstacle/margin flags into CAN messages sent out to the robot controllers.

### `UnityCanController/CANController.cs` and `PCANBasic.cs`
The low-level CAN transport layer. `CANController.cs` initializes the PEAK-System **PCAN-Basic** USB-to-CAN adapter at start-up, runs a background thread to continuously read incoming CAN frames into a shared dictionary, and sends queued outgoing messages every physics tick. `PCANBasic.cs` is the third-party C# binding for PEAK's PCAN-Basic DLL and generally should not need modification.

### `Cursor.cs` / `CursorHRX2.cs`
Each script drives one subject's on-screen cursor. A `Key`/`HRX` selector (read from a Master Canvas field) lets the cursor be driven by keyboard input (useful for testing without the robots connected) or by the live angle of the corresponding HRX-1 robot (read from `CANRecorder`), converted to a screen-space x-position.

### `WavesSpawner.cs` (class `WaveSpawner`)
Procedurally generates the task environment. It builds the descending trajectory as a smooth "filling curve" (a sum of sine waves) interrupted by periodic **splits**, spawns the left/right `LineRenderer` paths, and instantiates an obstacle prefab at each split. For each obstacle it randomly decides:
- Whether it appears on the left or right branch (or not at all), and
- Which Unity physics **layer** it's assigned to, which determines who can see it — Player 1 only, Player 2 only, both ("Lone" trials), or neither — based on the active control mode and obstacle-visibility setting from the Master Canvas.

It also tracks each obstacle's downward motion and timing so `HandleExperiment.cs` can detect, frame-accurately, when a subject's cursor passes a visible obstacle.

### `Subject1ScreenFlash.cs` / `Subject2ScreenFlash.cs`
Lightweight feedback scripts: each briefly fades a colored overlay image in and out on the corresponding subject's display when `HandleExperiment.cs` detects that subject is near a visible obstacle.

### `DisplayLog.cs`
A debugging aid — subscribes to Unity's `Application.logMessageReceived` event and mirrors the last 5 log messages onto an in-scene TMP text element, so the experimenter can see runtime log output in the built executable without an attached console.

### `TrajectoryMover.cs` (class `WaveMover`)
A small generic utility that moves an object downward at a constant speed and destroys it once it moves off-screen. 

## Requirements

- Unity Editor 2022.3.1.18f1
- Two HRX-1 haptic robotic devices, connected via a PEAK-System PCAN-Basic USB-to-CAN adapter (CAN bus, 1 Mbit/s)
- Cometa EMG/gaze acquisition system (wrist flexor/extensor EMG, Subject 1 gaze)
- MATLAB (for running `visualisation.m`)

## Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/Alireza8Kh/TrustHumanHuman.git
   ```
2. Open the project in Unity (via `Assets/` and `ProjectSettings/`), or run the prebuilt executable in `UnityGame/`.
3. Connect and calibrate the two HRX-1 devices (via the PCAN-Basic CAN adapter) and the Cometa EMG/gaze system, adjusting Cometa's device settings as needed for your hardware.
4. On the Master Canvas, set the control mode, stiffness/viscosity, subject IDs, obstacle visibility, and trial number for the upcoming trial.
5. Press **Start** to run the trial (countdown → EMG/gaze recording starts automatically via simulated F5) and **Stop** to end it (recording stops automatically via simulated F6).
6. Block order: **Solo** first (fixed baseline), followed by the 5 connected blocks (KL, KH, 1-sided-1, 1-sided-2, 2-sided) in **randomized order**, each with 4 randomized trials.
7. Use `visualisation.m` in MATLAB to inspect or visualize the collected trial data.

## Contact

For questions about this experiment or codebase, please open an issue on this repository.
