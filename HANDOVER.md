# 인수인계

Unity 2020.3.21f1 / 2D 로그라이크(시렌류) / 모바일 목표

---

## 0. 가장 먼저 — 검증 환경

**코드를 고쳤으면 반드시 이걸 돌린다.**

```bash
dotnet run --project Tools/Verify/Verify.csproj
```

마지막 줄이 `ALL PASS` 여야 한다. 컴파일만 보려면:

```bash
dotnet build Tools/Check/Check.csproj --no-incremental
```

**에러 0 / 경고 0 이 기준선이다.** 자세한 내용은 [Tools/README.md](Tools/README.md).

현재 상태: **에러 0 · 경고 0 · 31종 ALL PASS**

이 환경은 Unity 에디터 없이 게임 코드를 컴파일하고 순수 계산 부분을 실제로 돌려 본다.
`Tools/` 는 git 에 들어가 있다(`.gitignore` 의 `!Tools/**/*.csproj` 줄을 지우지 말 것).

### 여기서 확인할 수 없는 것

MonoBehaviour 생명주기 · 코루틴 · 씬 로드 · 물리 트리거 · Tilemap · 프리팹.
**`ALL PASS` 는 "에디터에서 잘 돈다" 는 뜻이 아니다.** 반대로 여기서 실패하면 확실히 잘못된 것이다.

---

## 1. 작업할 때 반드시 지킬 것

### 소스는 UTF-8 BOM

원래 CP949 였던 것을 변환했다. 파일을 쓸 때마다 BOM 을 다시 붙여야 한다.

```bash
python -c "
import glob
for f in glob.glob('Assets/Code/**/*.cs',recursive=True):
    b=open(f,'rb').read()
    if not b.startswith(b'\xef\xbb\xbf'): open(f,'wb').write(b'\xef\xbb\xbf'+b)"
```

### 씬 파일은 Unity 를 끈 뒤에만 건드린다

이번 세션에서 가장 크게 헤맨 지점이다. 씬을 열어 둔 상태에서 `.unity` 를 고치면
사용자가 저장하는 순간 **메모리의 옛 버전이 덮어쓴다.** 그 사실을 모르고
"고쳤는데 안 된다" 를 다른 원인으로 오해해 애셋까지 건드리며 세 번 오진했다.

씬을 고쳐야 하면 **먼저 닫아 달라고 요청**하고, 확인받은 뒤에 편집한다.
가능하면 코드로 해결한다.

### 테스트를 넣으면 반드시 한 번 실패시켜 본다

고친 코드를 잠깐 되돌려 `FAILURES` 가 뜨는 걸 확인한 다음 되돌린다.
이걸 안 하면 아무것도 검사하지 않는 테스트가 조용히 통과만 하고 있게 된다.
실제로 이번에 "가까운 적 우선" 규칙이 검사되지 않고 있던 것을 이 방법으로 잡았다.

### 게임 규칙은 칸 좌표(정수)로 센다

**칸 중심은 월드에서 `x + 0.5` 다**(`GridUtil.CellCenterOffset`). 타일맵에 그리는
자리를 게임 오브젝트와 맞추려고 그렇게 두었다. 그 0.5 는 **화면에 놓을 때의
사실**이지 규칙이 아니다.

그래서 규칙을 계산하는 코드는 `Vector3Int` · `RectInt` 로만 세고, 월드 좌표로
바꾸는 일은 부르는 쪽이 `GridUtil.CellCenter` 로 **마지막에 한 번만** 한다.
`ChaseMap` · `AimTargeting` · `RangedShot` · `TileRules` · `ItemThrower` ·
`Dungeon` 이 전부 그렇게 하고 있고, **그 중 어느 것도 이 실수를 낸 적이 없다.**

월드 float 로 자리를 고르다 실제로 두 번 틀렸다 —
반 칸 어긋남(`CeilToInt(1.5)` → 2), 한 칸 넘침(`Rect.width` 를 칸 수+1 로 셈).
둘 다 에러가 나지 않고 그냥 이상한 자리에 놓였다.

`RectInt` 는 `xMax` 가 마지막 칸의 **다음**이라 `for (x = xMin; x < xMax; x++)`
로 세면 개수가 저절로 맞는다. 이게 정수를 쓰는 진짜 이유다.

