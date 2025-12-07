// Unity 에디터 스크립트 예시
// Assets/Editor/MapDataExporter.cs 에 배치하세요

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Unity에서 맵 데이터를 내보내는 에디터 스크립트
/// </summary>
public class MapDataExporter : EditorWindow
{
    private GameObject mapRoot;
    private uint mapId = 1;
    private string mapName = "시작의 마을";
    private float mapWidth = 100f;
    private float mapHeight = 100f;
    private float mapDepth = 100f;
    
    [MenuItem("Tools/Map Data Exporter")]
    public static void ShowWindow()
    {
        GetWindow<MapDataExporter>("Map Data Exporter");
    }
    
    void OnGUI()
    {
        GUILayout.Label("맵 데이터 내보내기", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        mapRoot = EditorGUILayout.ObjectField("맵 루트 오브젝트", mapRoot, typeof(GameObject), true) as GameObject;
        mapId = (uint)EditorGUILayout.IntField("맵 ID", (int)mapId);
        mapName = EditorGUILayout.TextField("맵 이름", mapName);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("맵 크기");
        mapWidth = EditorGUILayout.FloatField("Width", mapWidth);
        mapHeight = EditorGUILayout.FloatField("Height", mapHeight);
        mapDepth = EditorGUILayout.FloatField("Depth", mapDepth);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("맵 데이터 내보내기"))
        {
            ExportMapData();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "맵 루트 오브젝트의 자식 오브젝트들을 스캔합니다:\n" +
            "- 'SpawnPoint' 태그: 스폰 포인트\n" +
            "- 'Monster' 태그: 몬스터 스폰\n" +
            "- 'StaticObject' 태그: 정적 오브젝트\n" +
            "- 'Obstacle' 태그: 장애물",
            MessageType.Info
        );
    }
    
