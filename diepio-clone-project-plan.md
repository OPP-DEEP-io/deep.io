# diep.io Clone — Project Plan (T120B516 Objektinis programų projektavimas)

> **Purpose of this document:** hand-off spec for an AI assistant helping a 4-person team build a 2D multiplayer diep.io clone in C# for a KTU design-patterns course. It encodes the graded requirements so nothing gets built that fails the rubric.

---

## 0. Progress log

> Living status of the build — update as milestones land. Legend: ✅ done · 🟡 in progress · ⏳ not started.

**2026-09-10 — Prototype (before patterns) implemented and verified.**

- ✅ **Repo + solution skeleton** — `DiepClone.sln` with `DiepClone.Shared`, `DiepClone.Server`, `DiepClone.Client` (repo `OPP-DEEP-io/deep.io`).
- ✅ **Transport locked and implemented** — SignalR (JSON) over ASP.NET Core, not just decided.
- ✅ **Authoritative 25 Hz server tick loop** — `GameLoop` + `GameWorld`, the 8-stage pipeline from §6.
- ✅ **§3.1 compliance in code** — hand-rolled `Camera` (no `Camera2D`) and hand-rolled `Collision` (no `CheckCollision*`); raylib used only for window + primitives.
- ✅ **Gameplay** — WASD movement, mouse aim, firing; bullets damage shapes and tanks; scoring; shapes maintained/respawned; tanks respawn.
- ✅ **Verified end-to-end** — headless SignalR test client (join → move → shoot → collide, PASS) plus a live single-client run on Windows.
- ⚠️ **Framework** — currently targets **net9.0** (only .NET 9/10 SDKs on the dev machine). Plan target is net8.0: one-line switch in `Directory.Build.props` once the .NET 8 SDK is installed.

**Open before the Part 1 prototype demo (§11 phase 3):**

- 🟡 **2+ clients in one arena** — single client confirmed; multi-client run not yet done.
- ⏳ **Linux run** — Windows only so far (§5 / §12 require Windows *and* Linux).
- ⏳ **Send the §3.1 raylib argument to the lecturer in writing** — before the demo.
- ⏳ **Initial UML** (≥10 classes, ≥2 packages) + **Use Case diagram** (§11 phase 2), then the **Bridge refactor** that confines raylib to one file.

---

## 1. Hard constraints (non-negotiable, from the course spec)

| # | Constraint |
|---|---|
| 1 | 2D **multiplayer network** game. Team of 4. |
| 2 | **No game engines / frameworks** (Unity, Godot, MonoGame, etc.). |
| 3 | Language: **C#** (Java/TypeScript also allowed — we chose C#). |
| 4 | Desktop or web app. We choose **desktop**. |
| 5 | **Client-server architecture**, using WebSockets / SignalR / REST, payloads in **JSON or XML**. |
| 6 | 2D graphics via **primitive drawing APIs** — `System.Drawing` (GDI+) is explicitly named. No sprite/engine libs. |
| 7 | Map elements / enemies **may** be persisted to a database (optional, but cheap credibility). |
| 8 | Every defended pattern needs: problem description, justification, **UML class diagram before + after**, key code fragment, proof the pattern-specific requirement is met. |
| 9 | Each pattern must be demonstrable through a **working `main()`**. |
| 10 | Diagrams must come from **MagicDraw**, cleanly laid out. |
| 11 | Patterns **cannot repeat** across the team. |
| 12 | Class diagram: ≥10 classes / ≥2 packages at start → **≥20 classes** (part 1) → **≥40 classes** (part 2). |
| 13 | A **working prototype** (client-server comms + graphics + controls) must exist and be shown to the lecturer *before* patterns are applied. |

**Pattern pools**
- **Part 1 (12):** Singleton, Factory, Abstract Factory, Strategy, Observer, Builder, Prototype, Decorator, Command, Adapter, Facade, Bridge → exactly 4 students × 3.
- **Part 2 (11):** Template Method, Iterator, Composite, Flyweight, State, Proxy, Chain of Responsibility, Interpreter, Mediator, Memento, Visitor → **11 patterns for 12 slots**. ⚠️ Ask the lecturer early whether one student may do 2, or whether an unused Part-1 pattern can fill the gap.

---

## 2. Assumptions to confirm with the team/lecturer

