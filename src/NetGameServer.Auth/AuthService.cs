using System.Security.Cryptography;
using System.Text;
using NetGameServer.Common.Packets;

namespace NetGameServer.Auth;

/// <summary>
/// 인증 서비스 구현 (간단한 메모리 기반 구현)
/// 실제 프로덕션에서는 데이터베이스와 연동해야 합니다.
/// </summary>
public class AuthService : IAuthService
{
    // 간단한 메모리 기반 사용자 저장소
    private readonly Dictionary<string, string> _users = new(); // username -> hashed password
    private readonly Dictionary<string, string> _tokens = new(); // token -> username
    private readonly HashSet<string> _activeTokens = new();
    
    public async Task<LoginResponsePacket> LoginAsync(LoginRequestPacket request)
    {
        await Task.Delay(1); // 비동기 시뮬레이션
        
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return new LoginResponsePacket
            {
                Success = false,
                Message = "사용자명과 비밀번호를 입력해주세요."
            };
        }
        
        // 사용자 확인
        if (!_users.TryGetValue(request.Username, out var hashedPassword))
        {
            return new LoginResponsePacket
            {
                Success = false,
                Message = "사용자명 또는 비밀번호가 올바르지 않습니다."
            };
        }
        
        // 비밀번호 확인
        var inputHash = HashPassword(request.Password);
        if (hashedPassword != inputHash)
        {
            return new LoginResponsePacket
            {
                Success = false,
                Message = "사용자명 또는 비밀번호가 올바르지 않습니다."
            };
        }
        
        // 토큰 생성
        var token = GenerateToken(request.Username);
        _tokens[token] = request.Username;
        _activeTokens.Add(token);
        
        return new LoginResponsePacket
        {
            Success = true,
            Message = "로그인 성공",
            Token = token
        };
    }
    
    public async Task<bool> ValidateTokenAsync(string token)
    {
        await Task.Delay(1);
        return !string.IsNullOrEmpty(token) && _activeTokens.Contains(token);
    }
    
    public async Task<bool> RegisterAsync(string username, string password)
    {
        await Task.Delay(1);
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }
        
        if (_users.ContainsKey(username))
        {
            return false; // 이미 존재하는 사용자
        }
        
        var hashedPassword = HashPassword(password);
        _users[username] = hashedPassword;
        
        return true;
    }
    
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
    
    private string GenerateToken(string username)
    {
        var tokenData = $"{username}:{DateTime.UtcNow:O}:{Guid.NewGuid()}";
        var bytes = Encoding.UTF8.GetBytes(tokenData);
        return Convert.ToBase64String(bytes);
    }
}

