using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using System;

public class GameManager : MonoBehaviour
{
    //public static float turnDelay;//.1f;
    //public static float EnemyDelay;

    public float CheckTurnD;
    public float CheckEnemyD;

    public static GameManager instance = null;
    public  RandomDung MapMake;

    public ItemDescription[] allItemDescriptions; // 모든 아이템 정보를 담은 배열

    public bool playersTurn = true;

    public List<Enemy> enemies;

    public List<Vector2> enemyPositions; // 모든 적의 예상 이동 위치를 저장하는 리스트

    public Vector2 PlayerPosition;//플레이어 위치를 저장

    private bool enemiesMoving;

    public RectTransform healthBar; // 조절할 이미지의 RectTransform
    public RectTransform MaxhealthBar;

    public static float MaxHp = 20;
    public static float PlayerHp = MaxHp;
    public static int PlayerAttack = 3;
    public static int PlayerDeffense = 1;
    public static int Playerstrength = 8;
    public static int MaxPlayerstrength = Playerstrength;
    public static int PlayerHunger = 100;
    public static int MaxPlayerHunger = PlayerHunger;
    public static int PlayerLV = 1;
    public static int PlayerExp = 0;
    public static int MaxExp = 10;
    public static int PlayerGold = 0;


    public static int Gameturn;
    public static int Stage = 0;

    public GameObject LevelImage;
    public GameObject UiCanvas;
    public GameObject Player;

    public TextMeshProUGUI LevelTextMeshPro;
    public TextMeshProUGUI LevelCanvas;
    public TextMeshProUGUI PlayerLVCanvas;
    public TextMeshProUGUI HpCanvas;
    public TextMeshProUGUI GoldCanvas;

    public GameObject panel;
    public Image TextimageBox;
    public TextMeshProUGUI AttackTextMeshPro;

    public GameObject Selectpanel;
    public TextMeshProUGUI SelectTextMeshPro;

    public GameObject BagPanel;
    public TextMeshProUGUI BagTextMeshPro;
    public TextMeshProUGUI BagCusor;

    public GameObject itemChosePanel;
    public TextMeshProUGUI itemChoseText;

    public GameObject ExplainitemPanel;
    public Image Explainimage;
    public TextMeshProUGUI Showitemname;
    public TextMeshProUGUI Showitemprice;
    public TextMeshProUGUI ShowexplainText;

    bool doingSetup;

    public int EnmiesMoveDone;

    public static bool PlayerTextCheck = false;

    public bool StopHide = false;

    public static List<GameObject> items = new List<GameObject>();
    public static int maxbag = 10;
    public static bool bagOpen;

    public Scene currentScene;

    public static List<Vector3> GroundSetPositions = new List<Vector3>();
    public List<Vector3> UpdateGround = new List<Vector3>();//위의 리스트 확인용

    public static bool StairOut = false;

    public static Vector2 Lookdirection;
    public static Tilemap tilemap;
    public List<string> impassableTileNames = new List<string> { "S1Wall" };


    void Awake()
    {

        Stage++;
        //turnDelay = 0f;
        //EnemyDelay = turnDelay;

        EnmiesMoveDone = 0;
        playersTurn = true;

        Lookdirection = new Vector2(0, -1);
        

        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            Destroy(UiCanvas);
        }
            

        DontDestroyOnLoad(gameObject);
        DontDestroyOnLoad(UiCanvas);

        enemies = new List<Enemy>();
        GroundSetPositions.Clear();

        TextimageBox.enabled = false;

        doingSetup = true;   

