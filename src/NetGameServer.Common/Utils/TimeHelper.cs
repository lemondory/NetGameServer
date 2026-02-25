namespace NetGameServer.Common.Utils;

/// <summary>
/// 시간/날짜 유틸리티
/// </summary>
public static class TimeHelper
{
    /// <summary>
    /// 현재 UTC 시간
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;
    
    /// <summary>
    /// 현재 로컬 시간
    /// </summary>
    public static DateTime Now => DateTime.Now;
    
    /// <summary>
    /// Unix 타임스탬프 (초)
    /// </summary>
    public static long UnixTimestampSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    /// <summary>
    /// Unix 타임스탬프 (밀리초)
    /// </summary>
    public static long UnixTimestampMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    /// <summary>
    /// DateTime을 Unix 타임스탬프(초)로 변환
    /// </summary>
    public static long ToUnixTimestampSeconds(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }
    
    /// <summary>
    /// DateTime을 Unix 타임스탬프(밀리초)로 변환
    /// </summary>
    public static long ToUnixTimestampMilliseconds(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
    }
    
    /// <summary>
    /// Unix 타임스탬프(초)를 DateTime으로 변환
    /// </summary>
    public static DateTime FromUnixTimestampSeconds(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
    }
    
    /// <summary>
    /// Unix 타임스탬프(밀리초)를 DateTime으로 변환
    /// </summary>
    public static DateTime FromUnixTimestampMilliseconds(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
    }
    
    /// <summary>
    /// 시간 경과 여부 확인
    /// </summary>
    public static bool HasElapsed(DateTime startTime, TimeSpan duration)
    {
        return UtcNow - startTime >= duration;
    }
    
    /// <summary>
    /// 시간 경과 여부 확인 (밀리초)
    /// </summary>
    public static bool HasElapsed(DateTime startTime, long milliseconds)
    {
        return HasElapsed(startTime, TimeSpan.FromMilliseconds(milliseconds));
    }
    
    /// <summary>
    /// 남은 시간 계산
    /// </summary>
    public static TimeSpan GetRemainingTime(DateTime startTime, TimeSpan duration)
    {
        var elapsed = UtcNow - startTime;
        var remaining = duration - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
    
    /// <summary>
    /// 시간 포맷팅 (예: "1시간 30분 전")
    /// </summary>
    public static string FormatTimeAgo(DateTime dateTime)
    {
        var timeSpan = UtcNow - dateTime;
        
        if (timeSpan.TotalSeconds < 60)
            return "방금 전";
        
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}분 전";
        
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}시간 전";
        
        if (timeSpan.TotalDays < 30)
            return $"{(int)timeSpan.TotalDays}일 전";
        
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)}개월 전";
        
        return $"{(int)(timeSpan.TotalDays / 365)}년 전";
    }
    
    /// <summary>
    /// 시간 포맷팅 (예: "1h 30m")
    /// </summary>
    public static string FormatDuration(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        
        return $"{timeSpan.Seconds}s";
    }
    
    /// <summary>
    /// 시간 포맷팅 (밀리초 포함)
    /// </summary>
    public static string FormatDurationWithMilliseconds(TimeSpan timeSpan)
    {
        return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
    }
    
    /// <summary>
    /// 날짜 포맷팅 (예: "2024-01-01 12:00:00")
    /// </summary>
    public static string FormatDateTime(DateTime dateTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        return dateTime.ToString(format);
    }
    
    /// <summary>
    /// 날짜 포맷팅 (UTC)
    /// </summary>
    public static string FormatDateTimeUtc(DateTime dateTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        return dateTime.ToUniversalTime().ToString(format);
    }
    
    /// <summary>
    /// 오늘 시작 시간 (00:00:00)
    /// </summary>
    public static DateTime TodayStart => UtcNow.Date;
    
    /// <summary>
    /// 오늘 종료 시간 (23:59:59.999)
    /// </summary>
    public static DateTime TodayEnd => TodayStart.AddDays(1).AddTicks(-1);
    
    /// <summary>
    /// 이번 주 시작 (월요일)
    /// </summary>
    public static DateTime WeekStart
    {
        get
        {
            var today = UtcNow.Date;
            var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            return today.AddDays(-diff);
        }
    }
    
    /// <summary>
    /// 이번 달 시작
    /// </summary>
    public static DateTime MonthStart => new(UtcNow.Year, UtcNow.Month, 1);
    
    /// <summary>
    /// 이번 년도 시작
    /// </summary>
    public static DateTime YearStart => new(UtcNow.Year, 1, 1);
    
    /// <summary>
    /// 시간대 변환
    /// </summary>
    public static DateTime ConvertTimeZone(DateTime dateTime, string fromTimeZone, string toTimeZone)
    {
        var fromTz = TimeZoneInfo.FindSystemTimeZoneById(fromTimeZone);
        var toTz = TimeZoneInfo.FindSystemTimeZoneById(toTimeZone);
        
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, fromTz);
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, toTz);
    }
    
    /// <summary>
    /// UTC를 로컬 시간으로 변환
    /// </summary>
    public static DateTime ToLocalTime(DateTime utcTime)
    {
        return utcTime.ToLocalTime();
    }
    
    /// <summary>
    /// 로컬 시간을 UTC로 변환
    /// </summary>
    public static DateTime ToUtcTime(DateTime localTime)
    {
        return localTime.ToUniversalTime();
    }
    
    /// <summary>
    /// 시간 범위 내에 있는지 확인
    /// </summary>
    public static bool IsInTimeRange(DateTime time, DateTime startTime, DateTime endTime)
    {
        return time >= startTime && time <= endTime;
    }
    
    /// <summary>
    /// 시간 범위 내에 있는지 확인 (UTC)
    /// </summary>
    public static bool IsInTimeRangeUtc(DateTime time, DateTime startTime, DateTime endTime)
    {
        var utcTime = time.ToUniversalTime();
        var utcStart = startTime.ToUniversalTime();
        var utcEnd = endTime.ToUniversalTime();
        return utcTime >= utcStart && utcTime <= utcEnd;
    }
}
