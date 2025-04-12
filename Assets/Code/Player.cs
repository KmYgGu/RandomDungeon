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
    private Vector2 lastInputDirection = Vector2.zero;  // 마지막 입력된 방향

    private bool needsUpdate = false;
    private float updateDelay = 0.1f; // 업데이트 지연 시간 (초 단위)
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

        //이동하지 않고 방향만 전환
        if (isXPressed)
        {
            canMove2 = !canMove2; // Toggle the movement state
            ArrowTransform.SetActive(!canMove2);
        }

        //플레이어가 텍스트 확인 중이 아닐때
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
                    //쉬프트를 누르면 대각선 이동만 가능
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
                        // Lookdirection에 따른 회전값과 방향 문자열 가져오기
                        var (zRotation, Charint) = GetDirectionInfo(GameManager.Lookdirection);

                        // 자식 오브젝트의 로테이션을 변경
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
        // 전체 시작 시간 기록
        float startTime = Time.time;

        // 기존 Sorting Layer 저장
        string originalSortingLayer = spriteRenderer.sortingLayerName;

        // Sorting Layer를 "Attack"으로 변경
        spriteRenderer.sortingLayerName = "attack";

        float sqrRemainingDistance = (childTransform.position - attackPosition).sqrMagnitude;

        // 자식 오브젝트가 해당 위치로 빠르게 이동
        while (sqrRemainingDistance > float.Epsilon)
        {
            Vector3 newPosition = Vector3.MoveTowards(childTransform.position, attackPosition, inverseMoveTime * Time.deltaTime * 5);
            childTransform.position = newPosition;
            sqrRemainingDistance = (childTransform.position - attackPosition).sqrMagnitude;
            yield return null;
        }

        // 잠시 대기
        yield return new WaitForSeconds(0.2f);

        // 자식 오브젝트가 원래 위치로 천천히 돌아오기
        sqrRemainingDistance = (childTransform.position - startPosition).sqrMagnitude;
        while (sqrRemainingDistance > float.Epsilon)
        {
            Vector3 newPosition = Vector3.MoveTowards(childTransform.position, startPosition, inverseMoveTime * Time.deltaTime * 2);
            childTransform.position = newPosition;
            sqrRemainingDistance = (childTransform.position - startPosition).sqrMagnitude;
            yield return null;
        }
        // 전체 시간 계산
        totalMoveTime2 = Time.time - startTime;
        //GameManager.turnDelay = totalMoveTime2;

        spriteRenderer.sortingLayerName = originalSortingLayer;

        //attackPosition이 GameManager의 enemies리스트 중 적이 있는 위치와 하나라도 같을 경우, 해당 적의 Damaged();를 호출
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (Vector3.Distance(enemy.transform.position, attackPosition) < 0.1f)  // 거리가 거의 같다면
            {
                // 적 클래스의 인스턴스를 가져옴
                Enemy targetEnemy = enemy.GetComponent<Enemy>();

                if (targetEnemy != null)
                {
                    //Debug.Log("위치맞음");
                    int adjustedAttackPower = GameManager.PlayerAttack * (int)Math.Round(GameManager.Playerstrength / 8.0);
                    targetEnemy.Damaged(adjustedAttackPower);
                    StartCoroutine(GameManager.instance.NeedTextandTurnEnd(null, 0, 0, false));//여기수정 고쳐야함
                    //SavePosition(transform.position);
                }
                
                yield break;  // 적 하나에게 피해를 입히면 루프 종료
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
            SavePosition((Vector2)transform.position + direction2);//플레이어 위치 저장
            StartCoroutine(SmoothMovement(end));
        }*/
        if (Move(direction2, out hit))
        {
            CheckDead();
            isMoving = false;
            SavePosition((Vector2)transform.position + direction2);//플레이어 위치 저장
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
                // 오브젝트 이름에서 "(Clone)" 제거
                other.gameObject.name = other.gameObject.name.Replace("(Clone)", "");
                string itemName = other.gameObject.name; // 수정된 이름 사용
                GameManager.items.Add(other.gameObject);
                GameManager.GroundSetPositions.Remove(transform.position);

                //String ItemName = other.gameObject.name.Replace("(Clone)", ""); // "(Clone)" 제거;
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

        int randomFactor = UnityEngine.Random.Range(-3, 4); // -3에서 +3 사이의 랜덤 값
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
            //Debug.Log("빗나감");
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
