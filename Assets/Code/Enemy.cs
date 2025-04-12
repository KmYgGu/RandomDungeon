using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;


public class Enemy : MovingObject
{
    public List<Rect> roomdata; //RandomDung으로 생성던 무작위 던전의 '방'들의 위치좌표가 담긴 값
    public List<Vector3> connectionPoints; //방 근처의 통로로 갈수 있는 위치들의 좌표가 담긴 값
    private bool hasReachedClosestPoint = false;
    private List<Vector2> walkRemember = new List<Vector2>();//이동한 거리를 다시 되돌아  가지 않게 왔던 길을 기억

    [SerializeField] private bool isPatrolTargetSet = false;
    [SerializeField] private Vector3 closestPoint;

    public int ThisHp;
    int ThisAttack;
    int ThisDefense;

    bool attacking;


    protected override void Start()
    {
        connectionPoints = FindObjectOfType<RandomDung>().edgePoints;
        GameManager.instance.AddEnemyToList(this);
        roomdata = FindObjectOfType<RandomDung>().RoomData;

        base.Start();
    }

    public enum EnemyMode
    {
        Chase,
        Patrol
    }
    private EnemyMode currentMode = EnemyMode.Patrol;

    protected override void AttemptMove<T>(Vector2 direction)
    {
        base.AttemptMove<T>(direction);
        if (canMove)
        {
            StartCoroutine(SmoothMovement(end));
        }
    }
    

