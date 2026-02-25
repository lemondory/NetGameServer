using System.Security.Cryptography;
using System.Text;

namespace NetGameServer.Common.Utils;

/// <summary>
/// 암호화/해싱 유틸리티
/// </summary>
public static class CryptoHelper
{
    /// <summary>
    /// SHA256 해시 생성
    /// </summary>
    public static string Sha256(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// SHA512 해시 생성
    /// </summary>
    public static string Sha512(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        using var sha512 = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha512.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// MD5 해시 생성 (보안이 중요하지 않은 경우에만 사용)
    /// </summary>
    public static string Md5(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// 비밀번호 해싱 (SHA256 + Salt)
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;
        
        return Sha256(password + salt);
    }
    
    /// <summary>
    /// 비밀번호 해싱 (Salt 자동 생성)
    /// </summary>
    public static (string Hash, string Salt) HashPasswordWithSalt(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (string.Empty, string.Empty);
        
        var salt = RandomHelper.GenerateToken(16);
        var hash = HashPassword(password, salt);
        return (hash, salt);
    }
    
    /// <summary>
    /// 비밀번호 검증
    /// </summary>
    public static bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            return false;
        
        var computedHash = HashPassword(password, salt);
        return computedHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// HMAC-SHA256 서명 생성
    /// </summary>
    public static string HmacSha256(string message, string key)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(key))
            return string.Empty;
        
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// HMAC-SHA256 서명 검증
    /// </summary>
    public static bool VerifyHmacSha256(string message, string signature, string key)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(key))
            return false;
        
        var computedSignature = HmacSha256(message, key);
        return computedSignature.Equals(signature, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// AES 암호화
    /// </summary>
    public static byte[] AesEncrypt(byte[] data, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }
    
    /// <summary>
    /// AES 복호화
    /// </summary>
    public static byte[] AesDecrypt(byte[] encryptedData, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }
    
    /// <summary>
    /// 문자열을 AES로 암호화 (Base64 반환)
    /// </summary>
    public static string AesEncryptString(string plainText, string key, string iv)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;
        
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var ivBytes = Encoding.UTF8.GetBytes(iv);
        var dataBytes = Encoding.UTF8.GetBytes(plainText);
        
        var encrypted = AesEncrypt(dataBytes, keyBytes, ivBytes);
        return Convert.ToBase64String(encrypted);
    }
    
    /// <summary>
    /// Base64 문자열을 AES로 복호화
    /// </summary>
    public static string AesDecryptString(string encryptedText, string key, string iv)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;
        
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var ivBytes = Encoding.UTF8.GetBytes(iv);
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            
            var decrypted = AesDecrypt(encryptedBytes, keyBytes, ivBytes);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Base64 인코딩
    /// </summary>
    public static string Base64Encode(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;
        
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(bytes);
    }
    
    /// <summary>
    /// Base64 디코딩
    /// </summary>
    public static string Base64Decode(string base64Text)
    {
        if (string.IsNullOrEmpty(base64Text))
            return string.Empty;
        
        try
        {
            var bytes = Convert.FromBase64String(base64Text);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