1. The team is mixed Windows/Linux, so the client must be **cross-platform**. Rendering goes through **raylib** (`Raylib-cs`), not `System.Drawing`. §3.1 defines the allowed raylib API subset and the compliance argument — read it before writing any render code.
2. Report language: **Lithuanian** (pattern names stay in English).
3. Target: .NET 8, C# 12.
4. Server runs locally during development; Azure deploy optional (spec allows either).

---

## 3. Tech stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | .NET 8 | LTS, `Lazy<T>`, records, `System.Text.Json`. |
| Server transport | **ASP.NET Core + SignalR** (JSON) | Explicitly permitted by the spec; removes framing/heartbeat plumbing. Raw `System.Net.WebSockets` is the alternative if lower latency or binary framing is wanted — decide once, in week 1. |
| Client window + input | **raylib** via `Raylib-cs` | Cross-platform on Windows/Linux/macOS, and the NuGet package ships the native binaries — no system install on anyone's machine. Used strictly as a windowing + primitive-drawing layer; see the allowed/banned API list in §3.1. |
| Client rendering | raylib primitives: `DrawCircleV`, `DrawPoly`, `DrawLineEx`, `DrawRectanglePro`, `DrawTriangleFan` | diep.io is entirely flat vector shapes — no textures, sprites or asset pipeline anywhere in the project. |
| Text rendering | `DrawText` with raylib's built-in font | Works out of the box. This is raylib's main practical advantage over SDL2, which has no text API at all. |
| Serialization | `System.Text.Json` | JSON is spec-approved. |
| Persistence (optional) | EF Core + SQLite | Store map layouts, shape definitions, leaderboard history. Enough to satisfy req. 7 without infra work. |
| Modelling | MagicDraw | Required for diagrams. |
| Source control | Git, one repo, feature branches per student | Patterns are individually graded; keep authorship traceable in commit history. |

**Do not** add: MonoGame, FNA, Unity, Godot, Box2D or any physics library, any ECS framework. Hand-rolled math and collision only.

### 3.1 Rendering choice: raylib, kept on a short leash

raylib is the team's call, and it is workable — but the compliance risk is real, so it has to be managed deliberately rather than ignored.

Requirement 8 reads *"2D grafikai naudoti **primityvią grafiką, pvz.** System.Drawing (C#), javax.swing (Java) bibliotekas"* — "**for example**", so the list is illustrative, not closed. Requirement 3 bans frameworks. The test a lecturer actually applies is: **are you drawing primitives yourself, or is the library playing the game for you?**

raylib can be either, depending on which functions you call. So decide that up front and hold the line.

#### Allowed raylib API

| Area | Functions |
|---|---|
| Window / loop | `InitWindow`, `CloseWindow`, `WindowShouldClose`, `SetTargetFPS`, `SetTraceLogLevel` |
| Frame | `BeginDrawing`, `EndDrawing`, `ClearBackground`, `GetFrameTime` |
| Primitives | `DrawCircleV`, `DrawPoly`, `DrawLineEx`, `DrawRectanglePro`, `DrawTriangleFan` |
| Text | `DrawText`, `MeasureText` |
| Input | `IsKeyDown`, `IsKeyPressed`, `GetMousePosition`, `IsMouseButtonDown`, `GetMouseWheelMove` |

That is the entire dependency surface. Roughly 20 functions.

#### Banned raylib API — write these yourself

| Banned | Why | Write instead |
|---|---|---|
| `Camera2D`, `BeginMode2D`, `GetScreenToWorld2D` | A camera handed to you is the clearest "framework did it" signal | `Camera` class: world→screen transform, zoom, viewport culling. ~30 lines. |
| `CheckCollisionCircles`, `CheckCollisionRecs`, `CheckCollisionCircleRec`, `CheckCollisionPointCircle` | **Collision logic is an explicitly graded requirement** (spec part 1, req. 2) | `Collision` static class: circle-circle, circle-AABB, swept circle for fast bullets. |
| `LoadTexture`, `DrawTexture*`, `LoadImage` | Sprites imply an asset pipeline | Nothing — every diep.io entity is a flat polygon or circle. You lose nothing. |
| `InitAudioDevice`, `LoadSound`, `PlaySound` | Pulls in a whole engine subsystem for zero marks | Skip audio entirely. It is not in the rubric. |
| raymath helpers (`Vector2MoveTowards`, `Lerp`, `Vector2Rotate`, …) | Free game math | `MathUtil` on top of `System.Numerics.Vector2`. |
| raygui (`GuiButton`, `GuiSlider`, …) | An entire UI framework | Your own HUD from primitives + `DrawText`. |
| Anything 3D, models, shaders, `RenderTexture` | Obviously out of scope | — |