    //적의 모드에 따라 행동이 달라짐
    public IEnumerator MoveEnemy()
    {
        attacking = false;
        yield return StartCoroutine(CheckForPlayer());
        if (attacking) yield break;
        switch (currentMode)
        {
            case EnemyMode.Chase:
                Vector2 chaseDirection = GetDirectionToPlayer();
                Vector2 chasePosition = (Vector2)transform.position + chaseDirection;

                //적이 이동할 경로에 1.다른 적이 있는지, 2.벽에 가로막혔는지, 3.대각선으로 가야되는데 그 사이에 벽이 있는지
                if (IsEnemyAtPosition(chasePosition) || IsWallAtPosition(chasePosition) || !CanMoveDiagonally(transform.position, chaseDirection))//||!IsBlockedByWater(chaseDirection)
                {
                    //적이 플레이어에게 빠르게 접근할 수 있는 가장 가까운 방향으로 설정
                    Vector2[] alternateDirections = GetAlternateDirections(chaseDirection);

                    foreach (Vector2 altDirection in alternateDirections)
                    {
                        Vector2 altPosition = (Vector2)transform.position + altDirection;
                        if (!IsEnemyAtPosition(altPosition) && !IsWallAtPosition(altPosition) && !IsBlockedByWater(altPosition))
                        {
                            SavePosition(altPosition);
                            AttemptMove<Player>(altDirection);
                            yield return new WaitForFixedUpdate();
                            GameManager.instance.EnemyTurnCompleted();

                            yield break;
                        }
                    }
                    GameManager.instance.EnemyTurnCompleted();//?
                    Debug.Log("문제점5");
                    yield break; // 모든 방향이 막혔을 경우 즉시 종료
                }

                SavePosition(chasePosition);
                AttemptMove<Player>(chaseDirection);
                yield return new WaitForFixedUpdate();
                GameManager.instance.EnemyTurnCompleted();
                break;

            case EnemyMode.Patrol:

                Vector2 lastPosition = transform.position; // Enemy의 마지막 위치를 저장하는 변수

                if (!isPatrolTargetSet)
                {
                    closestPoint = FindClosestPoint(connectionPoints);
                    isPatrolTargetSet = true;
                }
                if (hasReachedClosestPoint)
                {
                    // closestPointFloat에 도달한 이후에는 무작위 방향 이동만 수행
                    Vector2[] randomDirections = new Vector2[]
                    {
                        Vector2.up,
                        Vector2.down,
                        Vector2.left,
                        Vector2.right
                    };


                    // 무작위로 섞기
                    System.Random rng = new System.Random();
                    randomDirections = randomDirections.OrderBy(x => rng.Next()).ToArray();

                    foreach (Vector2 direction in randomDirections)
                    {
                        Vector2 newPosition = (Vector2)transform.position + direction;
                        if (!IsEnemyAtPosition(newPosition) && !IsWallAtPosition(newPosition) && !walkRemember.Contains(newPosition) && !IsBlockedByWater(newPosition))
                        {
                            SavePosition(newPosition);                         
                            walkRemember.Add(newPosition);
                            AttemptMove<Player>(direction);
                            yield return new WaitForFixedUpdate();
                            
                            //현재 적이 방에 있는지
                            Vector2 currentPosition = new Vector2(transform.position.x, transform.position.y);
                            foreach (Rect room in roomdata)
                            {
                                if (room.Contains(currentPosition))
                                {
                                    hasReachedClosestPoint = false;
                                    isPatrolTargetSet = false;
                                    break;
                                }
                            }

                            if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                            {
                                Debug.LogWarning("무작위로 움직이는 도중 길이 막혔다. 다시움직인다1");
                                walkRemember.Clear();
                                hasReachedClosestPoint = true;
                                GameManager.instance.EnemyTurnCompleted();
                                yield break;
                            }
                            lastPosition = transform.position;
                            GameManager.instance.EnemyTurnCompleted();
                            yield break;
                        }                      
                    }
                    if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                    {
                        Debug.LogWarning("재확인. 4다시움직인다4");
                        walkRemember.Clear();
                        hasReachedClosestPoint = true;
                        GameManager.instance.EnemyTurnCompleted();
                        yield break ;
                    }
                    lastPosition = transform.position;
                    GameManager.instance.EnemyTurnCompleted();//?
                    Debug.Log("문제점1");
                    yield break;
                }
                else
                {
                    Vector2 patrolDirection = GetPatrolDirection();
                    Vector2 patrolPosition = (Vector2)transform.position + patrolDirection;

                    if (IsEnemyAtPosition(patrolPosition) || IsWallAtPosition(patrolPosition) || !CanMoveDiagonally(transform.position, patrolDirection))// || !IsBlockedByWater(patrolPosition)
                    {
                        Vector2[] alternateDirections = GetAlternateDirections(patrolDirection);

                        foreach (Vector2 altDirection in alternateDirections)
                        {
                            Vector2 altPosition = (Vector2)transform.position + altDirection;
                            if (!IsEnemyAtPosition(altPosition) && !IsWallAtPosition(altPosition) && !walkRemember.Contains(altPosition) && !IsBlockedByWater(altPosition))//
                            {
                                SavePosition(altPosition);                              
                                AttemptMove<Player>(altDirection);
                                walkRemember.Add(altPosition);
                                //yield return new WaitForFixedUpdate();주석처리해봄
                                //GameManager.instance.EnemyTurnCompleted();
                                if (HasReachedTarget())
                                {
                                    hasReachedClosestPoint = true;  // 상태 플래그 설정
                                    GameManager.instance.EnemyTurnCompleted();//?
                                    Debug.Log("문제점2");
                                    yield break;
                                }
                                if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                                {
                                    Debug.LogWarning("정해진 방향으로 가던 도중, 움직일수 없다. 2다시움직인다2");
                                    /*walkRemember.Clear();
                                    hasReachedClosestPoint = true;*/
                                    GameManager.instance.EnemyTurnCompleted();//?
                                    yield break;
                                }
                                lastPosition = transform.position;
                                //Debug.Log("문제점3");
                                GameManager.instance.EnemyTurnCompleted();//?
                                yield break;
                            }
                            
                        }
                        if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                        {
                            Debug.LogWarning("한번더 재확인. 다시움직인다5");
                            walkRemember.Clear();
                            hasReachedClosestPoint = true;
                            GameManager.instance.EnemyTurnCompleted();//?
                            yield break;
                        }
                        GameManager.instance.EnemyTurnCompleted();//?
                        Debug.Log("문제점4");
                        lastPosition = transform.position;
                        yield break;
                    }

                    walkRemember.Add(patrolPosition);
                    SavePosition(patrolPosition);
                    AttemptMove<Player>(patrolDirection);
                    yield return new WaitForFixedUpdate();
                    
                    if (HasReachedTarget())
                    {
                        //Debug.Log("Patrol1 target reached: " + closestPoint);
                        hasReachedClosestPoint = true;  // 상태 플래그 설정
                        GameManager.instance.EnemyTurnCompleted();
                        yield break;
                    }
                    if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                    {
                        Debug.LogWarning("잠깐 가만히 있었는데, 움직일 수 없다. 다시움직인다3");
                        walkRemember.Clear();
                        hasReachedClosestPoint = true;
                        GameManager.instance.EnemyTurnCompleted();
                        yield break ;
                    }
                    GameManager.instance.EnemyTurnCompleted();
                    lastPosition = transform.position;

                }
                break;

        }
    }

