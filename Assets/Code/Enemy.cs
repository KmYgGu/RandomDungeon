using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;


public class Enemy : MovingObject
{
    public List<Rect> roomdata; //RandomDung���� ������ ������ ������ '��'���� ��ġ��ǥ�� ��� ��
    public List<Vector3> connectionPoints; //�� ��ó�� ��η� ���� �ִ� ��ġ���� ��ǥ�� ��� ��
    private bool hasReachedClosestPoint = false;
    private List<Vector2> walkRemember = new List<Vector2>();//�̵��� �Ÿ��� �ٽ� �ǵ���  ���� �ʰ� �Դ� ���� ���

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
    

    //���� ��忡 ���� �ൿ�� �޶���
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

                //���� �̵��� ��ο� 1.�ٸ� ���� �ִ���, 2.���� ���θ�������, 3.�밢������ ���ߵǴµ� �� ���̿� ���� �ִ���
                if (IsEnemyAtPosition(chasePosition) || IsWallAtPosition(chasePosition) || !CanMoveDiagonally(transform.position, chaseDirection))//||!IsBlockedByWater(chaseDirection)
                {
                    //���� �÷��̾�� ������ ������ �� �ִ� ���� ����� �������� ����
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
                    Debug.Log("������5");
                    yield break; // ��� ������ ������ ��� ��� ����
                }

                SavePosition(chasePosition);
                AttemptMove<Player>(chaseDirection);
                yield return new WaitForFixedUpdate();
                GameManager.instance.EnemyTurnCompleted();
                break;

            case EnemyMode.Patrol:

                Vector2 lastPosition = transform.position; // Enemy�� ������ ��ġ�� �����ϴ� ����

