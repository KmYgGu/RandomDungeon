using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Player : MovingObject
{
    public float restartLevelDelay = 1f;

    private int food;

    bool isMoving = false;

    float totalMoveTime2;

    Vector3 attackPosition;
    private bool canMove2 = true;
    public GameObject ArrowTransform;
    private Vector2 lastInputDirection = Vector2.zero;  // ������ �Էµ� ����

    private bool needsUpdate = false;
    private float updateDelay = 0.1f; // ������Ʈ ���� �ð� (�� ����)
    private float lastUpdateTime = 0f;

    // Start is called before the first frame update
    protected override void Start()
    {
        ArrowTransform = GameObject.Find("2arrow");
        ArrowTransform.SetActive(false);
        base.Start();
        
    }
    private void OnDisable()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        if (!GameManager.instance.playersTurn) return;

        int horizontal = (int)Input.GetAxisRaw("Horizontal");
        int vertical = (int)Input.GetAxisRaw("Vertical");

        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift);
        bool isXPressed = Input.GetKeyDown(KeyCode.X); // Use GetKeyDown to detect single press

        //�̵����� �ʰ� ���⸸ ��ȯ
        if (isXPressed)
        {
            canMove2 = !canMove2; // Toggle the movement state
            ArrowTransform.SetActive(!canMove2);
        }

        //�÷��̾ �ؽ�Ʈ Ȯ�� ���� �ƴҶ�
        if (Input.GetKeyDown(KeyCode.A) && !GameManager.PlayerTextCheck)
        {
            Vector3 startPosition = transform.position;
            attackPosition = transform.position + new Vector3(GameManager.Lookdirection.x, GameManager.Lookdirection.y, 0);
            StartCoroutine(AttackMovement2(startPosition, attackPosition, childTransform));
        }
        else if (Input.GetKeyDown(KeyCode.S) && !GameManager.PlayerTextCheck)
        {
            GameManager.PlayerTextCheck = true;
            //BagUi bag = new BagUi();
            //bag.bagTextBox();
            StartCoroutine(GameManager.instance.bagTextBox());
        }
        else if (Input.GetKeyDown(KeyCode.C) && !GameManager.PlayerTextCheck)
        {
            SceneManager.LoadScene("SampleScene");
        }
        else
        {
            if (!GameManager.PlayerTextCheck)
            {
                if (canMove2)
                {
                    //����Ʈ�� ������ �밢�� �̵��� ����
                    if (isShiftPressed)
                    {
                        if (horizontal != 0 && vertical != 0)
                        {
                            GameManager.StairOut = false;
                            GameManager.Lookdirection = new Vector2(horizontal, vertical);
                            AttemptMove<Enemy>(GameManager.Lookdirection);
                            var (zRotation, Charint) = GetDirectionInfo(GameManager.Lookdirection);
                            spriteRenderer.sprite = directionsprites[Charint];
                        }
                    }
                    else
                    {
                        if (horizontal != 0 || vertical != 0)
                        {
                            GameManager.StairOut = false;
                            GameManager.Lookdirection = new Vector2(horizontal, vertical);
                            AttemptMove<Enemy>(GameManager.Lookdirection);
                            var (zRotation, Charint) = GetDirectionInfo(GameManager.Lookdirection);
                            spriteRenderer.sprite = directionsprites[Charint];
                        }
                    }
                }
                else
                {

                    if (horizontal != 0 || vertical != 0)
                    {
                        GameManager.Lookdirection = new Vector2(horizontal, vertical);
                        // Lookdirection�� ���� ȸ������ ���� ���ڿ� ��������
                        var (zRotation, Charint) = GetDirectionInfo(GameManager.Lookdirection);

                        // �ڽ� ������Ʈ�� �����̼��� ����
                        ArrowTransform.transform.localEulerAngles = new Vector3(0, 0, zRotation);
                        spriteRenderer.sprite = directionsprites[Charint];
                        //Debug.Log(GameManager.Lookdirection);
                    }
                }
            }
        }
    }
    protected IEnumerator AttackMovement2(Vector3 startPosition, Vector3 attackPosition, Transform childTransform)
    {
        // ��ü ���� �ð� ���
        float startTime = Time.time;

        // ���� Sorting Layer ����
        string originalSortingLayer = spriteRenderer.sortingLayerName;

        // Sorting Layer�� "Attack"���� ����
        spriteRenderer.sortingLayerName = "attack";

        float sqrRemainingDistance = (childTransform.position - attackPosition).sqrMagnitude;

        // �ڽ� ������Ʈ�� �ش� ��ġ�� ������ �̵�
        while (sqrRemainingDistance > float.Epsilon)
        {
            Vector3 newPosition = Vector3.MoveTowards(childTransform.position, attackPosition, inverseMoveTime * Time.deltaTime * 5);
            childTransform.position = newPosition;
            sqrRemainingDistance = (childTransform.position - attackPosition).sqrMagnitude;
            yield return null;
        }

        // ��� ���
        yield return new WaitForSeconds(0.2f);

        // �ڽ� ������Ʈ�� ���� ��ġ�� õõ�� ���ƿ���
        sqrRemainingDistance = (childTransform.position - startPosition).sqrMagnitude;
        while (sqrRemainingDistance > float.Epsilon)
        {
            Vector3 newPosition = Vector3.MoveTowards(childTransform.position, startPosition, inverseMoveTime * Time.deltaTime * 2);
            childTransform.position = newPosition;
            sqrRemainingDistance = (childTransform.position - startPosition).sqrMagnitude;
            yield return null;
        }
        // ��ü �ð� ���
        totalMoveTime2 = Time.time - startTime;
        //GameManager.turnDelay = totalMoveTime2;

        spriteRenderer.sortingLayerName = originalSortingLayer;

        //attackPosition�� GameManager�� enemies����Ʈ �� ���� �ִ� ��ġ�� �ϳ��� ���� ���, �ش� ���� Damaged();�� ȣ��
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (Vector3.Distance(enemy.transform.position, attackPosition) < 0.1f)  // �Ÿ��� ���� ���ٸ�
            {
                // �� Ŭ������ �ν��Ͻ��� ������
                Enemy targetEnemy = enemy.GetComponent<Enemy>();

                if (targetEnemy != null)
                {
                    //Debug.Log("��ġ����");
                    int adjustedAttackPower = GameManager.PlayerAttack * (int)Math.Round(GameManager.Playerstrength / 8.0);
                    targetEnemy.Damaged(adjustedAttackPower);
                    StartCoroutine(GameManager.instance.NeedTextandTurnEnd(null, 0, 0, false));//������� ���ľ���
                    //SavePosition(transform.position);
                }
                
                yield break;  // �� �ϳ����� ���ظ� ������ ���� ����
            }
        }

        StartCoroutine(GameManager.instance.NeedTextandTurnEnd(null, 0, 0, false));
        yield return new WaitForFixedUpdate();
    }

    protected override void AttemptMove<T>(Vector2 direction2)
    {
        isMoving = true;                  

        base.AttemptMove<T>(direction2);

        RaycastHit2D hit;
        /*if (canMove)
        {
            isMoving = false;
            SavePosition((Vector2)transform.position + direction2);//�÷��̾� ��ġ ����
            StartCoroutine(SmoothMovement(end));
        }*/
        if (Move(direction2, out hit))
        {
            CheckDead();
            isMoving = false;
            SavePosition((Vector2)transform.position + direction2);//�÷��̾� ��ġ ����
            //StartCoroutine(SmoothMovement2(end));
            StartCoroutine(GameManager.instance.NeedTextandTurnEnd(null, 0, 0, false));
        }
        
    }
    protected IEnumerator SmoothMovement2(Vector3 end)
    {
        float sqrRemaingDistance = (transform.position - end).sqrMagnitude;

        while (sqrRemaingDistance > float.Epsilon)
        {
            Vector3 newPosition = Vector3.MoveTowards(rb2D.position, end, inverseMoveTime * Time.deltaTime * 5);
            rb2D.MovePosition(newPosition);
            sqrRemaingDistance = (transform.position - end).sqrMagnitude;
            yield return null;
        }

        rb2D.MovePosition(end);
        yield return new WaitForFixedUpdate();
        StartCoroutine(GameManager.instance.NeedTextandTurnEnd(null, 0, 0, false));
        //yield return null;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.tag == "Exit" && (!GameManager.StairOut))
        {
            GameManager.StairOut = true;

            GameManager.PlayerTextCheck = true;
            StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, 0, 4));
        }

        if (other.tag == "Coin" && (!GameManager.StairOut))
        {
            GameManager.StairOut = true;

            int randomIndex = UnityEngine.Random.Range(100, 500);
            GameManager.PlayerGold += randomIndex;

            GameManager.GroundSetPositions.Remove(transform.position);

            StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, randomIndex, 5));
            other.gameObject.SetActive(false);
        }

        if (other.tag == "Item" && (!GameManager.StairOut))
        {
            GameManager.StairOut = true;
            if (GameManager.items.Count < GameManager.maxbag)
            {
                DontDestroyOnLoad(other);
                // ������Ʈ �̸����� "(Clone)" ����
                other.gameObject.name = other.gameObject.name.Replace("(Clone)", "");
                string itemName = other.gameObject.name; // ������ �̸� ���
                GameManager.items.Add(other.gameObject);
                GameManager.GroundSetPositions.Remove(transform.position);

                //String ItemName = other.gameObject.name.Replace("(Clone)", ""); // "(Clone)" ����;
                StartCoroutine(GameManager.instance.ShowDamagedTextBox(itemName, 0, 6));
                other.gameObject.SetActive(false);
            }
            else
            {
                StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, 0, 7));
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {   
        
    }
    protected override void onCantMove<T> (T componet)
    {
        /*Enemy hitEnemy = componet as Enemy;
        //GameManager.instance.playersTurn = false;
        if (hitEnemy != null)
        {
            int adjustedAttackPower = GameManager.PlayerAttack * (int)Math.Round(GameManager.Playerstrength / 8.0);
            hitEnemy.Damaged(adjustedAttackPower);

            //StartCoroutine(AttackMovement(transform.position, hitEnemy.transform.position, childTransform));
        }*/

    }

    private void Restart()
    {
        //Application.LoadLevel(Application.loadedLevel);
        StopAllCoroutines();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        //StopAllCoroutines();
        //GameManager.instance.InitGame();
    }

    public void Damaged (int EnemyPower)
    {

        int randomFactor = UnityEngine.Random.Range(-3, 4); // -3���� +3 ������ ���� ��
        if (UnityEngine.Random.value < 0.9f)
        {
            int baseDamage = Math.Max((EnemyPower - GameManager.PlayerDeffense) + randomFactor, 1);
            StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, baseDamage,0));
            GameManager.PlayerHp -= baseDamage;
            if (GameManager.PlayerHp < 0)
            {
                GameManager.PlayerHp = 0;
            }
        }
        else
        {
            StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, 0, 1));
            //Debug.Log("������");
        }
        CheckDead();
    }

    private void CheckDead()
    {
        GameManager.instance.GameOver();   
            
    }
    private void SavePosition(Vector2 position)
    {
        GameManager.instance.PlayerPosition = position;
    }
}