    private IEnumerator CheckForPlayer()
    {
        Vector2 playerPosition = GameManager.instance.Player.transform.position;
        Vector2 enemyPosition = transform.position;

        // 1. 적의 한 칸 근처에 플레이어가 있는지 확인
        Vector2[] directions = {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1)
    };

        foreach (Vector2 direction in directions)
        {
            Vector2 checkPosition = enemyPosition + direction;

            // 플레이어가 있는 위치가 공격 가능한 위치인지 확인
            if (Vector2.Distance(playerPosition, checkPosition) < 0.1f && !IsWallAtPosition(checkPosition))
            {
                // 대각선 방향일 경우, CanMoveDiagonally로 추가 확인
                if (Mathf.Abs(direction.x) > 0 && Mathf.Abs(direction.y) > 0)
                {
                    if (CanMoveDiagonally(enemyPosition, direction))
                    {
                        currentMode = EnemyMode.Chase;
                        yield return StartCoroutine(AttackPlayer());
                        yield break;
                    }
                }
                else
                {
                    currentMode = EnemyMode.Chase;
                    yield return StartCoroutine(AttackPlayer());
                    yield break;
                }
            }
        }

        // 기존 CheckForPlayer 내용 유지
        Rect currentRoom = Rect.zero;
        foreach (Rect room in roomdata)
        {
            if (room.Contains(enemyPosition))
            {
                currentRoom = room;
                break;
            }
        }

        if (currentRoom != Rect.zero && currentRoom.Contains(playerPosition))
        {
            currentMode = EnemyMode.Chase;
            //return;
            yield break;
        }

