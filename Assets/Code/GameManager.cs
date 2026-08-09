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
///   ItemCatalog        - 아이템 이름 → 설명 조회
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
    public ItemDescription[] allItemDescriptions;

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
    private ItemCatalog itemCatalog;

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
            this, panel, TextimageBox, AttackTextMeshPro, menuPanel);

        statusView = new StatusView(
            StatusPanel,
            StatusWeaponIcon, StatusWeaponEnhance,
            StatusArmorIcon, StatusArmorEnhance,
            StatusAccessoryIcon, StatusAccessoryEnhance,
            StatusLeftColumn, StatusRightColumn);

        // 굶는 동안 화면의 모든 글자에 테를 두른다.
        starvingTint = new StarvingTint(UiCanvas);

        itemCatalog = new ItemCatalog(allItemDescriptions);

        // 장비는 씬을 넘어가도 유지되므로 한 번만 만든다.
        if (Gear == null) Gear = new Equipment(itemCatalog);

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
        statusView.Render(Stats, Gear, itemCatalog, TurnCount);

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

            case StatusMenu.Encyclopedia:
            case StatusMenu.Log:
            case StatusMenu.Records:
            case StatusMenu.Abort:
                messageBox.Show($"{StatusMenuCatalog.LabelOf(menu)}은(는) 아직 준비되지 않았다.");
                break;
        }
    }

    /// <summary>
    /// 상태 화면. 지금 걸려 있는 것들을 큰 판에 늘어놓는다.
    ///
    /// 판에 다 안 들어가면 상하 방향키로 넘겨 본다. 몇 줄이 들어가는지는
    /// 글꼴과 화면 비율에 달렸으므로 매 프레임 다시 물어본다 —
    /// 창 크기가 바뀌어도 스크롤이 어긋나지 않는다.
    /// </summary>
    private IEnumerator ShowConditions()
    {
        InputFocus.Give(InputOwner.Prompt);

        List<ConditionDefinition> active = ConditionReport.Active(Stats);
        int offset = 0;

        while (true)
        {
            int capacity = menuPanel.PageLineCapacity;
            offset = ConditionReport.ClampOffset(offset, active.Count, capacity);

            menuPanel.ShowPage(ConditionReport.BuildText(active, offset, capacity));

            // 이 화면을 연 그 키가 곧바로 닫아 버리지 않도록 한 프레임 넘긴다.
            yield return null;

            if (GameInput.AssistPressed || GameInput.StatusPressed) break;

            offset += GameInput.MenuStep;
        }

        menuPanel.Hide();
        InputFocus.ReturnToPlayer();
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

        Stage = 0;              // 뒤이어 StartNextStage() 가 1층으로 올린다.
        TurnCount = 0;
        FloorTilemap = null;
        LookDirection = Vector2.down;
        InteractionHandled = false;
        FastForward = false;

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

    /// <summary>적을 쓰러뜨렸을 때: 경험치를 주고, 레벨업했으면 그것도 알린다.</summary>
    public void RewardForKill(string enemyName, int exp)
    {
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

        TurnCount++;

        ApplyHungerTick();

        // 빨리감기 중에는 연출용 대기를 건너뛴다. 이 대기가 없으면 아래 과정이
        // 전부 같은 프레임 안에서 끝나므로, 달리기 한 번이 한 프레임에 처리된다.
        if (!FastForward)
            yield return new WaitForSeconds(EnemyTurnDelay);

        // 적들이 서로 같은 칸으로 몰리지 않도록, 이번 턴의 예약 위치를 새로 받는다.
        enemyPositions.Clear();

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
    private void ApplyHungerTick()
    {
        if (TurnCount % HungerTickInterval == 0 && !Stats.IsStarving)
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
            menuPanel, messageBox, Bag, itemCatalog, Gear, ground, startOnGround);

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

        string takenName = TakeFromGround(groundItem);

        // 몸에 걸치고 있던 것을 내려놓게 되면 반드시 벗겨야 한다.
        GameObject given = Bag.Replace(index, groundItem);
        string givenName = given.name.Replace("(Clone)", string.Empty);
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

        string itemName = groundItem.name.Replace("(Clone)", string.Empty);

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

        Ground.Remove(groundItem.transform.position);
        Destroy(groundItem);
        InputFocus.ReturnToPlayer();

        if (ItemEffects.TryUse(itemName, Stats, out GameMessage useMessage))
            EndPlayerTurn(useMessage, itemName);
        else
            EndPlayerTurn();
    }

    /// <summary>발밑의 것을 줍지 않고 그 자리에서 던진다.</summary>
    private IEnumerator ThrowFromGround(GameObject groundItem)
    {
        if (groundItem == null) { InputFocus.ReturnToPlayer(); yield break; }

        string itemName = groundItem.name.Replace("(Clone)", string.Empty);

        // 날아가는 동안에는 이 칸에 없으므로 바닥 목록에서 먼저 뺀다.
        // ItemThrower 가 떨어진 자리에 다시 넣어 준다.
        Ground.Remove(groundItem.transform.position);

        // 밟고 선 칸에 있으니 이미 같은 자리지만, 던지기는 출발점을 기준으로
        // 거리를 세므로 어긋난 채로 시작하지 않도록 맞춰 둔다.
        groundItem.transform.position = PlayerPosition;

        var thrower = new ItemThrower(FloorTilemap, Ground, enemies);
        GameMessage? result = null;

        yield return thrower.Throw(
            groundItem, itemName, LookDirection, Stats, message => result = message);

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
        string itemName = item.name;

        // 장비는 가방에서 빠지지 않는다. 차고 있다는 표시만 바뀐다.
        if (action == BagMenuController.ItemAction.Use && Gear.SlotOf(item) != EquipmentSlot.None)
        {
            ToggleEquip(item, itemName);
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

                if (ItemEffects.TryUse(itemName, Stats, out GameMessage useMessage))
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
                    item, itemName, LookDirection, Stats,
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
