using System.Security.Cryptography;

namespace NetGameServer.Common.Utils;

/// <summary>
/// Thread-safe 랜덤 생성 및 키 생성 유틸리티
/// </summary>
public static class RandomHelper
{
    private static readonly ThreadLocal<Random> _random = new(() => new Random(Guid.NewGuid().GetHashCode()));
    
    /// <summary>
    /// Thread-safe 랜덤 인스턴스 가져오기
    /// </summary>
    public static Random GetRandom() => _random.Value!;
    
    /// <summary>
    /// 지정된 범위 내의 랜덤 정수 생성
    /// </summary>
    public static int Next(int minValue, int maxValue)
    {
        return GetRandom().Next(minValue, maxValue);
    }
    
    /// <summary>
    /// 0부터 maxValue 미만의 랜덤 정수 생성
    /// </summary>
    public static int Next(int maxValue)
    {
        return GetRandom().Next(maxValue);
    }
    
    /// <summary>
    /// 랜덤 정수 생성
    /// </summary>
    public static int Next()
    {
        return GetRandom().Next();
    }
    
    /// <summary>
    /// 랜덤 실수 생성 (0.0 ~ 1.0)
    /// </summary>
    public static double NextDouble()
    {
        return GetRandom().NextDouble();
    }
    
    /// <summary>
    /// 랜덤 float 생성 (0.0f ~ 1.0f)
    /// </summary>
    public static float NextFloat()
    {
        return (float)GetRandom().NextDouble();
    }
    
    /// <summary>
    /// 지정된 범위 내의 랜덤 float 생성
    /// </summary>
    public static float NextFloat(float minValue, float maxValue)
    {
        return minValue + (maxValue - minValue) * NextFloat();
    }
    
    /// <summary>
    /// 랜덤 바이트 배열 생성
    /// </summary>
    public static byte[] NextBytes(int length)
    {
        var bytes = new byte[length];
        GetRandom().NextBytes(bytes);
        return bytes;
    }
    
    /// <summary>
    /// 암호학적으로 안전한 랜덤 바이트 배열 생성
    /// </summary>
    public static byte[] NextSecureBytes(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
    
    /// <summary>
    /// 랜덤 문자열 생성 (영문자, 숫자)
    /// </summary>
    public static string GenerateString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Next(s.Length)]).ToArray());
    }
    
    /// <summary>
    /// 랜덤 숫자 문자열 생성
    /// </summary>
    public static string GenerateNumericString(int length)
    {
        const string chars = "0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Next(s.Length)]).ToArray());
    }
    
    /// <summary>
    /// 랜덤 토큰 생성 (Base64 URL-safe)
    /// </summary>
    public static string GenerateToken(int length = 32)
    {
        var bytes = NextSecureBytes(length);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
    
    /// <summary>
    /// 랜덤 GUID 생성
    /// </summary>
    public static string GenerateGuid()
    {
        return Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// 리스트에서 랜덤 요소 선택
    /// </summary>
    public static T? RandomElement<T>(IList<T> list)
    {
        if (list == null || list.Count == 0)
            return default;
        
        return list[Next(list.Count)];
    }
    
    /// <summary>
    /// 리스트에서 랜덤 요소 여러 개 선택 (중복 없음)
    /// </summary>
    public static List<T> RandomElements<T>(IList<T> list, int count)
    {
        if (list == null || list.Count == 0)
            return new List<T>();
        
        var shuffled = list.OrderBy(_ => Next()).ToList();
        return shuffled.Take(Math.Min(count, shuffled.Count)).ToList();
    }
    
    /// <summary>
    /// 리스트 셔플 (Fisher-Yates 알고리즘)
    /// </summary>
    public static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    /// <summary>
    /// 가중치 기반 랜덤 선택
    /// </summary>
    /// <param name="weights">각 항목의 가중치</param>
    /// <returns>선택된 인덱스</returns>
    public static int WeightedRandom(IList<int> weights)
    {
        if (weights == null || weights.Count == 0)
            return -1;
        
        int totalWeight = weights.Sum();
        if (totalWeight <= 0)
            return -1;
        
        int randomValue = Next(totalWeight);
        int currentWeight = 0;
        
        for (int i = 0; i < weights.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue < currentWeight)
                return i;
        }
        
        return weights.Count - 1;
    }
    
    /// <summary>
    /// 가중치 기반 랜덤 선택 (float 가중치)
    /// </summary>
    public static int WeightedRandom(IList<float> weights)
    {
        if (weights == null || weights.Count == 0)
            return -1;
        
        float totalWeight = weights.Sum();
        if (totalWeight <= 0)
            return -1;
        
        float randomValue = NextFloat() * totalWeight;
        float currentWeight = 0;
        
        for (int i = 0; i < weights.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue < currentWeight)
                return i;
        }
        
        return weights.Count - 1;
    }
}