Every banned item is something the rubric wants to see *you* write. Skipping them costs you build time and gains you marks.

#### Enforce it architecturally, not by discipline

Put the entire raylib dependency behind the Bridge implementor from §8:

- `using Raylib_cs;` may appear in **exactly two files**: `DiepClone.Client/Rendering/RaylibRenderApi.cs` and `DiepClone.Client/Input/RaylibInputSource.cs`.
- `DiepClone.Server` and `DiepClone.Shared` must not reference the `Raylib-cs` package **at all** — enforce with project references, not convention.
- Add a build-failing check (a unit test or a CI `grep -rl "Raylib_cs"`) that trips if the namespace leaks anywhere else.

This is worth doing for its own sake, and it also gives you the defence answer.

#### The line to say at defence

> *"raylib is our window and primitive-drawing layer only. Our entire use of it is these ~20 functions in one class — here is the file. The camera, all collision detection, interpolation, the entity model and the game loop are ours. We deliberately banned raylib's collision and camera helpers because implementing them is part of the assignment."*

Then open `RaylibRenderApi.cs`. A 120-line file with 20 raylib calls in it ends the conversation.

**Send this argument to the lecturer in writing before the prototype demo.** It is the one choice in the plan that could be rejected, and a rejection in week 8 means rewriting the render layer.

#### raylib practical notes

- ⚠️ **raylib is not thread-safe — every raylib call must be on the main thread.** SignalR delivers snapshots on background threads, so push them into a `ConcurrentQueue<Snapshot>` and drain it inside the render loop. Getting this wrong produces crashes that look random. It also pairs naturally with the thread-safe Singleton work in §8.
- `DrawPoly(center, sides, radius, rotation, color)` draws **regular** polygons — which is exactly what diep.io squares, triangles and pentagons are. Use `DrawTriangleFan` for anything irregular.
- `Raylib-cs` uses `System.Numerics.Vector2`, so `DiepClone.Shared` math types pass straight through with no conversion layer.
- Silence the startup log spam: `Raylib.SetTraceLogLevel(TraceLogLevel.Warning);`
- Pin the `Raylib-cs` version in `Directory.Packages.props` and record the matching native raylib version in the README — the bindings track upstream raylib releases and APIs do shift between majors.
- On Linux, raylib needs X11/Wayland present. Another reason the server must never reference it: headless bots, load tests and CI would break.

## 4. Solution layout

```
DiepClone.sln
├── DiepClone.Shared/          # protocol DTOs, vector math, shared enums, IEntity contracts
├── DiepClone.Server/          # authoritative simulation, networking, persistence
│   ├── Net/                   # SignalR hub, packet pipeline
│   ├── World/                 # arena, spatial index, tick loop
│   ├── Entities/              # tanks, projectiles, shapes, walls
│   ├── Combat/                # damage pipeline, collision
│   ├── AI/                    # bot strategies
│   ├── Modes/                 # game modes
│   └── Persistence/           # EF Core context + repositories
├── DiepClone.Client/          # raylib window + loop, render backends, input, HUD
└── DiepClone.PatternDemos/    # console app: ONE main() that runs an isolated demo per pattern
```

`DiepClone.PatternDemos` exists specifically for spec requirement 9 ("demonstrated through a working `main()`"). It must call the **real production classes**, not copies. Structure it as:

```
> dotnet run --project DiepClone.PatternDemos -- singleton
> dotnet run --project DiepClone.PatternDemos -- prototype
> dotnet run --project DiepClone.PatternDemos -- flyweight --count 100000
```

Each demo prints the evidence the lecturer asks for (memory addresses, timings, chain traces, state transitions).

---

## 5. Game scope

**What "diep.io clone" means here:** top-down arena shooter. Player controls a circular tank with barrels, moves with WASD, aims with the mouse, fires bullets. The arena is populated with passive polygon shapes (Square, Triangle, Pentagon, Alpha Pentagon) worth XP. XP → levels → stat upgrade points (8 stats) and class evolution at levels 15/30/45.

### MVP prototype (must exist before any pattern work)

> **Status 2026-09-10:** implemented; single client verified on Windows. See §0.