        instance.InitGame();
        
    }
    private void Start()
    {

    }

    public void DestroyAllEnemies()
    {
        // 리스트에 있는 모든 Enemy 객체를 파괴
        foreach (Enemy enemy in enemies)
        {
            Debug.Log("모든 적 삭제");
            Destroy(enemy.gameObject);
        }
    }

        public void InitGame()
    {

        // 1.5초 후에 이미지를 숨기는 코루틴 시작
        LevelTextMeshPro.text = ("무작위 던전 " + Stage + "층");
        StartCoroutine(FadeOutLevelImage());
        enemies.Clear();
        MapMake.GenerateDungeon();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static public void CallbackInitialization()
    {
        //register the callback to be called everytime the scene is loaded
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        //instance.Stage++;
        //instance.InitGame();
        //MapMake = GameObject.Find("MapMaker").GetComponent<RandomDung>();

    }
    private IEnumerator FadeOutLevelImage()
    {
        Image imageComponent = LevelImage.GetComponent<Image>();  // Image 컴포넌트를 가져옴
        imageComponent.color = new Color(255 / 255f, 107 / 255f, 107 / 255f, 255 / 255f);
        yield return new WaitForSeconds(1f);  // 1초 동안 대기

        
        float fadeDuration = 0.5f;  // 페이드 아웃 지속 시간
        float elapsedTime = 0f;

        //Color originalColor = imageComponent.color;  // 원래 색상을 저장

        // 0.5초 동안 알파값을 줄임
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);  // 알파값을 1에서 0으로 줄임
            imageComponent.color = new Color(255 / 255f, 107 / 255f, 107 / 255f, newAlpha);
            yield return null;
        }

        //imageComponent.color = new Color(255, 107, 107, 0f);  // 알파값을 0으로 설정
        //LevelImage.SetActive(false);  // 이미지를 비활성화
        //yield return new WaitForSeconds(0.5f);  // 1초 동안 대기
        LevelTextMeshPro.text = ("");
        doingSetup = false;  // 설정 완료
    }

    public void UpdateHealthBar()
    {
        // 최대 체력 바의 너비 조정
        Vector2 sizeDelta2 = MaxhealthBar.sizeDelta;
        sizeDelta2.x = 60 * (MaxHp / 20f); // 초기 최대 체력에 비례하여 너비 설정
        MaxhealthBar.sizeDelta = sizeDelta2;

        // 현재 체력의 비율 계산
        float healthPercentage = PlayerHp / MaxHp;

        // 원래 너비에 비율을 곱하여 새로운 너비 설정
        Vector2 sizeDelta = MaxhealthBar.sizeDelta;
        sizeDelta.x = sizeDelta.x * healthPercentage;
        healthBar.sizeDelta = sizeDelta;
    }

    public void GameOver()
    {
        if (PlayerHp <= 0)
        {
            Debug.Log("dead");
            Image imageComponent = LevelImage.GetComponent<Image>();  // Image 컴포넌트를 가져옴
            imageComponent.color = new Color(255 / 255f, 107 / 255f, 107 / 255f, 255 / 255f);
            LevelTextMeshPro.text = ("게임 오버");
            gameObject.SetActive(false);
            //enabled = false;
        }
        
    }

    void Update()
    {
        UpdateHealthBar();
        LevelCanvas.text = ("B " + Stage +"F");
        PlayerLVCanvas.text = ("LV " + PlayerLV);
        HpCanvas.text = ("HP " + PlayerHp + "/" + MaxHp);
        GoldCanvas.text = (PlayerGold + "G" + "\n" + " Food : " + PlayerHunger);

        UpdateGround = GroundSetPositions;

    }

    public void AddEnemyToList(Enemy script)
    {
        enemies.Add(script);
    }

    public void RemoveEnemyList(Enemy script)
    {
        enemies.Remove(script);
    }
    public IEnumerator NeedTextandTurnEnd(string name, int Damge, int Switch, bool TextBox)
    {
        if (TextBox)
        {
            yield return StartCoroutine(ShowDamagedTextBox(name, Damge, Switch));
            StartCoroutine(MoveEnemies());
        }
        else
        {
            StartCoroutine(MoveEnemies());
        }
    }
    public IEnumerator MoveEnemies()
    {
        playersTurn = false;
        Gameturn++;
        if (Gameturn % 5 == 0 && PlayerHunger > 0)
        {
            HungerHp();
        }
        if(PlayerHunger <= 0 && PlayerHp >= 1)
        {
            PlayerHp--;
            GameOver();
        }
        yield return new WaitForSeconds(0.1f);
        if (enemies.Count == 0)
        {
            playersTurn = true;
        }
        else
        {
            
            enemyPositions.Clear(); // 새로운 이동 목표 위치 리스트 초기화

            // 각 적의 이동 정보 저장
            for (int i = 0; i < enemies.Count; i++)
            {

                StartCoroutine(enemies[i].MoveEnemy());
                //yield return 
                //yield return new WaitForFixedUpdate();
                //yield return new WaitForSeconds(EnemyDelay);
                //yield return new WaitForSeconds(0.1f);
            }
            //playersTurn = true;
            //enemiesMoving = false;
        }


    }

    public void EnemyTurnCompleted()
    {
        EnmiesMoveDone++;
        if(EnmiesMoveDone == enemies.Count || enemies.Count == 0)
        {
            StairOut = true;// 임시 설정
            EnmiesMoveDone = 0;
            playersTurn = true;
        }

    }
    // 현재 실행 중인 HidePanel 코루틴의 참조를 저장할 변수
    private Coroutine hidePanelCoroutine;
    private void RestartHidePanelCoroutine()
    {
        StopHidePanelCoroutine();  // 기존 코루틴 중지
        hidePanelCoroutine = StartCoroutine(HidePanel());  // 새로운 코루틴 시작
    }

    private void StopHidePanelCoroutine()
    {
        if (hidePanelCoroutine != null)
        {
            StopCoroutine(hidePanelCoroutine);  // 기존 코루틴이 실행 중이면 중지
            hidePanelCoroutine = null;  // 참조를 해제
        }
    }
    public IEnumerator ShowDamagedTextBox(string name,int Damge, int Switch)
    {
        switch (Switch)
        {
            case 0:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("플레이어에게 " + Damge + "데미지!");
                RestartHidePanelCoroutine();
                break;

            case 1:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("그러나 공격은 빗나갔다");
                RestartHidePanelCoroutine();
                break;

            case 2:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("회심의 일격! 적에게 " + Damge + "데미지!");
                RestartHidePanelCoroutine();
                break;

            case 3:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("적에게 " + Damge + "데미지!");
                //StopCoroutine(HidePanel());
                //StartCoroutine(HidePanel());
                RestartHidePanelCoroutine();
                break;

            case 4:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                //StopCoroutine(HidePanel());
                RestartHidePanelCoroutine();
                AttackTextMeshPro.text = ("지하로 내려가겠습니까?");

                Selectpanel.SetActive(true);

                yield return StartCoroutine(AnswerChose());
                break;

            case 5:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (Damge + "골드를 획득했습니다.");
                RestartHidePanelCoroutine();
                break;

            case 6:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "을(를) 주워서 가방안에 넣었다.");
                RestartHidePanelCoroutine();
                break;

            case 7:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("가방이 가득차 더 이상 넣을 수 없다.");
                RestartHidePanelCoroutine();
                break;

            case 8:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "를 쓰러뜨렸다." + "\n" + Damge + "경험치를 얻었다.");
                PlayerExp += Damge;
                RestartHidePanelCoroutine();
                yield return StartCoroutine(LevelUpCheck());
                break;

            case 9:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                if(Damge == 0)
                {
                    AttackTextMeshPro.text = (name + "를 먹었다.\n위장이 넓어졌다.");
                }
                else
                {
                    AttackTextMeshPro.text = (name + "를 먹었다.\n배가 조금 찼다.");
                }
                
                RestartHidePanelCoroutine();
                break;

            case 10:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                if(Damge == 0)
                {
                    AttackTextMeshPro.text = (name + "를 먹었다.\n체력최대치가 증가했다.");
                }
                else
                {
                    AttackTextMeshPro.text = (name + "를 먹었다.\n체력이 회복됬다.");
                }
                
                RestartHidePanelCoroutine();
                break;

            case 11:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "를 마셨다.\n힘이 돌아왔다.");
                RestartHidePanelCoroutine();
                break;

            case 12:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "를 사용했다.\n힘이 세진것 같다.");
                RestartHidePanelCoroutine();
                break;

            case 13:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "를 땅에 두었다.");
                RestartHidePanelCoroutine();
                break;

            case 14:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "은 땅에 떨어졌다.");
                RestartHidePanelCoroutine();
                break;

            case 15:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "벽에 부딪혀 땅에 떨어졌다.");
                RestartHidePanelCoroutine();
                break;

            default:
                break;
        }
        
        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator LevelUpCheck()
    {
        bool SendBox = false;
        while (PlayerExp >= MaxExp)
        {
            SendBox = true;
            PlayerLV++;
            PlayerAttack++;
            PlayerDeffense++;
            MaxHp++;
            PlayerExp -= MaxExp;
            MaxExp += (int)MaxExp / 2;
            yield return new WaitForSeconds(0);
        }
        if (SendBox)
        {
            TextimageBox.enabled = true;
            panel.SetActive(true);
            AttackTextMeshPro.text = ("레벨업!");
            RestartHidePanelCoroutine();
        }
    }

    public IEnumerator bagTextBox()
    {
        panel.SetActive(false);
        BagPanel.SetActive(true);

        while (PlayerTextCheck)
        {
            if (items.Count == 0)
            {
                BagTextMeshPro.text = ("가방에는 아무것도 없다");
            }
            else if (items.Count == 1)
            {
                // 아이템이 1개일 경우
                BagTextMeshPro.text = items[0].name;
            }
            else
            {
                // 아이템이 2개 이상일 경우
                string bagContents = "";

                for (int i = 0; i < items.Count; i++)
                {
                    bagContents += items[i].name;

                    // 마지막 아이템이 아닌 경우 줄 바꿈 추가
                    if (i < items.Count - 1)
                    {
                        bagContents += "\n";
                    }
                }

                BagTextMeshPro.text = bagContents;
            }

            yield return StartCoroutine(MakeBagCursor(0.2f));  
        }

        yield return null;// 프레임마다 확인
    }
    public int selectednumber = 0;
    public IEnumerator MakeBagCursor(float waittime)
    {
        UpdateBagCursor(selectednumber);
        yield return new WaitForSeconds(waittime); // 0.2초 지연 추가
        bool isMenuActive = false; // 메뉴 활성화 상태를 나타내는 플래그
        bool isBagOpen = true; // 가방이 열려 있는지 여부

        if (items.Count == 0)
        {
            BagCusor.text = "";
        }



        while (isBagOpen)
        {
            if (!isMenuActive)
            {
                if (items.Count > 0)
                {
                    // 가방 커서가 움직일 때
                    UpdateBagCursor(selectednumber);

                    // 방향키 입력 처리
                    if (Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        selectednumber--;

                        // 범위를 벗어났을 경우
                        if (selectednumber < 0)
                        {
                            selectednumber = items.Count - 1;
                        }
                    }
                    else if (Input.GetKeyDown(KeyCode.DownArrow))
                    {
                        selectednumber++;

                        // 범위를 벗어났을 경우
                        if (selectednumber >= items.Count)
                        {
                            selectednumber = 0;
                        }
                    }
                    else if (Input.GetKeyDown(KeyCode.A))
                    {
                        // 메뉴를 활성화하고 메뉴 커서를 활성화
                        isMenuActive = true;
                        itemChosePanel.SetActive(true);

                        // 메뉴 커서 관리 코루틴 시작
                        StartCoroutine(MenuCursor(0));
                    }
                }
                
                // S키를 눌러 가방을 닫기
                if (Input.GetKeyDown(KeyCode.S))
                {
                    selectednumber = 0;
                    // 가방 닫기
                    isBagOpen = false;
                    
                    closeBag2();
                }
            }

            yield return null; // 매 프레임마다 이 루프를 실행
        }
    }

    void closeBag2()
    {
        Debug.Log("작동중2?");
        PlayerTextCheck = false;
        BagPanel.SetActive(false);
    }

    private IEnumerator MenuCursor(int selectedOptionstart)
    {
        // 코루틴이 바로 A키 입력을 받지 않도록, 한 프레임 대기
        yield return null;
        int selectedOption = selectedOptionstart;
        bool isMenuActive = true;
        //isMenuActive2 = true;

        while (isMenuActive)
        {
            UpdateMenuCursor(selectedOption);

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedOption--;

                // 범위를 벗어났을 경우
                if (selectedOption < 0)
                {
                    selectedOption = 3; // 메뉴 옵션이 4개이므로 인덱스는 0~3
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedOption++;

                // 범위를 벗어났을 경우
                if (selectedOption > 3)
                {
                    selectedOption = 0;
                }
            }
            else if (Input.GetKeyDown(KeyCode.S)) //  메뉴 종료
            {
                // 메뉴 종료
                isMenuActive = false;
                itemChosePanel.SetActive(false);
                // 가방 커서 다시 활성화
                StartCoroutine(MakeBagCursor(0f)); // 가방 커서 재활성화

                // 가방 커서 다시 활성화
                yield break;
            }
            else if (Input.GetKeyDown(KeyCode.A)) // 결정
            {
                
                // 선택된 옵션에 따라 행동 수행
                PerformAction(selectedOption);
                
                isMenuActive = false;
                itemChosePanel.SetActive(false);
                BagPanel.SetActive(false);

                yield break;
            }

            yield return null; // 매 프레임마다 이 루프를 실행
            
        }
        //yield break;
    }

    private void UpdateMenuCursor(int selectedOption)
    {
        
        string menuContents = "";

        // 현재 선택된 아이템의 ItemType에 따라 첫 번째 옵션 결정
        //string[] options = new string[4];
        string[] options = { "먹는다", "확인한다", "둔다", "던진다" };

        // 현재 선택된 아이템의 이름을 가져옴
        string selectedItemName = items[selectednumber].name;

        // GetItemDescriptionByName 함수를 사용하여 아이템 설명 가져오기
        //ItemDescription selectedItem = GetItemDescriptionByName(selectedItemName);

        ItemDescription selectedItem = null;
        foreach (var itemDesc in allItemDescriptions)
        {
            if (itemDesc.itemName == selectedItemName)
            {
                selectedItem = itemDesc;
                break;
            }
        }

        if (selectedItem != null)
        {
            // ItemType에 따라 첫 번째 옵션 설정
            if (selectedItem.itemType == ItemDescription.ItemType.Food)
            {
                options[0] = "먹는다";
            }
            else if (selectedItem.itemType == ItemDescription.ItemType.Expendables)
            {
                options[0] = "사용한다";
            }
            else
            {
                options[0] = "사용한다"; // 기본적으로 "사용한다"로 설정
            }

            // 나머지 옵션 설정
            options[1] = "확인한다";
            options[2] = "둔다";
            options[3] = "던진다";

            // 메뉴 내용을 구성
            for (int i = 0; i < options.Length; i++)
            {
                if (i == selectedOption)
                {
                    menuContents += "> " + options[i]; // 선택된 옵션에는 "> " 추가
                }
                else
                {
                    menuContents += "  " + options[i]; // 선택되지 않은 옵션에는 "  " 추가
                }

                if (i < options.Length - 1)
                {
                    menuContents += "\n"; // 마지막 옵션이 아닌 경우 줄바꿈 추가
                }
            }
        }
        else
        {
            // 만약 일치하는 아이템이 없으면 기본 설정
            options[0] = "안된다";
            options[1] = "확인한다";
            options[2] = "둔다";
            options[3] = "던진다";

            for (int i = 0; i < options.Length; i++)
            {
                if (i == selectedOption)
                {
                    menuContents += "> " + options[i];
                }
                else
                {
                    menuContents += "  " + options[i];
                }

                if (i < options.Length - 1)
                {
                    menuContents += "\n";
                }
            }
        }

        // 메뉴 텍스트 갱신
        itemChoseText.text = menuContents;
    }

    public ItemDescription GetItemDescriptionByName(string itemName)
    {
        foreach (var itemDesc in allItemDescriptions)
        {
            if (itemDesc.itemName == itemName)
            {
                return itemDesc; // 이름과 일치하는 ItemDescription 반환
            }
        }
        return null; // 일치하는 아이템이 없으면 null 반환
    }


    private void PerformAction(int selectedOption)
    {
        // 선택된 옵션에 따라 행동을 수행하는 로직 작성
        switch (selectedOption)
        {
            case 0:
                string itemnameRemember = null;
                itemnameRemember = items[selectednumber].name;
                //Dontdestoryload로 가져온 오브젝트를 다시 돌려놓기 위한 준비.
                currentScene = SceneManager.GetActiveScene();
                SceneManager.MoveGameObjectToScene(items[selectednumber], currentScene);
                items.RemoveAt(selectednumber);
                PlayerTextCheck = false;
                switch (itemnameRemember)
                {
                    case "사과":
                        if (PlayerHunger == MaxPlayerHunger) 
                        {
                            MaxPlayerHunger += 5;
                            PlayerHunger = MaxPlayerHunger;
                            StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 9, true));
                        }
                        else
                        {
                            PlayerHunger = Math.Min(PlayerHunger + 30, MaxPlayerHunger);
                            StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 1, 9, true));
                        }

                        
                        break;
                    case "회복씨앗":
                        PlayerHunger = Math.Min(PlayerHunger + 5, MaxPlayerHunger);
                        if (PlayerHp == MaxHp)
                        {
                            MaxHp++;
                            StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 10, true));
                        }
                        else
                        {
                            PlayerHp = Math.Min(PlayerHp + 10, MaxHp);
                            StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 1, 10, true));
                        }

                        
                        break;
                    case "기력회복약":
                        PlayerHunger = Math.Min(PlayerHunger + 5, MaxPlayerHunger);
                        Playerstrength = Math.Min(Playerstrength++, MaxPlayerstrength);
                        StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 11, true));
                        break;
                    case "힘의구슬":
                        MaxPlayerstrength++;
                        StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 12, true));
                        break;
                }
                selectednumber = 0;
                break;
            case 1:
                //Debug.Log("확인한다");
                itemnameRemember = items[selectednumber].name;

                // 아이템 설명 정보를 가져온다.
                ItemDescription selectedItem = null;
                foreach (var itemDesc in allItemDescriptions)
                {
                    if (itemDesc.itemName == itemnameRemember)
                    {
                        selectedItem = itemDesc;
                        break;
                    }
                }

                // 만약 아이템이 존재하면, UI 요소에 데이터를 할당한다.
                if (selectedItem != null)
                {
                    Explainimage.sprite = selectedItem.sprites;
                    Explainimage.preserveAspect = true; // 원본 비율 유지
                    Showitemname.text = selectedItem.itemName;
                    Showitemprice.text = "판매 가격 : " + selectedItem.SellPrice + "G";  // 가격을 문자열로 변환
                    ShowexplainText.text = selectedItem.description;
                }
                else
                {
                    Debug.LogWarning("Item description not found for item: " + itemnameRemember);
                }
                BagPanel.SetActive(false);
                itemChosePanel.SetActive(false);
                ExplainitemPanel.SetActive(true);
                StartCoroutine(WaitForCloseExplainPanel());
                //PlayerTextCheck = false;
                break;
            case 2:
                itemnameRemember = items[selectednumber].name;
                //Debug.Log("둔다");
                currentScene = SceneManager.GetActiveScene();
                SceneManager.MoveGameObjectToScene(items[selectednumber], currentScene);
                items[selectednumber].transform.position = PlayerPosition;
                items[selectednumber].SetActive(true);
                items.RemoveAt(selectednumber);
                GroundSetPositions.Add(transform.position);
                PlayerTextCheck = false;
                StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 13, true));
                selectednumber = 0;
                break;
            case 3:
                itemnameRemember = items[selectednumber].name;
                //Debug.Log("던진다");
                currentScene = SceneManager.GetActiveScene();
                SceneManager.MoveGameObjectToScene(items[selectednumber], currentScene);
                items[selectednumber].transform.position = PlayerPosition;
                items[selectednumber].SetActive(true);

                StartCoroutine(ThrowItem(items[selectednumber], itemnameRemember));

                items.RemoveAt(selectednumber);
                selectednumber = 0;
                //PlayerTextCheck = false;


                break;
        }
    }
    private IEnumerator WaitForCloseExplainPanel()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                // 설명창 비활성화
                ExplainitemPanel.SetActive(false);


                // 가방 UI와 선택 UI 다시 활성화
                BagPanel.SetActive(true);
                itemChosePanel.SetActive(true);
                StartCoroutine(MenuCursor(1));

                // 코루틴 종료
                yield break;
            }

            yield return null; // 매 프레임마다 이 루프를 실행
        }
    }

    private IEnumerator ThrowItem(GameObject item, string itemname)
    {
        Vector2 startPosition = item.transform.position;
        Vector2 direction = Lookdirection;
        int throwDistance = Mathf.Max(Playerstrength, 1); // Playerstrength가 0일 경우 최소값 1로 설정
        float step = 0.1f; // 한 프레임마다 이동할 거리
        float currentDistance = 0f; // 현재까지 이동한 거리

        // 충돌 감지를 위한 변수
        bool collided = false;
        bool moved = true;
        Vector2 collisionPosition = Vector2.zero;

        while (currentDistance < throwDistance)
        {
            Vector2 newPosition = new Vector2(
                startPosition.x + direction.x * currentDistance,
                startPosition.y + direction.y * currentDistance
            );

            // 타일맵에서 충돌 감지
            Vector3Int cellPosition = tilemap.WorldToCell(newPosition);
            TileBase tile = tilemap.GetTile(cellPosition);

            if (tile != null && impassableTileNames.Contains(tile.name))
            {
                collisionPosition = (Vector2)tilemap.GetCellCenterWorld(cellPosition) - direction * 1f;
                collided = true;
                StartCoroutine(NeedTextandTurnEnd(itemname, 0, 15, true));
                break;
            }

            item.transform.position = newPosition;
            currentDistance += step;
            yield return null; // 한 프레임 대기
        }

        if (!collided)
        {
            // 벽에 부딪히지 않았고, 아이템이 지정된 거리까지 쭉 날아갔을 때의 처리
            collisionPosition = item.transform.position;
        }
        else
        {
            // 벽에 부딪힌 경우
            item.transform.position = collisionPosition;
        }
        // 8방향 탐색
        Vector2[] directions = new Vector2[]
        {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1)
        };

        moved = false;
        if (GroundSetPositions.Contains(item.transform.position))
        {
            foreach (Vector2 dir in directions)
            {
                Vector2 adjacentPosition = collisionPosition + dir;

                if (!GroundSetPositions.Contains(adjacentPosition) && !IsBlockedByTile(adjacentPosition))
                {
                    item.transform.position = adjacentPosition;
                    GroundSetPositions.Add(adjacentPosition);
                    moved = true;
                    break;
                }
            }
        }
        
        GroundSetPositions.Add(item.transform.position);
        if (!moved)
        {
            Debug.Log("이동할 수 있는 위치를 찾지 못했습니다.");
        }
        PlayerTextCheck = false;
        StartCoroutine(NeedTextandTurnEnd(itemname, 0, 14, true));
    }
    protected bool IsBlockedByTile(Vector3 end)
    {
        if (tilemap != null)
        {
            Vector3Int cellPosition = tilemap.WorldToCell(end);
            TileBase tile = tilemap.GetTile(cellPosition);
            if (tile != null)
            {
                string tileName = tile.name;
                if (impassableTileNames.Contains(tileName))
                {
                    return true;
                }
            }
        }
        return false;
    }



    private void UpdateBagCursor(int selectednumber)
    {
        string bagContents = "";

        for (int i = 0; i < items.Count; i++)
        {
            if (i == selectednumber)
            {
                bagContents += ">";
            }
            else
            {
                bagContents += ""; // 빈 줄 추가
            }

            // 마지막 아이템이 아닌 경우 줄 바꿈 추가
            if (i < items.Count - 1)
            {
                bagContents += "\n";
            }
        }

        BagCusor.text = bagContents;
    }

    public IEnumerator AnswerChose()
    {
        StopHide = true;
        bool isYesSelected = true;  // 초기 선택값 설정

        while (PlayerTextCheck)
        {
            // 방향키 입력 처리
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                isYesSelected = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                isYesSelected = false;
            }

            string line1 = isYesSelected ? "> 예" : "  예";
            string line2 = isYesSelected ? "  아니오" : "> 아니오";

            SelectTextMeshPro.text = line1 + "\n" + line2;

            // A 키 입력 처리
            if (Input.GetKeyDown(KeyCode.A))
            {
                StopHide = false;
                panel.SetActive(false);
                Selectpanel.SetActive(false);
                PlayerTextCheck = false;
                if (isYesSelected)
                {
                    //DestroyAllEnemies();
                    SceneManager.LoadScene("SampleScene");
                }
            }

            yield return null;  // 다음 프레임까지 대기
        }

    }


    public IEnumerator HidePanel()
    {
        yield return new WaitForSeconds(3f);
        if (panel != null && (!StopHide))
            panel.SetActive(false);

    }

    public void HungerHp()
    {
        
        PlayerHunger--;
        if (PlayerHp < MaxHp && Gameturn % 10 == 0)
        {
            // 힘 값에 따라 회복 비율 조정
            double recoveryPercentage = (Playerstrength / 8.0) * 0.05;

            // 회복량 계산
            int recoveryAmount = (int)(MaxHp * recoveryPercentage);

            // 회복량이 MaxHp를 초과하지 않도록 조정
            PlayerHp = Math.Min(PlayerHp + recoveryAmount, MaxHp);
        }
    }

    //bool isXPressed = Input.GetKeyDown(KeyCode.X);
    /*public IEnumerator XArrow()
    {
        yield return new WaitForSeconds(.1f);

        while (isXPressed)
        {
            int horizontal = (int)Input.GetAxisRaw("Horizontal");
            int vertical = (int)Input.GetAxisRaw("Vertical");

            GameManager.Lookdirection = new Vector2(horizontal, vertical);
            // Lookdirection에 따른 회전값과 방향 문자열 가져오기
            //var (zRotation, Charint) = GetDirectionInfo(GameManager.Lookdirection);

            // 자식 오브젝트의 로테이션을 변경
            //ArrowTransform.transform.localEulerAngles = new Vector3(0, 0, zRotation);
            //Player.image.sprite = directionsprites[Charint];
        }
    }*/
}
