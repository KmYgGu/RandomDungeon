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
///   ItemCommands       - 가방 화면 · 발밑 명령 · 줍기 · 교환 · 던지기 · 장착
///   ForcedMoves        - 구멍 · 워프 · 날리기와 그 결말
///   TurnLoop           - 플레이어가 행동한 뒤의 한 턴(동료 → 적 → 만복도)
///   StatusScreen       - 상태창과 그 메뉴
///
/// 협력 객체들은 MonoBehaviour 가 아니라 순수 C# 클래스다. 그래서 씬에
/// 컴포넌트를 새로 붙이거나 Inspector 참조를 다시 연결할 필요가 없다.
///
/// ── 여기 남아 있는 것 ─────────────────────────────────────────────
/// 남은 것은 <b>"언제 누구를 부를지"</b> 와, 여러 곳이 함께 쓰는 판 단위 상태
/// (Stats · Bag · Gear · Party · Conditions …)다. 상태이상을 거는 일도 여기
/// 남겼는데, 함정 · 아이템 · 능력이 모두 부르는 공용 동작이라 어느 한 파일의
/// 것이라고 하기 어렵기 때문이다. 규칙 자체는 이미 밖에 있다(ConditionRules).
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

    /// <summary>
    /// 동료가 한 번에 아무는 양.
    ///
    /// 플레이어는 힘에 비례해 회복하지만(PlayerStats.NaturalRecoveryAmount)
    /// 동료에게는 힘이 없다. 고정으로 두되, 동료의 체력이 플레이어보다 훨씬
    /// 적으므로(10~25) 같은 1 이라도 비율로는 더 크다.
    /// </summary>
    private const int PartnerNaturalRecovery = 1;

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

    /// <summary>
    /// 방금 어디서 소리가 났는가. 싸움·던지기·함정이 적고, 플레이어를 놓친
    /// 동료가 듣는다(앞으로 몇몇 적도 듣는다).
    ///
    /// <b>층 단위</b>다. 지난 층의 소리를 따라가면 갈 수 없는 자리를 향해 걷는다.
    /// StartNextStage 와 ResetRun 이 비운다.
    /// </summary>
    public static NoiseBoard Noise { get; } = new NoiseBoard();

    /// <summary>소리가 났다고 적는다. 칸 좌표로 적는다(월드 좌표를 받아 바꾼다).</summary>
    public static void ReportNoise(Vector3 where)
        => Noise.Report(
            new Vector3Int(Mathf.FloorToInt(where.x), Mathf.FloorToInt(where.y), 0), TurnCount);

    public static int Stage { get; private set; }
    public static int TurnCount { get; internal set; }

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
    public static bool ForcedMoveRunning { get; internal set; }
    // ──────────────────────────────────────────────────────────────────────

    public bool playersTurn = true;
    public List<Enemy> enemies = new List<Enemy>();

    /// <summary>이 층에 서 있는 동료 오브젝트들. 층이 바뀌면 새로 만들어진다.</summary>
    public List<Partner> partners = new List<Partner>();

    /// <summary>
    /// 데리고 다니는 동료 명단. <b>층을 넘어가도 남는 것은 이쪽</b>이고,
    /// 오브젝트(partners)는 층마다 이 명단대로 다시 만들어진다.
    /// 판이 끝나면 ResetRun 이 비운다.
    /// </summary>
    public static PartnerRoster Party { get; private set; } = new PartnerRoster();
    public List<Vector2> enemyPositions = new List<Vector2>();
    public Vector2 PlayerPosition;

    // 협력 객체들. Awake 에서 Inspector 참조를 넘겨 만든다.
    private HudView hud;
    private MessageBoxView messageBox;

    /// <summary>턴 흐름이 "중요한 알림을 읽는 중인가" 를 물을 때 쓴다.</summary>
    internal MessageBoxView MessageBox => messageBox;
    private StatusView statusView;
    private PanelView menuPanel;

    /// <summary>가방·아이템 관련 일 전부. 400 줄이라 여기서 떼어 냈다.</summary>
    private ItemCommands items;

    /// <summary>구멍·워프·날리기와 그 결말. 250 줄이라 여기서 떼어 냈다.</summary>
    private ForcedMoves forcedMoves;

    /// <summary>상태창과 그 메뉴. 메뉴가 늘어날 자리라 떼어 냈다.</summary>
    private StatusScreen statusScreen;

    /// <summary>한 턴의 흐름. 220 줄이라 여기서 떼어 냈다.</summary>
    private TurnLoop turnLoop;
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

        // 가방·아이템. 창을 둘 받아 두고 나머지는 GameManager 를 통해 쓴다.
        items = new ItemCommands(this, messageBox, menuPanel);

        // 구멍·워프·날리기.
        forcedMoves = new ForcedMoves(this);

        // 상태창과 그 메뉴. statusView 가 만들어진 뒤라야 한다.
        statusScreen = new StatusScreen(this, messageBox, menuPanel, statusView);

        // 한 턴의 흐름.
        turnLoop = new TurnLoop(this);


        // 장비는 씬을 넘어가도 유지되므로 한 번만 만든다.
        if (Gear == null) Gear = new Equipment();

        statusView.SetVisible(false);
        menuPanel.Hide();
    }
    /// <summary>
    /// 캐릭터 상태창을 연다. 실제 흐름은 StatusScreen 이 갖고 있다 —
    /// 메뉴가 늘어날 자리라 갓 오브젝트 밖으로 뺐다.
    /// </summary>
    public void OpenStatus() => statusScreen.OpenStatus();


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

        // 동료 명단. 비우지 않으면 죽은 판의 동료가 다음 판까지 따라온다 —
        // 에디터에서는 도메인 리로드가 가려 주고 빌드에서만 드러난다.
        Party.Clear();
        Noise.Clear();

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
        partners.Clear();          // 오브젝트는 씬과 함께 사라졌다. 명단(Party)은 남는다.
        enemyPositions.Clear();
        Ground.Clear();
        Noise.Clear();      // 지난 층의 소리를 새 층에서 따라가면 안 된다.
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
        // 어느 함정이 무엇을 하는지는 TrapActions 의 표가 안다.
        // 여기 있던 switch 는 함정을 하나 더할 때마다 이 파일을 열게 만들었다.
        // 함정이 터지는 소리.
        if (PlayerComponent != null) ReportNoise(PlayerComponent.transform.position);

        if (TrapActions.PlaysAnimationFirst(trapId))
            StartCoroutine(PlayTrapAnimation(animation));

        TrapActions.Find(trapId)?.Invoke(this);
    }

    // ── 표가 부르는 동작들 ─────────────────────────────────────────────────
    //
    // 코루틴을 시작하는 일은 MonoBehaviour 만 할 수 있어서 표가 직접 못 한다.
    // 그래서 "시작해 달라" 는 한 줄짜리 창구를 여기 둔다.

    public void RunFallThroughHole() => StartCoroutine(FallThroughHole());

    public void RunWarpPlayer() => StartCoroutine(WarpPlayer());

    /// <summary>등 뒤로 날아간다. 함정이 부르는 기본 거리로.</summary>
    public void RunBlowPlayer()
        => StartCoroutine(BlowPlayer(-LookDirection, TrapEffects.BlowDistance));

    // 구멍 · 워프 · 날리기의 실제 동작은 ForcedMoves 가 한다(250 줄).
    // 셋 다 "플레이어를 옮기는 동안 턴을 멈추고, 도착한 땅으로 결말이 갈린다" 는
    // 같은 흐름이라 한 파일에 모아 두었다.

    /// <summary>무작위 방으로 옮긴다. 함정 말고 지팡이·능력에서도 부른다.</summary>
    public IEnumerator WarpPlayer() => forcedMoves.WarpPlayer();

    /// <summary>그 방향으로 날려 보낸다. 함정 말고 지팡이·능력에서도 부른다.</summary>
    public IEnumerator BlowPlayer(Vector2 direction, int cells)
        => forcedMoves.BlowPlayer(direction, cells);

    /// <summary>구멍에 빠져 다음 층으로 떨어진다.</summary>
    public IEnumerator FallThroughHole() => forcedMoves.FallThroughHole();


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

        ShowMessage(GameMessage.BecameSticky, ItemInstance.FullNameOf(chosen));
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
        foreach (ConditionEffect active in ActiveEffects)
        {
            if (active == effect) return true;
        }
        return false;
    }

    /// <summary>
    /// 지금 걸려 있는 효과들. 무엇을 못 하게 되는지는 ConditionRules 가 정한다.
    ///
    /// 부르는 쪽이 효과를 직접 묻지 않게 하려고 이 목록을 내놓는다 —
    /// "SkipTurn 이 걸렸나" 가 아니라 "행동할 수 있나" 를 묻게 하기 위해서다.
    /// </summary>
    public static IEnumerable<ConditionEffect> ActiveEffects
    {
        get
        {
            foreach (string id in Conditions.ActiveIds)
            {
                ConditionDefinition definition = ConditionCatalog.Find(id);
                if (definition != null) yield return definition.Effect;
            }
        }
    }

    /// <summary>이번 턴에 스스로 행동할 수 있는가(잠·마비 따위에 걸리지 않았는가).</summary>
    public static bool PlayerCanAct => ConditionRules.CanAct(ActiveEffects);

    /// <summary>가려던 방향이 제멋대로 바뀌는가(혼란).</summary>
    public static bool PlayerDirectionScrambled
        => ConditionRules.ScramblesDirection(ActiveEffects);

    /// <summary>한 턴이 지났다. 남은 턴을 줄이고 풀린 것을 알린다.</summary>
    internal void TickConditions()
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

    /// <summary>
    /// 중립 동료에게 말을 건다. 승낙하면 그때부터 함께 다닌다.
    ///
    /// 계단 앞 물음과 같은 흐름을 쓴다 — 물어보는 동안 입력 권한을 가져가고,
    /// 답을 고르면 돌려준다. 물음 자체는 이력에 남기지 않는다(9절).
    ///
    /// <b>턴을 쓰지 않는다.</b> 말을 거는 것은 자리를 옮기지도 때리지도 않는데
    /// 그 사이에 적이 한 걸음 다가오면, 말을 걸었다는 이유로 손해를 본다.
    /// </summary>
    public IEnumerator PromptJoin(Partner partner)
    {
        if (partner == null) yield break;

        InputFocus.Give(InputOwner.Prompt);

        // 말을 걸 때 누른 그 A 를 물음이 그대로 읽어 버린다. 소비해 두지 않으면
        // 예/아니오가 뜨자마자 '예' 로 정해져, 고를 틈도 없이 동료가 된다.
        // 한 프레임을 흘려보내는 것은 가방 화면이 쓰는 것과 같은 장치다.
        GameInput.ConsumeAction();
        yield return null;

        // 만났을 때의 대사를 그대로 띄우고 예/아니오를 받는다.
        //
        // "함께 가겠습니까?" 를 따로 붙이지 않는다. 시트의 대사가 이미 묻고 있고
        // ("같이 동행할까?"), 예/아니오가 떠 있으면 무엇을 고르는지는 자명하다.
        // 두 번 물으면 같은 말이 겹쳐 보인다.
        //
        // 대사를 안 적어 둔 동료(지금 일곱)는 이름만 뜬다. 대사가 비는 것은
        // 잘못이 아니라서(PartnerLineSheet) 여기서 메꾸지 않는다.
        string meet = partner.MeetLine;

        messageBox.ShowPersistent(
            meet.Length > 0 ? $"{partner.DisplayName}: {meet}" : partner.DisplayName);

        var answer = new MessageBoxView.Choice();
        yield return messageBox.AskYesNo(answer);

        InputFocus.ReturnToPlayer();

        if (!answer.Value)
        {
            ShowMessage(GameMessage.PartnerDeclined, partner.DisplayName);
            yield break;
        }

        if (partner.TryJoin())
            ShowMessage(GameMessage.PartnerJoined, partner.DisplayName);
    }

    /// <summary>
    /// 이미 동료인 상대에게 말을 건다. 세 마디를 한 상자에 담는다.
    ///
    ///   지금 체력이 어떤지 — 숫자가 아니라 말투로(HpLine)
    ///   특기를 청하는 말   — 특기 자체는 아직 없다
    ///   다음 레벨까지      — 동료는 자기 경험치로 따로 자란다
    ///
    /// 나눠서 띄우면 앞의 것이 곧바로 덮여 읽을 수 없다. 대사를 안 적어 둔
    /// 동료는 그 줄만 빠진다 — 비는 것은 잘못이 아니다(PartnerLineSheet).
    ///
    /// <b>턴을 쓰지 않는다.</b> 물어보는 것뿐인데 그 사이에 적이 다가오면
    /// 말을 걸었다는 이유로 손해를 본다.
    /// </summary>
    public void TalkToPartner(Partner partner)
    {
        if (partner == null) return;

        // 특기는 식었고 그 동료에게 대사가 적혀 있을 때만 후보가 된다.
        bool actionAvailable =
            partner.ActionLine.Length > 0 && Party.IsActionReady(partner.Id, TurnCount);

        switch (PartnerPlan.PickTalk(UnityEngine.Random.value, actionAvailable))
        {
            case PartnerTalk.Action:
                Party.StartActionCooldown(partner.Id, TurnCount);
                ShowText($"{partner.DisplayName}: {partner.ActionLine}");
                return;

            case PartnerTalk.Hp:
                // 대사를 안 적어 둔 동료는 체력을 말할 수 없다. 그때는 경험치를
                // 알려 준다 — 아무 말도 안 하면 말을 건 것이 먹지 않은 것처럼 보인다.
                if (partner.HpLine.Length > 0)
                {
                    ShowText($"{partner.DisplayName}: {partner.HpLine}");
                    return;
                }
                break;
        }

        ShowMessage(GameMessage.PartnerExpRemaining, amount: partner.ExpRemaining);
    }

    // ── 턴 진행 ────────────────────────────────────────────────────────────

    /// <summary>플레이어 행동이 끝났다. 적들에게 차례를 넘긴다.</summary>
    public void EndPlayerTurn()
    {
        StartCoroutine(turnLoop.MoveEnemies());
    }

    /// <summary>메시지를 띄우고 차례를 넘긴다.</summary>
    public void EndPlayerTurn(GameMessage message, string name = null, int amount = 0, string secondName = null)
    {
        ShowMessage(message, name, amount, secondName);
        EndPlayerTurn();
    }
    // 한 턴의 흐름(동료 → 적 → 만복도 → 자연 회복)은 TurnLoop 이 갖고 있다.
    // 여기 남는 것은 "차례를 넘긴다" 는 창구뿐이다.



    public void AddPartner(Partner partner)
    {
        if (partner != null && !partners.Contains(partner)) partners.Add(partner);
    }

    public void RemovePartner(Partner partner) => partners.Remove(partner);

    /// <summary>이 칸에 서 있는 동료. 없으면 null. 자리 바꾸기가 쓴다.</summary>
    public Partner PartnerAt(Vector3 position)
    {
        foreach (Partner partner in partners)
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;

            if (Vector2.Distance(partner.transform.position, position) < 0.1f)
                return partner;
        }
        return null;
    }

    public void AddEnemyToList(Enemy enemy) => enemies.Add(enemy);

    public void RemoveEnemyList(Enemy enemy) => enemies.Remove(enemy);

    public void DestroyAllEnemies()
    {
        foreach (Enemy enemy in enemies)
            Destroy(enemy.gameObject);

        enemies.Clear();
    }

    // ── 가방·아이템 ────────────────────────────────────────────────────────
    //
    // 실제 일은 ItemCommands 가 한다(400 줄). 여기 남는 것은 바깥에서 부르는
    // 창구뿐이다 — Player 가 이 셋을 부른다.

    /// <summary>가방을 연다. 닫히거나 아이템을 쓸 때까지 조작이 잠긴다.</summary>
    public void OpenBag() => items.OpenBag();

    /// <summary>발밑의 것을 쓴다. 함정이면 다시 밟고, 아이템이면 화면을 연다.</summary>
    public void UseUnderFoot() => items.UseUnderFoot();

    /// <summary>바닥의 아이템을 줍는다. 가방이 가득 찼으면 줍지 못한다.</summary>
    public void PickUpItem(GameObject item) => items.PickUpItem(item);

    /// <summary>씬의 플레이어에 붙은 Player 컴포넌트. 없으면 null.</summary>
    public Player PlayerComponent => Player != null ? Player.GetComponent<Player>() : null;

}