> 좌표계 자체를 정수로 바꾸는 것도 검토했지만, 두 씬이 `TilemapCollider2D` 를
> 쓰고 **Tile Anchor 는 스프라이트만 옮기고 콜라이더는 칸 영역에 남는다.**
> 보이는 벽과 막히는 벽이 반 칸 어긋나므로 하지 않았다.

### 타일의 지형은 애셋이 정한다 (이름이 아니라)

`TileRules` 는 원래 타일 **이름**으로 지형을 갈랐다("S1Wall" 이면 벽). 스테이지가
하나뿐일 때는 돌았지만, 2스테이지 벽을 만들면 "S2Wall" 은 어느 목록에도 없어서
**걸어서 통과되는 벽**이 된다. 에러는 나지 않는다.

지금은 타일 애셋이 `ITerrainTile` 로 스스로 답한다. `SiblingRuleTile` ·
`TerrainAnimatedTile` 의 인스펙터에 **Terrain** 과 **Destructible** 칸이 있다.

- **Rule Override Tile 은 바탕 타일의 지형을 따라간다.** 스테이지를 늘릴 때
  그림만 갈아 끼우면 코드를 한 줄도 고칠 필요가 없다.
- 이름 목록(`ImpassableTileNames` 등)은 **아직 옮기지 않은 타일용 뒷길**이다.
  새 타일은 목록에 더하지 말고 애셋에서 고를 것.
- **겉모습과 규칙은 별개다.** 맵 끝자락의 물(`S1BedrockWater`)은 물처럼 보이지만
  `Terrain = Wall` 이다. 물로 두면 물에 들어갈 수 있는 상태로 헤엄쳐 맵 밖으로 나간다.

### 주석은 "왜" 를 쓴다

무엇을 하는지가 아니라 **왜 그렇게 했는지**, 예전에 무엇이 문제였는지를 한국어로 남긴다.
이 프로젝트의 주석 대부분이 그렇게 되어 있다.

### 사용자는 한국어로 소통한다

---

## 2. 구조

`GameManager` 갓 오브젝트를 순수 C# 클래스로 분해한 상태. **MonoBehaviour 가 아니라서
씬의 Inspector 연결을 건드리지 않는다.**

| 계층 | 주요 파일 |
|---|---|
| Core (규칙) | `PlayerStats` `Inventory` `Equipment` `ItemCatalog` `ItemEffects` `GoldPile` `GroundObjectRegistry` `TileRules` `GridUtil` `GameMessage` |
| Core (입력) | `GameInput` `InputKeys` `ButtonPress` `DirectionInput` `InputFocus` `AutoRunner` |
| Core (판정) | `ChaseMap` `RangedShot` `AimTargeting` `IWalkableGrid` `ItemThrower` `HungerWarning` `PanelLayout` |
| Core (데이터) | `ConditionSheet` `TrapSheet` `MonsterSheet` `PlayerCondition`(=ConditionCatalog·ConditionReport) |
| Data | `ExcelData` (엑셀 연동 ScriptableObject) `SheetCatalogs`(=ExcelSheets·TrapCatalog·MonsterCatalog) |
| 정의 애셋 | `ItemDescription` `TrapDescription` `MonsterDescription` — **번호와 그림만** 넣으면 나머지는 시트에서 |
| Dungeon | `RoomLayoutBuilder` `RoomGraph` `CorridorCarver` `CorridorThinner` `DungeonTilePainter` `DungeonPopulator` `WaterwayDecorator` `DungeonLayout` `RoomInfo` |
| UI | `HudView` `MessageBoxView` `PanelView` `BagMenuController` `StatusView` `StatusMenu` `StatusMenuController` `StarvingTint` |
| MonoBehaviour | `GameManager` `Player` `Enemy` `MovingObject` `RandomDung` `CameraPlayer` `Trap` `ItemDescription` `ItemInstance` |

### 기억해야 할 축

- **`InputFocus`** — 누가 입력을 받는가 (`Player` / `BagMenu` / `Prompt` / `Status`)
- **`Player.Mode`** — `Normal` / `Aiming` / `Running` / `Acting`
- **`GameManager.FastForward`** — 연출 대기 생략 (달리기가 켠다)
- **`GameManager.InteractionHandled`** — **'도착' 단위** 표시.
  새 칸에 실제로 도착했을 때만 열리고(`TryStep`), 그 칸의 것을 처리하면 닫힌다.
  턴 종료 시점에 닫으면 안 된다 — 달려서 밟은 함정·계단이 통째로 무시된다.

