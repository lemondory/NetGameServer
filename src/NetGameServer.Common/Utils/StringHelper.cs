using System.Text;
using System.Text.RegularExpressions;

namespace NetGameServer.Common.Utils;

/// <summary>
/// 문자열 유틸리티
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// 문자열이 null이거나 비어있는지 확인
    /// </summary>
    public static bool IsNullOrEmpty(string? value)
    {
        return string.IsNullOrEmpty(value);
    }
    
    /// <summary>
    /// 문자열이 null이거나 공백인지 확인
    /// </summary>
    public static bool IsNullOrWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
    
    /// <summary>
    /// 문자열을 안전하게 자르기 (null 체크 포함)
    /// </summary>
    public static string SafeSubstring(string? value, int startIndex, int length)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (startIndex < 0)
            startIndex = 0;
        
        if (startIndex >= value.Length)
            return string.Empty;
        
        if (startIndex + length > value.Length)
            length = value.Length - startIndex;
        
        return value.Substring(startIndex, length);
    }
    
    /// <summary>
    /// 문자열을 지정된 길이로 자르기 (초과 시 ... 추가)
    /// </summary>
    public static string Truncate(string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (value.Length <= maxLength)
            return value;
        
        return value.Substring(0, maxLength - suffix.Length) + suffix;
    }
    
    /// <summary>
    /// 문자열에서 HTML 태그 제거
    /// </summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        
        return Regex.Replace(html, "<.*?>", string.Empty);
    }
    
    /// <summary>
    /// 문자열에서 특수문자 제거
    /// </summary>
    public static string RemoveSpecialCharacters(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return Regex.Replace(value, @"[^a-zA-Z0-9가-힣\s]", string.Empty);
    }
    
    /// <summary>
    /// 문자열에서 공백 제거
    /// </summary>
    public static string RemoveWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return Regex.Replace(value, @"\s+", string.Empty);
    }
    
    /// <summary>
    /// 문자열을 카멜케이스로 변환
    /// </summary>
    public static string ToCamelCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (value.Length == 1)
            return value.ToLowerInvariant();
        
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
    
    /// <summary>
    /// 문자열을 파스칼케이스로 변환
    /// </summary>
    public static string ToPascalCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (value.Length == 1)
            return value.ToUpperInvariant();
        
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
    
    /// <summary>
    /// 문자열을 스네이크케이스로 변환
    /// </summary>
    public static string ToSnakeCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return Regex.Replace(value, @"([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
    }
    
    /// <summary>
    /// 문자열을 케밥케이스로 변환
    /// </summary>
    public static string ToKebabCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return Regex.Replace(value, @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }
    
    /// <summary>
    /// 문자열을 바이트 배열로 변환 (UTF-8)
    /// </summary>
    public static byte[] ToBytes(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return Array.Empty<byte>();
        
        return Encoding.UTF8.GetBytes(value);
    }
    
    /// <summary>
    /// 바이트 배열을 문자열로 변환 (UTF-8)
    /// </summary>
    public static string FromBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;
        
        return Encoding.UTF8.GetString(bytes);
    }
    
    /// <summary>
    /// 문자열을 16진수로 변환
    /// </summary>
    public static string ToHex(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    
    /// <summary>
    /// 16진수 문자열을 원본 문자열로 변환
    /// </summary>
    public static string FromHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return string.Empty;
        
        try
        {
            var bytes = Convert.FromHexString(hex);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// 문자열을 반복
    /// </summary>
    public static string Repeat(string? value, int count)
    {
        if (string.IsNullOrEmpty(value) || count <= 0)
            return string.Empty;
        
        return string.Concat(Enumerable.Repeat(value, count));
    }
    
    /// <summary>
    /// 문자열을 지정된 구분자로 분할
    /// </summary>
    public static string[] Split(string? value, string separator, StringSplitOptions options = StringSplitOptions.None)
    {
        if (string.IsNullOrEmpty(value))
            return Array.Empty<string>();
        
        return value.Split(separator, options);
    }
    
    /// <summary>
    /// 문자열 배열을 지정된 구분자로 결합
    /// </summary>
    public static string Join(string separator, IEnumerable<string>? values)
    {
        if (values == null)
            return string.Empty;
        
        return string.Join(separator, values);
    }
    
    /// <summary>
    /// 문자열에서 숫자만 추출
    /// </summary>
    public static string ExtractNumbers(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return Regex.Replace(value, @"[^\d]", string.Empty);
    }
    
    /// <summary>
    /// 문자열에서 문자만 추출
    /// </summary>
    public static string ExtractLetters(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return Regex.Replace(value, @"[^a-zA-Z가-힣]", string.Empty);
    }
    
    /// <summary>
    /// 문자열을 마스킹 처리 (예: "test@example.com" -> "t***@e***.com")
    /// </summary>
    public static string Mask(string? value, int visibleStart = 1, int visibleEnd = 1, char maskChar = '*')
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (value.Length <= visibleStart + visibleEnd)
            return new string(maskChar, value.Length);
        
        var start = value.Substring(0, visibleStart);
        var end = value.Substring(value.Length - visibleEnd);
        var masked = new string(maskChar, value.Length - visibleStart - visibleEnd);
        
        return start + masked + end;
    }
    
    /// <summary>
    /// 이메일 마스킹 (예: "test@example.com" -> "t***@example.com")
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return string.Empty;
        
        var parts = email.Split('@');
        if (parts.Length != 2)
            return Mask(email);
        
        return Mask(parts[0], 1, 0) + "@" + parts[1];
    }
}
