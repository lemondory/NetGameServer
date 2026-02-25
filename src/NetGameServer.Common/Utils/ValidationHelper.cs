using System.Text.RegularExpressions;

namespace NetGameServer.Common.Utils;

/// <summary>
/// 입력 검증 유틸리티
/// </summary>
public static class ValidationHelper
{
    // 이메일 정규식
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // 사용자명 정규식 (영문자, 숫자, 언더스코어, 하이픈, 3-20자)
    private static readonly Regex UsernameRegex = new(
        @"^[a-zA-Z0-9_-]{3,20}$",
        RegexOptions.Compiled);
    
    // 비밀번호 정규식 (최소 8자, 영문자, 숫자 포함)
    private static readonly Regex PasswordRegex = new(
        @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$",
        RegexOptions.Compiled);
    
    /// <summary>
    /// 이메일 형식 검증
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        
        return EmailRegex.IsMatch(email);
    }
    
    /// <summary>
    /// 사용자명 검증
    /// </summary>
    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;
        
        return UsernameRegex.IsMatch(username);
    }
    
    /// <summary>
    /// 비밀번호 강도 검증
    /// </summary>
    public static bool IsValidPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;
        
        return PasswordRegex.IsMatch(password);
    }
    
    /// <summary>
    /// 비밀번호 강도 검증 (상세)
    /// </summary>
    public static PasswordStrength ValidatePasswordStrength(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new PasswordStrength { IsValid = false, Score = 0, Message = "비밀번호가 비어있습니다." };
        
        int score = 0;
        var messages = new List<string>();
        
        // 길이 검증
        if (password.Length >= 8)
            score += 1;
        else
            messages.Add("비밀번호는 최소 8자 이상이어야 합니다.");
        
        if (password.Length >= 12)
            score += 1;
        
        // 영문자 포함
        if (Regex.IsMatch(password, @"[a-z]"))
            score += 1;
        else
            messages.Add("소문자를 포함해야 합니다.");
        
        if (Regex.IsMatch(password, @"[A-Z]"))
            score += 1;
        else
            messages.Add("대문자를 포함해야 합니다.");
        
        // 숫자 포함
        if (Regex.IsMatch(password, @"\d"))
            score += 1;
        else
            messages.Add("숫자를 포함해야 합니다.");
        
        // 특수문자 포함
        if (Regex.IsMatch(password, @"[!@#$%^&*(),.?\"":{}|<>]"))
            score += 1;
        else
            messages.Add("특수문자를 포함하는 것을 권장합니다.");
        
        var strength = score switch
        {
            <= 2 => "약함",
            <= 4 => "보통",
            <= 5 => "강함",
            _ => "매우 강함"
        };
        
        return new PasswordStrength
        {
            IsValid = score >= 3,
            Score = score,
            Strength = strength,
            Message = messages.Count > 0 ? string.Join(" ", messages) : "비밀번호가 안전합니다."
        };
    }
    
    /// <summary>
    /// 문자열 길이 검증
    /// </summary>
    public static bool IsValidLength(string? value, int minLength, int maxLength)
    {
        if (value == null)
            return minLength == 0;
        
        return value.Length >= minLength && value.Length <= maxLength;
    }
    
    /// <summary>
    /// 숫자 검증
    /// </summary>
    public static bool IsNumeric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        
        return long.TryParse(value, out _);
    }
    
    /// <summary>
    /// 정수 범위 검증
    /// </summary>
    public static bool IsInRange(int value, int min, int max)
    {
        return value >= min && value <= max;
    }
    
    /// <summary>
    /// IP 주소 검증
    /// </summary>
    public static bool IsValidIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;
        
        return System.Net.IPAddress.TryParse(ipAddress, out _);
    }
    
    /// <summary>
    /// URL 검증
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
    
    /// <summary>
    /// SQL Injection 방지 (기본 검증)
    /// </summary>
    public static bool ContainsSqlInjection(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "EXEC", "EXECUTE", "UNION", "SCRIPT" };
        var upperInput = input.ToUpperInvariant();
        
        return sqlKeywords.Any(keyword => upperInput.Contains(keyword));
    }
    
    /// <summary>
    /// XSS 방지 (기본 검증)
    /// </summary>
    public static bool ContainsXss(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var xssPatterns = new[] { "<script", "javascript:", "onerror=", "onload=", "onclick=", "<iframe" };
        var lowerInput = input.ToLowerInvariant();
        
        return xssPatterns.Any(pattern => lowerInput.Contains(pattern));
    }
}

/// <summary>
/// 비밀번호 강도 정보
/// </summary>
public class PasswordStrength
{
    public bool IsValid { get; set; }
    public int Score { get; set; }
    public string Strength { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
