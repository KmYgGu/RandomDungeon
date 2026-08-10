using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임 전체의 진행을 조율하는 역할.
///
/// 예전에는 이 클래스 하나가 플레이어 수치 / 턴 진행 / 체력바 / 메시지창 /
/// 가방 UI / 커서 조작 / 아이템 효과 / 아이템 던지기 물리 / 타일 충돌 판정을
/// 전부 들고 있는 1,100줄짜리 클래스였다.
///
/// 지금은 각 책임을 아래 협력 객체들이 나눠 갖고, GameManager 는
/// "언제 누구를 부를지" 만 정한다.
///   PlayerStats        - 플레이어 수치와 그 규칙
///   Inventory          - 가방의 내용과 용량
///   ItemEffects        - 아이템을 쓰면 일어나는 일
///   GroundObjectRegistry - 바닥에 놓인 물건의 위치
///   HudView / MessageBoxView / PanelView / StatusView - 화면 표시
///   BagMenuController  - 가방 화면의 흐름
///   ItemThrower        - 던진 아이템의 비행과 착지
///
/// 협력 객체들은 MonoBehaviour 가 아니라 순수 C# 클래스다. 그래서 씬에
/// 컴포넌트를 새로 붙이거나 Inspector 참조를 다시 연결할 필요가 없다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance = null;

    // ── Inspector 연결 (이름을 바꾸면 씬의 연결이 끊기므로 그대로 둔다) ────────
    //    공용 창 관련 필드는 씬 파일을 함께 고쳐서 이름을 바꿔 두었다.
    public RandomDung MapMake;

    public GameObject LevelImage;
    public GameObject UiCanvas;
    public GameObject Player;

    public RectTransform healthBar;
    public RectTransform MaxhealthBar;

    public TextMeshProUGUI LevelTextMeshPro;
    public TextMeshProUGUI LevelCanvas;
    public TextMeshProUGUI PlayerLVCanvas;
    public TextMeshProUGUI HpCanvas;
    public TextMeshProUGUI GoldCanvas;

    public GameObject panel;
    public Image TextimageBox;
    public TextMeshProUGUI AttackTextMeshPro;

    [Header("공용 창 (목록 · 메뉴 · 아이템 설명)")]
    public GameObject MenuPanel;
    public TextMeshProUGUI MenuText;
    public Image DetailImage;
    public TextMeshProUGUI DetailName;
    public TextMeshProUGUI DetailPrice;
    public TextMeshProUGUI DetailText;

    [Header("캐릭터 상태창")]
    public GameObject StatusPanel;
    public Image StatusWeaponIcon;
    public TextMeshProUGUI StatusWeaponEnhance;
    public Image StatusArmorIcon;
    public TextMeshProUGUI StatusArmorEnhance;
    public Image StatusAccessoryIcon;
    public TextMeshProUGUI StatusAccessoryEnhance;
    public TextMeshProUGUI StatusLeftColumn;
    public TextMeshProUGUI StatusRightColumn;
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>가방에 넣을 수 있는 최대 개수.</summary>
    public const int BagCapacity = 10;

    /// <summary>적들이 움직이기 전의 연출용 대기.</summary>
    private const float EnemyTurnDelay = 0.1f;

    /// <summary>몇 턴마다 배가 고파지는가.</summary>
    private const int HungerTickInterval = 5;

    /// <summary>몇 턴마다 체력이 자연 회복되는가.</summary>
    private const int RecoveryTickInterval = 10;

    /// <summary>층 진입 안내 문구가 사라지기까지의 시간.</summary>
    private const float LevelTextHoldTime = 1f;
    private const float LevelTextFadeTime = 0.5f;

    /// <summary>게임 오버 문구가 뜬 뒤, 확인 입력을 받기까지 기다리는 시간.</summary>
    private const float GameOverHoldTime = 1.5f;

    private static readonly Color LevelImageColor = new Color(255f / 255f, 107f / 255f, 107f / 255f);

    // ── 씬을 넘어가도 유지되는 게임 상태 ────────────────────────────────────
    public static PlayerStats Stats { get; private set; } = new PlayerStats();
    public static Inventory Bag { get; private set; } = new Inventory(BagCapacity);

    /// <summary>지금 착용 중인 장비. Awake 에서 아이템 목록을 받아 만들어진다.</summary>
    public static Equipment Gear { get; private set; }

    /// <summary>장비까지 반영한 최종 공격력.</summary>
    public static int AttackPower => Stats.TotalAttack(Gear != null ? Gear.AttackBonus : 0);

    /// <summary>장비까지 반영한 최종 방어력.</summary>
    public static int DefensePower => Stats.TotalDefense(Gear != null ? Gear.DefenseBonus : 0);
    public static GroundObjectRegistry Ground { get; private set; } = new GroundObjectRegistry();

    /// <summary>
    /// 플레이어까지의 걸음 수 지도. 추격하는 적들이 길을 찾는 데 쓴다.
    /// 턴이 시작될 때 한 번 갱신하고, 그 턴의 모든 적이 함께 본다.
    /// </summary>
    public static ChaseMap Chase { get; private set; } = new ChaseMap();

    /// <summary>
    /// 메시지 창을 지나간 글의 이력. 상태창의 '로그' 가 읽는다.
    ///
    /// 층을 넘어가도 이어져야 한다. 아래층으로 내려가는 순간 이력이 끊기면
    /// 정작 되짚어 보고 싶은 직전 몇 수가 함께 사라진다.
    ///
    /// 바꿔 끼울 수 없게 읽기 전용으로 두었다. 메시지 창이 이 객체를 그대로
    /// 들고 있어서, 새 것으로 갈아 끼우면 창은 지난 판의 것에 계속 쌓는다.
    /// 판을 새로 시작할 때는 ResetRun() 이 비운다.
    /// </summary>
    public static MessageLog Log { get; } = new MessageLog();

    /// <summary>
    /// 지금 걸려 있는 상태이상. 함정 · 아이템 · 능력이 모두 여기에 건다.
    ///
    /// 판 단위 상태라 층을 넘어가도 이어진다. ResetRun() 이 비운다.
    /// Log 와 같은 이유로 읽기 전용이다 — 갈아 끼우면 걸어 둔 곳과 보는 곳이 갈린다.
    /// </summary>
    public static ConditionTracker Conditions { get; } = new ConditionTracker();

    /// <summary>
    /// 아이템 종류마다 손으로 붙여 둔 표식. 가방·바닥 표시가 이것을 읽는다.
    ///
    /// 그 판에서 알아낸 것이라 층을 넘어가도 남고 새 판에서 지워진다.
    /// Log 와 같은 이유로 읽기 전용이다 — 갈아 끼우면 적어 둔 곳과 보는 곳이 갈린다.
    /// </summary>
    public static MarkBook Marks { get; } = new MarkBook();

    public static int Stage { get; private set; }
    public static int TurnCount { get; private set; }

    /// <summary>바닥 타일맵. RandomDung 이 층을 만들 때 채워 넣는다.</summary>
    public static Tilemap FloorTilemap { get; set; }

    /// <summary>플레이어가 바라보는 방향. 공격과 던지기가 이 방향을 쓴다.</summary>
    public static Vector2 LookDirection { get; set; } = Vector2.down;

    /// <summary>이번 턴에 이미 밟은 것(계단/아이템)을 처리했는가. 중복 처리를 막는다.</summary>
    public static bool InteractionHandled { get; set; }

    /// <summary>
    /// 턴을 빨리감기 중인가. 달리기(오토런)가 켠다.
    ///
    /// 켜져 있으면 연출을 위한 대기(적 턴 사이의 0.1초, 공격 모션, 물리 프레임 대기)를
    /// 전부 건너뛴다. 턴 자체는 평소와 똑같이 돌아가므로 적도 제 차례에 움직이고,
    /// 달리는 도중에 따라잡혀 맞는 일도 그대로 일어난다. 눈에 보이는 시간만 사라진다.
    /// </summary>
    public static bool FastForward { get; set; }

    /// <summary>
    /// 워프·날리기처럼 캐릭터를 강제로 옮기는 연출이 도는 중인가.
    ///
    /// 적 턴은 이것이 끝나기를 기다린다(MoveEnemies). 그러지 않으면 아직 옮겨지는
    /// 중인 플레이어를 놓고 적이 "지난 자리" 를 기준으로 움직이고 때린다.
    /// 실제로 발밑 명령으로 워프 함정을 밟았을 때 옆에 있던 적이 빈 칸을 때렸다.
    ///
    /// ── 이 한 겹으로 충분한 이유 ──────────────────────────────────
    /// 코루틴은 첫 yield 까지 동기로 실행된다. 강제 이동은 갈 곳을 먼저 계산하고
    /// <b>연출을 시작하기 전에</b> 이 표시를 세우므로, 뒤이어 불리는
    /// EndPlayerTurn() 이 적 턴을 시작할 때는 이미 서 있다.
    ///
    ///   TryTrigger() → StartCoroutine(WarpPlayer())   ← 여기서 표시가 선다
    ///   EndPlayerTurn() → MoveEnemies()               ← 그래서 곧바로 기다린다
    ///
    /// 그래서 적이 때리기 직전에 "아직 옆에 있나" 를 다시 볼 필요가 없다.
    /// 한때 그 재확인도 함께 두었는데, 어느 쪽이 실제로 막고 있는지 알 수 없어졌다.
    ///
    /// 함정만이 아니라 지팡이·능력으로 옮길 때도 같은 표시를 쓴다.
    /// </summary>
    public static bool ForcedMoveRunning { get; private set; }
    // ──────────────────────────────────────────────────────────────────────

    public bool playersTurn = true;
    public List<Enemy> enemies = new List<Enemy>();
    public List<Vector2> enemyPositions = new List<Vector2>();
    public Vector2 PlayerPosition;

    // 협력 객체들. Awake 에서 Inspector 참조를 넘겨 만든다.
    private HudView hud;
    private MessageBoxView messageBox;
    private StatusView statusView;
    private PanelView menuPanel;
    private StarvingTint starvingTint;

    /// <summary>게임 오버 처리를 이미 시작했는가. 죽음은 여러 곳에서 알려 온다.</summary>
    private bool gameOverStarted;

    private void Awake()
    {
        // 아래층으로 내려가면 씬을 다시 불러오므로 GameManager 가 새로 하나 더 생긴다.
        // 살아남는 것은 처음 것(DontDestroyOnLoad)이고, 새로 생긴 쪽은 사라지기 전에
        // "다음 층을 시작하라"고 알려 주는 역할만 한다.
        //
        // GameManager 와 RandomDung 은 같은 GameObject 에 있어서 함께 살아남는다.
        // 그래서 살아남은 쪽의 MapMake 참조는 계속 유효하고,
        // RandomDung 이 새 씬의 타일맵을 다시 찾아 층을 만든다.
        if (instance != null && instance != this)
        {
            instance.StartNextStage();
            Destroy(UiCanvas);
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);
        DontDestroyOnLoad(UiCanvas);

        // 살아남은 GameManager 가 없었다는 것은 새 판이 시작됐다는 뜻이다.
        // static 으로 들고 있는 값들은 씬을 다시 불러도 남으므로 여기서 되돌린다.
        // (Gear 를 비우기 때문에 BuildCollaborators 보다 반드시 앞에 와야 한다.)
        ResetRun();

        BuildCollaborators();
        StartNextStage();
    }

    /// <summary>Inspector 로 받아 둔 UI 참조를 각 뷰에 나눠 준다.</summary>
    private void BuildCollaborators()
    {
        hud = new HudView(
            healthBar, MaxhealthBar,
            LevelCanvas, PlayerLVCanvas, HpCanvas, GoldCanvas);

        // 가방 목록 · 행동 메뉴 · 아이템 설명 · 계단 앞 예/아니오 · 상태창 메뉴가
        // 전부 이 창 하나를 나눠 쓴다.
        menuPanel = new PanelView(
            MenuPanel, MenuText,
            DetailImage, DetailName, DetailPrice, DetailText);

        messageBox = new MessageBoxView(
            this, panel, TextimageBox, AttackTextMeshPro, menuPanel, Log);

        statusView = new StatusView(
            StatusPanel,
            StatusWeaponIcon, StatusWeaponEnhance,
            StatusArmorIcon, StatusArmorEnhance,
            StatusAccessoryIcon, StatusAccessoryEnhance,
            StatusLeftColumn, StatusRightColumn);

        // 굶는 동안 화면의 모든 글자에 테를 두른다.
        starvingTint = new StarvingTint(UiCanvas);


        // 장비는 씬을 넘어가도 유지되므로 한 번만 만든다.
        if (Gear == null) Gear = new Equipment();

        statusView.SetVisible(false);
        menuPanel.Hide();
    }

    /// <summary>
    /// 캐릭터 상태창을 연다. 열려 있는 동안에는 조작이 잠기고,
    /// 아무 키(A 또는 B)나 상태창 키를 다시 누르면 닫힌다.
    ///
    /// 가방과 달리 턴을 쓰지 않는다. 그냥 들여다보는 화면이다.
    /// </summary>
    public void OpenStatus()
    {
        InputFocus.Give(InputOwner.Status);
        StartCoroutine(ShowStatusUntilClosed());
    }

    private IEnumerator ShowStatusUntilClosed()
    {
        statusView.SetVisible(true);
        statusView.Render(Stats, Gear, TurnCount);

        // 메뉴가 열려 있는 동안 턴은 흐르지 않으므로 수치는 한 번만 그리면 된다.
        StatusMenu? chosen = null;
        yield return new StatusMenuController(messageBox).Run(result => chosen = result);

        statusView.SetVisible(false);
        menuPanel.Hide();
        messageBox.Hide();
        InputFocus.ReturnToPlayer();

        if (chosen.HasValue)
            HandleStatusMenu(chosen.Value);
    }

    /// <summary>
    /// 상태창 메뉴에서 고른 것을 처리한다.
    ///
    /// 여섯 항목 모두 앞으로 만들 기능이라, 지금은 자리만 잡아 두었다.
    /// 기능이 생기면 해당 case 한 줄만 바꾸면 된다.
    /// </summary>
    private void HandleStatusMenu(StatusMenu menu)
    {
        switch (menu)
        {
            case StatusMenu.UnderFoot:
                UseUnderFoot();
                break;

            case StatusMenu.Condition:
                StartCoroutine(ShowConditions());
                break;

            case StatusMenu.Log:
                StartCoroutine(ShowLog());
                break;

            case StatusMenu.Encyclopedia:
            case StatusMenu.Records:
            case StatusMenu.Abort:
                messageBox.Show($"{StatusMenuCatalog.LabelOf(menu)}은(는) 아직 준비되지 않았다.");
                break;
        }
    }

    /// <summary>
    /// 여러 줄을 큰 판에 늘어놓고 상하 방향키로 넘겨 보는 화면.
    /// 상태와 로그가 같은 흐름을 쓴다.
    ///
    /// 판에 몇 줄이 들어가는지는 글꼴과 화면 비율에 달렸으므로 매 프레임 다시
    /// 물어본다 — 창 크기가 바뀌어도 스크롤이 어긋나지 않는다.
    /// 반대로 줄 수는 열 때 한 번만 받는다. 이 화면이 떠 있는 동안에는 턴이
    /// 흐르지 않아 내용이 늘거나 줄지 않기 때문이다.
    /// </summary>
    /// <param name="lineCount">늘어놓을 줄 수. 스크롤이 넘어갈 범위를 정한다.</param>
    /// <param name="buildText">(시작 줄, 담을 줄 수) → 판에 채울 글자.</param>
    /// <param name="startOffset">처음 보여 줄 자리. int.MaxValue 면 맨 끝에서 시작한다.</param>
    private IEnumerator ShowScrollingPage(
        int lineCount, Func<int, int, string> buildText, int startOffset)
    {
        InputFocus.Give(InputOwner.Prompt);

        int offset = startOffset;

        while (true)
        {
            int capacity = menuPanel.PageLineCapacity;
            offset = PageScroll.ClampOffset(offset, lineCount, capacity);

            menuPanel.ShowPage(buildText(offset, capacity));

            // 이 화면을 연 그 키가 곧바로 닫아 버리지 않도록 한 프레임 넘긴다.
            yield return null;

            if (GameInput.AssistPressed || GameInput.StatusPressed) break;

            offset += GameInput.MenuStep;
        }

        menuPanel.Hide();
        InputFocus.ReturnToPlayer();
    }

    /// <summary>상태 화면. 지금 걸려 있는 것들을 위에서부터 늘어놓는다.</summary>
    private IEnumerator ShowConditions()
    {
        List<ConditionDefinition> active = ConditionReport.Active(Stats, Conditions);

        return ShowScrollingPage(
            active.Count,
            (offset, capacity) => ConditionReport.BuildText(active, offset, capacity),
            startOffset: 0);
    }

    /// <summary>
    /// 로그 화면. 메시지 창을 지나간 글을 오래된 것부터 늘어놓는다.
    ///
    /// 맨 끝에서 시작한다. 이 화면을 여는 이유는 대개 "방금 무엇에 맞았지" 라서,
    /// 열자마자 직전 몇 줄이 보여야 한다. 위로 올릴수록 과거로 간다.
    /// </summary>
    private IEnumerator ShowLog()
    {
        return ShowScrollingPage(Log.Count, Log.BuildText, startOffset: int.MaxValue);
    }

    private void Update()
    {
        hud.Refresh(Stats, Stage);

        // 아사 피해를 입기 시작하는 그 순간부터 글자에 테가 둘러진다.
        // 먹어서 벗어나면 저절로 풀린다.
        starvingTint.SetStarving(Stats.IsStarving);
    }

    // ── 층 진행 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 판 하나가 끝나고 새로 시작할 때, 판 단위 상태를 전부 처음으로 되돌린다.
    ///
    /// 이 값들은 층을 넘어가도 유지돼야 해서 static 으로 두었는데, static 은
    /// 씬을 다시 불러도 초기화되지 않는다. 에디터에서는 Play 를 누를 때마다
    /// 도메인이 다시 로드돼 저절로 새 값이 되므로 이 문제가 드러나지 않지만,
    /// 빌드에서는 프로세스가 살아 있는 동안 계속 남는다. 그래서 에디터에서만
    /// 멀쩡하고, 빌드에서는 죽은 캐릭터의 수치와 가방을 그대로 물려받은 채로
    /// 다음 판이 시작된다. 에디터 테스트로는 절대 드러나지 않는 종류의 차이다.
    ///
    /// "판을 처음으로 되돌리는 일" 을 이 한 곳에 모아 두고,
    /// 살아남은 GameManager 가 없을 때(= 새 판) Awake 에서 부른다.
    /// </summary>
    public static void ResetRun()
    {
        // 가방에 넣은 아이템은 DontDestroyOnLoad 로 옮겨 둔 것이라 씬을 다시
        // 불러도 사라지지 않는다. 여기서 직접 지우지 않으면 판을 거듭할수록
        // 보이지 않는 오브젝트가 계속 쌓인다.
        for (int i = 0; i < Bag.Count; i++)
        {
            if (Bag[i] != null) Destroy(Bag[i]);
        }

        Stats = new PlayerStats();
        Bag = new Inventory(BagCapacity);
        Ground = new GroundObjectRegistry();
        Chase = new ChaseMap();

        // 장비는 아이템 목록이 있어야 만들 수 있다. 곧 BuildCollaborators 가 만든다.
        Gear = null;

        // 새 객체로 갈아 끼우지 않고 비운다 — 위 Log 의 설명 참고.
        Log.Clear();
        Conditions.Clear();
        Marks.Clear();

        Stage = 0;              // 뒤이어 StartNextStage() 가 1층으로 올린다.
        TurnCount = 0;
        FloorTilemap = null;
        LookDirection = Vector2.down;
        InteractionHandled = false;
        FastForward = false;
        ForcedMoveRunning = false;

        InputFocus.Reset();
        GameInput.Reset();
    }

    /// <summary>다음 층으로 넘어간다. 씬을 다시 불러온 직후에 호출된다.</summary>
    public void StartNextStage()
    {
        Stage++;
        TurnCount = 0;

        playersTurn = true;
        LookDirection = Vector2.down;
        InputFocus.Reset();
        GameInput.Reset();
        InteractionHandled = false;

        // 달리는 도중에 층이 바뀌면 코루틴이 중간에 사라져 켜진 채로 남을 수 있다.
        FastForward = false;

        enemies.Clear();
        enemyPositions.Clear();
        Ground.Clear();
        messageBox.Hide();

        LevelTextMeshPro.text = $"던전의 지하 {Stage}층";
        StartCoroutine(FadeOutLevelImage());

        MapMake.GenerateDungeon();
    }

    private IEnumerator FadeOutLevelImage()
    {
        Image imageComponent = LevelImage.GetComponent<Image>();
        imageComponent.color = LevelImageColor;

        yield return new WaitForSeconds(LevelTextHoldTime);

        float elapsed = 0f;
        while (elapsed < LevelTextFadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / LevelTextFadeTime);
            imageComponent.color = new Color(LevelImageColor.r, LevelImageColor.g, LevelImageColor.b, alpha);
            yield return null;
        }

        LevelTextMeshPro.text = string.Empty;
    }

    /// <summary>
    /// 죽었으면 게임 오버 화면을 띄우고, 확인을 받은 뒤 새 판을 시작한다.
    ///
    /// 예전에는 여기서 gameObject.SetActive(false) 한 줄로 끝냈다. 그런데 이
    /// 오브젝트는 DontDestroyOnLoad 라 씬을 다시 불러도 꺼진 채로 살아남는다.
    /// 그러면 새로 생긴 GameManager 는 Awake 에서 "이미 instance 가 있다"고 보고
    /// 꺼져 있는 쪽의 StartNextStage() 를 부르려다 실패한 뒤(꺼진 오브젝트에서는
    /// 코루틴이 돌지 않는다) 자기 자신을 지웠다. 되살아날 길이 없어 죽는 순간
    /// 게임이 멈춰 버렸다.
    ///
    /// 이제는 살아남는 것들을 확실히 정리하고 instance 를 비운다. 그래야 다음
    /// 씬의 Awake 가 "새 판" 으로 인식해 ResetRun() 을 거쳐 처음부터 시작한다.
    /// </summary>
    public void GameOver()
    {
        if (!Stats.IsDead) return;

        // 이동·피격·굶주림 세 곳에서 부르므로, 한 번 시작했으면 다시 받지 않는다.
        if (gameOverStarted) return;
        gameOverStarted = true;

        StartCoroutine(GameOverThenRestart());
    }

    private IEnumerator GameOverThenRestart()
    {
        // 죽은 뒤에 한 턴 더 움직이는 일이 없도록 입력을 먼저 뺏는다.
        // Player.Update 는 권한 검사에서 곧바로 돌아 나가므로, 아래에서
        // instance 를 비워도 없는 것을 건드리지 않는다.
        InputFocus.Give(InputOwner.Prompt);
        FastForward = false;

        // 메시지창은 일부러 그대로 둔다. 마지막에 무엇에 얼마나 맞고 죽었는지가
        // 남아 있어야 하기 때문이다. 잠시 뒤 알아서 닫힌다.

        Image imageComponent = LevelImage.GetComponent<Image>();
        imageComponent.color = LevelImageColor;
        LevelTextMeshPro.text = "게임 오버";

        // 죽기 직전에 누르고 있던 키가 곧바로 화면을 넘겨 버리지 않도록 잠시 막는다.
        yield return new WaitForSeconds(GameOverHoldTime);

        while (!GameInput.ActionPressed)
            yield return null;

        // 다음 Awake 가 새 판으로 인식하도록, 씬을 넘어 살아남는 것들을 모두 정리한다.
        instance = null;
        Destroy(UiCanvas);
        Destroy(gameObject);

        SceneManager.LoadScene("SampleScene");
    }

    // ── 메시지 ─────────────────────────────────────────────────────────────

    /// <summary>메시지 창에 문구를 띄운다.</summary>
    public void ShowMessage(GameMessage message, string name = null, int amount = 0, string secondName = null)
    {
        messageBox.Show(MessageCatalog.Format(message, name, amount, secondName));
    }

    /// <summary>
    /// 이미 만들어진 글을 그대로 띄운다.
    ///
    /// MessageCatalog 를 거치지 않는 길이라 함부로 쓰면 문구가 코드 곳곳으로
    /// 흩어진다. 시트에서 읽어 온 글처럼 <b>문구를 코드가 정하지 않는 경우</b>에만 쓴다.
    /// </summary>
    public void ShowText(string text)
    {
        messageBox.Show(text);
    }

    /// <summary>
    /// 같은 글이 방금 떠 있었으면 다시 띄우지 않는다.
    ///
    /// 잠든 동안처럼 <b>매 턴 같은 말을 해야 하는</b> 경우에 쓴다. 그대로 띄우면
    /// 이력이 같은 줄로 가득 차고, 화면에서는 이미 있는 글을 다시 그리기만 한다.
    ///
    /// 사이에 다른 일이 끼면(적에게 맞는 등) 그 글이 마지막이 되므로 다시 뜬다.
    /// 그래야 "맞았다 → 그래도 자고 있다" 가 이어서 읽힌다.
    /// </summary>
    public void ShowMessageUnlessRepeated(GameMessage message, string name = null, int amount = 0)
    {
        string text = MessageCatalog.Format(message, name, amount);
        if (messageBox.LastShown == text) return;

        messageBox.Show(text);
    }

    /// <summary>
    /// 두 줄을 한 상자에 담아 띄운다.
    ///
    /// 잇달아 Show 를 두 번 부르면 앞의 것이 곧바로 덮여 읽을 수 없다.
    /// "무엇이 일어났고 그래서 어떻게 됐다" 처럼 짝을 이루는 글에 쓴다.
    /// </summary>
    public void ShowTwoLines(string first, string second)
    {
        messageBox.Show(first + "\n" + second);
    }

    /// <summary>적을 쓰러뜨렸을 때: 경험치를 주고, 레벨업했으면 그것도 알린다.</summary>
    public void RewardForKill(string enemyName, int exp)
    {
        // 장비가 경험치를 더 얹어 준다. 표시되는 값도 함께 늘어야
        // "왜 도감의 3 보다 많이 들어오지" 가 되지 않는다.
        exp += Gear != null ? Gear.AbilityValue(EquipAbility.BonusExp) : 0;

        ShowMessage(GameMessage.EnemyDefeated, enemyName, exp);

        if (Stats.AddExp(exp))
            StartCoroutine(Announce($"레벨이 {Stats.Level}(으)로 올랐다!"));
    }

    /// <summary>
    /// 놓치면 안 되는 것을 알리고, 플레이어가 확인할 때까지 기다린다.
    ///
    /// 기다리는 동안 조작을 막는다. 그러지 않으면 확인하려고 누른 A 가
    /// 그대로 공격이 되어 한 턴이 지나가 버린다.
    ///
    /// 레벨업이 지금 유일한 사용처지만, 앞으로 붙을 대화도 이 길을 쓴다.
    /// </summary>
    public IEnumerator Announce(string message)
    {
        InputFocus.Give(InputOwner.Prompt);

        yield return messageBox.ShowImportant(message);

        InputFocus.ReturnToPlayer();
    }

    /// <summary>
    /// 개발용: 확인 없이 곧바로 아래층으로 내려간다.
    /// 씬을 다시 불러오면 새로 생긴 GameManager 가 StartNextStage() 를 부른다.
    /// </summary>
    public void DescendImmediately()
    {
        SceneManager.LoadScene("SampleScene");
    }

    // ── 함정 효과 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 함정이 작동했을 때 실제로 일어나는 일. 종류는 시트의 id2 로 갈린다.
    ///
    /// 갈래를 <b>번호가 아니라 이름으로</b> 나누는 이유는, 코드에 10005 라고
    /// 적혀 있으면 그게 무엇인지 알 수 없기 때문이다(문서 5절).
    /// note 열의 글자로 나누지 않는 이유도 같다 — 그건 사람이 읽는 메모라
    /// 말투를 다듬는 순간 게임이 조용히 바뀐다.
    ///
    /// 아직 만들지 않은 함정은 아무 일도 하지 않는다. 문구는 이미 떴으므로
    /// 플레이어에게는 "밟았지만 별일 없었다" 로 보인다.
    /// </summary>
    public void ApplyTrapEffect(string trapId, TrapAnimation animation = TrapAnimation.None)
    {
        // 몸짓을 먼저 보여 준다.
        //
        // 워프만 예외다. 워프의 '빙글' 은 위로 올라가는 동안 도는 것이라
        // 그 연출 안에 이미 들어 있고, 여기서 또 돌리면 두 번 돈다.
        if (trapId != TrapIds.Warp) StartCoroutine(PlayTrapAnimation(animation));

        switch (trapId)
        {
            case TrapIds.Hole:
                StartCoroutine(FallThroughHole());
                break;

            case TrapIds.Warp:
                StartCoroutine(WarpPlayer());
                break;

            case TrapIds.Blow:
                StartCoroutine(BlowPlayer(-LookDirection, TrapEffects.BlowDistance));
                break;

            case TrapIds.Dart:
                LoseStrength(TrapEffects.DartStrengthLoss);
                break;

            // 아래 셋은 거는 상태만 다르다. 지속 턴도 문구도 시트가 정한다.
            case TrapIds.Gas:
                ApplyCondition(ConditionCatalog.SleepId);
                break;

            case TrapIds.Spin:
                ApplyCondition(ConditionCatalog.DisarrayId);
                break;

            case TrapIds.Restraint:
                ApplyCondition(ConditionCatalog.RestraintId);
                break;

            case TrapIds.Sticky:
                MakeRandomBagItemSticky();
                break;
        }
    }

    /// <summary>
    /// 구멍. 방어력을 무시하고 아프게 한 뒤 다음 층으로 떨어뜨린다.
    ///
    /// 피해가 먼저다. 이 피해로 죽을 수 있고, 그때는 내려가지 않고 거기서 끝난다.
    /// 순서를 뒤집으면 죽은 채로 다음 층이 시작된다.
    /// </summary>
    private IEnumerator FallThroughHole()
    {
        Player player = PlayerComponent;

        // 떨어지는 것도 강제 이동이다. 바닥을 뚫고 내려가는 중에 적이 때리면
        // 이미 그 층에 없는 사람을 때리는 셈이 된다.
        ForcedMoveRunning = true;
        InputFocus.Give(InputOwner.Prompt);

        // 먼저 사라지고 나서 아프다. 순서를 뒤집으면 멀쩡히 서 있는 캐릭터가
        // 느닷없이 5 만큼 깎여서, 무엇 때문에 아픈지가 화면에 없다.
        if (player != null) yield return player.FallThroughFloor();

        Stats.TakeDamage(TrapEffects.HoleDamage);
        ShowMessage(GameMessage.PlayerDamaged, amount: TrapEffects.HoleDamage);

        if (Stats.IsDead)
        {
            // 층이 넘어가지 않으므로 여기서 되돌리지 않으면 보이지 않는 채로 죽는다.
            player?.RestoreAfterFall();
            InputFocus.ReturnToPlayer();
            ForcedMoveRunning = false;
            GameOver();
            yield break;
        }

        // 문구를 읽을 틈을 준다. 달리는 중이면 건너뛴다.
        if (!FastForward) yield return new WaitForSeconds(TrapMessagePause);

        player?.RestoreAfterFall();
        InputFocus.ReturnToPlayer();
        ForcedMoveRunning = false;

        SceneManager.LoadScene("SampleScene");
    }

    /// <summary>
    /// 워프. 다른 방의 빈 칸으로 옮긴다.
    ///
    /// 함정 전용이 아니다 — 지팡이·능력에서도 그대로 부르면 된다.
    /// 옮기는 동안 ForcedMoveRunning 을 세워, 적이 지난 자리를 기준으로
    /// 움직이거나 때리지 않게 한다.
    ///
    /// 갈 곳을 먼저 정하고 나서 연출을 시작한다. 연출을 띄워 놓고 자리를 못
    /// 찾으면 올라갔다가 제자리로 내려오는 꼴이 되기 때문이다.
    /// </summary>
    public IEnumerator WarpPlayer()
    {
        Player player = PlayerComponent;
        if (player == null || MapMake == null || FloorTilemap == null) yield break;

        // 자리를 고르는 일은 전부 칸 좌표로 한다. 월드 좌표는 칸 중심이 x+0.5 라서
        // 그걸로 계산하면 반 칸씩 어긋난다(TrapEffects 주석). 월드로 되돌리는 것은
        // 맨 마지막 한 번뿐이다.
        bool found = TrapEffects.TryPickWarpDestination(
            MapMake.RoomCells,
            EnemyCells(),
            FloorTilemap.WorldToCell(player.transform.position),
            // using System 이 있어 Random 만으로는 어느 쪽인지 가려지지 않는다.
            (min, max) => UnityEngine.Random.Range(min, max),
            out Vector3Int destinationCell);

        // 갈 곳이 없으면(방이 하나뿐이고 적으로 가득 찬 경우) 아무 일도 없다.
        if (!found) yield break;

        ForcedMoveRunning = true;
        InputFocus.Give(InputOwner.Prompt);

        yield return player.WarpTo(GridUtil.CellCenter(FloorTilemap, destinationCell));

        InputFocus.ReturnToPlayer();
        ForcedMoveRunning = false;
    }

    /// <summary>
    /// 날리기. direction 방향으로 cells 칸 밀어낸다.
    ///
    /// 함정 전용이 아니다 — 밀어내는 지팡이나 능력도 이것을 부르면 된다.
    /// 그래서 방향과 거리를 밖에서 받는다.
    ///
    /// 날아가는 판정은 던진 아이템과 같은 RangedShot 을 쓴다. 같은 규칙으로
    /// 날아가야 "던진 것은 저기까지 가는데 나는 왜 여기서 멈추지" 가 안 생긴다.
    /// </summary>
    public IEnumerator BlowPlayer(Vector2 direction, int cells)
    {
        Player player = PlayerComponent;
        if (player == null || FloorTilemap == null) yield break;
        if (direction == Vector2.zero || cells <= 0) yield break;

        var step = new Vector2Int(
            Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));

        Vector3Int origin = FloorTilemap.WorldToCell(player.transform.position);

        // 살아 있는 적을 판정 대상으로 넘긴다. 부딪히면 거기서 멈춘다.
        var living = new List<Enemy>();
        var targets = new List<Vector3Int>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            living.Add(enemy);
            targets.Add(FloorTilemap.WorldToCell(enemy.transform.position));
        }

        RangedShot.Result shot = RangedShot.Trace(
            new TilemapWalkableGrid(FloorTilemap), origin, step, cells, targets);

        // 적에게 부딪혔으면 그 앞 칸이 내가 설 자리다.
        // 같은 칸에 겹쳐 서면 둘 다 빠져나오지 못한다.
        Vector3Int landingCell = shot.HitTarget
            ? new Vector3Int(shot.ImpactCell.x - step.x, shot.ImpactCell.y - step.y, 0)
            : shot.ImpactCell;

        Vector3 landing = GridUtil.CellCenter(FloorTilemap, landingCell);

        ForcedMoveRunning = true;
        InputFocus.Give(InputOwner.Prompt);

        yield return player.BlowTo(landing, direction, shot.StoppedByWall || shot.HitTarget);

        // 부딪힌 적이 있으면 둘 다 아프다. 서로 부딪힌 것이므로 한쪽만 아플 이유가 없다.
        if (shot.HitTarget)
            yield return CollideWith(living[shot.TargetIndex]);

        // 떨어진 자리의 땅이 무엇이냐에 따라 갈린다.
        TrapEffects.BlowOutcome outcome = TrapEffects.ResolveBlow(
            TileRules.KindAt(FloorTilemap, landing), shot.StoppedByWall);

        yield return ApplyBlowOutcome(outcome);

        InputFocus.ReturnToPlayer();
        ForcedMoveRunning = false;
    }

    /// <summary>날려지다 적과 부딪혔다. 둘 다 방어력 무시 피해를 입는다.</summary>
    private IEnumerator CollideWith(Enemy target)
    {
        if (target == null) yield break;

        ShowTwoLines(
            MessageCatalog.Format(GameMessage.BlownIntoEnemy, target.DisplayName),
            MessageCatalog.Format(GameMessage.PlayerDamaged, amount: TrapEffects.BlowWallDamage));

        // 맞은 쪽이 먼저다. 플레이어가 여기서 죽으면 게임 오버로 넘어가는데,
        // 그 뒤에 적을 때리면 이미 끝난 판에서 경험치가 들어온다.
        target.TakeTrueDamage(TrapEffects.BlowWallDamage);
        Stats.TakeDamage(TrapEffects.BlowWallDamage);

        if (Stats.IsDead) GameOver();

        if (!FastForward) yield return new WaitForSeconds(TrapMessagePause);
    }

    /// <summary>살아 있는 적들이 선 칸.</summary>
    private List<Vector3Int> EnemyCells()
    {
        var cells = new List<Vector3Int>();
        if (FloorTilemap == null) return cells;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
                cells.Add(FloorTilemap.WorldToCell(enemy.transform.position));
        }
        return cells;
    }

    /// <summary>날려진 끝에 일어나는 일. 판정은 TrapEffects 가 이미 정했다.</summary>
    private IEnumerator ApplyBlowOutcome(TrapEffects.BlowOutcome outcome)
    {
        switch (outcome)
        {
            case TrapEffects.BlowOutcome.HitWall:
                ShowTwoLines(
                    MessageCatalog.Format(GameMessage.BlownIntoWall),
                    MessageCatalog.Format(
                        GameMessage.PlayerDamaged, amount: TrapEffects.BlowWallDamage));
                Stats.TakeDamage(TrapEffects.BlowWallDamage);
                if (Stats.IsDead) GameOver();
                break;

            case TrapEffects.BlowOutcome.Water:
                ShowMessage(GameMessage.BlownIntoWater);
                if (!FastForward) yield return new WaitForSeconds(TrapMessagePause);

                // 물에 들어갈 수 없는 상태로 물 한가운데 놓이면 나올 길이 없다.
                //
                // 안쪽의 WarpPlayer 가 끝나면서 ForcedMoveRunning 을 내린다.
                // 그 시점에는 옮기는 일이 이미 다 끝나 있어서, 그때 적 턴이
                // 풀려도 적은 새 자리를 보고 움직인다.
                yield return WarpPlayer();
                break;

            case TrapEffects.BlowOutcome.Lava:
                int burn = TrapEffects.LavaDamage(Stats.Hp);
                ShowTwoLines(
                    MessageCatalog.Format(GameMessage.BlownIntoLava),
                    MessageCatalog.Format(GameMessage.PlayerDamaged, amount: burn));
                Stats.TakeDamage(burn);

                if (Stats.IsDead) { GameOver(); break; }

                if (!FastForward) yield return new WaitForSeconds(TrapMessagePause);
                yield return WarpPlayer();
                break;

            case TrapEffects.BlowOutcome.Pit:
                // 절벽은 구멍 함정과 똑같이 다룬다. 같은 일이 일어난 것이므로
                // 처리도 한 곳에 두어야 한쪽만 고쳐지는 일이 없다.
                yield return FallThroughHole();
                break;
        }
    }

    // ── 상태이상 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 상태이상을 건다. <b>함정 · 아이템 · 능력이 모두 이 한 곳을 쓴다.</b>
    ///
    /// 지속 턴은 시트가 정한다(Conditions 시트의 turn 열). 거는 쪽마다 숫자를
    /// 들고 있으면 같은 '수면' 인데 함정은 5턴, 두루마리는 8턴이 되어 버린다.
    /// 길이를 바꾸고 싶으면 엑셀 한 칸만 고치면 된다.
    ///
    /// 표에 없는 id 를 주면 아무 일도 하지 않고 알린다 — 조용히 안 걸리면
    /// "왜 함정이 아무것도 안 하지" 를 함정 코드에서 찾게 된다.
    /// </summary>
    public void ApplyCondition(string conditionId)
    {
        ConditionDefinition definition = ConditionCatalog.Find(conditionId);
        if (definition == null)
        {
            Debug.LogWarning($"상태이상 '{conditionId}' 를 표에서 찾지 못했습니다.");
            return;
        }

        // 일부러 거는 상태인데 표가 "효과 없음" 이라고 하면 걸리기만 하고 아무 일도
        // 일어나지 않는다. 상태창에는 뜨는데 게임은 그대로라 알아채기가 어렵다 —
        // 실제로 잠·혼란·봉인 셋이 그 상태로 한동안 돌아갔다.
        //
        // 만복도에서 저절로 나오는 것들(배고픔 등)은 이 길을 지나지 않으므로
        // 여기서 걸러도 시끄러워지지 않는다.
        if (definition.Effect == ConditionEffect.None)
        {
            Debug.LogWarning(
                $"상태이상 '{conditionId}'({definition.Name}) 의 효과가 '없음' 입니다. " +
                "엑셀 Conditions 시트의 effect 열을 확인하세요.");
        }

        bool wasAlready = Conditions.Has(conditionId);
        Conditions.Apply(conditionId, definition.DurationTurns);

        // 이미 걸려 있던 것에 또 걸리면 알리지 않는다. 같은 함정을 연달아 밟을 때
        // "잠들었다!" 가 계속 뜨면 정작 그 사이의 다른 소식이 묻힌다.
        if (!wasAlready)
            ShowText(MessageCatalog.Format(GameMessage.ConditionApplied, definition.Name));
    }

    /// <summary>함정이 시키는 몸짓. 플레이어가 없으면 아무 일도 하지 않는다.</summary>
    private IEnumerator PlayTrapAnimation(TrapAnimation animation)
    {
        if (animation == TrapAnimation.None) yield break;

        Player player = PlayerComponent;
        if (player != null) yield return player.PlayTrapAnimation(animation);
    }

    /// <summary>
    /// 가방의 아이템 하나를 끈적끈적하게 만든다. 끈적끈적 함정이 쓴다.
    ///
    /// 이미 끈적한 것은 고르지 않는다. 그러지 않으면 가방에 끈적한 것이 하나만
    /// 남았을 때 함정이 아무 일도 하지 않은 것처럼 보인다.
    /// </summary>
    public void MakeRandomBagItemSticky()
    {
        var candidates = new List<GameObject>();
        for (int i = 0; i < Bag.Count; i++)
        {
            GameObject item = Bag[i];
            if (item != null && !ItemInstance.IsSticky(item)) candidates.Add(item);
        }

        if (candidates.Count == 0) return;

        GameObject chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        ItemInstance.SetSticky(chosen, true);

        ShowMessage(GameMessage.BecameSticky, ItemInstance.DisplayNameOf(chosen));
    }

    /// <summary>
    /// 힘을 깎는다. 독침 함정 · 앞으로 만들 아이템이 함께 쓴다.
    ///
    /// 이미 0 이면 아무 말도 하지 않는다. "힘이 0 깎였다!" 는 거짓말이다.
    /// </summary>
    public void LoseStrength(int amount)
    {
        int lost = Stats.LoseStrength(amount);
        if (lost > 0) ShowMessage(GameMessage.StrengthLost, amount: lost);
    }

    /// <summary>이 상태가 걸려 있는가. 능력·행동이 막혔는지 물을 때 쓴다.</summary>
    public static bool HasCondition(string conditionId) => Conditions.Has(conditionId);

    /// <summary>
    /// 걸려 있는 상태들 중 이 효과를 가진 것이 있는가.
    ///
    /// id 가 아니라 <b>효과</b>로 묻는다. 잠이 얕은 것과 깊은 것 둘로 나뉘어
    /// 있듯이 같은 효과를 내는 상태가 여럿일 수 있고, 부르는 쪽이 그 목록을
    /// 외우고 있으면 표에 하나 더할 때마다 코드를 고쳐야 한다.
    /// </summary>
    public static bool HasEffect(ConditionEffect effect)
    {
        foreach (string id in Conditions.ActiveIds)
        {
            ConditionDefinition definition = ConditionCatalog.Find(id);
            if (definition != null && definition.Effect == effect) return true;
        }
        return false;
    }

    /// <summary>한 턴이 지났다. 남은 턴을 줄이고 풀린 것을 알린다.</summary>
    private void TickConditions()
    {
        foreach (string id in Conditions.Tick())
        {
            ConditionDefinition definition = ConditionCatalog.Find(id);
            if (definition != null)
                ShowText(MessageCatalog.Format(GameMessage.ConditionEnded, definition.Name));
        }
    }

    /// <summary>함정 문구를 읽을 틈. 층이 넘어가기 전에 이만큼 기다린다.</summary>
    private const float TrapMessagePause = 1f;

    /// <summary>계단 위에서 아래층으로 내려갈지 묻는다.</summary>
    public IEnumerator PromptDescend()
    {
        InputFocus.Give(InputOwner.Prompt);
        messageBox.ShowPersistent(MessageCatalog.Format(GameMessage.StairsPrompt));

        var answer = new MessageBoxView.Choice();
        yield return messageBox.AskYesNo(answer);

        InputFocus.ReturnToPlayer();

        if (answer.Value)
            SceneManager.LoadScene("SampleScene");
    }

    // ── 턴 진행 ────────────────────────────────────────────────────────────

    /// <summary>플레이어 행동이 끝났다. 적들에게 차례를 넘긴다.</summary>
    public void EndPlayerTurn()
    {
        StartCoroutine(MoveEnemies());
    }

    /// <summary>메시지를 띄우고 차례를 넘긴다.</summary>
    public void EndPlayerTurn(GameMessage message, string name = null, int amount = 0, string secondName = null)
    {
        ShowMessage(message, name, amount, secondName);
        EndPlayerTurn();
    }

    private IEnumerator MoveEnemies()
    {
        playersTurn = false;

        // 중요한 알림을 읽는 동안에는 적을 움직이지 않는다.
        //
        // 레벨업 같은 것을 읽으라고 띄워 놓고 그 사이에 적이 때리면, 읽을 수도 없고
        // 맞은 것도 못 본다. 확인을 누를 때까지 턴 자체를 멈춘다.
        // 턴 수도 아직 올리지 않는다 — 굶주림 같은 것이 읽는 동안 흘러가면 안 된다.
        while (messageBox.ShowingImportant)
            yield return null;

        // 워프·날리기가 끝나기를 기다린다. 옮겨진 뒤의 자리를 기준으로 움직여야
        // 적이 빈 칸을 쫓거나 때리는 일이 없다.
        while (ForcedMoveRunning)
            yield return null;

        TurnCount++;

        ApplyHungerTick();
        TickConditions();

        // 빨리감기 중에는 연출용 대기를 건너뛴다. 이 대기가 없으면 아래 과정이
        // 전부 같은 프레임 안에서 끝나므로, 달리기 한 번이 한 프레임에 처리된다.
        if (!FastForward)
            yield return new WaitForSeconds(EnemyTurnDelay);

        // 적들이 서로 같은 칸으로 몰리지 않도록, 이번 턴의 예약 위치를 새로 받는다.
        enemyPositions.Clear();

        foreach (Enemy enemy in enemies)
            if (enemy != null) enemy.BeginTurn();

        // 이번 턴의 길찾기 지도를 한 번만 만든다. 적이 몇 마리든 탐색은 한 번이다.
        if (enemies.Count > 0 && Player != null && FloorTilemap != null)
            Chase.Rebuild(new TilemapWalkableGrid(FloorTilemap), Player.transform.position);

        // ── 적의 한 턴은 세 단계로 나뉜다 ──────────────────────────────────
        //
        // 예전에는 적 하나하나를 처음부터 끝까지 차례로 돌렸다. 그런데 이동하는 적은
        // 저마다 WaitForFixedUpdate 를 한 번씩 기다리므로, 그 대기가 마릿수만큼
        // 쌓여서 적이 많은 방일수록 플레이어 조작이 눈에 띄게 굼떠졌다.
        //
        // 차례로 돌아야 하는 것은 '공격' 뿐이다. 겹쳐 때리면 누구에게 얼마나 맞았는지
        // 메시지창에 마지막 하나만 남기 때문이다. 이동은 겹쳐도 아무 문제가 없다.

        // 1. 무엇을 할지 먼저 전부 정한다.
        //    이동보다 먼저 정해 두어야, 앞의 적이 움직여 붙는 바람에 뒤의 적 판단이
        //    달라지는 일이 없다. 목록 순서가 규칙이 되어서는 안 된다.
        //
        //    적이 행동하다 죽어 목록에서 빠질 수 있으므로 복사본을 돌린다.
        var movers = new List<Enemy>();
        var attackers = new List<Enemy>();

        foreach (Enemy enemy in new List<Enemy>(enemies))
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (enemy.DecideAttack()) attackers.Add(enemy);
            else movers.Add(enemy);
        }

        // 2. 이동은 마릿수와 상관없이 같은 프레임에 끝낸다.
        //    같은 칸으로 겹쳐 들어가는 것은 enemyPositions 예약이 막아 준다.
        //
        //    가까운 적부터 움직인다. 줄지어 쫓아올 때 앞선 적이 먼저 비켜 주어야
        //    뒤의 적이 그 칸을 이어받는다. 순서가 반대면 뒤의 적이 아직 차 있는
        //    칸을 보고 물러서서, 도망치는 플레이어와의 간격이 계속 벌어진다.
        movers.Sort((a, b) => a.StepsToPlayer.CompareTo(b.StepsToPlayer));

        foreach (Enemy enemy in movers)
            enemy.TakeStep();

        // 옮긴 좌표(MovePosition)가 화면에 반영되도록 한 번만 기다린다.
        // 적이 백 마리든 이 대기는 한 번뿐이다.
        if (!FastForward && movers.Count > 0)
            yield return new WaitForFixedUpdate();

        // 3. 때리는 적만 한 마리씩. 플레이어에 붙어 있는 것들뿐이라 많아야 여덟이다.
        foreach (Enemy enemy in attackers)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (FastForward)
            {
                // 빨리감기 중에는 공격에 기다림이 없다(Enemy.AttackTurn).
                // 코루틴으로 넘기면 그것만으로 한 프레임을 쓰므로 그 자리에서 돌린다.
                IEnumerator turn = enemy.AttackTurn();
                while (turn.MoveNext()) { }
            }
            else
            {
                yield return enemy.AttackTurn();
            }

            // 도중에 죽었으면 나머지 적을 움직일 이유가 없다.
            if (Stats.IsDead) break;
        }

        // 여기서 InteractionHandled 를 건드리지 않는다.
        //
        // 그 값은 "밟은 칸을 아직 살펴보지 않았다" 는 뜻이고, 새 칸에 도착할 때(TryStep)
        // 열려서 실제로 처리한 순간(OnTriggerStay2D)에 닫힌다. 즉 턴이 아니라
        // '도착' 단위의 표시다.
        //
        // 예전에는 턴이 끝날 때 여기서 닫았는데, 달리는 중에는 이 코루틴이 동기로
        // 끝나기 때문에 물리 프레임이 오기도 전에 닫혀 버렸다. 그래서 달려서 밟은
        // 함정·계단·아이템이 하나도 처리되지 않았다 — 달리기로 함정을 통째로
        // 피할 수 있었다는 뜻이다.
        //
        // 놓자마자 도로 줍는 것을 막는 일은 Player.placedUnderFoot 가 따로 맡고 있어서,
        // 여기서 닫지 않아도 그 보호는 그대로다.
        playersTurn = true;
    }

    /// <summary>턴이 지날 때마다 배가 고파지고, 굶으면 체력이 깎인다.</summary>
    /// <summary>
    /// 만복도가 줄어드는 간격. 장비가 늘려 준다.
    ///
    /// 간격이 길어질수록 배가 늦게 고파진다. 소모량을 줄이지 않고 간격을 늘리는
    /// 이유는, 소모량은 이미 1 이라 더 줄일 수가 없기 때문이다.
    /// </summary>
    private int HungerInterval
        => HungerTickInterval + (Gear != null ? Gear.AbilityValue(EquipAbility.SlowHunger) : 0);

    private void ApplyHungerTick()
    {
        if (TurnCount % HungerInterval == 0 && !Stats.IsStarving)
        {
            int before = Stats.Hunger;
            Stats.ConsumeHunger();

            HungerWarning.Level level =
                HungerWarning.For(before, Stats.Hunger, Stats.MaxHunger);

            if (level != HungerWarning.Level.None)
                ShowMessage(HungerWarning.MessageOf(level));

            if (!Stats.IsHpFull && TurnCount % RecoveryTickInterval == 0)
                Stats.Heal(Stats.NaturalRecoveryAmount());
        }

        if (Stats.IsStarving && !Stats.IsDead)
        {
            Stats.TakeDamage(1);
            GameOver();
        }
    }

    public void AddEnemyToList(Enemy enemy) => enemies.Add(enemy);

    public void RemoveEnemyList(Enemy enemy) => enemies.Remove(enemy);

    public void DestroyAllEnemies()
    {
        foreach (Enemy enemy in enemies)
            Destroy(enemy.gameObject);

        enemies.Clear();
    }

    // ── 가방 ───────────────────────────────────────────────────────────────

    /// <summary>가방을 연다. 닫히거나 아이템을 쓸 때까지 조작이 잠긴다.</summary>
    public void OpenBag() => OpenItemMenu(startOnGround: false);

    /// <summary>
    /// 발밑 화면을 연다. 밟고 선 것이 무엇이냐에 따라 하는 일이 다르다.
    ///
    ///   함정  - 다시 밟아 작동을 시도한다(턴을 쓴다)
    ///   아이템 - 줍기 / 교환하기 화면을 연다
    ///   아무것도 없으면 그렇다고 알린다(턴을 쓰지 않는다)
    ///
    /// 함정을 먼저 보는 이유는, 함정과 아이템이 같은 칸에 놓이지 않도록
    /// 던전을 만들 때 떨어뜨려 두기 때문이다(DungeonPopulator.PlaceTraps).
    /// 그래도 겹치는 일이 생기면 함정 쪽이 급한 일이다.
    /// </summary>
    public void UseUnderFoot()
    {
        Player player = PlayerComponent;

        if (player != null && player.TrapUnderFoot != null)
        {
            player.TrapUnderFoot.TryTrigger();
            EndPlayerTurn();
            return;
        }

        if (player != null && player.ItemUnderFoot != null)
        {
            OpenItemMenu(startOnGround: true);
            return;
        }

        ShowMessage(GameMessage.NothingUnderFoot);
    }

    private void OpenItemMenu(bool startOnGround)
    {
        InputFocus.Give(InputOwner.BagMenu);
        messageBox.Hide();

        Player player = PlayerComponent;
        GameObject ground = player != null ? player.ItemUnderFoot : null;

        var menu = new BagMenuController(
            menuPanel, messageBox, Bag, Gear, ground, startOnGround);

        StartCoroutine(RunBagMenu(menu, ground));
    }

    /// <summary>씬의 플레이어에 붙은 Player 컴포넌트. 없으면 null.</summary>
    private Player PlayerComponent => Player != null ? Player.GetComponent<Player>() : null;

    private IEnumerator RunBagMenu(BagMenuController menu, GameObject groundItem)
    {
        BagMenuController.ItemAction? action = null;
        int index = 0;

        yield return menu.Run((chosen, at) =>
        {
            action = chosen;
            index = at;
        });

        if (action == null)
        {
            // 아무것도 안 하고 닫았다. 턴은 지나가지 않는다.
            InputFocus.ReturnToPlayer();
            yield break;
        }

        switch (action.Value)
        {
            case BagMenuController.ItemAction.PickUp:
                PickUpFromGround(groundItem);
                yield break;

            case BagMenuController.ItemAction.Swap:
                SwapWithGround(groundItem, index);
                yield break;

            case BagMenuController.ItemAction.GroundUse:
                yield return UseOnGround(groundItem);
                yield break;

            case BagMenuController.ItemAction.GroundThrow:
                yield return ThrowFromGround(groundItem);
                yield break;
        }

        yield return PerformItemAction(action.Value, index);
    }

    /// <summary>
    /// 발밑의 것을 줍는다.
    ///
    /// 가방이 가득 차 있으면 줍지 못하고, 이때는 턴도 쓰지 않는다.
    /// 아무 일도 일어나지 않았는데 적이 한 걸음 다가오면 억울하기 때문이다.
    /// </summary>
    private void PickUpFromGround(GameObject groundItem)
    {
        InputFocus.ReturnToPlayer();

        if (groundItem == null) return;

        // 돈은 가방에 넣는 물건이 아니라 소지금으로 들어간다.
        // 그래서 가방이 가득 차 있어도 주울 수 있다.
        if (GoldPile.IsGold(groundItem))
        {
            int amount = GoldPile.AmountOf(groundItem);
            Stats.AddGold(amount);

            Ground.Remove(groundItem.transform.position);
            groundItem.SetActive(false);

            EndPlayerTurn(GameMessage.GoldPickedUp, amount: amount);
            return;
        }

        if (Bag.IsFull)
        {
            ShowMessage(GameMessage.BagFull);
            return;
        }

        string itemName = TakeFromGround(groundItem);
        Bag.TryAdd(groundItem);

        EndPlayerTurn(GameMessage.ItemPickedUpFromGround, itemName);
    }

    /// <summary>
    /// 발밑의 것과 가방의 index 번째를 맞바꾼다.
    ///
    /// 가방이 가득 차 있어도 할 수 있다는 점이 '줍는다' 와 다르다.
    /// 개수가 늘지 않기 때문이다. 가방이 꽉 찼을 때 바닥의 더 좋은 물건을
    /// 가져갈 방법이 이것뿐이라, 이 경우를 막으면 교환의 쓸모가 거의 사라진다.
    /// </summary>
    private void SwapWithGround(GameObject groundItem, int index)
    {
        InputFocus.ReturnToPlayer();

        if (groundItem == null || Bag.Count == 0) return;

        // 차고 있는 끈적한 장비는 내놓을 수 없다. 교환도 가방을 떠나는 일이라
        // 여기서 막지 않으면 아래 UnequipIfWearing 이 조용히 벗겨 버린다.
        GameObject offered = Bag[index];
        if (Gear.IsEquipped(offered) && ItemInstance.IsSticky(offered))
        {
            ShowMessage(GameMessage.StickyStuck, ItemInstance.DisplayNameOf(offered));
            return;
        }

        string takenName = TakeFromGround(groundItem);

        // 몸에 걸치고 있던 것을 내려놓게 되면 반드시 벗겨야 한다.
        GameObject given = Bag.Replace(index, groundItem);
        string givenName = ItemInstance.DisplayNameOf(given);
        Gear.UnequipIfWearing(given);

        // 내려놓는 쪽은 씬으로 되돌려 그 자리에 놓는다.
        SceneManager.MoveGameObjectToScene(given, SceneManager.GetActiveScene());
        given.transform.position = PlayerPosition;
        given.SetActive(true);
        Ground.Add(PlayerPosition);

        // 바꿔 내놓은 것도 그 자리에서 도로 집으면 교환이 없던 일이 된다.
        PlayerComponent?.KeepUnderFootWithoutTaking(given);

        EndPlayerTurn(GameMessage.ItemSwapped, givenName, secondName: takenName);
    }

    /// <summary>
    /// 발밑의 것을 줍지 않고 그 자리에서 쓴다.
    ///
    /// 장비는 걸치는 것이라 가방을 거쳐야 한다. 몸에 걸친 물건은 가방 안에 있다는
    /// 것이 이 게임의 규칙이라(Equipment 주석), 바닥에 둔 채로 착용시키면
    /// "가방에 없는데 공격력이 붙어 있는" 상태가 된다. 그래서 장비는 먼저 줍는다.
    /// </summary>
    private IEnumerator UseOnGround(GameObject groundItem)
    {
        if (groundItem == null) { InputFocus.ReturnToPlayer(); yield break; }

        int itemId = ItemInstance.IdOf(groundItem);
        string itemName = ItemInstance.DisplayNameOf(groundItem);

        if (Gear.SlotOf(groundItem) != EquipmentSlot.None)
        {
            if (Bag.IsFull)
            {
                InputFocus.ReturnToPlayer();
                ShowMessage(GameMessage.BagFull);
                yield break;
            }

            TakeFromGround(groundItem);
            Bag.TryAdd(groundItem);
            ToggleEquip(groundItem, itemName);
            yield break;
        }

        // 끈적한 것은 바닥에 있든 가방에 있든 쓸 수 없다.
        if (ItemInstance.IsSticky(groundItem))
        {
            InputFocus.ReturnToPlayer();
            ShowMessage(GameMessage.StickyUnusable, itemName);
            yield break;
        }

        Ground.Remove(groundItem.transform.position);
        Destroy(groundItem);
        InputFocus.ReturnToPlayer();

        if (ItemEffects.TryUse(itemId, Stats, out GameMessage useMessage))
            EndPlayerTurn(useMessage, itemName);
        else
            EndPlayerTurn();
    }

    /// <summary>발밑의 것을 줍지 않고 그 자리에서 던진다.</summary>
    private IEnumerator ThrowFromGround(GameObject groundItem)
    {
        if (groundItem == null) { InputFocus.ReturnToPlayer(); yield break; }

        string itemName = ItemInstance.DisplayNameOf(groundItem);

        // 날아가는 동안에는 이 칸에 없으므로 바닥 목록에서 먼저 뺀다.
        // ItemThrower 가 떨어진 자리에 다시 넣어 준다.
        Ground.Remove(groundItem.transform.position);

        // 밟고 선 칸에 있으니 이미 같은 자리지만, 던지기는 출발점을 기준으로
        // 거리를 세므로 어긋난 채로 시작하지 않도록 맞춰 둔다.
        groundItem.transform.position = PlayerPosition;

        var thrower = new ItemThrower(FloorTilemap, Ground, enemies);
        GameMessage? result = null;

        yield return thrower.Throw(
            groundItem, LookDirection, Stats, message => result = message);

        InputFocus.ReturnToPlayer();

        if (result.HasValue) EndPlayerTurn(result.Value, itemName);
        else EndPlayerTurn();
    }

    /// <summary>바닥의 물건을 손에 넣을 수 있는 상태로 만든다. 이름을 돌려준다.</summary>
    private string TakeFromGround(GameObject groundItem)
    {
        DontDestroyOnLoad(groundItem);
        groundItem.name = groundItem.name.Replace("(Clone)", string.Empty);

        Ground.Remove(groundItem.transform.position);
        groundItem.SetActive(false);

        return groundItem.name;
    }

    /// <summary>가방에서 고른 행동을 실제로 수행한다.</summary>
    private IEnumerator PerformItemAction(BagMenuController.ItemAction action, int index)
    {
        GameObject item = Bag[index];
        int itemId = ItemInstance.IdOf(item);
        string itemName = ItemInstance.DisplayNameOf(item);

        // 장비는 가방에서 빠지지 않는다. 차고 있다는 표시만 바뀐다.
        if (action == BagMenuController.ItemAction.Use && Gear.SlotOf(item) != EquipmentSlot.None)
        {
            ToggleEquip(item, itemName);
            yield break;
        }

        // ── 끈적끈적 ───────────────────────────────────────────────────────
        //
        // 소모품이면 쓸 수 없다. 버리거나 던지는 것은 되므로 여기서만 막는다.
        if (action == BagMenuController.ItemAction.Use && ItemInstance.IsSticky(item))
        {
            InputFocus.ReturnToPlayer();
            ShowMessage(GameMessage.StickyUnusable, itemName);
            yield break;
        }

        // 차고 있는 끈적한 장비는 가방을 떠날 수 없다. 벗을 수 없다는 것이
        // 곧 "버리지도 던지지도 교환하지도 못한다" 는 뜻이다 — 여기서 막지 않으면
        // 아래 UnequipIfWearing 이 조용히 벗겨서 규칙에 구멍이 난다.
        if (Gear.IsEquipped(item) && ItemInstance.IsSticky(item))
        {
            InputFocus.ReturnToPlayer();
            ShowMessage(GameMessage.StickyStuck, itemName);
            yield break;
        }

        // 몸에 걸치고 있던 것이 가방을 떠나면 반드시 벗겨야 한다.
        // 그러지 않으면 손에 없는 물건의 공격력이 계속 붙어 있게 된다.
        Gear.UnequipIfWearing(item);

        // DontDestroyOnLoad 로 옮겨 뒀던 오브젝트를 현재 씬으로 되돌린다.
        SceneManager.MoveGameObjectToScene(item, SceneManager.GetActiveScene());
        Bag.RemoveAt(index);

        switch (action)
        {
            case BagMenuController.ItemAction.Use:
                Destroy(item);
                InputFocus.ReturnToPlayer();

                if (ItemEffects.TryUse(itemId, Stats, out GameMessage useMessage))
                    EndPlayerTurn(useMessage, itemName);
                else
                    EndPlayerTurn();
                break;

            case BagMenuController.ItemAction.Drop:
                item.transform.position = PlayerPosition;
                item.SetActive(true);
                Ground.Add(PlayerPosition);

                // 방금 내 손으로 놓은 것이므로 그 자리에서 도로 집지 않는다.
                PlayerComponent?.KeepUnderFootWithoutTaking(item);

                InputFocus.ReturnToPlayer();
                EndPlayerTurn(GameMessage.ItemDropped, itemName);
                break;

            case BagMenuController.ItemAction.Throw:
                item.transform.position = PlayerPosition;
                item.SetActive(true);

                var thrower = new ItemThrower(FloorTilemap, Ground, enemies);
                GameMessage? result = null;

                yield return thrower.Throw(
                    item, LookDirection, Stats,
                    message => result = message);

                InputFocus.ReturnToPlayer();

                // 적에게 맞았다면 그쪽에서 이미 결과를 알렸으므로 덧붙이지 않는다.
                if (result.HasValue)
                    EndPlayerTurn(result.Value, itemName);
                else
                    EndPlayerTurn();
                break;
        }
    }

    /// <summary>
    /// 장비를 차거나 벗는다. 어느 쪽이든 가방에는 그대로 남는다.
    /// 같은 슬롯에 이미 뭔가 차고 있었다면 그것과 바꿔 낀다.
    /// </summary>
    private void ToggleEquip(GameObject item, string itemName)
    {
        InputFocus.ReturnToPlayer();

        if (Gear.IsEquipped(item))
        {
            // 차는 것은 됐지만 벗는 것은 안 된다. 끈적한 장비를 집어 든 순간이
            // 아니라 <b>차는 순간</b>이 돌이킬 수 없는 지점이다.
            if (ItemInstance.IsSticky(item))
            {
                ShowMessage(GameMessage.StickyStuck, itemName);
                return;
            }

            Gear.Unequip(Gear.SlotOf(item));
            EndPlayerTurn(GameMessage.Unequipped, itemName);
            return;
        }

        Gear.TryEquip(item);
        EndPlayerTurn(GameMessage.Equipped, itemName);
    }

    /// <summary>바닥의 아이템을 줍는다. 가방이 가득 찼으면 줍지 못한다.</summary>
    public void PickUpItem(GameObject item)
    {
        if (Bag.IsFull)
        {
            ShowMessage(GameMessage.BagFull);
            return;
        }

        DontDestroyOnLoad(item);
        item.name = item.name.Replace("(Clone)", string.Empty);

        Bag.TryAdd(item);
        Ground.Remove(item.transform.position);

        ShowMessage(GameMessage.ItemPickedUp, item.name);
        item.SetActive(false);
    }
}