                if (!isPatrolTargetSet)
                {
                    closestPoint = FindClosestPoint(connectionPoints);
                    isPatrolTargetSet = true;
                }
                if (hasReachedClosestPoint)
                {
                    // closestPointFloat�� ������ ���Ŀ��� ������ ���� �̵��� ����
                    Vector2[] randomDirections = new Vector2[]
                    {
                        Vector2.up,
                        Vector2.down,
                        Vector2.left,
                        Vector2.right
                    };


                    // �������� ����
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
                            
                            //���� ���� �濡 �ִ���
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
                                Debug.LogWarning("�������� �����̴� ���� ���� ������. �ٽÿ����δ�1");
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
                        Debug.LogWarning("��Ȯ��. 4�ٽÿ����δ�4");
                        walkRemember.Clear();
                        hasReachedClosestPoint = true;
                        GameManager.instance.EnemyTurnCompleted();
                        yield break ;
                    }
                    lastPosition = transform.position;
                    GameManager.instance.EnemyTurnCompleted();//?
                    Debug.Log("������1");
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
                                //yield return new WaitForFixedUpdate();�ּ�ó���غ�
                                //GameManager.instance.EnemyTurnCompleted();
                                if (HasReachedTarget())
                                {
                                    hasReachedClosestPoint = true;  // ���� �÷��� ����
                                    GameManager.instance.EnemyTurnCompleted();//?
                                    Debug.Log("������2");
                                    yield break;
                                }
                                if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                                {
                                    Debug.LogWarning("������ �������� ���� ����, �����ϼ� ����. 2�ٽÿ����δ�2");
                                    /*walkRemember.Clear();
                                    hasReachedClosestPoint = true;*/
                                    GameManager.instance.EnemyTurnCompleted();//?
                                    yield break;
                                }
                                lastPosition = transform.position;
                                //Debug.Log("������3");
                                GameManager.instance.EnemyTurnCompleted();//?
                                yield break;
                            }
                            
                        }
                        if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                        {
                            Debug.LogWarning("�ѹ��� ��Ȯ��. �ٽÿ����δ�5");
                            walkRemember.Clear();
                            hasReachedClosestPoint = true;
                            GameManager.instance.EnemyTurnCompleted();//?
                            yield break;
                        }
                        GameManager.instance.EnemyTurnCompleted();//?
                        Debug.Log("������4");
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
                        hasReachedClosestPoint = true;  // ���� �÷��� ����
                        GameManager.instance.EnemyTurnCompleted();
                        yield break;
                    }
                    if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                    {
                        Debug.LogWarning("��� ������ �־��µ�, ������ �� ����. �ٽÿ����δ�3");
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

        // 1. ���� �� ĭ ��ó�� �÷��̾ �ִ��� Ȯ��
        Vector2[] directions = {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1)
    };

        foreach (Vector2 direction in directions)
        {
            Vector2 checkPosition = enemyPosition + direction;

            // �÷��̾ �ִ� ��ġ�� ���� ������ ��ġ���� Ȯ��
            if (Vector2.Distance(playerPosition, checkPosition) < 0.1f && !IsWallAtPosition(checkPosition))
            {
                // �밢�� ������ ���, CanMoveDiagonally�� �߰� Ȯ��
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

        // ���� CheckForPlayer ���� ����
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
            Debug.Log("�÷��̾ ���ƽ��ϴ�. ������ �����մϴ�.");
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

        // ���� ��ġ�� �����ϴ� Rect�� ã��
        Rect currentRoom = Rect.zero;
        foreach (Rect room in roomdata)
        {
            if (room.Contains(currentPosition))
            {
                currentRoom = room;
                break;
            }
        }

        // currentRoom�� �������� ������ (��, ���� ��ġ�� rooms�� ���Ե��� ������) �⺻�� ��ȯ
        if (currentRoom == Rect.zero)
        {
            Debug.LogError("No room found containing the enemy position.");
            return Vector3.zero;
        }

        // currentRoom ���� ������ ���� ����� connectionPoint�� ã��
        Vector3 closestPoint = Vector3.zero;

        // currentRoom ���� ������ ������ ��� connectionPoint�� ����Ʈ�� ����
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

        // ���� ��ȿ�� ����Ʈ�� ���ٸ� walkRemember�� �ʱ�ȭ�ϰ� �ٽ� �õ�
        if (validPoints.Count == 0)
        {
            Debug.LogWarning("���δ� �����ְų�, �Դ� ���Դϴ�. �ٽ� ��θ� ��Ž���մϴ�");
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

        // ��ȿ�� ����Ʈ�� �߿��� �������� �ϳ��� ����
        if (validPoints.Count > 0)
        {
            System.Random rng = new System.Random();
            closestPoint = validPoints[rng.Next(validPoints.Count)];
        }
        else
        {
            Debug.LogError("No valid points found even after clearing walkRemember.");
            return Vector3.zero; // �� ��쿣 �⺻���� ��ȯ
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

    // �̵� �Ϸ� �� ��ġ�� �����ϴ� �޼���
    private void SavePosition(Vector2 position)
    {
        GameManager.instance.enemyPositions.Add(position);
    }

    public Vector2 GetDirectionToPlayer()
    {
        //Vector2 playerPosition = target.position;
        Vector2 playerPosition = GameManager.instance.Player.transform.position;
        Vector2 enemyPosition = transform.position;

        // �켱 �÷��̾���� �Ÿ��� ���
        float distanceToPlayer = Vector2.Distance(playerPosition, enemyPosition);

        if (distanceToPlayer <= 0.5f)
        {
            // �÷��̾ ��ó�� �ִ� ��� ������ �õ�
            return Vector2.zero; // �̵����� �ʰ� ����
        }

        float xDir = 0;
        float yDir = 0;

        if (Mathf.Abs(playerPosition.y - enemyPosition.y) < float.Epsilon)
        {
            // �÷��̾�� ���� y�࿡ ���� ��, x������ �̵�
            xDir = playerPosition.x > enemyPosition.x ? 1 : -1;
        }
        else if (Mathf.Abs(playerPosition.x - enemyPosition.x) < float.Epsilon)
        {
            // �÷��̾�� ���� x�࿡ ���� ��, y������ �̵�
            yDir = playerPosition.y > enemyPosition.y ? 1 : -1;
        }
        else
        {
            // �밢������ �̵�
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
            // ���� y�࿡ ���� ��, x������ �̵�
            xDir = closestPoint.x > enemyPosition.x ? 1 : -1;
        }
        else if (Mathf.Abs(closestPoint.x - enemyPosition.x) < float.Epsilon)
        {
            // ���� x�࿡ ���� ��, y������ �̵�
            yDir = closestPoint.y > enemyPosition.y ? 1 : -1;
        }
        else
        {
            // �밢������ �̵�
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
            if (Vector2.Distance(position, enemyPosition) < 0.1f) // �ݰ� 0.1f ���� �ٸ� ���� �ִ� ���
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

        int randomFactor = UnityEngine.Random.Range(-1, 3); // -1���� +2 ������ ���� ��
        if (UnityEngine.Random.value < 0.9f)
        {
            int baseDamage = Math.Max((PlayerPower - ThisDefense) + randomFactor, 1);
            
            if (UnityEngine.Random.value < 0.1f)
            {
                baseDamage = (int)Math.Round(baseDamage * 1.8);//1.8 ũ��Ƽ�� ������
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
            string itemName = gameObject.name; // ������ �̸� ���

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