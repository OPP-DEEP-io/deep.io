# 2 dalis: State, Interpreter, Mediator — įgyvendinimo planas

Autorius: tas, kuris 1 dalyje darė Strategy, Observer, Command.
Šaka: `State-Mediator-Interpreter` (nuo `main`, kaip ir ankstesnės šablonų šakos).

## 0. Bendra idėja

Visi trys šablonai sprendžia vieną naują žaidimo funkciją ir remiasi vienas kitu.

| Šablonas | Kur | Kokią problemą sprendžia |
|---|---|---|
| **State** | serveris, `Tank` | Dabar mirus tankas atgimsta tą pačią akimirką (`GameWorld.Cleanup` → `t.Respawn(...)`). Nėra nei mirties ekrano, nei apsaugos atgimus, nei „hyper regen“ po kovos. Nuo būsenos turi priklausyti visas tanko elgesys: judėjimas, šaudymas, žala, regeneracija. |
| **Interpreter** | serveris, `Scripting/` | Žaidimo valdymo komandos per konsolę: `give 500 xp to me; state all`. Jas galima rašyti serverio konsolėje (stdin) ir žaidimo konsolėje (klavišas `` ` ``). |
| **Mediator** | klientas, `Hud/` | Atsiradus žaidimo konsolei ir mirties ekranui, HUD dalys turi derinti savo veiksmus. Kai atidaryta konsolė, WASD rašo tekstą, o ne vairuoja tanką, Backspace trina simbolį, o ne atšaukia upgrade, o mirus įvestis nesiunčiama. Be tarpininko kiekvienas komponentas turėtų žinoti apie visus kitus. |

Siūloma tvarka: **State → Interpreter → Mediator**. Interpreter komanda `state` rodo State būsenas, o Mediator reaguoja į State (mirties ekranas) ir į Interpreter (konsolės įvestis ir atsakymai).

Kiekvienam šablonui ataskaitoje reikia (PDF 2 sk. 9 p.): problemos aprašymo ir pagrindimo, UML klasių diagramos **prieš** ir **po**, esminio kodo fragmento ir specifinių reikalavimų įvykdymo. Kiekvieno šablono demonstracija turi būti per `main()`. Serveryje jau yra `--patterns-demo` (`Diagnostics/PatternDemo.cs`), tad prie jo pridedame savo `Demonstrate...` metodus.

---

## 1. State — tanko gyvavimo ciklas

### 1.1 Reikalavimas
> State – būsenų diagrama – ne mažiau 4 būsenų, paaiškinti skirtumą nuo Strategy.

### 1.2 Būsenos (4)

| Būsena | Juda | Šaudo | Gauna žalą | Regen | Botai ją taikosi | Išeina į |
|---|---|---|---|---|---|---|
| `SpawnProtectedState` | taip | taip | **ne** (0) | ×1 | ne | `Alive` po 3 s arba po pirmo šūvio; `Dead` tik su `ForceKill` (konsolė) |
| `AliveState` | taip | taip | taip | ×1 | taip | `Regenerating` po 5 s be žalos ir be šūvių, jei `Hp < MaxHp`; `Dead`, kai `Hp ≤ 0` |
| `RegeneratingState` | taip | taip | taip | **×4** | taip | `Alive`, kai gauna žalą, iššauna arba `Hp == MaxHp`; `Dead`, kai `Hp ≤ 0` |
| `DeadState` | ne | ne | ne | **×0** | ne | `SpawnProtected` po 3 s (`Respawn()`) |

Konstantas sudėkite į būsenų klases: `SpawnProtectionSeconds = 3`, `OutOfCombatSeconds = 5`, `RespawnDelaySeconds = 3`, `HyperRegenMultiplier = 4`.

### 1.3 Klasės (`DeepIo.Server/Entities/States/`)

```csharp
/// State. Viena tanko gyvavimo ciklo fazė; Tank (context) deleguoja jai viską, kas nuo fazės priklauso.
public abstract class TankState
{
    public abstract TankLifeState Kind { get; }          // enum Shared'e, keliauja per tinklą
    public virtual bool CanMove => true;
    public virtual bool CanFire => true;
    public virtual bool IsTargetable => true;            // botai ir kolizijos
    public virtual float RegenMultiplier => 1f;
    public virtual float SecondsLeft => 0f;              // klientui: countdown / skydo laikas

    public virtual void Enter(Tank tank) { }
    public virtual void Update(Tank tank, float dt) { }
    public virtual float FilterDamage(Tank tank, float amount) => amount;  // kiek žalos iš tikro tenka
    public virtual void OnFired(Tank tank) { }
    public virtual void OnKilled(Tank tank) => tank.TransitionTo(new DeadState());
}

public sealed class SpawnProtectedState : TankState { ... }  // _timer; FilterDamage => 0; OnFired => Alive
public sealed class AliveState : TankState { ... }           // _sinceDamage; Update => Regenerating
public sealed class RegeneratingState : TankState { ... }    // RegenMultiplier => 4; FilterDamage => Alive + amount
public sealed class DeadState : TankState { ... }            // _timer; Update => Respawn + SpawnProtected
```

- **Būsenos pačios sprendžia, į kurią būseną pereiti** (`tank.TransitionTo(new AliveState())` būsenos viduje). Tai pagrindinis skirtumas nuo Strategy (žr. 1.7).
- Būsenos turi laikmačius, todėl kiekvienam perėjimui kuriamas **naujas objektas** (`new AliveState()`). Bendrinti vieno egzemplioriaus visiems tankams negalima.
- `DeepIo.Shared/Enums.cs`: `public enum TankLifeState { SpawnProtected, Alive, Regenerating, Dead }`.
- `DeepIo.Server/Events/GameEvents.cs`: `TankStateChangedEvent(Tank Tank, TankLifeState From, TankLifeState To)`. Jį publikuoja `TransitionTo`, todėl ryšys su Observer lieka. Pvz., `NetworkBroadcastObserver` gali parodyti „X respawned“, bet tai neprivaloma.

### 1.4 Pakeitimai esamame kode (context = `Tank`)

1. **`Tank.cs`**
   - `public TankState State { get; private set; } = new SpawnProtectedState();`
   - `internal void TransitionTo(TankState next)`: išsaugoti `from`, priskirti `State = next`, iškviesti `next.Enter(this)`, publikuoti `TankStateChangedEvent` per `EventBus`.
   - `Update`: `Velocity = State.CanMove ? MoveDir * MoveSpeed : Vector2.Zero;`, tada `State.Update(this, dt)`. Regeneraciją dauginti iš `State.RegenMultiplier`. **Svarbu:** dabar `if (Hp < MaxHp) Heal(...)` gydytų ir mirusį tanką, o `DeadState.RegenMultiplier = 0` tai sutvarko.
   - `CanFire => State.CanFire && Firing && ReloadTimer <= 0f;`
   - `BeginReload()` papildomai kviečia `State.OnFired(this)` (šūvis nutraukia spawn apsaugą ir regen).
   - `public void ForceKill()`: konsolės `kill` komandai, kuri veikia ir per spawn skydą. Kviečia `Hp = 0`, publikuoja `EntityDestroyedEvent(this, null)` ir `TransitionTo(new DeadState())`.
   - **`Clone()`** (Prototype!): deep kopija turi gauti `copy.State = new SpawnProtectedState();`, kitaip klonai dalintųsi tuo pačiu laikmačiu. Patikrinimą pridėkite prie esamo `DemonstratePrototype`.
   - `ToDto()`: `St = (int)State.Kind, StT = State.SecondsLeft`.
2. **`Entity.cs`**: į `ApplyDamage` įterpti du kabliukus, kad `Tank` galėtų deleguoti būsenai, o `Shape` ir `Bullet` liktų nepakitę:
   ```csharp
   protected virtual float FilterIncomingDamage(float amount) => amount;
   protected virtual void OnKilled() { }
   ```
   `ApplyDamage` pradžioje: `amount = FilterIncomingDamage(amount); if (amount <= 0f) return false;`. Po `EntityDestroyedEvent`: `OnKilled();`. `Tank` juos perrašo: `State.FilterDamage(this, a)` ir `State.OnKilled(this)`.
3. **`GameWorld.cs`**
   - `Cleanup()`: išimti `case Tank t when t.Dead: t.Respawn(...)`, nes atgimimą dabar valdo `DeadState`.
   - `RandomPoint` iškelti į `World/SpawnPoints.cs` (`public static Vector2 Random(Random rng)`), kad `DeadState` galėtų jį kviesti.
4. **`IMovementStrategy.cs` (`BotContext.Observe`)**: `if (t == self || t.Dead || !t.State.IsTargetable) continue;`, kad botai nešaudytų į tanką su spawn skydu.
5. **`CollisionResolver.cs`**: niekas nesikeičia. Kulka į skydą pataiko (`RegisterHit`), bet žala lygi 0, nes ją nufiltruoja `FilterIncomingDamage`.
6. **`Snapshot.cs` (`EntityDto`)**: `public int St { get; init; }`, `public float StT { get; init; }`.
7. **Klientas `Renderer.cs`**: `St == Dead` tankų nepiešti; `SpawnProtected` piešti su skaidriu žiedu (skydu); `Regenerating` HP juostą nuspalvinti kitaip. Šiuos minimalius pakeitimus suderinkite su Bridge autoriumi.

### 1.5 Būsenų diagrama (MagicDraw → State Machine Diagram)

```
[●] ──► SpawnProtected
SpawnProtected ──after(3s) / fire──► Alive
SpawnProtected ──forceKill──► Dead
Alive ──after(5s) [no damage ∧ no shots ∧ Hp < MaxHp]──► Regenerating
Regenerating ──damaged / fire / [Hp = MaxHp]──► Alive
Alive ──damaged [Hp ≤ 0]──► Dead
Regenerating ──damaged [Hp ≤ 0]──► Dead
Dead ──after(3s) / Respawn()──► SpawnProtected
(bet kuri) ──player left (Esc / atsijungė)──► [◉]
```
Kiekvienai būsenai įrašykite `entry` ir `do` veiksmus (pvz., Dead: `entry / Velocity = 0, Firing = false`).

### 1.6 Demonstracija (`PatternDemo.DemonstrateState`)

Tanką sukurkite per `TankAssembler` ir, kaip ir dabar, įvykius stebėkite per `GameEventBus`. Laiką „sukite“ `tank.Update(dt)` ciklu, be tinklo.
1. Naujas tankas yra `SpawnProtected`; `ApplyDamage(50)` → HP nepasikeitė.
2. `Update` 3 s → `Alive`; `ApplyDamage(10)` → HP sumažėjo.
3. `Update` 5 s → `Regenerating`; HP kyla 4× greičiau nei `Alive`.
4. `ApplyDamage(Hp)` → `Dead`; `CanFire == false`, `Velocity == 0`, HP nesigydo.
5. `Update` 3 s → `SpawnProtected`, nauja pozicija, `Score` perpus.
6. Atspausdinti perėjimų žurnalą (iš `TankStateChangedEvent`), jį ir įkelti į ataskaitą.

### 1.7 State ir Strategy skirtumas (gynimui)

Abu šablonai turi tą pačią UML struktūrą (context → abstraktus tipas → konkretūs), bet skiriasi tikslas ir tai, **kas keičia** objektą.

| | Strategy (mūsų `IMovementStrategy`) | State (`TankState`) |
|---|---|---|
| Kas parenka | **Klientas iš išorės**: `BotBrain.SelectStrategy()` | **Pačios būsenos**: `AliveState` sako `tank.TransitionTo(new DeadState())` |
| Ar konkretūs tipai žino vienas kitą | Ne, strategijos nepriklausomos | Taip, būsena žino savo įpėdinius (perėjimų lentelė) |
| Kas keičiasi | Vienas algoritmas (`ComputeVelocity`) | **Visas objekto elgesys**: judėjimas, šaudymas, žala, regen, piešimas. Objektas tarsi „pakeičia klasę“ |
| Istorija | Nėra (strategija parenkama kas 0,05 s iš aplinkos) | Yra: fazė trunka, turi laikmatį, perėjimai turi eilę |
| Ar klientas žino konkrečias klases | Taip (`BotBrain` kuria visas 5) | Ne: `GameWorld` nežino nė vienos būsenos |

Projekte abu šablonai veikia kartu: kai botas `DeadState` būsenoje, `BotBrain` jo strategijos išvis nevykdo.

### 1.8 Ką gali liepti pakeisti gynime (pasiruoškite)
- Pridėti 5-ą būseną, pvz., `StunnedState` (po smūgio > 30 % MaxHp 1 s negali šaudyti). Reikės vienos naujos klasės, vienos eilutės `AliveState.FilterDamage` metode ir rodyklės diagramoje.
- Pakeisti perėjimo sąlygą (skydas baigiasi ir pajudėjus, ne tik iššovus): `SpawnProtectedState.Update` tikrina `tank.MoveDir`.
- Paversti būsenas bendrais vienetiniais objektais: laikmatį perkelti į `Tank.StateTimer` ir paaiškinti kompromisą.

---

## 2. Interpreter — konsolės komandos

### 2.1 Reikalavimas
> Interpreter – panaudojimas komandoms per console.

Konsolės bus dvi, o interpretatorius vienas:
- **serverio konsolė**: stdin `dotnet run --project DeepIo.Server`, administratorius;
- **žaidimo konsolė** kliente: `` ` `` arba `Enter`. Tekstas keliauja per SignalR, interpretuojamas serveryje, atsakymas grįžta į klientą. Serveris diegiamas (`deploy-server.yml`), o ten stdin nėra, todėl žaidimo konsolė reikalinga.

### 2.2 Gramatika (BNF, įdėti į ataskaitą)

```
<script>    ::= <command> { ";" <command> }
<command>   ::= "help"
              | "list"
              | "state" <target>
              | "give" <number> "xp" "to" <target>
              | "upgrade" <target> <stat> [ <number> ]
              | "heal" <target>
              | "kill" <target>
              | "bots" <number>
              | "repeat" <number> "(" <script> ")"
<target>    ::= <selector> { ( "and" | "except" ) <selector> }
<selector>  ::= "me" | "all" | "bots" | "players" | <string> | "#" <number>
<stat>      ::= "regen" | "health" | "body" | "bulletspeed" | "penetration"
              | "damage" | "reload" | "speed"
<number>    ::= <digit> { <digit> }
<string>    ::= '"' { <char> } '"'          (vardai su tarpais: "Bot Alpha")
```

Pavyzdžiai:
```
give 500 xp to me
upgrade me reload 3; state me
give 200 xp to bots except "Bot Alpha"
kill bots and "Tomas"
repeat 3 (give 100 xp to all)
bots 8
list
```

### 2.3 Klasės (`DeepIo.Server/Scripting/`)

Vardų sritis `Scripting`, ne `Console` (kitaip konfliktuotų su `System.Console`) ir ne `Commands` (ten Command šablonas).

**Abstrakti išraiška**
```csharp
/// Interpreter AbstractExpression. Kiekvienas gramatikos taisyklės mazgas moka interpretuoti save.
public interface IExpression<out T>
{
    T Interpret(ConsoleContext context);
}
```

**Terminalinės išraiškos** (gramatikos lapai):

| Klasė | `T` | Gramatika |
|---|---|---|
| `NumberLiteral` | `int` | `<number>` |
| `StatLiteral` | `StatKind` | `<stat>` |
| `MeSelector` | `IReadOnlyList<Tank>` | `"me"` |
| `AllSelector` | ″ | `"all"` |
| `BotsSelector` | ″ | `"bots"` |
| `PlayersSelector` | ″ | `"players"` |
| `NameSelector` | ″ | `<string>` |
| `IdSelector` | ″ | `"#" <number>` |

**Neterminalinės išraiškos** (turi vaikines išraiškas):

| Klasė | `T` | Vaikai |
|---|---|---|
| `UnionTarget` | `IReadOnlyList<Tank>` | kairė, dešinė (`and`) |
| `ExceptTarget` | ″ | kairė, dešinė (`except`) |
| `SequenceCommand` | `string` | `List<IExpression<string>>` (`;`) |
| `RepeatCommand` | `string` | `IExpression<int>`, `IExpression<string>` |
| `GiveXpCommand` | `string` | `IExpression<int>`, target |
| `UpgradeCommand` | `string` | target, `IExpression<StatKind>`, `IExpression<int>` |
| `HealCommand` / `KillCommand` / `ShowStateCommand` | `string` | target |
| `SetBotCountCommand` | `string` | `IExpression<int>` |
| `ListCommand` / `HelpCommand` | `string` | — |

**Infrastruktūra**
- `ConsoleContext`: interpretavimo kontekstas. Turi `IConsoleWorld World`, `Tank? Caller` (null serverio konsolėje, tada `me` → klaida) ir `bool IsAdmin`.
- `IConsoleWorld`: siaura sąsaja, kurią realizuoja `GameWorld`: `IReadOnlyList<Tank> Tanks`, `bool IsBot(Tank)`, `void SetBotCount(int)`. Dėl jos demonstracija gali naudoti netikrą pasaulį (`FakeConsoleWorld`) be singletono.
- `Token`, `TokenKind`, `ConsoleLexer`: tekstas → tokenai (žodžiai, skaičiai, `"..."`, `;`, `(`, `)`, `#`).
- `ConsoleParser`: rekursinio nusileidimo analizatorius, kuris iš tokenų stato išraiškų medį (AST). Pastaba gynimui: GoF Interpreter parsinimo neapima, šablonas prasideda nuo jau sukurto medžio. Parseris yra pagalbinis.
- `ConsoleSyntaxException`, `ConsoleRuntimeException`: klaidos su aiškiu tekstu („unknown stat 'relaod' at 3“).
- `ConsoleInterpreter`: `string Execute(string line, ConsoleContext ctx)`, t. y. lex → parse → `Interpret` → tekstas, o klaidas paverčia atsakymu.
- Kiekviena išraiška perrašo `ToString()` ir grąžina medį, pvz. `Seq(GiveXp(200, Except(Bots, Name("Bot Alpha"))), State(Me))`. Tai praverčia demonstracijai ir ataskaitai (objektų diagrama).

**Svarbu:** `GiveXpCommand` negali tik iškviesti `tank.AddScore`. Jis turi elgtis kaip `XpAwardObserver`: `AddScore`, o pakilus lygiui publikuoti `PlayerLeveledUpEvent`. Geriausia tai iškelti į bendrą pagalbinį metodą, kurį naudotų abu. Kitaip neveiks achievementai ir feed. `UpgradeCommand` eina per `tank.UpgradeHistory.Execute(new UpgradeStatCommand(...))`, todėl Backspace ją atšaukia (ryšys su Command). `KillCommand` kviečia `tank.ForceKill()` (ryšys su State).

### 2.4 Srautas ir gijos

```
Klientas: ConsolePanel ──ExecuteConsole(text)──► GameHub.ExecuteConsole(line)
   (validacija: ilgis ≤ 200, ne tuščia)          │
                                                 ▼
                         GameWorld.EnqueueConsole(connId, line)   ← ConcurrentQueue
                                                 │
            GameWorld.Update → DrainConsole() (po DrainUpgrades)  ← žaidimo ciklo gija
            ConsoleInterpreter.Execute(line, ctx) → tekstas
                                                 │
                         _consoleReplies.Enqueue((connId, tekstas))
                                                 │
     GameLoop po Update: Clients.Client(connId).SendAsync("ConsoleOutput", tekstas)
```
- Interpretuojama **tik žaidimo ciklo gijoje**, kaip ir visa kita būsenos mutacija (žr. `GameWorld` komentarą). Todėl užraktų nereikia.
- **Serverio stdin**: `World/ServerConsoleService : BackgroundService` skaito `Console.In.ReadLineAsync()` → `EnqueueConsole(ServerConsoleId, line)`. Atsakymai su `ServerConsoleId` spausdinami į stdout. Kai `ReadLine` grąžina `null` (diegime stdin nėra), servisas tyliai baigiasi.
- **Teisės:** serverio konsolė yra `IsAdmin = true`. Žaidėjams iš kliento leidžiamos tik komandos su `me` ir skaitymo komandos (`list`, `state`, `help`), nebent serveris paleistas su `--open-console` (laboratoriniams). *Pasiūlymas:* šią patikrą gali realizuoti Proxy autorius kaip protection proxy apie `ConsoleInterpreter`. Suderinkite, bet tai neprivaloma.

### 2.5 Demonstracija (`PatternDemo.DemonstrateInterpreter`)
1. Sukurti `FakeConsoleWorld` su 2 „žaidėjais“ ir 2 „botais“ (per `TankAssembler`).
2. Parsinti `give 200 xp to bots except "B1"; state all` ir atspausdinti AST (`ToString()`).
3. Interpretuoti ir `Check(...)`: B2 gavo XP, B1 negavo, išvesties tekstas teisingas.
4. `repeat 3 (give 100 xp to me)` → +300.
5. Klaidos: `give xp to me` → sintaksės klaida su pozicija; `upgrade me relaod` → nežinomas stat.
6. Gyvai: paleisti serverį ir stdin įvesti `list`, `bots 8`, `kill bots`. Žaidime atsidaryti `` ` `` ir įvesti `give 500 xp to me`.

### 2.6 Ką gali liepti pakeisti gynime
- Nauja komanda (`teleport me 0 0`, `freeze bots`): nauja išraiškos klasė, viena šaka `ConsoleParser.ParseCommand` ir eilutė `help` tekste.
- Naujas selektorius (`level > 10`): nauja terminalinė išraiška.
- Naujas operatorius (`or`): nauja neterminalinė išraiška ir gramatikos eilutė.

Todėl parseris turi būti tvarkingas: vienas `Parse<Taisyklė>()` metodas kiekvienai BNF taisyklei.

---

## 3. Mediator — kliento HUD

### 3.1 Reikalavimas
> Mediator – parodyti tarpininkavimą tarp mažiausiai 3 skirtingų klasių, mokėti paaiškinti skirtumą su Observer.

### 3.2 Problema („prieš“)
Dabar `GameplayController.RunFrame` pats skaito WASD, pelę, 1–8 ir Backspace. Pridėjus konsolę ir mirties ekraną:
- atidarius konsolę, WASD neturi vairuoti tanko, 1–8 neturi leisti taškų, o Backspace turi trinti simbolį, ne atšaukti upgrade;
- atidarius konsolę, tankas turi **sustoti**. Kitaip serveris kartos paskutinį `InputMessage` (judėk į dešinę, šauk);
- mirus įvestis nesiunčiama ir upgrade klavišai išjungti, bet konsolė turi veikti (`state me`);
- atgimus įvestis grįžta, **jei konsolė uždaryta**;
- `Esc` uždaro konsolę, jei ji atidaryta, o kitu atveju (ir mirus) grąžina į prisijungimo langą.

Jei komponentai vienas kitą kviestų tiesiogiai, kiekvienas žinotų apie kiekvieną (N×N ryšių). „Prieš“ diagramoje tai yra dabartinis `GameplayController` ir hipotetiniai tiesioginiai ryšiai.

### 3.3 Klasės (`DeepIo.Client/Hud/`)

```csharp
public enum HudEvent { ConsoleOpened, ConsoleClosed, ConsoleSubmitted, PlayerDied, PlayerRespawned, LeaveRequested }

/// Mediator. Kolegos praneša jam, kas nutiko; jis vienas žino, ką tai reiškia kitiems.
public interface IHudMediator
{
    void Notify(HudComponent sender, HudEvent hudEvent);
}

/// Colleague. Žino tik tarpininką, ne kitus komponentus.
public abstract class HudComponent
{
    protected HudComponent(IHudMediator mediator) => Mediator = mediator;
    protected IHudMediator Mediator { get; }
    public abstract void Update(HudFrame frame);   // logika, be piešimo, kad būtų galima testuoti
    public virtual void Draw(IRenderBackend backend) { }
}
```

**Kolegos (≥ 3, realizuota 5):**

| Klasė | Ką daro | Ką praneša tarpininkui | Ką tarpininkas jai kviečia |
|---|---|---|---|
| `ConsolePanel` | `` ` ``/`Enter` atidaro, `Esc` uždaro, renka simbolius (`GetCharPressed`), rodo istoriją | `ConsoleOpened`, `ConsoleClosed`, `ConsoleSubmitted` (`LastSubmitted`) | `AppendLine(text)` |
| `TankInputComponent` | WASD, pelė, šūvis → `InputMessage` 30 Hz (perkelta iš `GameplayController`) | — | `Suspend()` (vieną kartą siunčia „stovėk, nešauk“), `Resume()` |
| `UpgradeHotkeys` | 1–8, Backspace → `UpgradeStat` / `UndoUpgrade` | — | `Enabled = true/false` |
| `DeathOverlay` | iš snapshot skaito savo tanko `St`/`StT` (State!) ir piešia „You were destroyed — respawning in 2.4 s“ | `PlayerDied`, `PlayerRespawned` | — |
| `LeaveHotkey` | `Esc` → prašo išeiti (pats neišeina) | `LeaveRequested` | `Enabled = true/false` |

**`GameplayHudMediator : IHudMediator`** sukuria kolegas (perduoda `this`) ir laiko taisykles vienoje vietoje:

```csharp
public void Notify(HudComponent sender, HudEvent e)
{
    switch (e)
    {
        case HudEvent.ConsoleOpened:
            TankInput.Suspend(); UpgradeHotkeys.Enabled = false; LeaveHotkey.Enabled = false; break;
        case HudEvent.PlayerDied:
            TankInput.Suspend(); UpgradeHotkeys.Enabled = false; break;      // konsolė ir Esc veikia
        case HudEvent.ConsoleClosed:
        case HudEvent.PlayerRespawned:
            LeaveHotkey.Enabled = !ConsolePanel.IsOpen;
            if (!ConsolePanel.IsOpen && !DeathOverlay.IsShowing) { TankInput.Resume(); UpgradeHotkeys.Enabled = true; }
            break;
        case HudEvent.ConsoleSubmitted:
            ConsolePanel.AppendLine("> " + ConsolePanel.LastSubmitted);
            _actions.ExecuteConsole(ConsolePanel.LastSubmitted); break;
        case HudEvent.LeaveRequested:
            _actions.LeaveGame(); break;                     // atsijungia → prisijungimo langas
    }
}
```
`LeaveHotkey` atnaujinamas prieš konsolę, todėl `Esc`, kuris uždaro konsolę, nėra kartu suprantamas kaip „išeiti“. Kiekvienam naujam žaidimui `Program.cs` sukuria naują `GameplayController` (ir HUD).

Serverio atsakymus (`ConsoleOutput`) kas kadrą paima tarpininkas iš `ClientSessionFacade` ir perduoda `_console.AppendLine(...)`.

### 3.4 Pakeitimai esamame kode
1. **`IRenderBackend.cs`**: į `ClientKey` pridėti `Grave` ir `Escape`. Abiejuose backend'uose (`RaylibRenderBackend`, `Sdl3RenderBackend`) pridėti klavišų atvaizdavimą.
   - **Raylib spąstai:** pagal nutylėjimą `Esc` uždaro langą (`WindowShouldClose`). `OpenWindow` metode reikia `Raylib.SetExitKey(KeyboardKey.Null)`.
   - Atidarant konsolę, iš `GetCharPressed` reikia praleisti patį `` ` `` simbolį.
2. *(demonstracijai)* Įvesties metodus (`IsKeyDown`, `IsKeyPressed`, `IsKeyPressedRepeat`, `GetCharPressed`, `MousePosition`, `IsPrimaryMouseButtonDown`) iškelti į mažą `IInputSource`, o `IRenderBackend : IInputSource`. Esamas kodas nesikeičia, o kolegas galima testuoti su `FakeInput`.
3. **`IGameConnection` / `SignalRGameConnectionAdapter`**: `Task ExecuteConsoleAsync(string line)` (`SendAsync("ExecuteConsole", line)`), `_conn.On<string>("ConsoleOutput", ...)` → `ConcurrentQueue<string>` (callback ateina iš fono gijos, kaip `SnapshotMailbox`).
4. **`ActiveSession` / `ClientSessionFacade`**: `ExecuteConsole(string)`, `bool TryTakeConsoleOutput(out string)` ir `LeaveGame()`. Facade realizuoja `IGameActions`.
5. **`GameplayController`**: lieka plonas. Atnaujina kamerą ir `_hud.Update(frame)`, `Renderer` piešia pasaulį, `_hud.Draw()` piešia perdangas. Upgrade panelės piešimas lieka `Renderer` (Bridge), keičiasi tik klavišų apdorojimas.
6. **Serveris**: `GameHub.ExecuteConsole(string line)` (žr. 2.4).

Paliečiami 1 dalies šablonų failai: Adapter, Facade, Bridge. Pakeitimai tik papildantys, bet apie juos praneškite tam komandos nariui.

### 3.5 Demonstracija
- **Gyvai:** 2 klientai. Atidarius konsolę ir spaudžiant `D`, tankas stovi ir rašomas tekstas. `Backspace` trina, upgrade neatšaukiamas. Įvedus `kill me`, rodomas mirties ekranas su laikmačiu, WASD neveikia, o konsolė veikia. `Esc` atidarytoje konsolėje tik ją uždaro. Po 3 s tankas atgimsta su skydu, o įvestis grįžta tik uždarius konsolę. `Esc` uždarius konsolę grąžina į prisijungimo langą.
- **`main()` be grafikos** (rekomenduojama): `DeepIo.Client -- --hud-demo` → `HudDemo.Run()` su `FakeInput` ir netikru sesijos objektu, kuris patikrina taisykles `Check(...)` būdu kaip `PatternDemo`.

### 3.6 Mediator ir Observer skirtumas (gynimui)

| | Observer (mūsų `GameEventBus`) | Mediator (`GameplayHudMediator`) |
|---|---|---|
| Ryšio forma | vienas subjektas → daug stebėtojų, **vienkryptis** pranešimas | daug kolegų ↔ vienas tarpininkas ↔ daug kolegų, **dvikryptis** |
| Ką žino centras | tik `IGameObserver` sąsają, ne konkrečias klases | **konkrečius kolegas** ir kviečia jų specifinius metodus (`Suspend`, `Enabled`) |
| Kur logika | kiekvienas stebėtojas pats sprendžia, ką daryti (`XpAwardObserver`); stebėtojai tarpusavyje nesiderina | **koordinavimo taisyklė** („atidaryta konsolė ⇒ nėra judėjimo ir hotkeys“) vienoje vietoje; kolegos „kvaili“ |
| Tikslas | atsieti įvykio šaltinį nuo nežinomo skaičiaus reaguojančių (dinaminis prenumeravimas) | sumažinti N×N priklausomybes tarp fiksuoto bendradarbiaujančių objektų rinkinio |
| Pavyzdys projekte | nužudymas → XP, lentelė, feed, achievementai, kiekvienas atskirai | konsolė atsidarė → įvestis sustoja, hotkeys išsijungia, lentelė pasislepia |

Mediator viduje galima naudoti Observer mechanizmą (kolegos „praneša“ tarpininkui), bet esmė yra **kur gyvena koordinavimo logika**.

### 3.7 Ką gali liepti pakeisti gynime
- Pridėti kolegą (`ScoreboardPanel` / `ChatPanel`; taip buvo pridėtas `LeaveHotkey`): nauja klasė ir 1–2 eilutės `Notify`. Kiti kolegos nesikeičia, ir būtent tai verta parodyti.
- Pakeisti taisyklę (mirus konsolė irgi užsidaro): keičiasi tik `GameplayHudMediator`.

---

## 4. Darbų eiga (checklist)

**A. State**
- [ ] `TankLifeState` (Shared), `EntityDto.St/StT`
- [ ] `Entities/States/`: `TankState` + 4 būsenos
- [ ] `Entity` kabliukai, `Tank.State/TransitionTo/ForceKill`, `Clone` fiksas
- [ ] `GameWorld.Cleanup` be respawn, `SpawnPoints`
- [ ] `BotContext.Observe` praleidžia neliečiamus
- [ ] `TankStateChangedEvent`
- [ ] Renderer: nepiešti mirusių, skydo žiedas
- [ ] `PatternDemo.DemonstrateState` + Prototype patikra
- [ ] MagicDraw: state machine diagrama, klasių „prieš/po“

**B. Interpreter**
- [ ] `Scripting/`: `IExpression<T>`, terminalinės, neterminalinės išraiškos
- [ ] `ConsoleLexer`, `ConsoleParser`, išimtys, `ConsoleInterpreter`, `ConsoleContext`, `IConsoleWorld`
- [ ] `GameWorld`: `IConsoleWorld`, `EnqueueConsole`, `DrainConsole`, `SetBotCount` (`MaintainBots` naudoja kintamą tikslą vietoj `GameConstants.BotCount`)
- [ ] `GameHub.ExecuteConsole`, `GameLoop` siunčia atsakymus
- [ ] `ServerConsoleService` (stdin)
- [ ] `PatternDemo.DemonstrateInterpreter`
- [ ] Ataskaitai: BNF, klasių diagrama, AST objektų diagrama vienam sakiniui

**C. Mediator**
- [ ] `ClientKey.Grave/Escape`, backend'ų atvaizdavimas, `SetExitKey(Null)`
- [ ] `IInputSource` (nebūtina, bet reikalinga demonstracijai)
- [ ] Tinklas: `ExecuteConsoleAsync`, `ConsoleOutput` eilė, facade metodai
- [ ] `Hud/`: `IHudMediator`, `GameplayHudMediator`, `HudComponent`, `HudEvent`, 4 kolegos
- [ ] `GameplayController` supaprastinti
- [ ] `HudDemo` (`--hud-demo`)
- [ ] MagicDraw: „prieš“ (`GameplayController` monolitas) ir „po“

**D. Bendra patikra**
- [ ] `dotnet build DeepIo.sln` be įspėjimų
- [ ] `dotnet run --project DeepIo.Server -- --patterns-demo` (visi `Check` praeina)
- [ ] Serveris ir 2 klientai: scenarijus iš 3.5 ir konsolės komandos iš 2.5
- [ ] Ataskaita įkelta į Moodle prieš gynimą

## 5. Indėlis į 40 klasių diagramą

| Šablonas | Naujos klasės / sąsajos / enum |
|---|---|
| State | `TankState`, `SpawnProtectedState`, `AliveState`, `RegeneratingState`, `DeadState`, `TankLifeState`, `TankStateChangedEvent`, `SpawnPoints` (8) |
| Interpreter | `IExpression<T>`, 8 terminalinės, 10 neterminalinių, `ConsoleContext`, `IConsoleWorld`, `ConsoleLexer`, `Token`, `TokenKind`, `ConsoleParser`, `ConsoleInterpreter`, 2 išimtys, `ServerConsoleService` (~28) |
| Mediator | `IHudMediator`, `GameplayHudMediator`, `HudComponent`, `HudEvent`, `ConsolePanel`, `TankInputComponent`, `UpgradeHotkeys`, `DeathOverlay`, `LeaveHotkey`, `IGameActions`, `HudFrame`, `IInputSource` (12) |

Bendroje komandos diagramoje Interpreter išraiškų užtenka parodyti reprezentatyviai (po 2–3 kiekvienos rūšies), o pilną medį palikite savo šablono diagramai.