- [x] Server tick loop at 25 Hz, authoritative. — `GameLoop` + `GameWorld`.
- [~] 2+ clients connect, see each other move in real time. — server broadcasts to all; **1 client verified, 2-client run pending**.
- [x] Tank movement + mouse aim + shooting.
- [x] Bullets collide with shapes and tanks; HP and damage applied.
- [x] Shapes respawn.
- [x] Rendered with raylib primitives: circles for tanks, rectangles for barrels, polygons for shapes, HP bars.
- [ ] Verified running on **both Windows and Linux** before any pattern work starts. — **Windows only so far**.

### Full scope (grown across both parts)
- 8 upgradable stats: Health Regen, Max Health, Body Damage, Bullet Speed, Bullet Penetration, Bullet Damage, Reload, Movement Speed.
- Tank classes: Basic → Twin, Sniper, Machine Gun, Flank Guard → Triple Shot, Quad Tank, Assassin, Destroyer, Overseer (drones), Trapper (traps).
- Projectile kinds: Bullet, Trap, Drone (AI-controlled, follows owner's cursor).
- Game modes: FFA, Team Deathmatch, Domination.
- Leaderboard, minimap, spawn protection, respawn.
- Bots to fill the arena (also the natural home for the Strategy pattern).

---

## 6. Architecture

### Server tick loop
```
every 40 ms (25 Hz):
  1. drain input queue (commands from clients)
  2. apply commands to entities
  3. integrate physics (position += velocity * dt, drag)
  4. spatial index rebuild / update (uniform grid or quadtree)
  5. collision detection + damage pipeline
  6. spawn/despawn (shapes, respawns)
  7. AI update (bots, drones)
  8. build per-client snapshot (viewport culling) and broadcast
```

### Client loop
- Render at 60 FPS with interpolation between the last two server snapshots (~80 ms buffer).
- Send input intent at 25–30 Hz: `{ moveX, moveY, aimAngle, firing, autoFire }`.
- Client-side prediction for own tank movement, server reconciliation on mismatch. (This is also the justification for `Command.Undo()` — rollback.)

### Networking contract (JSON)

Client → Server:
```jsonc
{ "t": "input", "seq": 412, "mx": 1, "my": 0, "aim": 1.57, "fire": true }
{ "t": "upgrade", "stat": "BulletDamage" }
{ "t": "evolve",  "class": "Sniper" }
{ "t": "console", "cmd": "spawn pentagon 5 at 100 200" }   // Interpreter pattern
```

Server → Client:
```jsonc
{
  "t": "snapshot", "tick": 8123, "ack": 412,
  "entities": [
    { "id": 7, "k": "tank",  "x": 120.5, "y": 88.0, "r": 1.2, "hp": 40, "max": 50, "lvl": 12, "cls": "Twin", "team": 1 },
    { "id": 9, "k": "shape", "x": 300.0, "y": 210.0, "r": 0.4, "sh": "pentagon", "hp": 100 },
    { "id": 11,"k": "bullet","x": 130.0, "y": 90.0,  "own": 7 }
  ],
  "leaderboard": [ { "name": "T", "score": 4200 } ]
}
```

Keep field names short — snapshots are the bandwidth hot path.

---

## 7. Class-count roadmap

| Milestone | Target | Composition |
|---|---|---|
| Initial diagram | ≥10 classes, ≥2 packages | `Entities` (Entity, Tank, Bullet, Shape, Barrel), `Net` (GameHub, Session, PacketDto), `World` (Arena, GameLoop) |
| Part 1 end | ≥20 classes | + factories, builders, decorators, commands, strategies, observers, bridge renderers |
| Part 2 end | ≥40 classes | + damage-chain handlers, visitors, iterators, states, flyweight factory, composites, mementos, proxies, interpreter AST nodes |

Realistic final count is 60+ — the AST nodes and chain handlers alone add ~15. Every class in the diagram must carry **attributes and methods**, not just a name box.

---

## 8. Part 1 — pattern assignments

Four students, three patterns each, aligned with subsystem ownership so nobody blocks anyone.

### Student A — Networking & Session

**Singleton** — *thread-safe required*
- Class: `GameWorld` (or `ServerRegistry`) — single entity registry touched by both the network thread and the tick thread.
- Implementation: `private static readonly Lazy<GameWorld> _instance = new(() => new GameWorld(), LazyThreadSafetyMode.ExecutionAndPublication);`
- Proof for the report: demo spawns 200 `Task`s hitting `.Instance`, an `Interlocked.Increment` counter in the private constructor prints `1`. Also show the naive non-locking version failing under the same test. Be ready to rewrite it as double-checked locking with `volatile` at defence.

**Facade** — *≥2 client classes, ≥3 subsystem classes*
- `GameSessionFacade` with `JoinMatch()`, `LeaveMatch()`, `SendInput()`.
- Subsystems (≥3): `MatchmakingService`, `ArenaLoader`, `PlayerRepository`, `SnapshotBroadcaster`.
- Clients (≥2): `RaylibGameClient`, `BotClient` (headless, also useful for load testing).

**Adapter** — *Adapter and Adaptee must have different method counts*
- Adaptee: `LegacyStatsExporter` — a deliberately awkward XML-based stats module with 6 methods (`OpenFile`, `WriteHeader`, `WriteRow`, `WriteFooter`, `Flush`, `Close`).
- Target interface: `IMatchReporter` with 2 methods (`Report(MatchResult)`, `Dispose()`).
- Adapter: `LegacyStatsAdapter` collapses the 6-call sequence into 2. Method-count difference is 6 vs 2 — state this explicitly in the report table.

### Student B — Entities & Spawning

**Factory** — *≥3 classes in the family*
- `ShapeFactory.Create(ShapeKind)` → `Square`, `Triangle`, `Pentagon`, `AlphaPentagon` (4 products).
- Justification: shape spawning frequency/weights are data-driven; the arena spawner must not know concrete types.

**Abstract Factory** — *≥2 concrete factories, each family ≥3 products*
- `IArenaFactory` → `FfaArenaFactory`, `MazeArenaFactory`, `DominationArenaFactory`.
- Product family (3 types): `IObstacle`, `IShapeSpawner`, `ISpawnPointProvider`.
- So: `MazeWall`/`OpenFieldMarker`, `MazeShapeSpawner`/`FfaShapeSpawner`, `MazeSpawnPoints`/`FfaSpawnPoints`.

**Builder** — *≥2 concrete builders*
- `ITankBuilder` with `SetHull()`, `AddBarrel()`, `SetStats()`, `SetAppearance()`, `Build()`.
- Concrete: `SniperTankBuilder`, `TwinTankBuilder`, `OctoTankBuilder` (3).
- `TankDirector.Construct(builder)` drives the sequence. Justification: tank classes differ in barrel count/angle/recoil/stat multipliers — telescoping constructors are unmanageable.

### Student C — Combat & Projectiles

**Prototype** — *compare deep vs shallow copy; print memory addresses*
- `Bullet` holds a reference-typed `BulletStats` (damage, penetration, speed) and a `List<StatusEffect>`.
- Each `Barrel` owns a prototype bullet, cloned on every shot (`ShallowClone()` / `DeepClone()`).
- Report evidence — print for original and both clones:
  - identity hash: `RuntimeHelpers.GetHashCode(obj)`
  - raw address (note in the report that the GC may relocate objects):
    ```csharp
    static unsafe IntPtr AddressOf(object o) {
        TypedReference tr = __makeref(o);
        return **(IntPtr**)(&tr);
    }
    ```
  - Then mutate `clone.Stats.Damage` and show the shallow clone corrupts the original while the deep clone does not.
- Be ready to switch between `MemberwiseClone()` and manual deep copy live at defence.

**Decorator** — *≥3 decoration levels*
- Component: `IProjectile`. Concrete: `BasicBullet`.
- Decorators: `PenetratingProjectile`, `ExplosiveProjectile`, `HomingProjectile`, `PoisonProjectile`.
- Runtime composition: `new HomingProjectile(new ExplosiveProjectile(new PenetratingProjectile(new BasicBullet())))` — 3 wrapping levels, driven by the player's upgrade choices. Print the resulting `Describe()` chain in the demo.

**Command** — *must support `undo()`*
- `ICommand { Execute(); Undo(); }`
- Commands: `MoveCommand`, `RotateCommand`, `FireCommand`, `UpgradeStatCommand`, `EvolveClassCommand`.
- Two genuinely useful undo cases (say this in the justification — it's what raises the mark):
  1. `UpgradeStatCommand.Undo()` refunds a skill point (respec).
  2. Server keeps a ring buffer of the last N ticks of movement commands; on reconciliation mismatch it undoes and replays — real rollback netcode.
- `CommandHistory` (invoker) holds the stack.

### Student D — Rendering, AI & Events

**Strategy** — *≥4 strategy classes*
- `IMovementStrategy { Vector2 ComputeVelocity(BotContext ctx); }`
- Concrete (5): `ChaseNearestTargetStrategy`, `FleeWhenLowHpStrategy`, `OrbitTargetStrategy`, `FarmShapesStrategy`, `PatrolWaypointsStrategy`.
- Swapped at runtime by `BotController` based on HP/level/threat. Also reused for drone AI.

**Observer** — *must be demonstrated with a sequence diagram*
- Subject: `GameEventBus` / `Entity` raising `EntityDamaged`, `EntityDestroyed`, `PlayerLeveledUp`.
- Observers (4): `LeaderboardObserver`, `XpAwardObserver`, `AchievementObserver`, `NetworkBroadcastObserver`.
- Sequence diagram to draw in MagicDraw: `Bullet → CollisionResolver → Tank.ApplyDamage → GameEventBus.Notify → [each observer]`. Do **not** use C# `event`/`delegate` as the only implementation — implement the explicit `IObserver`/`ISubject` interfaces so the diagram matches the code (you can mention `event` as the language-native alternative when asked).

**Bridge** — *≥2 abstractions, ≥2 implementations; explain the difference from Strategy and Adapter*
- Abstraction hierarchy: `Renderable` → `TankRenderable`, `ShapeRenderable`, `ProjectileRenderable`.
- Implementor: `IRenderApi` → `RaylibRenderApi`, `AsciiConsoleRenderApi`, `SvgExportRenderApi`.
- Primitive ops the implementor must expose: `DrawCircle`, `DrawPolygon`, `DrawRect`, `DrawLine`, `DrawText`.
- **This pattern is doing double duty.** It is self-justifying as a design choice (entity hierarchy and rendering backend vary independently), *and* it is the mechanism that contains raylib to two files per §3.1. `AsciiConsoleRenderApi` is not a toy: it proves the entity code has zero raylib coupling, and it lets the `PatternDemos` console harness render a real game state without opening a window. Say both things at defence.
- Primitive ops on `IRenderApi`: `DrawCircle`, `DrawPolygon`, `DrawRect`, `DrawText`.
- Defence answer to memorise:
  - **Bridge** — structural, decided at design time, splits *two independently varying hierarchies* so both can grow.
  - **Strategy** — behavioural, one algorithm slot swapped at runtime inside a single class.
  - **Adapter** — retrofits an *existing incompatible* interface; Bridge is designed in up front.

---

## 9. Part 2 — pattern assignments

⚠️ 11 patterns, 12 slots. Draft below gives A/B/C three each and D two — confirm with the lecturer.

### Student A — Networking & Session

**Chain of Responsibility** — *chain ≥4 links*
- Inbound packet pipeline: `RateLimitHandler → AuthenticationHandler → SchemaValidationHandler → AntiCheatHandler → CommandDispatchHandler` (5 links).
- Each handler can consume, reject, or pass. Demo prints which link stopped a malformed/spam packet.

**Proxy** — *must switch realization at defence (security / added functionality / delayed creation); report speed and memory figures*
- Build all three so switching is trivial:
  - `CachingLeaderboardProxy` — memoises DB queries (added functionality).
  - `AdminConsoleProxy` — permission check before `/kill`, `/spawn` (security).
  - `LazyArenaChunkProxy` — map chunk loaded on first access (delayed creation).
- Report: `Stopwatch` for 10 000 leaderboard reads with vs without cache; `GC.GetTotalMemory(true)` before/after lazy chunk load.

**Mediator** — *mediate between ≥3 classes; explain the difference from Observer*
- `MatchMediator` coordinating `PlayerSession`, `ChatService`, `SpawnManager`, `LeaderboardService`, `TeamBalancer` — colleagues talk only to the mediator.
- Defence answer: **Mediator** centralises *bidirectional many-to-many* coordination and knows all colleagues; **Observer** is *one-to-many broadcast* where the subject knows nothing about who is listening.

### Student B — World & Modes

**Flyweight** — *measure speed and memory*
- Intrinsic (shared): `ShapeDefinition` — polygon vertex array, colour, base HP, XP value, body damage.
- Extrinsic (per-instance): position, rotation, current HP, id.
- `ShapeDefinitionFactory` returns pooled instances.
- Benchmark to include in the report: spawn 100 000 shapes with and without flyweight; record `GC.GetTotalMemory(true)` delta and `Stopwatch` spawn time. Expect a large memory drop — put the table in the report.

**Template Method** — *≥2 concrete classes, `sealed`*
- `abstract class GameModeBase` with `public void RunRound()` containing: `SetupArena() → SpawnPlayers() → while(!IsRoundOver()) Tick() → ResolveWinner() → PersistResults()`.
- Hooks abstract/virtual; concrete `sealed class FfaMode`, `sealed class TeamDeathmatchMode`, `sealed class DominationMode`.

**Composite** — *must switch visibility/safety at defence; explain the difference from Decorator*
- Tree: `IGameObject` → leaf `Barrel`, `Hull`, `Drone`; composite `TankAssembly`, `SquadGroup`, `SceneNode`. `Update()`, `Render()`, `GetTotalMass()` recurse.
- Prepare **both** variants and be able to swap:
  - *Transparent*: `Add`/`Remove` declared on `IGameObject` (uniform, unsafe for leaves).
  - *Safe*: `Add`/`Remove` only on `Composite` (type-safe, requires casting).
- Defence answer: **Composite** = part-whole tree, one-to-many children, uniform treatment. **Decorator** = one-to-one wrapping that adds responsibility without changing the tree structure.

### Student C — Entity Lifecycle

**State** — *≥4 states, state diagram, explain difference from Strategy*
- `TankState`: `Spawning → Invulnerable (spawn protection) → Active → Reloading → Dead → Respawning` (6 states).
- Each state controls whether input is accepted, whether damage applies, and what gets rendered. Transitions triggered by timers and damage events.
- Draw the state machine diagram in MagicDraw.
- Defence answer: **State** objects know about and trigger transitions to each other; the change is driven internally by the object's lifecycle. **Strategy** is chosen externally by the client and the strategies are mutually unaware.

**Visitor** — *≥3 visitor classes*
- `IEntityVisitor` with `Visit(Tank)`, `Visit(Shape)`, `Visit(Bullet)`, `Visit(Wall)`.
- Visitors (4): `CollisionVisitor`, `SnapshotSerializationVisitor`, `StatisticsVisitor`, `RenderVisitor`.
- Pairs naturally with the Composite tree — traverse the scene graph and apply each visitor.

**Memento** — *secure restore; other classes must not access the data*
- Originator: `PlayerBuild` (level, XP, 8 stat allocations, tank class).
- Memento: `private sealed class BuildMemento : IBuildMemento` nested **inside** `PlayerBuild`. `IBuildMemento` is an empty marker interface — the caretaker can hold it but cannot read a single field.
- Caretaker: `RespecHistory` — supports undoing a stat allocation.
- In the report, show a compile error screenshot proving an outside class cannot read the memento's state.

### Student D — Collections & Tooling

**Iterator** — *iterate through ≥3 different data structures*
- One interface `IEntityIterator { bool HasNext(); Entity Next(); }` implemented over:
  1. `EntityPool` — flat array with holes (skips inactive slots).
  2. `QuadTree` — depth-first spatial traversal, plus a bounded `IterateRegion(rect)` variant.
  3. `Dictionary<int, PlayerSession>` — hash map.
  4. `LinkedList<ScoreEntry>` — leaderboard ordering.
- Show identical client code (`RenderAll`) consuming all four.

**Interpreter** — *used for console commands*
- Grammar for an in-game dev console:
  ```
  spawn <shape> <count> at <x> <y>
  kill all <shape|player> where hp < <n>
  set <player> <stat> <value>
  heal <player> <amount>
  ```
- AST: `IExpression { object Interpret(GameContext ctx); }` → `SpawnExpression`, `KillExpression`, `SetExpression`, `WhereExpression`, `NumberLiteral`, `IdentifierExpression`, `AndExpression`.
- `CommandParser` (tokeniser + recursive descent) builds the tree; the client sends `{"t":"console","cmd":"..."}` and the server interprets it. This adds ~8 classes toward the 40-class target.

---

## 10. Report structure (per defended pattern)

Repeat this block for every pattern — the rubric awards points line by line:

1. **Problem** — what breaks in the game without the pattern (concrete, game-specific; not textbook prose).
2. **Justification** — why *this* pattern and not the nearest alternative.
3. **UML before** — the naive design (MagicDraw).
4. **UML after** — with the pattern applied (MagicDraw).
5. **Key code fragment** — the interface + one concrete implementation + the call site. Not the whole file.
6. **Specific requirement fulfilled** — quote the requirement (e.g. "Decorator — ≥3 decoration levels") and point at the exact evidence.
7. **Demonstration** — command to run in `DiepClone.PatternDemos` + expected console output.

Plus, once per report: project description with screenshots, game requirements with **acceptance criteria** (levels, lives, player stats, enemy/artifact stats, map generation, object generation, movement logic, collision logic), Use Case diagram, and the full class diagram.

---

## 11. Milestones

| Phase | Deliverable |
|---|---|
| **1. Setup** | Topic registered in Moodle, team roles assigned, repo + solution skeleton, transport decision (SignalR vs raw WS) locked. <br>🟡 **repo + skeleton ✅, SignalR ✅; Moodle topic / team roles — track separately.** |
| **2. Design** | Initial UML: ≥10 classes, ≥2 packages. Use Case diagram. Requirements doc with acceptance criteria. |
| **3. Prototype** | Two clients connect, move, shoot, collide with shapes, raylib rendering, running on Windows *and* Linux. **Demo to lecturer**, with the §3.1 raylib argument already agreed in writing. No patterns yet — the "before" diagrams come from this codebase. <br>🟡 **built; single-client verified on Windows. Pending: 2-client run, Linux run, written raylib argument, lecturer demo.** |
| **4. Part 1 patterns** | Each student implements 3. Diagram grows to ≥20 classes. `PatternDemos` harness built. |
| **5. Part 1 defence** | Report in Moodle beforehand. Use the cumulative system — show finished patterns in lab sessions instead of saving everything for the deadline. |
| **6. Gameplay depth** | Tank classes, upgrades, game modes, bots, leaderboard, persistence. |
| **7. Part 2 patterns** | Each student implements 3. Diagram grows to ≥40 classes. Benchmarks captured for Flyweight and Proxy. |
| **8. Part 2 defence** | Updated report; **full multiplayer mode must work** (worth 2 points on its own). |

---

## 12. Risks and gotchas

- **raylib is the highest-risk decision in this plan.** Get §3.1 agreed with the lecturer in writing before the prototype demo. A rejection in week 8 means rewriting the render layer.
- **The banned-API list only works if it is enforced by the build.** "We agreed not to use `CheckCollisionCircles`" decays under deadline pressure. A failing CI check does not.
- **raylib calls must all be on the main thread.** Network callbacks arrive on background threads — queue and drain, never draw from a SignalR handler.
- **Keep `Raylib-cs` out of `DiepClone.Server` and `DiepClone.Shared`.** The server must run headless, or CI, bots and load tests break.
- **Verify a clean clone builds on the *other* OS.** Raylib-cs ships natives via NuGet, which usually just works — but have the Linux member build the Windows member's branch from scratch once, early.
- **`Lazy<T>` singleton looks too easy.** Lecturers routinely ask you to rewrite it as double-checked locking with a `volatile` field. Have that version ready in a comment.
- **Observer via `event`/`delegate`** doesn't match a classic UML Observer diagram. Implement the explicit interfaces.
- **Patterns must be load-bearing.** A Factory that returns one type, or a Strategy with two near-identical classes, reads as bolted-on. Every pattern here is placed where the game genuinely varies.
- **GC relocation** means printed addresses can shift between runs — say so in the Prototype section rather than being caught out.
- **Snapshot bandwidth**: culling by viewport from day one, otherwise 100 shapes × 25 Hz × 4 clients kills the demo on the projector.
- **Don't refactor away the "before" state.** Tag the prototype commit (`git tag prototype-before-patterns`) so the "before" UML diagrams are defensible and reproducible.
- **Part 2 has 11 patterns for 12 slots** — resolve this with the lecturer in week 1, not the week of defence.

---

## 13. Rubric checklist

**Part 1 (10 pts)**
- [ ] Report in Moodle, description + requirements *(0.5)*
- [ ] Use Case diagram *(0.5)*
- [ ] Class diagram matching the prototype *(1)*
- [ ] Working prototype *(2)* — 🟡 built & single-client verified on Windows; pending 2-client run, Linux run, and the lecturer demo.
- [ ] Pattern justification *(1)*
- [ ] Before/after UML *(1)*
- [ ] Pattern code *(1)*
- [ ] Live demonstration *(1)*
- [ ] Pattern-specific requirements met *(1)*
- [ ] Answers to questions *(1)*

**Part 2 (10 pts)**
- [ ] Updated report in Moodle *(1)*
- [ ] Class diagram ≥40 classes *(1)*
- [ ] Game working in multiplayer mode *(2)*
- [ ] Pattern justification *(1)*
- [ ] Before/after UML *(1)*
- [ ] Pattern code *(1)*
- [ ] Live demonstration *(1)*
- [ ] Pattern-specific requirements met *(1)*
- [ ] Answers to questions *(1)*