        if (currentMode == EnemyMode.Chase && Vector2.Distance(playerPosition, enemyPosition) > 15.0f)
        {
            Debug.Log("플레이어를 놓쳤습니다. 순찰을 시작합니다.");
            currentMode = EnemyMode.Patrol;
            isPatrolTargetSet = false;
        }
    }
    private IEnumerator AttackPlayer()
    {
        Player hitPlayer = GameManager.instance.Player.GetComponent<Player>();
        if (hitPlayer != null)
        {
            yield return StartCoroutine(AttackMovement(transform.position, hitPlayer.transform.position, childTransform));
            hitPlayer.Damaged(ThisAttack);
            yield return new WaitForFixedUpdate();
            GameManager.instance.EnemyTurnCompleted();
            attacking = true;
        }
    }

    private Vector3 FindClosestPoint(List<Vector3> points)
    {
        
        Vector2 currentPosition = new Vector2(transform.position.x, transform.position.y);

        // 적의 위치를 포함하는 Rect를 찾음
        Rect currentRoom = Rect.zero;
        foreach (Rect room in roomdata)
        {
            if (room.Contains(currentPosition))
            {
                currentRoom = room;
                break;
            }
        }

        // currentRoom이 존재하지 않으면 (즉, 적의 위치가 rooms에 포함되지 않으면) 기본값 반환
        if (currentRoom == Rect.zero)
        {
            Debug.LogError("No room found containing the enemy position.");
            return Vector3.zero;
        }

        // currentRoom 범위 내에서 가장 가까운 connectionPoint를 찾음
        Vector3 closestPoint = Vector3.zero;

        // currentRoom 범위 내에서 감지된 모든 connectionPoint를 리스트에 저장
        List<Vector3> validPoints = new List<Vector3>();
        int range = 1;
        Rect expandedRoom = new Rect(currentRoom.xMin - range, currentRoom.yMin - range, currentRoom.width + range * 2, currentRoom.height + range * 2);

        foreach (Vector3 point in points)
        {
            Vector2 point2D = new Vector2(point.x, point.y);

            if (expandedRoom.Contains(point2D) && !walkRemember.Contains(point2D))
            {
                validPoints.Add(point);
            }
        }

        // 만약 유효한 포인트가 없다면 walkRemember를 초기화하고 다시 시도
        if (validPoints.Count == 0)
        {
            Debug.LogWarning("전부다 막혀있거나, 왔던 길입니다. 다시 경로를 재탐색합니다");
            walkRemember.Clear();

            foreach (Vector3 point in points)
            {
                Vector2 point2D = new Vector2(point.x, point.y);

                if (expandedRoom.Contains(point2D))
                {
                    validPoints.Add(point);
                }
            }
        }

        // 유효한 포인트들 중에서 무작위로 하나를 선택
        if (validPoints.Count > 0)
        {
            System.Random rng = new System.Random();
            closestPoint = validPoints[rng.Next(validPoints.Count)];
        }
        else
        {
            Debug.LogError("No valid points found even after clearing walkRemember.");
            return Vector3.zero; // 이 경우엔 기본값을 반환
        }

        return closestPoint;

    }

    private Vector2[] GetAlternateDirections(Vector2 direction)
    {
        if (direction.x != 0 && direction.y != 0)
        {
            return new Vector2[]
            {
                new Vector2(direction.x, 0),
                new Vector2(0, direction.y)
            };
        }
        return new Vector2[]
        {
            new Vector2(-direction.x, direction.y),
            new Vector2(direction.x, -direction.y),
            new Vector2(-direction.x, -direction.y)
        };
    }

    // 이동 완료 후 위치를 저장하는 메서드
    private void SavePosition(Vector2 position)
    {
        GameManager.instance.enemyPositions.Add(position);
    }

    public Vector2 GetDirectionToPlayer()
    {
        //Vector2 playerPosition = target.position;
        Vector2 playerPosition = GameManager.instance.Player.transform.position;
        Vector2 enemyPosition = transform.position;

        // 우선 플레이어와의 거리를 계산
        float distanceToPlayer = Vector2.Distance(playerPosition, enemyPosition);

        if (distanceToPlayer <= 0.5f)
        {
            // 플레이어가 근처에 있는 경우 공격을 시도
            return Vector2.zero; // 이동하지 않고 공격
        }

        float xDir = 0;
        float yDir = 0;

        if (Mathf.Abs(playerPosition.y - enemyPosition.y) < float.Epsilon)
        {
            // 플레이어와 같은 y축에 있을 때, x축으로 이동
            xDir = playerPosition.x > enemyPosition.x ? 1 : -1;
        }
        else if (Mathf.Abs(playerPosition.x - enemyPosition.x) < float.Epsilon)
        {
            // 플레이어와 같은 x축에 있을 때, y축으로 이동
            yDir = playerPosition.y > enemyPosition.y ? 1 : -1;
        }
        else
        {
            // 대각선으로 이동
            xDir = playerPosition.x > enemyPosition.x ? 1 : -1;
            yDir = playerPosition.y > enemyPosition.y ? 1 : -1;
        }

        return new Vector2(xDir, yDir);
    }
    private bool HasReachedTarget()
    {
        //return Vector2.Distance(transform.position, closestPoint) < 0.3f;
        //return Vector2.Distance(this.transform.position, closestPoint) == 0;
        //return Mathf.Approximately(Vector2.Distance(transform.position, closestPoint), 0);
        return Mathf.Approximately(Vector2.Distance((Vector2)transform.position, (Vector2)closestPoint), 0);

    }
    private Vector2 GetPatrolDirection()
    {
        Vector2 enemyPosition = transform.position;

        float xDir = 0;
        float yDir = 0;

        if (Mathf.Abs(closestPoint.y - enemyPosition.y) < float.Epsilon)
        {
            // 같은 y축에 있을 때, x축으로 이동
            xDir = closestPoint.x > enemyPosition.x ? 1 : -1;
        }
        else if (Mathf.Abs(closestPoint.x - enemyPosition.x) < float.Epsilon)
        {
            // 같은 x축에 있을 때, y축으로 이동
            yDir = closestPoint.y > enemyPosition.y ? 1 : -1;
        }
        else
        {
            // 대각선으로 이동
            xDir = closestPoint.x > enemyPosition.x ? 1 : -1;
            yDir = closestPoint.y > enemyPosition.y ? 1 : -1;
        }

        return new Vector2(xDir, yDir);
    }

    protected override void onCantMove<T>(T component)
    {
        Player hitPlayer = component as Player;

        if (hitPlayer != null)
        {
            StartCoroutine(AttackMovement(transform.position, hitPlayer.transform.position, childTransform));
            
            hitPlayer.Damaged(ThisAttack);
            //Debug.Log("Player Damaged!");
        }
    }

    private bool IsEnemyAtPosition(Vector2 position)
    {

        foreach (Vector2 enemyPosition in GameManager.instance.enemyPositions)
        {
            if (Vector2.Distance(position, enemyPosition) < 0.1f) // 반경 0.1f 내에 다른 적이 있는 경우
            {
                return true;
            }
        }

        return false;
    }
    private bool IsWallAtPosition(Vector2 position)
    {
        return IsBlockedByTile(position);
    }

    public override void SetStats(int SetHp, int SetAttack, int SetDefense)
    {
        //base.SetStats(SetHp, SetAttack, SetDefense);
        ThisHp = SetHp;
        ThisAttack = SetAttack;
        ThisDefense = SetDefense;
    }
    public void Damaged(int PlayerPower)
    {

        int randomFactor = UnityEngine.Random.Range(-1, 3); // -1에서 +2 사이의 랜덤 값
        if (UnityEngine.Random.value < 0.9f)
        {
            int baseDamage = Math.Max((PlayerPower - ThisDefense) + randomFactor, 1);
            
            if (UnityEngine.Random.value < 0.1f)
            {
                baseDamage = (int)Math.Round(baseDamage * 1.8);//1.8 크리티컬 데미지
                StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, baseDamage, 2));
                ThisHp -= baseDamage;
                
                return;
            }
            StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, baseDamage, 3));
            ThisHp -= baseDamage;
        }
        else
        {
            StartCoroutine(GameManager.instance.ShowDamagedTextBox(null, 0, 1));
        }

        CheckDead();
    }
    private void CheckDead()
    {
        if (ThisHp <= 0)
        {
            gameObject.name = gameObject.name.Replace("(Clone)", "");
            string itemName = gameObject.name; // 수정된 이름 사용

            StartCoroutine(GameManager.instance.ShowDamagedTextBox(itemName, (ThisAttack+ThisDefense), 8));
            GameManager.instance.RemoveEnemyList(this);
            //StartCoroutine(GameManager.instance.MoveEnemies());
            gameObject.SetActive(false);
            //Destroy(gameObject);
            //return;
        }
        //StartCoroutine(GameManager.instance.MoveEnemies());
    }
}