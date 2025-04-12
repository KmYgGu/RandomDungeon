using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;

public abstract class MovingObject : MonoBehaviour
{
    public float moveTime = .1f;
    public LayerMask blockingLayer;
    public Tilemap tilemap;
    public List<string> impassableTileNames;
    public List<string> restrictedTileNames;

    public bool isSpecialState;

    private BoxCollider2D boxcollider;
    protected Rigidbody2D rb2D;
    protected float inverseMoveTime;

    public Sprite[] directionsprites;

    int HP;
    int Attack;
    int Defence;

    public Transform childTransform;
    public SpriteRenderer spriteRenderer;

    protected float totalMoveTime;

    // Start is called before the first frame update
    protected virtual void Start()//Start()
    {

        childTransform = transform.Find("Sprite");
        spriteRenderer = childTransform.GetComponent<SpriteRenderer>();

        tilemap = GameObject.Find("FloorMap").GetComponent<Tilemap>();
        boxcollider = GetComponent<BoxCollider2D>();
        rb2D = GetComponent<Rigidbody2D>();
        inverseMoveTime = 1f / moveTime;

        impassableTileNames = new List<string> { "S1Wall" }; // 필요에 따라 타일 이름 추가
        restrictedTileNames = new List<string> { "S1Water" }; // 새로운 리스트 초기화
        isSpecialState = false; // 기본적으로 특별한 상태가 아님
    }

    // 자식 오브젝트의 회전값과 방향 문자열을 담고 있는 딕셔너리
    protected Dictionary<Vector2, (float, int)> directionMap = new Dictionary<Vector2, (float, int)>
    {
        { new Vector2(0,0), (0f, 0) },//아무것도 없음
        { new Vector2(0, -1), (0f, 0) },        // 아래쪽
        { new Vector2(0, 1), (180f, 1) },         // 위쪽
        { new Vector2(-1, 0), (-90f, 2) },       // 왼쪽
        { new Vector2(1, 0), (90f, 3) },      // 오른쪽
        { new Vector2(-1, -1), (-45f, 4) }, // 왼쪽 아래 대각선
        { new Vector2(1, -1), (45f, 5) },// 오른쪽 아래 대각선
        { new Vector2(-1, 1), (-135f, 6) },   // 왼쪽 위 대각선
        { new Vector2(1, 1), (135f, 7) }   // 오른쪽 위 대각선
    };

    // Lookdirection에 따른 회전값과 방향 문자열을 반환하는 메서드
    protected (float, int) GetDirectionInfo(Vector2 lookDirection)
    {
        if (directionMap.TryGetValue(lookDirection, out var directionInfo))
        {
            return directionInfo;
        }

        return (0f, 0); // 기본값
    }
    protected Vector2 end;
    protected bool Move (Vector2 direction, out RaycastHit2D ObjectHit)//int xDir, int yDir
    {
        
        Vector2 start = transform.position;
        //Vector2 end = start + direction;
        end = start + direction;

        boxcollider.enabled = false;
        ObjectHit = Physics2D.Linecast(start, end, blockingLayer);
        boxcollider.enabled = true;

        if (Mathf.Abs(direction.x) > 0 && Mathf.Abs(direction.y) > 0)
        {
            if (!CanMoveDiagonally(start, direction))
            {
                return false;
            }
        }

        if (tilemap != null)
        {
            Vector3Int cellPosition = tilemap.WorldToCell(end);
            TileBase tile = tilemap.GetTile(cellPosition);
            if (tile != null)
            {
                string tileName = tile.name;
                if (impassableTileNames.Contains(tileName))
                {
                    return false;
                }

                // restrictedTileNames에 있는 타일은 특별한 상태가 아니면 이동 불가
                if (restrictedTileNames.Contains(tileName) && !isSpecialState)
                {
                    return false;
                }
                //canWaterTile();
            }
            
        }

        if (ObjectHit.transform == null)
        {
            StartCoroutine(SmoothMovement(end));
            return true;
        }

        return false;

    }

