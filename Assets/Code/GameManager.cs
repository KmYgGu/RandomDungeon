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

    public ItemDescription[] allItemDescriptions; // ��� ������ ������ ���� �迭

    public bool playersTurn = true;

    public List<Enemy> enemies;

    public List<Vector2> enemyPositions; // ��� ���� ���� �̵� ��ġ�� �����ϴ� ����Ʈ

    public Vector2 PlayerPosition;//�÷��̾� ��ġ�� ����

    private bool enemiesMoving;

    public RectTransform healthBar; // ������ �̹����� RectTransform
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
    public List<Vector3> UpdateGround = new List<Vector3>();//���� ����Ʈ Ȯ�ο�

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
        // ����Ʈ�� �ִ� ��� Enemy ��ü�� �ı�
        foreach (Enemy enemy in enemies)
        {
            Debug.Log("��� �� ����");
            Destroy(enemy.gameObject);
        }
    }

        public void InitGame()
    {

        // 1.5�� �Ŀ� �̹����� ����� �ڷ�ƾ ����
        LevelTextMeshPro.text = ("������ ���� " + Stage + "��");
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
        Image imageComponent = LevelImage.GetComponent<Image>();  // Image ������Ʈ�� ������
        imageComponent.color = new Color(255 / 255f, 107 / 255f, 107 / 255f, 255 / 255f);
        yield return new WaitForSeconds(1f);  // 1�� ���� ���

        
        float fadeDuration = 0.5f;  // ���̵� �ƿ� ���� �ð�
        float elapsedTime = 0f;

        //Color originalColor = imageComponent.color;  // ���� ������ ����

        // 0.5�� ���� ���İ��� ����
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);  // ���İ��� 1���� 0���� ����
            imageComponent.color = new Color(255 / 255f, 107 / 255f, 107 / 255f, newAlpha);
            yield return null;
        }

        //imageComponent.color = new Color(255, 107, 107, 0f);  // ���İ��� 0���� ����
        //LevelImage.SetActive(false);  // �̹����� ��Ȱ��ȭ
        //yield return new WaitForSeconds(0.5f);  // 1�� ���� ���
        LevelTextMeshPro.text = ("");
        doingSetup = false;  // ���� �Ϸ�
    }

    public void UpdateHealthBar()
    {
        // �ִ� ü�� ���� �ʺ� ����
        Vector2 sizeDelta2 = MaxhealthBar.sizeDelta;
        sizeDelta2.x = 60 * (MaxHp / 20f); // �ʱ� �ִ� ü�¿� ����Ͽ� �ʺ� ����
        MaxhealthBar.sizeDelta = sizeDelta2;

        // ���� ü���� ���� ���
        float healthPercentage = PlayerHp / MaxHp;

        // ���� �ʺ� ������ ���Ͽ� ���ο� �ʺ� ����
        Vector2 sizeDelta = MaxhealthBar.sizeDelta;
        sizeDelta.x = sizeDelta.x * healthPercentage;
        healthBar.sizeDelta = sizeDelta;
    }

    public void GameOver()
    {
        if (PlayerHp <= 0)
        {
            Debug.Log("dead");
            Image imageComponent = LevelImage.GetComponent<Image>();  // Image ������Ʈ�� ������
            imageComponent.color = new Color(255 / 255f, 107 / 255f, 107 / 255f, 255 / 255f);
            LevelTextMeshPro.text = ("���� ����");
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
            
            enemyPositions.Clear(); // ���ο� �̵� ��ǥ ��ġ ����Ʈ �ʱ�ȭ

            // �� ���� �̵� ���� ����
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
            StairOut = true;// �ӽ� ����
            EnmiesMoveDone = 0;
            playersTurn = true;
        }

    }
    // ���� ���� ���� HidePanel �ڷ�ƾ�� ������ ������ ����
    private Coroutine hidePanelCoroutine;
    private void RestartHidePanelCoroutine()
    {
        StopHidePanelCoroutine();  // ���� �ڷ�ƾ ����
        hidePanelCoroutine = StartCoroutine(HidePanel());  // ���ο� �ڷ�ƾ ����
    }

    private void StopHidePanelCoroutine()
    {
        if (hidePanelCoroutine != null)
        {
            StopCoroutine(hidePanelCoroutine);  // ���� �ڷ�ƾ�� ���� ���̸� ����
            hidePanelCoroutine = null;  // ������ ����
        }
    }
    public IEnumerator ShowDamagedTextBox(string name,int Damge, int Switch)
    {
        switch (Switch)
        {
            case 0:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("�÷��̾�� " + Damge + "������!");
                RestartHidePanelCoroutine();
                break;

            case 1:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("�׷��� ������ ��������");
                RestartHidePanelCoroutine();
                break;

            case 2:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("ȸ���� �ϰ�! ������ " + Damge + "������!");
                RestartHidePanelCoroutine();
                break;

            case 3:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("������ " + Damge + "������!");
                //StopCoroutine(HidePanel());
                //StartCoroutine(HidePanel());
                RestartHidePanelCoroutine();
                break;

            case 4:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                //StopCoroutine(HidePanel());
                RestartHidePanelCoroutine();
                AttackTextMeshPro.text = ("���Ϸ� �������ڽ��ϱ�?");

                Selectpanel.SetActive(true);

                yield return StartCoroutine(AnswerChose());
                break;

            case 5:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (Damge + "��带 ȹ���߽��ϴ�.");
                RestartHidePanelCoroutine();
                break;

            case 6:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "��(��) �ֿ��� ����ȿ� �־���.");
                RestartHidePanelCoroutine();
                break;

            case 7:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = ("������ ������ �� �̻� ���� �� ����.");
                RestartHidePanelCoroutine();
                break;

            case 8:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "�� �����߷ȴ�." + "\n" + Damge + "����ġ�� �����.");
                PlayerExp += Damge;
                RestartHidePanelCoroutine();
                yield return StartCoroutine(LevelUpCheck());
                break;

            case 9:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                if(Damge == 0)
                {
                    AttackTextMeshPro.text = (name + "�� �Ծ���.\n������ �о�����.");
                }
                else
                {
                    AttackTextMeshPro.text = (name + "�� �Ծ���.\n�谡 ���� á��.");
                }
                
                RestartHidePanelCoroutine();
                break;

            case 10:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                if(Damge == 0)
                {
                    AttackTextMeshPro.text = (name + "�� �Ծ���.\nü���ִ�ġ�� �����ߴ�.");
                }
                else
                {
                    AttackTextMeshPro.text = (name + "�� �Ծ���.\nü���� ȸ�����.");
                }
                
                RestartHidePanelCoroutine();
                break;

            case 11:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "�� ���̴�.\n���� ���ƿԴ�.");
                RestartHidePanelCoroutine();
                break;

            case 12:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "�� ����ߴ�.\n���� ������ ����.");
                RestartHidePanelCoroutine();
                break;

            case 13:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "�� ���� �ξ���.");
                RestartHidePanelCoroutine();
                break;

            case 14:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "�� ���� ��������.");
                RestartHidePanelCoroutine();
                break;

            case 15:
                TextimageBox.enabled = true;
                panel.SetActive(true);
                AttackTextMeshPro.text = (name + "���� �ε��� ���� ��������.");
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
            AttackTextMeshPro.text = ("������!");
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
                BagTextMeshPro.text = ("���濡�� �ƹ��͵� ����");
            }
            else if (items.Count == 1)
            {
                // �������� 1���� ���
                BagTextMeshPro.text = items[0].name;
            }
            else
            {
                // �������� 2�� �̻��� ���
                string bagContents = "";

                for (int i = 0; i < items.Count; i++)
                {
                    bagContents += items[i].name;

                    // ������ �������� �ƴ� ��� �� �ٲ� �߰�
                    if (i < items.Count - 1)
                    {
                        bagContents += "\n";
                    }
                }

                BagTextMeshPro.text = bagContents;
            }

            yield return StartCoroutine(MakeBagCursor(0.2f));  
        }

        yield return null;// �����Ӹ��� Ȯ��
    }
    public int selectednumber = 0;
    public IEnumerator MakeBagCursor(float waittime)
    {
        UpdateBagCursor(selectednumber);
        yield return new WaitForSeconds(waittime); // 0.2�� ���� �߰�
        bool isMenuActive = false; // �޴� Ȱ��ȭ ���¸� ��Ÿ���� �÷���
        bool isBagOpen = true; // ������ ���� �ִ��� ����

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
                    // ���� Ŀ���� ������ ��
                    UpdateBagCursor(selectednumber);

                    // ����Ű �Է� ó��
                    if (Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        selectednumber--;

                        // ������ ����� ���
                        if (selectednumber < 0)
                        {
                            selectednumber = items.Count - 1;
                        }
                    }
                    else if (Input.GetKeyDown(KeyCode.DownArrow))
                    {
                        selectednumber++;

                        // ������ ����� ���
                        if (selectednumber >= items.Count)
                        {
                            selectednumber = 0;
                        }
                    }
                    else if (Input.GetKeyDown(KeyCode.A))
                    {
                        // �޴��� Ȱ��ȭ�ϰ� �޴� Ŀ���� Ȱ��ȭ
                        isMenuActive = true;
                        itemChosePanel.SetActive(true);

                        // �޴� Ŀ�� ���� �ڷ�ƾ ����
                        StartCoroutine(MenuCursor(0));
                    }
                }
                
                // SŰ�� ���� ������ �ݱ�
                if (Input.GetKeyDown(KeyCode.S))
                {
                    selectednumber = 0;
                    // ���� �ݱ�
                    isBagOpen = false;
                    
                    closeBag2();
                }
            }

            yield return null; // �� �����Ӹ��� �� ������ ����
        }
    }

    void closeBag2()
    {
        Debug.Log("�۵���2?");
        PlayerTextCheck = false;
        BagPanel.SetActive(false);
    }

    private IEnumerator MenuCursor(int selectedOptionstart)
    {
        // �ڷ�ƾ�� �ٷ� AŰ �Է��� ���� �ʵ���, �� ������ ���
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

                // ������ ����� ���
                if (selectedOption < 0)
                {
                    selectedOption = 3; // �޴� �ɼ��� 4���̹Ƿ� �ε����� 0~3
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedOption++;

                // ������ ����� ���
                if (selectedOption > 3)
                {
                    selectedOption = 0;
                }
            }
            else if (Input.GetKeyDown(KeyCode.S)) //  �޴� ����
            {
                // �޴� ����
                isMenuActive = false;
                itemChosePanel.SetActive(false);
                // ���� Ŀ�� �ٽ� Ȱ��ȭ
                StartCoroutine(MakeBagCursor(0f)); // ���� Ŀ�� ��Ȱ��ȭ

                // ���� Ŀ�� �ٽ� Ȱ��ȭ
                yield break;
            }
            else if (Input.GetKeyDown(KeyCode.A)) // ����
            {
                
                // ���õ� �ɼǿ� ���� �ൿ ����
                PerformAction(selectedOption);
                
                isMenuActive = false;
                itemChosePanel.SetActive(false);
                BagPanel.SetActive(false);

                yield break;
            }

            yield return null; // �� �����Ӹ��� �� ������ ����
            
        }
        //yield break;
    }

    private void UpdateMenuCursor(int selectedOption)
    {
        
        string menuContents = "";

        // ���� ���õ� �������� ItemType�� ���� ù ��° �ɼ� ����
        //string[] options = new string[4];
        string[] options = { "�Դ´�", "Ȯ���Ѵ�", "�д�", "������" };

        // ���� ���õ� �������� �̸��� ������
        string selectedItemName = items[selectednumber].name;

        // GetItemDescriptionByName �Լ��� ����Ͽ� ������ ���� ��������
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
            // ItemType�� ���� ù ��° �ɼ� ����
            if (selectedItem.itemType == ItemDescription.ItemType.Food)
            {
                options[0] = "�Դ´�";
            }
            else if (selectedItem.itemType == ItemDescription.ItemType.Expendables)
            {
                options[0] = "����Ѵ�";
            }
            else
            {
                options[0] = "����Ѵ�"; // �⺻������ "����Ѵ�"�� ����
            }

            // ������ �ɼ� ����
            options[1] = "Ȯ���Ѵ�";
            options[2] = "�д�";
            options[3] = "������";

            // �޴� ������ ����
            for (int i = 0; i < options.Length; i++)
            {
                if (i == selectedOption)
                {
                    menuContents += "> " + options[i]; // ���õ� �ɼǿ��� "> " �߰�
                }
                else
                {
                    menuContents += "  " + options[i]; // ���õ��� ���� �ɼǿ��� "  " �߰�
                }

                if (i < options.Length - 1)
                {
                    menuContents += "\n"; // ������ �ɼ��� �ƴ� ��� �ٹٲ� �߰�
                }
            }
        }
        else
        {
            // ���� ��ġ�ϴ� �������� ������ �⺻ ����
            options[0] = "�ȵȴ�";
            options[1] = "Ȯ���Ѵ�";
            options[2] = "�д�";
            options[3] = "������";

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

        // �޴� �ؽ�Ʈ ����
        itemChoseText.text = menuContents;
    }

    public ItemDescription GetItemDescriptionByName(string itemName)
    {
        foreach (var itemDesc in allItemDescriptions)
        {
            if (itemDesc.itemName == itemName)
            {
                return itemDesc; // �̸��� ��ġ�ϴ� ItemDescription ��ȯ
            }
        }
        return null; // ��ġ�ϴ� �������� ������ null ��ȯ
    }


    private void PerformAction(int selectedOption)
    {
        // ���õ� �ɼǿ� ���� �ൿ�� �����ϴ� ���� �ۼ�
        switch (selectedOption)
        {
            case 0:
                string itemnameRemember = null;
                itemnameRemember = items[selectednumber].name;
                //Dontdestoryload�� ������ ������Ʈ�� �ٽ� �������� ���� �غ�.
                currentScene = SceneManager.GetActiveScene();
                SceneManager.MoveGameObjectToScene(items[selectednumber], currentScene);
                items.RemoveAt(selectednumber);
                PlayerTextCheck = false;
                switch (itemnameRemember)
                {
                    case "���":
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
                    case "ȸ������":
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
                    case "���ȸ����":
                        PlayerHunger = Math.Min(PlayerHunger + 5, MaxPlayerHunger);
                        Playerstrength = Math.Min(Playerstrength++, MaxPlayerstrength);
                        StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 11, true));
                        break;
                    case "���Ǳ���":
                        MaxPlayerstrength++;
                        StartCoroutine(NeedTextandTurnEnd(itemnameRemember, 0, 12, true));
                        break;
                }
                selectednumber = 0;
                break;
            case 1:
                //Debug.Log("Ȯ���Ѵ�");
                itemnameRemember = items[selectednumber].name;

                // ������ ���� ������ �����´�.
                ItemDescription selectedItem = null;
                foreach (var itemDesc in allItemDescriptions)
                {
                    if (itemDesc.itemName == itemnameRemember)
                    {
                        selectedItem = itemDesc;
                        break;
                    }
                }

                // ���� �������� �����ϸ�, UI ��ҿ� �����͸� �Ҵ��Ѵ�.
                if (selectedItem != null)
                {
                    Explainimage.sprite = selectedItem.sprites;
                    Explainimage.preserveAspect = true; // ���� ���� ����
                    Showitemname.text = selectedItem.itemName;
                    Showitemprice.text = "�Ǹ� ���� : " + selectedItem.SellPrice + "G";  // ������ ���ڿ��� ��ȯ
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
                //Debug.Log("�д�");
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
                //Debug.Log("������");
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
                // ����â ��Ȱ��ȭ
                ExplainitemPanel.SetActive(false);


                // ���� UI�� ���� UI �ٽ� Ȱ��ȭ
                BagPanel.SetActive(true);
                itemChosePanel.SetActive(true);
                StartCoroutine(MenuCursor(1));

                // �ڷ�ƾ ����
                yield break;
            }

            yield return null; // �� �����Ӹ��� �� ������ ����
        }
    }

    private IEnumerator ThrowItem(GameObject item, string itemname)
    {
        Vector2 startPosition = item.transform.position;
        Vector2 direction = Lookdirection;
        int throwDistance = Mathf.Max(Playerstrength, 1); // Playerstrength�� 0�� ��� �ּҰ� 1�� ����
        float step = 0.1f; // �� �����Ӹ��� �̵��� �Ÿ�
        float currentDistance = 0f; // ������� �̵��� �Ÿ�

        // �浹 ������ ���� ����
        bool collided = false;
        bool moved = true;
        Vector2 collisionPosition = Vector2.zero;

        while (currentDistance < throwDistance)
        {
            Vector2 newPosition = new Vector2(
                startPosition.x + direction.x * currentDistance,
                startPosition.y + direction.y * currentDistance
            );

            // Ÿ�ϸʿ��� �浹 ����
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
            yield return null; // �� ������ ���
        }

        if (!collided)
        {
            // ���� �ε����� �ʾҰ�, �������� ������ �Ÿ����� �� ���ư��� ���� ó��
            collisionPosition = item.transform.position;
        }
        else
        {
            // ���� �ε��� ���
            item.transform.position = collisionPosition;
        }
        // 8���� Ž��
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
            Debug.Log("�̵��� �� �ִ� ��ġ�� ã�� ���߽��ϴ�.");
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
                bagContents += ""; // �� �� �߰�
            }

            // ������ �������� �ƴ� ��� �� �ٲ� �߰�
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
        bool isYesSelected = true;  // �ʱ� ���ð� ����

        while (PlayerTextCheck)
        {
            // ����Ű �Է� ó��
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                isYesSelected = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                isYesSelected = false;
            }

            string line1 = isYesSelected ? "> ��" : "  ��";
            string line2 = isYesSelected ? "  �ƴϿ�" : "> �ƴϿ�";

            SelectTextMeshPro.text = line1 + "\n" + line2;

            // A Ű �Է� ó��
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

            yield return null;  // ���� �����ӱ��� ���
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
            // �� ���� ���� ȸ�� ���� ����
            double recoveryPercentage = (Playerstrength / 8.0) * 0.05;

            // ȸ���� ���
            int recoveryAmount = (int)(MaxHp * recoveryPercentage);

            // ȸ������ MaxHp�� �ʰ����� �ʵ��� ����
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
            // Lookdirection�� ���� ȸ������ ���� ���ڿ� ��������
            //var (zRotation, Charint) = GetDirectionInfo(GameManager.Lookdirection);

            // �ڽ� ������Ʈ�� �����̼��� ����
            //ArrowTransform.transform.localEulerAngles = new Vector3(0, 0, zRotation);
            //Player.image.sprite = directionsprites[Charint];
        }
    }*/
}
