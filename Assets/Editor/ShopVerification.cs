using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>씬 저장 없이 실제 프리팹으로 상점 배치를 확인하는 배치 검사.</summary>
public static class ShopVerification
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("배치 전용 검사입니다.");
        try
        {
            int shopsSeen=0;
            for(int seed=0;seed<8;seed++)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                var game=UnityEngine.Object.FindObjectOfType<GameManager>(); GameManager.instance=game;
                GameManager.ResetRun();
                typeof(GameManager).GetProperty("Stage").SetValue(null,8);
                var generator=UnityEngine.Object.FindObjectOfType<RandomDung>();
                generator.minRoomSize=generator.maxRoomSize=6;
                generator.shopChance=1f; generator.useFixedSeed=true; generator.fixedSeed=seed;
                generator.GenerateDungeon();
                var layout=(DungeonLayout)typeof(RandomDung).GetField("layout",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(generator);
                int count=0;
                foreach(var room in layout.Rooms)
                    if(room.Kind==RoomKind.Shop)
                    { count++; Require(room.Bounds.width==room.Bounds.height && room.Doors.Count==1,"상점 구조 조건 위반"); }
                Require(count<=1,"한 층 상점 중복");
                foreach(var shop in UnityEngine.Object.FindObjectsOfType<ShopRoom>())
                {
                    shopsSeen++;
                    Require(shop.Merchant!=null && !shop.Merchant.Actor.IsCombatTarget,"상인 중립/연결 실패");
                    Require(shop.Merchant.Actor.ThisHp==9999,"상인 체력 적용 실패");
                    var cells=new HashSet<Vector3Int>();
                    foreach(var good in UnityEngine.Object.FindObjectsOfType<ShopGood>())
                        if(good.Owner==shop)
                        {
                            var cell=generator.floorTilemap.WorldToCell(good.transform.position);
                            Require(cells.Add(cell) && good.ForSale,"상품 중복/소유권 오류");
                            Require(shop.Interior.Contains(new Vector2Int(cell.x,cell.y)),"상점 바깥 상품 배치");
                        }
                    Require(cells.Count>=4,"상점 최소 상품 수 미달");
                    foreach(var enemy in UnityEngine.Object.FindObjectsOfType<Enemy>())
                    {
                        var cell=generator.floorTilemap.WorldToCell(enemy.transform.position);
                        Require(!enemy.IsCombatTarget || !shop.Interior.Contains(new Vector2Int(cell.x,cell.y)),"상점 내부 적 생성");
                    }
                }
            }
            Require(shopsSeen>0,"검사 시드에서 상점 미생성");
            Debug.Log("SHOP_VERIFICATION_PASS: geometry, population, ownership, merchant");
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Require(bool condition,string message)
    { if(!condition) throw new InvalidOperationException(message); }
}