---

## 3. 조작

| 입력 | 동작 |
|---|---|
| A | 공격 / 결정 |
| B 누른 채 방향 | 달리기. **적이 붙었거나 굶주리면 한 걸음만, 줍지 않고** |
| B 살짝 | 조준 모드 |
| 조준 중 B 살짝 | 다음 적으로. 옮길 곳 없으면 조준 해제 |
| 조준 중 B 길게(0.35s) | 조준 해제 |
| A + B | 제자리 대기 |
| Space | 상태창 |
| 가방/발밑에서 좌우 | 두 화면 오가기 |
| 가방에서 A + B | 이름순 정리 |

**PC 임시 키:** `I` 가방 · `X` 조준 · `C` 다음 층 · `Shift` 대각선 고정

시간으로 가르는 판정은 **`GameInput.LongPressSeconds` 하나뿐**이다.
(`DirectionInput` 의 대각선 유예 30ms 는 별개)

---

## 4. 지금까지 한 일

### 첫 세션 — 구조 정리

`GameManager` 갓 오브젝트 분해, 발밑 명령, 조준, 골드 액수, 던지기 결말,
만복도 경고, 메시지 2종, 상태창 '상태', 엑셀 연동, Pixel Perfect Camera.
버그 대부분의 원인이 **한 값이 두 뜻을 겸한 것**이었다(`InteractionHandled` 등).

### 둘째 세션 — 데이터와 규칙을 제자리로

**상태창 '로그'** — `MessageLog`, `PageScroll`. 기록하는 곳은 `MessageBoxView`
한 곳뿐이다("실제로 보였다" 를 아는 곳이 거기뿐이라서).

**이름 → 번호로 신원 옮기기.** 아이템·함정·적이 전부 GameObject 이름으로
식별되고 있었다. 시트의 `힘의구술` 과 프리팹의 `힘의구슬` 이 한 글자 어긋나
있었고, 함정 메시지에는 "trap_0이(가) 작동했다!" 가 떴다.
지금은 정의 애셋(번호 + 그림)이 신원이다(10절).

**엑셀 시트 넷을 전부 잇기.** `Trap`·`Monster`·`item` 이 `ExcelData` 에 필드가
없다는 이유만으로 통째로 버려지고 있었다.

**함정 효과** — `hole` `warp` `blow` `dart` `gas` `spin` `restraint` `sticky`.
`soil` 만 남았다. 워프·날리기는 함정 전용이 아니라 **지팡이·능력에서도 부를 수
있게** 만들었다(`GameManager.WarpPlayer` / `BlowPlayer`).

**상태이상을 거는 쪽** — `ConditionTracker`, `GameManager.ApplyCondition`.
지속 턴은 시트가 정한다. 잠·혼란이 실제로 동작한다.

**적 AI 두 가지 버그** — 뒤의 적이 앞 적의 칸을 골라 물리에 막히고 턴만
날리던 것, 도망칠 때 두 번째 적이 한 칸 벌어지던 것. 둘 다 **물리 판정과
논리 판정이 어긋나 있어서** 났고, 적 이동을 명시적 판정으로 옮겨 해결했다.

**타일 지형을 애셋으로**(1절), **맵 끝자락(베드락)**, **`SiblingRuleTile`**.

**가방** — 아이템 마킹(종류 단위), 이름순 정리(A+B), 끈적끈적(개체 단위).

**장비** — 특수 능력 세 가지(`경험치증가` `치명타무효` `만복도절약`),
드랍율을 등급(A+·B-)으로 시트에 옮김, 적마다 다른 치명타 확률.

---

## 5. 엑셀 연동

```
Assets/Resources/ExcelData.xlsx  ──저장──▶  ExcelData.asset  ──▶  게임
```

`Assets/ExcelImporter/` (NPOI 기반, **Editor 전용이라 빌드에 안 들어감**).

### 이름 세 곳이 맞아야 한다

