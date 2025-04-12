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

        impassableTileNames = new List<string> { "S1Wall" }; // �ʿ信 ���� Ÿ�� �̸� �߰�
        restrictedTileNames = new List<string> { "S1Water" }; // ���ο� ����Ʈ �ʱ�ȭ
        isSpecialState = false; // �⺻������ Ư���� ���°� �ƴ�
    }

    // �ڽ� ������Ʈ�� ȸ������ ���� ���ڿ��� ��� �ִ� ��ųʸ�
    protected Dictionary<Vector2, (float, int)> directionMap = new Dictionary<Vector2, (float, int)>
    {
        { new Vector2(0,0), (0f, 0) },//�ƹ��͵� ����
        { new Vector2(0, -1), (0f, 0) },        // �Ʒ���
        { new Vector2(0, 1), (180f, 1) },         // ����
        { new Vector2(-1, 0), (-90f, 2) },       // ����
        { new Vector2(1, 0), (90f, 3) },      // ������
        { new Vector2(-1, -1), (-45f, 4) }, // ���� �Ʒ� �밢��
        { new Vector2(1, -1), (45f, 5) },// ������ �Ʒ� �밢��
        { new Vector2(-1, 1), (-135f, 6) },   // ���� �� �밢��
        { new Vector2(1, 1), (135f, 7) }   // ������ �� �밢��
    };

    // Lookdirection�� ���� ȸ������ ���� ���ڿ��� ��ȯ�ϴ� �޼���
    protected (float, int) GetDirectionInfo(Vector2 lookDirection)
    {
        if (directionMap.TryGetValue(lookDirection, out var directionInfo))
        {
            return directionInfo;
        }

        return (0f, 0); // �⺻��
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

                // restrictedTileNames�� �ִ� Ÿ���� Ư���� ���°� �ƴϸ� �̵� �Ұ�
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

    //������ �ϴ� ���� ���� �����ִ���
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

    //�����ϰ� �̵��ϴ� ���� �ƴ� �ǽð� ����ó�� �ε巴�� �̵��ϴ� ��ó�� �����ִ� �����ε�..
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

    //�����ϴ� �ð� ȿ��
    protected IEnumerator AttackMovement(Vector3 startPosition, Vector3 attackPosition, Transform childTransform)
    {
        // ��ü ���� �ð� ���
        float startTime = Time.time;

        // ���� Sorting Layer ����
        string originalSortingLayer = spriteRenderer.sortingLayerName;
        
        // Sorting Layer�� "Attack"���� ����
        spriteRenderer.sortingLayerName = "attack";

        float sqrRemainingDistance = (childTransform.position - attackPosition).sqrMagnitude;

        // �ڽ� ������Ʈ�� �÷��̾� ��ġ�� ������ �̵�
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
        totalMoveTime = Time.time - startTime;

        spriteRenderer.sortingLayerName = originalSortingLayer;

        yield return new WaitForFixedUpdate();
        //yield return new WaitForSeconds(0.5f);
    }

    protected bool canMove;
    //�̵��ϱ� ���� �� ��ο� ���� �ִ��� Ž��
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

    //�������� ���� ���, �ش� ������Ʈ(���� �ڵ��� ���ӿ�����Ʈ)���� �˾Ƴ�
    protected abstract void onCantMove<T>(T componet)
        where T : Component;

}
