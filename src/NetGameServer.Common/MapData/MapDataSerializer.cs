using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetGameServer.Common.MapData;

/// <summary>
/// 맵 데이터 직렬화/역직렬화
/// </summary>
public static class MapDataSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    /// <summary>
    /// 맵 데이터를 JSON으로 직렬화
    /// </summary>
    public static string SerializeToJson(MapData mapData)
    {
        return JsonSerializer.Serialize(mapData, JsonOptions);
    }
    
    /// <summary>
    /// JSON에서 맵 데이터 역직렬화
    /// </summary>
    public static MapData? DeserializeFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MapData>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 파일에서 맵 데이터 로드
    /// </summary>
    public static MapData? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;
            
        try
        {
            var json = File.ReadAllText(filePath);
            return DeserializeFromJson(json);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 맵 데이터를 파일로 저장
    /// </summary>
    public static bool SaveToFile(MapData mapData, string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = SerializeToJson(mapData);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