    /*bool canWaterTile()
    {
        Vector3Int cellPosition = tilemap.WorldToCell(end);
        TileBase tile = tilemap.GetTile(cellPosition);
        string tileName = tile.name;
        if (restrictedTileNames.Contains(tileName) && !isSpecialState)
        {
            return false;
        }
        return true;
    }*/

    protected bool CanMoveDiagonally(Vector2 start, Vector2 direction)
    {
        if (tilemap != null)
        {
            Vector2[] diagonalOffsets = new Vector2[]
            {
                new Vector2(direction.x, 0),
                new Vector2(0, direction.y)
            };

            foreach (Vector2 offset in diagonalOffsets)
            {
                Vector3Int cellPosition = tilemap.WorldToCell(start + offset);
                TileBase tile = tilemap.GetTile(cellPosition);
                if (tile != null && impassableTileNames.Contains(tile.name))
                {
                    return false;
                }
            }
        }
        return true;
    }

    //갈려고 하는 곳에 벽이 막혀있는지
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

    protected bool IsBlockedByWater(Vector3 end)
    {
        if (tilemap != null)
        {
            Vector3Int cellPosition = tilemap.WorldToCell(end);
            TileBase tile = tilemap.GetTile(cellPosition);
            if (tile != null)
            {
                string tileName = tile.name;
                if (restrictedTileNames.Contains(tileName))
                {
                    return true;
                }
            }
        }
        return false;
    }

    //딱딱하게 이동하는 것이 아닌 실시간 게임처럼 부드럽게 이동하는 것처럼 보여주는 연출인데..
    protected IEnumerator SmoothMovement (Vector3 end)
    {
        /*float sqrRemaingDistance = (transform.position - end).sqrMagnitude;

        while (sqrRemaingDistance > float.Epsilon)
        {
            Vector3 newPosition = Vector3.MoveTowards(rb2D.position, end, inverseMoveTime * Time.deltaTime * 5);
            rb2D.MovePosition(newPosition);
            sqrRemaingDistance = (transform.position - end).sqrMagnitude;
            yield return null;
        }

        rb2D.MovePosition(end);
        yield return new WaitForFixedUpdate();*/
        rb2D.MovePosition(end);


        yield return null;
    }
    public bool Delay;

    //공격하는 시각 효과
    protected IEnumerator AttackMovement(Vector3 startPosition, Vector3 attackPosition, Transform childTransform)
    {
        // 전체 시작 시간 기록
        float startTime = Time.time;

        // 기존 Sorting Layer 저장
        string originalSortingLayer = spriteRenderer.sortingLayerName;
        
        // Sorting Layer를 "Attack"으로 변경
        spriteRenderer.sortingLayerName = "attack";

        float sqrRemainingDistance = (childTransform.position - attackPosition).sqrMagnitude;

        // 자식 오브젝트가 플레이어 위치로 빠르게 이동
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
        totalMoveTime = Time.time - startTime;

        spriteRenderer.sortingLayerName = originalSortingLayer;

        yield return new WaitForFixedUpdate();
        //yield return new WaitForSeconds(0.5f);
    }

    protected bool canMove;
    //이동하기 전에 갈 경로에 무언가 있는지 탐지
    protected virtual void AttemptMove <T> (Vector2 direction)
        where T : Component
    {
        RaycastHit2D hit;
        //bool canMove = Move(direction, out hit);
        canMove = Move(direction, out hit);

        if (hit.transform == null)
            return;

        T hitComponent = hit.transform.GetComponent<T>();

        if (!canMove && hitComponent != null)
        {
            //onCantMove(hitComponent);//
        }
    }


    public virtual void SetStats(int SetHp, int SetAttack, int SetDefense)
    {
        HP = SetHp;
        Attack = SetAttack;
        Defence = SetDefense;
    }

    //움직이지 못할 경우, 해당 컴포넌트(무슨 코드의 게임오브젝트)인지 알아냄
    protected abstract void onCantMove<T>(T componet)
        where T : Component;

}