| | 규칙 |
|---|---|
| 클래스 이름 | 엑셀 **파일** 이름 (`ExcelData`) |
| 필드 이름 | **시트** 이름 (`Conditions`) |
| 엔티티 필드 이름 | **열** 이름 (`id` `name` `explanation` …) |

**어긋나면 경고 없이 조용히 비어 버린다.** 임포터가 시트가 아니라 *필드* 를 훑기 때문에,
시트만 추가하면 무시된다. 시트를 늘릴 때는 `ExcelData` 에 필드도 함께 추가할 것.
실제로 `Trap` · `Monster` · `item` 세 시트가 필드가 없다는 이유만으로 한동안
통째로 버려지고 있었다. 열 이름에 **공백이 들어가도** 같은 일이 벌어진다
(`sell price` 는 C# 필드 이름이 될 수 없다).

`[ExcelAsset(LogOnImport = true)]` **를 켜 두었다.** 저장할 때마다
`Imported 4 sheets` 로그가 뜬다. **4보다 작으면 이름이 어긋난 것이다.**
이 로그가 조용한 실패를 알아채는 유일한 방법이라 끄지 말 것.

### 애셋이 코드보다 낡을 수 있다

임포터는 **엑셀을 저장할 때만** 돈다. 그래서 엑셀을 저장한 뒤에 `ExcelData` 의
필드를 늘리면, 애셋은 옛 모양 그대로 남는다. 실제로 `ConditionEntity` 에
`id2` 를 더한 날 애셋이 7분 먼저 만들어져 있었고, **상태이상 15줄이 전부
건너뛰어져 화면에서 사라졌다.** 에러는 하나도 나지 않았다.

**엔티티 클래스를 고쳤으면 엑셀을 한 번 다시 저장한다**(또는 우클릭 → Reimport).

### 지금 있는 시트 넷

| 시트 | 읽는 코드 | 상태 |
|---|---|---|
| `Conditions` | `ConditionSheet` → `ConditionCatalog` | 15종. 잠·혼란·봉인이 실제로 걸린다 |
| `Trap` | `TrapSheet` → `TrapCatalog` | 9종. **`soil` · `sticky` 만 효과가 없다** |
| `Monster` | `MonsterSheet` → `MonsterCatalog` | 1종. 수치·경험치가 실제로 쓰인다 |
| `item` | `ItemSheet` → `ItemCatalog` | 7종. **번호가 신원이다.** 장비 3종에 특수 능력 |

세 카탈로그는 `ExcelSheets` 하나를 거쳐 애셋을 읽는다. 표를 다시 읽히려면
`ExcelSheets.Reload()` 를 부른다(카탈로그 셋을 함께 비운다).

### `id` 와 `id2` — 코드가 쓰는 것은 `id2` 다

`Conditions` 와 `Trap` 은 열이 둘이다.

- **`id`** — 표 번호(`20002`). 표를 정리하는 사람의 것이다.
- **`id2`** — 코드가 가리키는 이름(`hungry`). **코드는 반드시 이쪽을 쓴다.**

`ConditionCatalog.HungryId` 같은 상수가 `id2` 값과 짝이다. 번호를 쓰면 코드에
`20002` 만 남아 그게 무엇인지 알 수 없고, 표에서 번호를 다시 매기는 순간 어긋난다.

한때 `id` 가 문자였다가 숫자로 바뀐 적이 있다. 그때 이 구분을 놓치면 `Id` 가
`"20002"` 가 되어 **배고픔·굶주림이 화면에서 조용히 사라진다.** 에러는 하나도
나지 않는다. Verify 의 `[상태이상 표]` 가 이것을 지키고 있다.

### 시트가 담는 것과 담지 않는 것

문구·분류·수치는 데이터라서 시트가 담는다. 하지만 "독은 매 턴 체력을 깎는다",
"빙글빙글은 빠르게 두 바퀴 돈다" 같은 **동작은 코드**다. 그래서 코드가 아는
목록(`ConditionEffect`, `TrapAnimation`)을 시트가 골라 쓰는 방식으로 나눴다.
시트에 모르는 값이 적히면 기본값으로 두고 무엇이 이상한지 알려 준다.

`note` 열은 사람이 읽는 메모다. **코드가 이 글자를 보고 갈래를 나누지 않는다** —
그렇게 하면 메모의 말투를 다듬는 순간 게임이 조용히 바뀐다.

---

## 6. 다음 사람이 이어갈 것

### 사용자가 손으로 해야 하는 것 (먼저 확인할 것)

- **`Monster` 시트에 `critical` 열 추가** — 치명타 확률(%). 비우면 0 = 치명타 없음.
  코드는 이미 읽을 준비가 돼 있다.
- **장비 프리팹에 `ItemInstance` 연결** — 장비 3종(나무몽둥이·헌옷·구리반지)의
  프리팹은 만들었지만, 실제로 떨어지고 차이는지는 아직 확인되지 않았다.
- **함정 프리팹 7개 · 적 프리팹의 `description` 연결** — 안 하면 GameObject
  이름으로 물러서고 `Awake` 에서 경고가 뜬다.

### 바로 할 수 있는 것

1. **새 상태이상 효과** — 표에 20종이 있는데 코드가 아는 효과는 다섯뿐이다
   (`턴당피해` `행동불가` `방향교란` `능력봉인` `없음`). 나머지는 줄마다 경고를
   남기고 `없음` 으로 동작한다 — **표가 코드보다 앞서 나가 있는 상태다.**
   `속도증가/감소` 는 턴 구조를 건드리므로 범위가 크고, `실명` `이동불가`
   `완전 무작위 행동` 은 비교적 작다.
2. **`soil`(오물) 함정** — 남은 마지막 함정. 먹을 수 있는 아이템을 상하게 한다.
   끈적끈적(`ItemInstance.sticky`)과 같은 자리에 표시를 하나 더 두면 된다.
3. **장비 합성** — `baseonly` 를 시트에서 이미 읽고 있다(`ItemDefinition.BaseOnly`).
   켜진 것은 재료로 쓸 수 없고 베이스로만 쓸 수 있다.
4. **상태창 나머지 메뉴** — 도감 · 기록 · 중단. `GameManager.HandleStatusMenu()`.
   셋 다 `ShowScrollingPage()` 를 그대로 쓰면 된다.
   도감은 `MonsterCatalog` · `ItemCatalog` 가 이미 다 들고 있다.
5. **`TrapAnimation.Hit` · `Sleep`** — 그림이 없어 아무것도 하지 않는다.
   그림이 생기면 `Player.PlayTrapAnimation()` 에 채우면 된다.

### 미해결

- **적 1종** — 콘텐츠가 절대적으로 부족하다. 난이도는 층 보정이 아니라
  **어떤 적이 나오는가**로 조절하기로 정했으므로(`DungeonPopulator.SetMonsterStats`),
  적을 늘리는 것이 곧 난이도 설계다.
- **적을 종류별로 뽑는 길이 없다.** `RandomDung.monsterPrefab` 이 하나뿐이라
  `MonsterDefinition.Rate`(상·중·하)가 아직 쓰이지 않는다. 아이템처럼
  프리팹 배열 + `WeightedPick` 으로 바꾸면 된다.
- **용암·구멍 타일이 없다.** 던지기·날리기 결말은 만들어 뒀고 `TileRules` 에
  이름만 등록돼 있다. 타일을 만들면 **이동 규칙(`CanEnter`)도 함께 정해야 한다.**
- **`S1BedrockWater` 가 없다.** 물로 가득한 층의 가장자리가 벽 모양으로 나온다.
- **`▼` 는 굶주림 연두 테를 받지만**, 런타임에 UI 를 더 만들면
  `StarvingTint.Register()` 를 부르는 걸 잊지 말 것.

### 시리즈 대비 미구현

미확인 아이템 · 두루마리 · 지팡이 · 항아리 · 시야 제한 · 상점 · 세이브 · 던전 클리어 조건

> 미확인 아이템이 오면 **표식(MarkBook)이 그대로 얹힌다.** 겉모습↔종류가 판마다
> 1:1 로 섞이므로, 번호로 적어 두는 것이 곧 겉모습으로 적어 두는 것이다.

---

## 7. 커밋 — 지금 120건이 미커밋이다

리팩터링 · 검증 환경 · 이 문서까지가 세 커밋으로 들어가 있고
(`9b1a8b7` `a62b60c` `3c2f5e8`), **그 뒤의 작업이 전부 미커밋이다.**

씬 파일이 추적되므로 커밋할 때는 **Unity 에서 저장한 뒤**에 한다.
나눈다면 이런 덩어리다 — 함정 효과 / 엑셀 데이터 확장 / 적 AI / 가방 기능
(마킹·정리·끈적끈적) / 타일 지형 애셋화.

---

## 8. 실제 플레이 확인이 필요한 것

에디터 실행은 사용자가 한다. 아래는 아직 확인되지 않았거나 회귀 위험이 있는 곳이다.

### 이번에 손댄 것 중 확인 안 된 것

- **장비가 실제로 떨어지는가** — 드랍율을 시트로 옮긴 뒤 확인 전.
  안 떨어지면 `droprate` 가 비었거나 프리팹에 `ItemInstance` 가 없는 것이고,
  둘 다 Console 에 경고가 뜬다.
- **장비 특수 능력 셋** — 경험치 +1 / 배 늦게 고픔 / 치명타 안 맞음.
- **끈적끈적** — 소모품은 못 쓰고, 찬 장비는 벗기·버리기·던지기·교환이 다 막히는가.
- **적 AI** — 넓은 방에서 둘러싸는가, 도망칠 때 바짝 붙는가,
  **적끼리 겹쳐 서지 않는가**(물리 대신 논리로 막게 되어 여기가 가장 위험하다).
- **가방 A+B 정리** — B 를 누른 채 A. 하위 화면에서 B 로 나올 때
  가방까지 닫히지 않는가.
- **마킹** — 같은 종류 전부에 붙는가, 바닥에서도 보이는가.

### 예전부터 남아 있는 것

- 평소 걸음으로 아이템을 밟으면 여전히 주워지는지
- 만복도를 채우면 달리기 제한이 풀리는지
- 상태창 '로그' 가 열자마자 직전 몇 줄을 보여 주는지

---

## 9. 상태창 '로그'

Space → 로그. 메시지 창을 지나간 글을 오래된 것부터 늘어놓고, 상하로 넘겨 본다.

| | |
|---|---|
| `MessageLog` (Core) | 줄 단위로 쌓는다. 200줄이 넘으면 오래된 것부터 버린다 |
| `PageScroll` (Core) | 넘겨 보는 계산. **상태 화면과 로그가 같은 것을 쓴다** |
| `GameManager.Log` | 판 단위 상태. 층을 넘어가도 이어지고, `ResetRun()` 이 비운다 |
| `GameManager.ShowScrollingPage()` | 두 화면이 함께 쓰는 흐름. 도감·기록도 이걸 쓰면 된다 |

### 정해 둔 규칙 세 가지

**기록하는 곳은 `MessageBoxView` 한 곳이다.** "플레이어에게 실제로 보였다" 를
아는 곳이 거기뿐이다. 부르는 쪽마다 따로 기록하게 하면 새 호출부가 생길 때마다
빠뜨리고, 중요한 알림에 밀려 뜨지 못한 메시지까지 이력에 섞인다.

**물음은 남기지 않는다.** `ShowPersistent`(계단 앞 예/아니오, 교환)는 기록하지
않는다. 물음은 일어난 일이 아니고, 답을 고르는 동안 매 프레임 다시 그려져서
그대로 두면 같은 줄이 이력을 가득 채운다.

**줄 단위로 쌓는다.** 메시지 하나가 두 줄인 것이 있다("…를 쓰러뜨렸다.\n…경험치를
얻었다."). 한 칸으로 세면 판에 몇 줄이 들어가는지 계산이 어긋나 스크롤 끝에서
마지막 줄이 잘린다. 차례는 오래된 것이 앞이라 두 줄의 앞뒤도 뒤집히지 않는다.
대신 **열 때 맨 끝으로 내려 놓아**(`startOffset: int.MaxValue`) 방금 일어난 일이
곧바로 보이게 했다.

### 앞으로

메시지에 턴 수나 층을 붙이려면 `MessageLog.Add()` 가 줄만 받고 있으니
거기부터 손대야 한다. 지금은 일부러 글자만 담는다.

---

## 10. 정의 애셋 — 번호와 그림만 넣는다

함정·적은 `ItemDescription` 과 같은 꼴의 애셋으로 정의한다. 사람이 채우는 것은
**번호(`id`)와 그림뿐**이고, 이름·수치·문구·몸짓은 전부 엑셀에서 읽는다.

| 애셋 | 사람이 넣는 것 | 시트에서 오는 것 |
|---|---|---|
| `TrapDescription` | `id` · `sprite` · `triggerChance` | 이름 · 코드 이름 · 문구 · 몸짓 |
| `MonsterDescription` | `id` · `sprite` | 이름 · 체력 · 공격 · 방어 · 경험치 · 빈도 · 설명 |

### 사본을 두지 않는다

시트 값을 애셋 **필드로 복사해 두지 않고 프로퍼티로 읽는다.** `OnValidate` 로
채워 넣으면 인스펙터에서는 편하지만 시트의 사본이 생기고, 시트를 고친 뒤
애셋을 다시 열지 않으면 조용히 어긋난다 — 5절의 "애셋이 코드보다 낡을 수 있다"
와 같은 종류의 사고다. **원본이 한 곳이면 어긋날 수가 없다.**

인스펙터가 비어 보이는 문제는 `Assets/Editor/DescriptionInspectors.cs` 가
읽기 전용 미리보기를 그려서 메운다. 번호를 잘못 적으면 거기서 경고가 뜬다.

### 애셋 만들기

`Tools ▸ 시트에서 함정 정의 만들기` / `시트에서 적 정의 만들기` 메뉴가
시트의 줄마다 애셋을 하나씩 만들어 준다(번호는 기계가 넣는다). 이미 있는
애셋은 건드리지 않으므로, 넣어 둔 그림이 지워지지 않는다.

**남은 손일:** 만들어진 애셋에 그림을 끌어다 놓고, 함정 프리팹 7개의
`Trap.description` 과 적 프리팹의 `Enemy.description` 에 애셋을 연결한다.
연결 전까지는 예전처럼 GameObject 이름으로 물러서므로 게임은 그대로 돈다.

| `ItemDescription` | `id` · `sprites` | 이름 · 설명 · 가격 · 갈래 · 장비 자리 · 성능 |

### 이름은 더 이상 신원이 아니다

예전에는 GameObject 이름이 곧 신원이었다. 함정 프리팹 이름이 `trap_0` 이라
메시지에 "trap_0이(가) 작동했다!" 가 떴고, 아이템은 시트가 `힘의구술`,
프리팹이 `힘의구슬` 로 **한 글자 어긋나 있었다.** 어긋나도 에러는 나지 않고
그냥 안 먹는다.

**셋 다 번호로 옮겼다.** 지금 이름은 화면에 보여 주는 데만 쓴다.
프리팹 이름은 무엇이든 상관없다.

| | 신원 |
|---|---|
| 함정 | `Trap.description` → `TrapDescription.id` |
| 적 | `Enemy.description` → `MonsterDescription.id` |
| 아이템 | `ItemInstance.description` → `ItemDescription.id` |

아이템은 오브젝트가 가방과 바닥을 오가므로 **오브젝트 쪽에 정의를 달았다**
(`ItemInstance`). 그래서 `ItemInstance.IdOf(item)` / `DisplayNameOf(item)` 로
어디서든 번호와 이름을 얻는다. `ItemEffects` 의 키도 번호다(`[20001] // 사과`).

옛 `ItemCatalog`(이름 → 애셋 색인)은 지워졌다. 오브젝트가 자기 정의를 직접
가리키게 되면서 그 다리가 필요 없어졌고, 이름은 시트를 들고 있는 쪽
(`ItemCatalog`, 번호 → `ItemDefinition`)이 물려받았다.

### 그림도 애셋이 원본이다

`SpriteApplier` 가 실행할 때 정의 애셋의 그림을 SpriteRenderer 에 입힌다.
그래서 **프리팹마다 그림을 넣어 둘 필요가 없다.** 애셋의 그림이 비어 있으면
아무것도 하지 않는다 — 그대로 대입하면 프리팹에 있던 그림까지 지워져
오브젝트가 화면에서 사라지기 때문이다.

적은 SpriteRenderer 가 자식에 달려 있고(MovingObject) 함정·아이템은 자기
자신에 달려 있어서, `GetComponentInChildren` 하나로 셋 다 처리한다.
