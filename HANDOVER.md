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

현재 상태: **에러 0 · 경고 0 · 19종 ALL PASS** · 소스 53개 8,779줄

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
| Core (데이터) | `ConditionSheet` `PlayerCondition`(=ConditionCatalog·ConditionReport) |
| Data | `ExcelData` (엑셀 연동 ScriptableObject) |
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

**PC 임시 키:** `I` 가방 · `X` 조준 · `C` 다음 층 · `Shift` 대각선 고정

시간으로 가르는 판정은 **`GameInput.LongPressSeconds` 하나뿐**이다.
(`DirectionInput` 의 대각선 유예 30ms 는 별개)

---

## 4. 이번 세션에 한 일

### 고친 버그 (원인이 대부분 구조에 있었다)

- **빌드에서 새 판이 시작되지 않음** — 판 상태가 `static` 이라 에디터에서는 도메인
  리로드로 저절로 초기화되지만 빌드에서는 안 된다. `GameManager.ResetRun()` 신설.
  게임 오버가 `SetActive(false)` 로 소프트락되던 것도 함께 해결.
- **놓자마자 되줍기** — `InteractionHandled` 가 적이 없는 층에서 안 켜졌다.
  `Player.placedUnderFoot` 로 명시적으로 막음.
- **턴 없이 아이템 획득** — 벽을 향해 밀면 이동은 실패하는데 문은 열려 있었다.
  문 여는 시점을 `Move` 성공 뒤로 옮김.
- **달리기로 함정·계단 무시** — 위 `InteractionHandled` 의 '도착 단위' 정리로 해결.
- **시작하자마자 함정 발동** — `Player.Start()` 에서 시작 칸을 처리된 것으로 표시.
- **적이 많으면 조작이 느려짐** — 적 턴을 순차로 만든 것이 원인.
  이동은 한꺼번에, 공격만 차례로 하도록 세 단계로 나눔.
- **굶주린 채 달리면 즉사** — 굶주림 피해는 매 턴 1, 달리기는 최대 60칸.
  굶주리면 한 걸음만 가도록 제한.
- **파란 세로선** — 타일 시트에 24×24 타일 사이 1픽셀 청록 격자선이 있고,
  비정수 배율 렌더링에서 샘플링이 그것을 물어왔다. **Pixel Perfect Camera** 로 해결.

### 새로 만든 것

- 발밑 명령(줍는다·교환한다·사용한다·던진다·확인한다), 가방↔발밑 좌우 전환
- 조준 2순위(직선으로 겨눌 수 있는 적), 조준 대상 옮기기
- 골드에 액수 개념(`GoldPile`), 던지면 액수/10 방어력 무시 피해
- 던진 것의 착지 지형별 결말(물·용암·구멍), 벽 튕김·비켜가기 연출
- 함정 뼈대(`Trap`) — 확률 판정까지. **효과는 비어 있다**
- 만복도 경고 5단계, 굶주릴 때 모든 글자에 연두 외곽선(`StarvingTint`)
- 메시지 2종 — 지나가는 것 / **중요한 알림**(A 로 확인, 읽는 시간 뒤 `▼` 깜빡임,
  읽는 동안 적 턴 정지)
- 상태창 '상태' 화면 (스크롤 지원)
- **엑셀 연동** — `Assets/Resources/ExcelData.xlsx` → `ExcelData.asset`

### UI / 카메라

- 패널 4개를 `MenuPanel` 하나로 통합, 내용에 맞춰 크기 조절(`PanelLayout`)
- **HUD 를 가리지 않는 규칙을 숫자로 고정** (`PanelLayout.HudBottom = 155`)
- Pixel Perfect Camera (PPU 24, 기준 432×240 = 18×10 타일, Crop + Stretch Fill)
- Canvas 를 **Screen Space - Camera** 로 — HUD 가 검은 여백을 침범하지 않게
- 정렬 레이어 맨 위에 **`UI`** 신설 — 안 그러면 던전이 UI 를 덮는다

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

`[ExcelAsset(LogOnImport = true)]` 를 켜면 저장할 때마다 몇 개 시트를 읽었는지 로그가 뜬다.
조용한 실패를 알아채는 데 쓸 만하다(아직 안 켜 둠).

---

## 6. 다음 사람이 이어갈 것

### 바로 할 수 있는 것

1. **상태이상 효과 만들기** — 표에 12종이 있지만 실제로 걸리는 것은 굶주림뿐이다.
   `ConditionEffect` 에 종류를 추가하고 `ConditionReport.IsActive` 에 잇는다.
   그리고 **상태이상을 거는 쪽**(독 함정, 두루마리)이 필요하다.
2. **함정 효과** — `Trap.Activate()` 가 비어 있다(문구만). 밟기·발밑·확률 판정은
   이미 그 한 곳을 지나가므로 효과만 채우면 모든 경로에 함께 적용된다.
3. **장비 아이템** — `Equipment` 는 완성돼 있는데 **장비가 하나도 없다.**
   `ItemDescription` 애셋에 `equipSlot` + `basePower` 를 채우면 된다.
4. **상태창 나머지 메뉴** — 도감 · 로그 · 기록 · 중단. `GameManager.HandleStatusMenu()`.
   로그가 가장 쉽다(메시지 이력만 쌓으면 된다).

### 미해결

- **`Assets/RCode/` 가 git 에 한 번도 안 들어갔다.** 아래 7번 참고.
- **적 1종 · 아이템 4종** — 콘텐츠가 절대적으로 부족하다.
- **용암·구멍 타일이 없다.** 던지기 결말은 만들어 뒀고
  `TileRules.LavaTileNames`/`PitTileNames` 에 이름만 등록돼 있다.
  타일을 만들면 **이동 규칙(`CanEnter`)도 함께 정해야 한다** — 지금은 그냥 걸어 들어간다.
- **`▼` 는 굶주림 연두 테를 받지만**, 앞으로 런타임에 UI 를 더 만들면
  `StarvingTint.Register()` 를 부르는 걸 잊지 말 것.

### 시리즈 대비 미구현

미확인 아이템 · 두루마리 · 지팡이 · 항아리 · 시야 제한 · 상점 · 세이브 · 던전 클리어 조건

---

## 7. 지금 가장 큰 위험

**8,779줄 전체가 커밋되지 않았다.** 미커밋 42건, 마지막 커밋은 리팩터링 이전이다.

```
bfe8284 RandomDungeon   ← 여기까지만 커밋됨
a0847ea Initial commit
```

특히 `Assets/RCode/` 는 git 에 **한 번도** 들어간 적이 없다.
지금 작업 디렉터리가 유일한 사본이다.

사용자에게 커밋을 권할 것. 씬 파일이 포함되므로 **Unity 에서 씬을 저장한 뒤**에 해야 한다.

---

## 8. 실제 플레이 확인이 필요한 것

에디터 실행 테스트는 이번 세션에서 한 번도 하지 못했다. 사용자가 확인 중이며,
아래는 아직 확인되지 않았거나 회귀 위험이 있는 지점이다.

- 적 여럿이 **같은 칸에 겹치지 않는지** (이동을 동시 처리로 바꿈)
- 평소 걸음으로 아이템을 밟으면 **여전히 주워지는지** (`InteractionHandled` 규칙 변경)
- 만복도를 채우면 **달리기 제한이 풀리는지**
- 엑셀 시트를 고치면 게임에 반영되는지
