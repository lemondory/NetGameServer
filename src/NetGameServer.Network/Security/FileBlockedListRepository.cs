using System.Text.Json;
using Serilog;

namespace NetGameServer.Network.Security;

/// <summary>
/// 파일 기반 차단 목록 저장소
/// </summary>
public class FileBlockedListRepository : IBlockedListRepository
{
    private readonly string _filePath;

    public FileBlockedListRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blocked_ips.json");
    }

    public Task SaveAsync(IReadOnlyCollection<BlockEntry> entries, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries.Select(e => new BlockEntryDto(e.Ip, e.BlockedAt, e.InvalidCount)).ToList());
            File.WriteAllText(_filePath, json);
            Log.Information("차단 목록 저장: {Count}개 IP, 경로={Path}", entries.Count, _filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "차단 목록 저장 실패: {Path}", _filePath);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BlockEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<BlockEntry>();
        if (!File.Exists(_filePath))
            return Task.FromResult<IReadOnlyList<BlockEntry>>(result);

        try
        {
            var json = File.ReadAllText(_filePath);
            var dtos = JsonSerializer.Deserialize<List<BlockEntryDto>>(json);
            if (dtos != null)
            {
                foreach (var dto in dtos)
                    result.Add(new BlockEntry(dto.Ip, dto.BlockedAt, dto.InvalidCount));
                Log.Information("차단 목록 로드: {Count}개 IP", result.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "차단 목록 로드 실패: {Path}", _filePath);
        }

        return Task.FromResult<IReadOnlyList<BlockEntry>>(result);
    }

    private record BlockEntryDto(string Ip, DateTime BlockedAt, int InvalidCount);
}