    void ExportMapData()
    {
        if (mapRoot == null)
        {
            EditorUtility.DisplayDialog("오류", "맵 루트 오브젝트를 선택해주세요.", "확인");
            return;
        }
        
        // 맵 데이터 구조 생성
        var mapData = new MapData
        {
            MapId = mapId,
            MapName = mapName,
            Width = mapWidth,
            Height = mapHeight,
            Depth = mapDepth
        };
        
        // 모든 자식 오브젝트 스캔
        ScanChildren(mapRoot.transform, mapData);
        
        // JSON으로 직렬화
        var json = JsonUtility.ToJson(mapData, true);
        
        // 파일 저장
        var path = EditorUtility.SaveFilePanel("맵 데이터 저장", "Assets", "map_" + mapId.ToString("D3"), "json");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("완료", $"맵 데이터가 저장되었습니다:\n{path}", "확인");
            AssetDatabase.Refresh();
        }
    }
    
    void ScanChildren(Transform parent, MapData mapData)
    {
        foreach (Transform child in parent)
        {
            var go = child.gameObject;
            var pos = go.transform.position;
            
            // 태그 기반으로 분류
            if (go.CompareTag("SpawnPoint"))
            {
                var spawnPoint = new SpawnPoint
                {
                    Id = go.name,
                    X = pos.x,
                    Y = pos.y,
                    Z = pos.z,
                    Type = GetSpawnPointType(go),
                    Tag = GetTagFromComponent(go)
                };
                mapData.SpawnPoints.Add(spawnPoint);
            }
            else if (go.CompareTag("Monster"))
            {
                var monsterSpawn = new MonsterSpawn
                {
                    MonsterType = GetMonsterType(go),
                    X = pos.x,
                    Y = pos.y,
                    Z = pos.z,
                    Count = GetSpawnCount(go),
                    RespawnTime = GetRespawnTime(go),
                    SpawnRadius = GetSpawnRadius(go),
                    Level = GetLevel(go),
                    Hp = GetHp(go),
                    MoveSpeed = GetMoveSpeed(go),
                    DetectRange = GetDetectRange(go),
                    AttackRange = GetAttackRange(go),
                    CanPatrol = GetCanPatrol(go),
                    PatrolRadius = GetPatrolRadius(go)
                };
                mapData.MonsterSpawns.Add(monsterSpawn);
            }
            else if (go.CompareTag("StaticObject"))
            {
                var staticObj = new StaticObject
                {
                    Id = go.name,
                    ObjectType = GetObjectType(go),
                    X = pos.x,
                    Y = pos.y,
                    Z = pos.z,
                    RotationX = go.transform.rotation.eulerAngles.x,
                    RotationY = go.transform.rotation.eulerAngles.y,
                    RotationZ = go.transform.rotation.eulerAngles.z,
                    ScaleX = go.transform.localScale.x,
                    ScaleY = go.transform.localScale.y,
                    ScaleZ = go.transform.localScale.z,
                    Properties = GetProperties(go)
                };
                mapData.StaticObjects.Add(staticObj);
            }
            else if (go.CompareTag("Obstacle"))
            {
                var obstacle = new Obstacle
                {
                    Id = go.name,
                    Type = GetObstacleType(go),
                    X = pos.x,
                    Y = pos.y,
                    Z = pos.z,
                    Width = GetWidth(go),
                    Height = GetHeight(go),
                    Depth = GetDepth(go),
                    RotationX = go.transform.rotation.eulerAngles.x,
                    RotationY = go.transform.rotation.eulerAngles.y,
                    RotationZ = go.transform.rotation.eulerAngles.z
                };
                mapData.Obstacles.Add(obstacle);
            }
            
            // 재귀적으로 자식도 스캔
            if (child.childCount > 0)
            {
                ScanChildren(child, mapData);
            }
        }
    }
    
    // 헬퍼 메서드들 (컴포넌트에서 정보 추출)
    SpawnPointType GetSpawnPointType(GameObject go)
    {
        // SpawnPointType 컴포넌트가 있으면 사용, 없으면 이름으로 판단
        var component = go.GetComponent<SpawnPointInfo>();
        if (component != null) return component.Type;
        
        var name = go.name.ToLower();
        if (name.Contains("player")) return SpawnPointType.Player;
        if (name.Contains("monster")) return SpawnPointType.Monster;
        if (name.Contains("npc")) return SpawnPointType.NPC;
        if (name.Contains("portal")) return SpawnPointType.Portal;
        return SpawnPointType.Other;
    }
    
    string GetTagFromComponent(GameObject go)
    {
        var component = go.GetComponent<SpawnPointInfo>();
        return component?.Tag ?? "";
    }
    
    string GetMonsterType(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        if (component != null) return component.MonsterType;
        return go.name; // 이름을 몬스터 타입으로 사용
    }
    
    int GetSpawnCount(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.Count ?? 1;
    }
    
    float GetRespawnTime(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.RespawnTime ?? 30f;
    }
    
    float GetSpawnRadius(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.SpawnRadius ?? 0f;
    }
    
    int? GetLevel(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.Level;
    }
    
    int? GetHp(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.Hp;
    }
    
    float? GetMoveSpeed(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.MoveSpeed;
    }
    
    float? GetDetectRange(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.DetectRange;
    }
    
    float? GetAttackRange(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.AttackRange;
    }
    
    bool GetCanPatrol(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.CanPatrol ?? true;
    }
    
    float GetPatrolRadius(GameObject go)
    {
        var component = go.GetComponent<MonsterSpawnInfo>();
        return component?.PatrolRadius ?? 10f;
    }
    
    string GetObjectType(GameObject go)
    {
        var component = go.GetComponent<StaticObjectInfo>();
        if (component != null) return component.ObjectType;
        return go.name;
    }
    
    Dictionary<string, string> GetProperties(GameObject go)
    {
        var component = go.GetComponent<StaticObjectInfo>();
        return component?.Properties;
    }
    
    ObstacleType GetObstacleType(GameObject go)
    {
        var component = go.GetComponent<ObstacleInfo>();
        if (component != null) return component.Type;
        
        var name = go.name.ToLower();
        if (name.Contains("wall")) return ObstacleType.Wall;
        if (name.Contains("box")) return ObstacleType.Box;
        if (name.Contains("rock")) return ObstacleType.Rock;
        if (name.Contains("tree")) return ObstacleType.Tree;
        if (name.Contains("building")) return ObstacleType.Building;
        return ObstacleType.Other;
    }
    
    float GetWidth(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.size.x;
        return 1f;
    }
    
    float GetHeight(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.size.y;
        return 1f;
    }
    
    float GetDepth(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.size.z;
        return 1f;
    }
}

