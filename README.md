# Unity_FinalProject_TaxiBookingUI
A taxi booking system based by Photon Fusion 2
# Multiplayer Taxi Dispatch Simulation

A real-time **multiplayer taxi management simulation** built with **Unity** and **Photon Fusion 2**, featuring spline-based road networks, Dijkstra pathfinding, Random traffic flow system,Raycast collision avoidance, and a server-authoritative networking architecture.



https://github.com/user-attachments/assets/a4fb502d-acfa-453c-967d-a0dd245bb2af

---

## Tech Stack

| Layer | Technology |
|---|---|
| Game Engine | Unity 6 (Universal Render Pipeline) |
| Networking | Photon Fusion v2 (Client-Host) |
| Road System | Unity Splines 2.7.1 |
| Transport | NanoSockets |
| UI | Unity UI + TextMesh Pro |
| Local Testing | ParrelSync (multi-editor cloning) |
| 3D Assets | Azerilo Car Model No.1201 |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                       HOST (Server)                     │
│                                                         │
│  ┌──────────────┐   ┌───────────────┐   ┌───────────┐   │
│  │  Dispatcher   │──▶│ PathRequest  │──▶│ Dijkstra │   │
│  │  (Order Queue)│   │   Manager    │   │ Pathfinder│   │
│  └──────────────┘   └───────────────┘   └───────────┘   │
│          │                                     │        │
│          ▼                                     ▼        │
│  ┌──────────────┐   ┌───────────────┐   ┌───────────┐   │
│  │  Taxi Pool   │──▶│   Movement    │──▶│ Collision│   │
│  │  Assignment  │   │  Simulation   │   │ Avoidance │   │
│  └──────────────┘   └───────────────┘   └───────────┘   │
│                            │                            │
│                       RPC sync                          │
│                            │                            │
├────────────────────────────┼────────────────────────────┤
│                       CLIENT(s)                         │
│                            ▼                            │
│  ┌──────────────┐   ┌───────────────┐   ┌───────────┐   │
│  │  Taxi Select │   │    Path       │   │  Status   │   │
│  │  (Click)     │   │  Visualizer   │   │  Display  │   │
│  └──────────────┘   └───────────────┘   └───────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## Core Systems

### 1. Server-Authoritative Networking (Photon Fusion)

All game logic runs on the Host — clients are visual-only, preventing desync and cheating.

- **Networked state** via `[Networked]` properties (vehicle skins, positions)
- **RPC pipeline** for path synchronization — only waypoint arrays are transmitted, not per-frame positions
- **Session management** via `INetworkRunnerCallbacks` with Host/Client mode selection

### 2. Spline-Based Road Network & Dijkstra Pathfinding

Roads are defined using **Unity Splines** (48+ curves), providing smooth, natural road geometry instead of rigid grid-based navigation.

- Custom **graph builder** that converts spline knots into a weighted, bidirectional graph structure
- **Dijkstra's shortest-path algorithm** finds optimal routes across the network
- **Zone-aware routing** — main roads (splines 0–15) are separated from station branches (16–47) to model realistic road hierarchy
- **Async request queue** (`PathRequestManager`) processes pathfinding via coroutines to avoid frame-rate drops under heavy load

### 3. Traffic Flow System

Autonomous vehicles populate the city to create a living traffic environment.

- Cars (`CarPlayer`) randomly select destinations and navigate independently
- **Raycast-based collision avoidance** with adaptive speed control — smooth deceleration when obstacles are detected ahead, emergency stop while two car meet each other with a turn
- Emergent traffic behavior arises from simple per-vehicle rules

### 4. Taxi Dispatch & Order Management

A dispatcher system manages passenger requests across 10 stations.

- **Order queue** with pickup/dropoff station assignments
- **Greedy assignment algorithm** — finds the closest available taxi for each order
- Taxis follow multi-stop routes: station → pickup → dropoff → return
- **Oil cost saving** - Taxis can receive order when they are empty or in the way to the taxi station
- Real-time **ETA and speed display** via world-space billboard UI

---



## Design Patterns

| Pattern | Where Used |
|---|---|
| **Singleton** | `PathRequestManager`, `GlobalPathRenderer` — single point of access for shared services |
| **Observer / Callback** | Pathfinding completion notifies requesters asynchronously |
| **Command Queue** | `TaxiInputDispatcher` queues orders; `PathRequestManager` queues path requests |
| **Server Authority** | Host owns physics & logic; clients receive state via RPC |
| **Billboard UI** | `TaxiStatusDisplay` — world-space panels always face camera |

---

## Project Structure

```
Assets/
├── BasicSpawner.cs              # Network session bootstrap & AI vehicle spawner
├── TaxiPlayer.cs                # Networked player-assigned taxi (426 lines)
├── CarPlayer.cs                 # Autonomous AI traffic vehicle
├── SplineGraphPathfinder.cs     # Dijkstra pathfinding on spline graph
├── PathRequestManager.cs        # Async pathfinding request queue
├── TaxiInputDispatcher.cs       # Host-only order dispatch system
├── TaxiCollisionAvoidance.cs    # Raycast-based obstacle detection
├── TaxiStatusDisplay.cs         # World-space speed / ETA billboard
├── TaxiVisuals.cs               # Networked material skinning
├── GlobalPathRenderer.cs        # Singleton path line visualization
├── TaxiPathShower.cs            # Click-to-select taxi info display
├── PathViewer.cs                # Per-taxi LineRenderer wrapper
├── Scenes/
│   └── SampleScene.unity        # Main simulation scene
├── Asset/                       # Prefabs (taxi variants, stations)
├── Azerilo/                     # 3D car model assets
├── Photon/                      # Fusion networking library
└── Plugins/
    ├── ParrelSync/              # Multi-editor local testing
    └── NanoSockets/             # Low-level network transport
```

---

## Getting Started

### Prerequisites

- **Unity 6** (6000.x) with Universal Render Pipeline
- **Photon Fusion v2** App ID ([Photon Dashboard](https://dashboard.photonengine.com/))

### Setup

1. Clone this repository
2. Open the project in Unity 6
3. Enter your Photon App ID in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`
4. Open `Assets/Scenes/SampleScene.unity`
5. Press Play — select **Host** to start a session

### Multiplayer Testing

Use **ParrelSync** (`Window > ParrelSync > Clones Manager`) to open a second editor instance and join as **Client**.

### Run .exe file

If you just want to test the results, execute the **final.exe**.

---

## Gameplay Flow

```
Host starts session
       │
       ▼
 Traffic flow spawns (7 vehicles)
       │
       ▼
 Dispatcher enters order ──▶ Closest taxi assigned
       │                            │
       │                     Dijkstra pathfinding
       │                            │
       ▼                            ▼
 Taxi navigates ──▶ Picks up ──▶ Drops off ──▶ Returns to station
       │
  Clients see path + status via RPC sync
```

---

## License

This project was developed as an academic capstone. Third-party assets (Photon Fusion, Azerilo car models) are subject to their respective licenses.