// Unity 컴포넌트 예시 (선택 사항 - 더 정확한 데이터를 위해 사용)

/// <summary>
/// 스폰 포인트 정보 컴포넌트
/// </summary>
public class SpawnPointInfo : MonoBehaviour
{
    public SpawnPointType Type = SpawnPointType.Player;
    public string Tag = "";
}

/// <summary>
/// 몬스터 스폰 정보 컴포넌트
/// </summary>
public class MonsterSpawnInfo : MonoBehaviour
{
    public string MonsterType = "Goblin";
    public int Count = 1;
    public float RespawnTime = 30f;
    public float SpawnRadius = 0f;
    public int? Level;
    public int? Hp;
    public float? MoveSpeed;
    public float? DetectRange;
    public float? AttackRange;
    public bool CanPatrol = true;
    public float PatrolRadius = 10f;
}

/// <summary>
/// 정적 오브젝트 정보 컴포넌트
/// </summary>
public class StaticObjectInfo : MonoBehaviour
{
    public string ObjectType = "NPC";
    public Dictionary<string, string> Properties = new();
}

/// <summary>
/// 장애물 정보 컴포넌트
/// </summary>
public class ObstacleInfo : MonoBehaviour
{
    public ObstacleType Type = ObstacleType.Wall;
}

// 맵 데이터 구조 (Common 프로젝트와 동일한 구조)
// 실제로는 Common 프로젝트의 DLL을 참조하거나, 동일한 구조를 복사해야 합니다.

[System.Serializable]
public class MapData
{
    public uint MapId;
    public string MapName;
    public float Width;
    public float Height;
    public float Depth;
    public List<SpawnPoint> SpawnPoints = new();
    public List<MonsterSpawn> MonsterSpawns = new();
    public List<StaticObject> StaticObjects = new();
    public List<Obstacle> Obstacles = new();
}

[System.Serializable]
public class SpawnPoint
{
    public string Id;
    public float X, Y, Z;
    public SpawnPointType Type;
    public string Tag;
}

[System.Serializable]
public class MonsterSpawn
{
    public string MonsterType;
    public float X, Y, Z;
    public int Count;
    public float RespawnTime;
    public float SpawnRadius;
    public int? Level;
    public int? Hp;
    public float? MoveSpeed;
    public float? DetectRange;
    public float? AttackRange;
    public bool CanPatrol;
    public float PatrolRadius;
}

[System.Serializable]
public class StaticObject
{
    public string Id;
    public string ObjectType;
    public float X, Y, Z;
    public float? RotationX, RotationY, RotationZ;
    public float? ScaleX, ScaleY, ScaleZ;
    public Dictionary<string, string> Properties;
}

[System.Serializable]
public class Obstacle
{
    public string Id;
    public ObstacleType Type;
    public float X, Y, Z;
    public float Width, Height, Depth;
    public float? RotationX, RotationY, RotationZ;
}

public enum SpawnPointType
{
    Player, Monster, NPC, Portal, Item, Other
}

public enum ObstacleType
{
    Wall, Box, Rock, Tree, Building, Other
}

#endif

