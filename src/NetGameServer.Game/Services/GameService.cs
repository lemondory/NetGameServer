using System.Collections.Concurrent;
using NetGameServer.Auth;
using NetGameServer.Common.MapData;
using NetGameServer.Common.Packets;
using NetGameServer.Game.Entities;
using NetGameServer.Game.Pooling;
using NetGameServer.Game.Spatial;
using NetGameServer.Game.Synchronization;
using NetGameServer.Game.World;
using NetGameServer.Network.Processing;
using NetGameServer.Network.Sessions;
using Serilog;

namespace NetGameServer.Game.Services;

/// <summary>
/// 게임 서비스 구현 - GameWorld 통합
/// </summary>
public class GameService : IGameService
{
    private readonly GameWorld _gameWorld;
    private readonly IAuthService _authService;
    private readonly ConcurrentDictionary<string, Character> _sessionToCharacter = new();
    private readonly ConcurrentDictionary<string, IClientSession> _sessions = new();
    // 토큰 -> 세션ID 매핑 (재연결용)
    private readonly ConcurrentDictionary<string, string> _tokenToSession = new();
    // 사용자명 -> 세션ID 매핑 (재연결용 - 토큰이 만료된 경우 대비)
    private readonly ConcurrentDictionary<string, string> _usernameToSession = new();
    // 연결 해제된 캐릭터 (재연결 대기용) - 세션ID -> (캐릭터, 연결 해제 시간)
    private readonly ConcurrentDictionary<string, (Character character, DateTime disconnectedAt)> _disconnectedCharacters = new();
    private readonly TimeSpan _reconnectTimeout = TimeSpan.FromSeconds(30); // 재연결 대기 시간
    
    // 기본 맵 (예시)
    private GameMap? _defaultMap;
    
    // 동기화 설정
    private readonly float _syncInterval = 0.1f; // 100ms마다 동기화
    private DateTime _lastSyncTime = DateTime.UtcNow;
    
    public GameService(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _gameWorld = new GameWorld();
        InitializeMaps();
        
        // 재연결 대기 중인 캐릭터 정리 작업 시작
        _ = Task.Run(CleanupDisconnectedCharactersAsync);
    }
    
    /// <summary>
    /// 재연결 대기 시간이 지난 캐릭터 정리
    /// </summary>
    private async Task CleanupDisconnectedCharactersAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(5000); // 5초마다 체크
                
                var now = DateTime.UtcNow;
                var toRemove = new List<string>();
                
                foreach (var kvp in _disconnectedCharacters)
                {
                    if (now - kvp.Value.disconnectedAt > _reconnectTimeout)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var sessionId in toRemove)
                {
                    if (_disconnectedCharacters.TryRemove(sessionId, out var disconnected))
                    {
                        // 캐릭터 완전히 제거
                        if (_defaultMap != null)
                        {
                            await NotifyObjectDespawnAsync(disconnected.character.ObjectId);
                            _defaultMap.RemoveObject(disconnected.character.ObjectId);
                            GameObjectPools.ReturnCharacter(disconnected.character);
                        }
                        
                        Log.Information("[서버] 재연결 타임아웃으로 캐릭터 제거: 세션 {SessionId}, 캐릭터 ID: {CharacterId}",
                            sessionId, disconnected.character.ObjectId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "재연결 대기 캐릭터 정리 중 오류 발생");
            }
        }
    }
    
    private void InitializeMaps()
    {
        // 맵 데이터 파일 경로
        var mapsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps");
        var defaultMapPath = Path.Combine(mapsDirectory, "map_001.json");
        
        MapData? mapData = null;
        
        // 맵 데이터 파일이 있으면 로드
        if (File.Exists(defaultMapPath))
        {
            mapData = MapDataSerializer.LoadFromFile(defaultMapPath);
            if (mapData != null)
            {
                Log.Information("맵 데이터 로드 성공: {MapName} (ID: {MapId})", mapData.MapName, mapData.MapId);
            }
            else
            {
                Log.Warning("맵 데이터 파일 로드 실패: {Path}", defaultMapPath);
            }
        }
        else
        {
            Log.Warning("맵 데이터 파일을 찾을 수 없음: {Path}", defaultMapPath);
            Log.Information("기본 맵 데이터로 초기화합니다.");
        }
        
        // 맵 생성
        if (mapData != null)
        {
            _defaultMap = new GameMap(mapData.MapId, mapData.MapName, updateRate: 20);
        }
        else
        {
            // 기본 맵 생성 (폴백)
            _defaultMap = new GameMap(1, "시작의 마을", updateRate: 20);
        }
        
        _gameWorld.AddMap(_defaultMap);
        
        // 맵 데이터가 있으면 오브젝트 스폰
        if (mapData != null)
        {
            SpawnMapObjects(mapData);
        }
        else
        {
            // 기본 몬스터 스폰 (폴백)
            SpawnDefaultMonsters();
        }
        
        Log.Information("게임 맵 초기화 완료: {MapName}", _defaultMap.MapName);
    }
    
    /// <summary>
    /// 맵 데이터 기반으로 오브젝트 스폰
    /// </summary>
    private void SpawnMapObjects(MapData mapData)
    {
        // 몬스터 스폰
        foreach (var spawn in mapData.MonsterSpawns)
        {
            for (int i = 0; i < spawn.Count; i++)
            {
                float x = spawn.X;
                float y = spawn.Y;
                float z = spawn.Z;
                
                // 스폰 반경이 있으면 랜덤 위치
                if (spawn.SpawnRadius > 0)
                {
                    var random = new Random();
                    var angle = random.NextSingle() * MathF.PI * 2;
                    var radius = random.NextSingle() * spawn.SpawnRadius;
                    x += MathF.Cos(angle) * radius;
                    z += MathF.Sin(angle) * radius;
                }
                
                var monster = GameObjectPools.RentMonster(x, y, z);
                
                // 몬스터 속성 설정
                if (spawn.Level.HasValue)
                {
                    // Level 속성이 Monster에 있으면 설정 (현재는 없지만 확장 가능)
                }
                if (spawn.Hp.HasValue)
                {
                    monster.Hp = spawn.Hp.Value;
                    monster.MaxHp = spawn.Hp.Value;
                }
                if (spawn.MoveSpeed.HasValue)
                {
                    monster.MoveSpeed = spawn.MoveSpeed.Value;
                }
                if (spawn.DetectRange.HasValue)
                {
                    monster.DetectRange = spawn.DetectRange.Value;
                }
                if (spawn.AttackRange.HasValue)
                {
                    monster.AttackRange = spawn.AttackRange.Value;
                }
                
                _defaultMap!.AddObject(monster);
            }
        }
        
        Log.Information("맵 오브젝트 스폰 완료: 몬스터 {MonsterCount}개", mapData.MonsterSpawns.Sum(s => s.Count));
    }
    
    /// <summary>
    /// 기본 몬스터 스폰 (폴백)
    /// </summary>
    private void SpawnDefaultMonsters()
    {
        for (int i = 0; i < 10; i++)
        {
            var monster = GameObjectPools.RentMonster(
                x: i * 5.0f,
                y: 0.0f,
                z: i * 5.0f
            );
            _defaultMap!.AddObject(monster);
        }
        
        Log.Information("기본 몬스터 스폰 완료: 10개");
    }
    
    /// <summary>
    /// 게임 시작 - 캐릭터 생성 및 맵에 추가
    /// </summary>
    public async Task StartGameAsync(IClientSession session)
    {
        if (_sessionToCharacter.ContainsKey(session.SessionId))
        {
            return; // 이미 게임이 진행 중
        }
        
        if (_defaultMap == null)
        {
            Log.Warning("기본 맵이 초기화되지 않았습니다.");
            return;
        }
        
        // 캐릭터 생성
        var character = GameObjectPools.RentCharacter(x: 0, y: 0, z: 0);
        character.SessionId = session.SessionId;
        
        // 맵에 추가
        if (_defaultMap.AddObject(character))
        {
            _sessionToCharacter.TryAdd(session.SessionId, character);
            _sessions.TryAdd(session.SessionId, session);
            
            // Interest Area 설정
            _defaultMap.InterestManager.SetInterestArea(session.SessionId, character.X, character.Y, character.Z);
            
            // 초기 스냅샷 전송
            await SendInitialSnapshotAsync(session, character);
            
            // 자신의 캐릭터 스폰 알림 (다른 클라이언트에게)
            await NotifyObjectSpawnAsync(character);
            
            Log.Information("[서버] 게임 시작: 세션 {SessionId}, 캐릭터 ID: {CharacterId}, 위치: ({X:F2}, {Y:F2}, {Z:F2})",
                session.SessionId, character.ObjectId, character.X, character.Y, character.Z);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 게임 액션 처리
    /// </summary>
    public async Task ProcessGameActionAsync(IClientSession session, string action)
    {
        if (!_sessionToCharacter.TryGetValue(session.SessionId, out var character))
        {
            return; // 게임이 진행 중이 아님
        }
        
        // 게임 액션 처리
        Log.Debug("게임 액션 처리: {SessionId}, Action: {Action}, 캐릭터 위치: ({X}, {Y}, {Z})",
            session.SessionId, action, character.X, character.Y, character.Z);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 게임 종료 - 캐릭터를 재연결 대기 상태로 전환 (즉시 제거하지 않음)
    /// </summary>
    public async Task EndGameAsync(IClientSession session)
    {
        if (_sessionToCharacter.TryRemove(session.SessionId, out var character))
        {
            if (_defaultMap != null)
            {
                // Interest Area만 제거 (캐릭터는 맵에 유지)
                _defaultMap.InterestManager.RemoveInterestArea(session.SessionId);
                
                // 다른 클라이언트에게는 제거 알림 (재연결 시 다시 스폰 알림)
                await NotifyObjectDespawnAsync(character.ObjectId);
            }
            _sessions.TryRemove(session.SessionId, out _);
            
            // 재연결 대기 목록에 추가 (타임아웃 후 자동 제거)
            _disconnectedCharacters.TryAdd(session.SessionId, (character, DateTime.UtcNow));
            
            Log.Information("[서버] 게임 종료 (재연결 대기): 세션 {SessionId}, 캐릭터 ID: {CharacterId}, 타임아웃: {Timeout}초",
                session.SessionId, character.ObjectId, _reconnectTimeout.TotalSeconds);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 오브젝트 동기화 (주기적으로 호출)
    /// </summary>
    public async Task SyncObjectsAsync()
    {
        if (_defaultMap == null)
            return;
        
        var now = DateTime.UtcNow;
        if ((now - _lastSyncTime).TotalSeconds < _syncInterval)
            return;
        
        _lastSyncTime = now;
        
        // 모든 오브젝트 업데이트
        foreach (var obj in _defaultMap.GetObjectsOfType<IGameObject>())
        {
            await SyncObjectAsync(obj);
        }
    }
    
    /// <summary>
    /// 개별 오브젝트 동기화
    /// </summary>
    private async Task SyncObjectAsync(IGameObject obj)
    {
        if (_defaultMap == null)
            return;
        
        // 이전 위치 저장 (캐릭터인 경우 Interest Area 업데이트)
        float oldX = obj.X, oldY = obj.Y, oldZ = obj.Z;
        
        // Delta 추출
        var (hasPosition, x, y, z, hasHp, hp, hasLevel, level) = _defaultMap.StateTracker.GetDelta(
            obj.ObjectId,
            obj.X, obj.Y, obj.Z,
            obj is Character c ? c.Hp : (obj is Monster m ? m.Hp : 0),
            obj is Character c2 ? c2.MaxHp : (obj is Monster m2 ? m2.MaxHp : 0),
            obj is Character c3 ? c3.Level : 0
        );
        
        // 위치가 변경된 경우 Interest Area 업데이트 (캐릭터인 경우)
        if (hasPosition && obj is Character character && !string.IsNullOrEmpty(character.SessionId))
        {
            _defaultMap.InterestManager.SetInterestArea(character.SessionId, x, y, z);
        }
        
        // 변경사항이 없으면 스킵
        if (!hasPosition && !hasHp && !hasLevel)
            return;
        
        // 관심 클라이언트 찾기 (위치 변경 고려)
        var interestedClients = hasPosition 
            ? _defaultMap.InterestManager.UpdateObjectInterest(obj.ObjectId, oldX, oldY, oldZ, x, y, z)
            : _defaultMap.InterestManager.GetInterestedClients(obj.ObjectId, obj.X, obj.Y, obj.Z);
        
        // Delta 패킷 생성
        var updatePacket = new ObjectUpdatePacket
        {
            ObjectId = obj.ObjectId,
            HasPosition = hasPosition,
            X = x,
            Y = y,
            Z = z,
            HasHp = hasHp,
            Hp = hp,
            HasLevel = hasLevel,
            Level = level
        };
        updatePacket.SetFlags();
        
        // 관심 클라이언트에게 전송
        foreach (var sessionId in interestedClients)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                await session.SendPacketAsync(updatePacket);
            }
        }
    }
    
    /// <summary>
    /// 오브젝트 스폰 시 클라이언트에게 알림
    /// </summary>
    public async Task NotifyObjectSpawnAsync(IGameObject obj)
    {
        if (_defaultMap == null)
            return;
        
        // 관심 클라이언트 찾기
        var interestedClients = _defaultMap.InterestManager.GetInterestedClients(
            obj.ObjectId, obj.X, obj.Y, obj.Z);
        
        // 스폰 패킷 생성
        var spawnPacket = new ObjectSpawnPacket
        {
            ObjectId = obj.ObjectId,
            ObjectType = (byte)obj.ObjectType,
            X = obj.X,
            Y = obj.Y,
            Z = obj.Z,
            Hp = obj is Character c ? c.Hp : (obj is Monster m ? m.Hp : 0),
            MaxHp = obj is Character c2 ? c2.MaxHp : (obj is Monster m2 ? m2.MaxHp : 0),
            Level = obj is Character c3 ? c3.Level : 0
        };
        
        // 상태 저장
        _defaultMap.StateTracker.SaveState(
            obj.ObjectId, obj.X, obj.Y, obj.Z,
            spawnPacket.Hp, spawnPacket.MaxHp, spawnPacket.Level);
        
        // 관심 클라이언트에게 전송
        foreach (var sessionId in interestedClients)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                await session.SendPacketAsync(spawnPacket);
            }
        }
    }
    
    /// <summary>
    /// 오브젝트 제거 시 클라이언트에게 알림
    /// </summary>
    public async Task NotifyObjectDespawnAsync(uint objectId)
    {
        if (_defaultMap == null)
            return;
        
        // 관심 클라이언트 찾기
        var interestedClients = _defaultMap.InterestManager.RemoveObjectInterest(objectId);
        if (interestedClients == null)
            return;
        
        // 제거 패킷 생성
        var despawnPacket = new ObjectDespawnPacket
        {
            ObjectId = objectId
        };
        
        // 상태 제거
        _defaultMap.StateTracker.RemoveState(objectId);
        
        // 관심 클라이언트에게 전송
        foreach (var sessionId in interestedClients)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                await session.SendPacketAsync(despawnPacket);
            }
        }
    }
    
    /// <summary>
    /// 초기 스냅샷 전송
    /// </summary>
    private async Task SendInitialSnapshotAsync(IClientSession session, Character character)
    {
        if (_defaultMap == null)
            return;
        
        // 관심 영역 내 오브젝트 조회
        var objectIds = _defaultMap.GetObjectsInInterest(session.SessionId);
        
        var snapshot = new ObjectSnapshotPacket();
        foreach (var objectId in objectIds)
        {
            // 자신의 캐릭터는 제외 (이미 알고 있음)
            if (objectId == character.ObjectId)
                continue;
            
            IGameObject? obj = null;
            if (objectId < 10000)
            {
                obj = _defaultMap.GetObject<Character>(objectId);
            }
            else
            {
                obj = _defaultMap.GetObject<Monster>(objectId);
            }
            if (obj != null)
            {
                snapshot.Objects.Add(new ObjectSnapshotPacket.ObjectData
                {
                    ObjectId = obj.ObjectId,
                    ObjectType = (byte)obj.ObjectType,
                    X = obj.X,
                    Y = obj.Y,
                    Z = obj.Z,
                    Hp = obj is Character c ? c.Hp : (obj is Monster m ? m.Hp : 0),
                    MaxHp = obj is Character c2 ? c2.MaxHp : (obj is Monster m2 ? m2.MaxHp : 0),
                    Level = obj is Character c3 ? c3.Level : 0
                });
            }
        }
        
        if (snapshot.Objects.Count > 0)
        {
            Log.Debug("초기 스냅샷 전송: 세션 {SessionId}, 오브젝트 수: {Count}", session.SessionId, snapshot.Objects.Count);
            await session.SendPacketAsync(snapshot);
        }
        else
        {
            Log.Debug("초기 스냅샷 전송: 세션 {SessionId}, 전송할 오브젝트 없음", session.SessionId);
        }
    }
    
    /// <summary>
    /// 세션으로 캐릭터 조회
    /// </summary>
    public Character? GetCharacterBySession(string sessionId)
    {
        _sessionToCharacter.TryGetValue(sessionId, out var character);
        return character;
    }
    
    /// <summary>
    /// 맵 조회
    /// </summary>
    public GameMap? GetMap(uint mapId)
    {
        return _gameWorld.GetMap(mapId);
    }
    
    /// <summary>
    /// 패킷 처리 (패킷 프로세서에서 호출)
    /// </summary>
    public void HandlePacket(PacketContext context)
    {
        try
        {
            switch (context.Packet)
            {
                case LoginRequestPacket loginRequest:
                    HandleLoginRequest(context);
                    break;
                    
                case ReconnectRequestPacket reconnectRequest:
                    HandleReconnectRequest(context, reconnectRequest);
                    break;
                    
                case MoveRequestPacket moveRequest:
                    HandleMoveRequest(context, moveRequest);
                    break;
                    
                // 다른 패킷 타입 처리 추가
                default:
                    Log.Warning("알 수 없는 패킷 타입: {PacketId}", context.Packet.PacketId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "패킷 처리 오류 발생");
        }
    }
    
    private async void HandleLoginRequest(PacketContext context)
    {
        if (context.Packet is not LoginRequestPacket loginRequest)
        {
            Log.Warning("잘못된 패킷 타입: {PacketType}", context.Packet.GetType());
            return;
        }
        
        Log.Information("로그인 요청 처리: {SessionId}, Username: {Username}",
            context.Session.SessionId, loginRequest.Username);
        
        // 인증 서비스를 통한 로그인 처리
        var response = await _authService.LoginAsync(loginRequest);
        
        // 로그인 실패 시 자동 등록 (테스트용 - 실제 프로덕션에서는 제거)
        if (!response.Success && !string.IsNullOrEmpty(loginRequest.Username) && !string.IsNullOrEmpty(loginRequest.Password))
        {
            Log.Information("사용자 자동 등록 시도: {Username}", loginRequest.Username);
            var registered = await _authService.RegisterAsync(loginRequest.Username, loginRequest.Password);
            if (registered)
            {
                // 다시 로그인 시도
                response = await _authService.LoginAsync(loginRequest);
                Log.Information("자동 등록 후 로그인: {Success}", response.Success);
            }
        }
        
        // 응답 패킷 전송
        Log.Debug("로그인 응답 패킷 전송 시작: {SessionId}", context.Session.SessionId);
        await context.Session.SendPacketAsync(response);
        Log.Information("로그인 응답 전송 완료: {SessionId}, Success: {Success}, Message: {Message}",
            context.Session.SessionId, response.Success, response.Message);
        
        // 로그인 성공 시 토큰-세션 매핑 저장 및 게임 시작
        if (response.Success && !string.IsNullOrEmpty(response.Token))
        {
            // 토큰 -> 세션ID 매핑 저장
            _tokenToSession.TryAdd(response.Token, context.Session.SessionId);
            // 사용자명 -> 세션ID 매핑 저장 (토큰 만료 대비)
            _usernameToSession.TryAdd(loginRequest.Username, context.Session.SessionId);
            
            Log.Debug("토큰-세션 매핑 저장: Token={TokenPrefix}..., SessionId={SessionId}, Username={Username}",
                response.Token.Substring(0, Math.Min(8, response.Token.Length)), 
                context.Session.SessionId, loginRequest.Username);
            
            await StartGameAsync(context.Session);
        }
    }
    
    private async void HandleReconnectRequest(PacketContext context, ReconnectRequestPacket reconnectRequest)
    {
        Log.Information("재연결 요청 처리: {SessionId}, Username: {Username}, Token: {Token}",
            context.Session.SessionId, reconnectRequest.Username, 
            string.IsNullOrEmpty(reconnectRequest.Token) ? "없음" : reconnectRequest.Token.Substring(0, Math.Min(8, reconnectRequest.Token.Length)) + "...");
        
        var response = new ReconnectResponsePacket
        {
            Success = false,
            Message = "재연결 실패"
        };
        
        string? oldSessionId = null;
        Character? existingCharacter = null;
        
        // 1. 토큰 검증 및 이전 세션 찾기
        if (!string.IsNullOrEmpty(reconnectRequest.Token))
        {
            // 토큰 유효성 검증
            var isTokenValid = await _authService.ValidateTokenAsync(reconnectRequest.Token);
            
            if (isTokenValid)
            {
                // 토큰으로 이전 세션ID 찾기
                if (_tokenToSession.TryGetValue(reconnectRequest.Token, out oldSessionId))
                {
                    Log.Debug("토큰으로 이전 세션 찾음: Token={TokenPrefix}..., OldSessionId={OldSessionId}",
                        reconnectRequest.Token.Substring(0, Math.Min(8, reconnectRequest.Token.Length)), oldSessionId);
                    
                    // 이전 세션의 캐릭터 찾기
                    if (_sessionToCharacter.TryGetValue(oldSessionId, out existingCharacter))
                    {
                        Log.Debug("이전 세션의 캐릭터 찾음: OldSessionId={OldSessionId}, CharacterId={CharacterId}",
                            oldSessionId, existingCharacter.ObjectId);
                    }
                    else
                    {
                        Log.Warning("이전 세션의 캐릭터를 찾을 수 없음: OldSessionId={OldSessionId}", oldSessionId);
                    }
                }
                else
                {
                    Log.Warning("토큰으로 이전 세션을 찾을 수 없음: Token={TokenPrefix}...",
                        reconnectRequest.Token.Substring(0, Math.Min(8, reconnectRequest.Token.Length)));
                }
            }
            else
            {
                Log.Warning("토큰이 유효하지 않음: Token={TokenPrefix}...",
                    reconnectRequest.Token.Substring(0, Math.Min(8, reconnectRequest.Token.Length)));
            }
        }
        
        // 2. 토큰으로 찾지 못했으면 사용자명으로 시도
        if (existingCharacter == null && !string.IsNullOrEmpty(reconnectRequest.Username))
        {
            if (_usernameToSession.TryGetValue(reconnectRequest.Username, out oldSessionId))
            {
                Log.Debug("사용자명으로 이전 세션 찾음: Username={Username}, OldSessionId={OldSessionId}",
                    reconnectRequest.Username, oldSessionId);
                
                // 활성 세션에서 찾기
                if (_sessionToCharacter.TryGetValue(oldSessionId, out existingCharacter))
                {
                    Log.Debug("이전 세션의 캐릭터 찾음 (활성): OldSessionId={OldSessionId}, CharacterId={CharacterId}",
                        oldSessionId, existingCharacter.ObjectId);
                }
                // 재연결 대기 목록에서 찾기
                else if (_disconnectedCharacters.TryGetValue(oldSessionId, out var disconnected))
                {
                    existingCharacter = disconnected.character;
                    Log.Debug("이전 세션의 캐릭터 찾음 (재연결 대기): OldSessionId={OldSessionId}, CharacterId={CharacterId}",
                        oldSessionId, existingCharacter.ObjectId);
                }
            }
        }
        
        // 3. 재연결 대기 목록에서 직접 찾기 (토큰/사용자명으로 찾지 못한 경우)
        if (existingCharacter == null && !string.IsNullOrEmpty(oldSessionId))
        {
            if (_disconnectedCharacters.TryGetValue(oldSessionId, out var disconnected))
            {
                existingCharacter = disconnected.character;
                Log.Debug("재연결 대기 목록에서 캐릭터 찾음: OldSessionId={OldSessionId}, CharacterId={CharacterId}",
                    oldSessionId, existingCharacter.ObjectId);
            }
        }
        
        // 4. 이전 캐릭터를 찾았으면 재연결 성공
        if (existingCharacter != null && !string.IsNullOrEmpty(oldSessionId))
        {
            // 재연결 대기 목록에서 제거
            _disconnectedCharacters.TryRemove(oldSessionId, out _);
            
            // 이전 세션 정리 (활성 세션에서)
            _sessionToCharacter.TryRemove(oldSessionId, out _);
            _sessions.TryRemove(oldSessionId, out _);
            
            // 새 세션에 캐릭터 연결
            existingCharacter.SessionId = context.Session.SessionId;
            _sessionToCharacter.TryAdd(context.Session.SessionId, existingCharacter);
            _sessions.TryAdd(context.Session.SessionId, context.Session);
            
            // 토큰-세션 매핑 업데이트
            if (!string.IsNullOrEmpty(reconnectRequest.Token))
            {
                _tokenToSession.AddOrUpdate(reconnectRequest.Token, context.Session.SessionId, (k, v) => context.Session.SessionId);
            }
            
            // 사용자명-세션 매핑 업데이트
            if (!string.IsNullOrEmpty(reconnectRequest.Username))
            {
                _usernameToSession.AddOrUpdate(reconnectRequest.Username, context.Session.SessionId, (k, v) => context.Session.SessionId);
            }
            
            // Interest Area 재설정
            if (_defaultMap != null)
            {
                _defaultMap.InterestManager.SetInterestArea(
                    context.Session.SessionId, 
                    existingCharacter.X, 
                    existingCharacter.Y, 
                    existingCharacter.Z
                );
                
                // 다른 클라이언트에게 재스폰 알림
                await NotifyObjectSpawnAsync(existingCharacter);
            }
            
            response.Success = true;
            response.Message = "재연결 성공";
            response.SessionId = context.Session.SessionId;
            
            Log.Information("재연결 성공: 세션 {NewSessionId} <- {OldSessionId}, 캐릭터 ID: {CharacterId}, 위치: ({X:F2}, {Y:F2}, {Z:F2})",
                context.Session.SessionId, oldSessionId, existingCharacter.ObjectId, 
                existingCharacter.X, existingCharacter.Y, existingCharacter.Z);
        }
        else
        {
            // 이전 캐릭터를 찾을 수 없으면 새 게임 시작
            Log.Warning("재연결 실패: 이전 세션을 찾을 수 없음. 새 게임 시작 (Username: {Username}, Token: {TokenPrefix}...)",
                reconnectRequest.Username ?? "없음",
                string.IsNullOrEmpty(reconnectRequest.Token) ? "없음" : reconnectRequest.Token.Substring(0, Math.Min(8, reconnectRequest.Token.Length)));
            
            response.Success = true; // 재연결 실패지만 새 게임 시작은 성공으로 처리
            response.Message = "이전 세션을 찾을 수 없어 새 게임을 시작합니다";
            response.SessionId = context.Session.SessionId;
            
            // 새 게임 시작 (로그인과 동일한 처리)
            var loginRequest = new LoginRequestPacket
            {
                Username = reconnectRequest.Username ?? "reconnect_user",
                Password = "" // 재연결이므로 비밀번호 불필요
            };
            
            var loginResponse = await _authService.LoginAsync(loginRequest);
            if (loginResponse.Success && !string.IsNullOrEmpty(loginResponse.Token))
            {
                _tokenToSession.TryAdd(loginResponse.Token, context.Session.SessionId);
                if (!string.IsNullOrEmpty(reconnectRequest.Username))
                {
                    _usernameToSession.TryAdd(reconnectRequest.Username, context.Session.SessionId);
                }
            }
            
            await StartGameAsync(context.Session);
        }
        
        // 응답 패킷 전송
        await context.Session.SendPacketAsync(response);
        Log.Information("재연결 응답 전송: {SessionId}, Success: {Success}, Message: {Message}",
            context.Session.SessionId, response.Success, response.Message);
    }
    
    private async void HandleMoveRequest(PacketContext context, MoveRequestPacket moveRequest)
    {
        if (!_sessionToCharacter.TryGetValue(context.Session.SessionId, out var character))
        {
            Log.Warning("[이동 요청] 세션 {SessionId}: 캐릭터를 찾을 수 없음", context.Session.SessionId);
            return;
        }
        
        var oldX = character.X;
        var oldY = character.Y;
        var oldZ = character.Z;
        
        // 이동 목표 설정
        character.SetMoveTarget(moveRequest.TargetX, moveRequest.TargetY, moveRequest.TargetZ);
        
        Log.Information("[이동 요청] 세션: {SessionId}, 캐릭터 ID: {CharacterId}, 위치: ({OldX:F2}, {OldY:F2}, {OldZ:F2}) → ({NewX:F2}, {NewY:F2}, {NewZ:F2})",
            context.Session.SessionId, character.ObjectId,
            oldX, oldY, oldZ,
            moveRequest.TargetX, moveRequest.TargetY, moveRequest.TargetZ);
        
        await Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _gameWorld.Dispose();
    }
}

/// <summary>
/// 게임 세션 (레거시 - 필요시 사용)
/// </summary>
public class GameSession
{
    public IClientSession ClientSession { get; }
    public DateTime StartTime { get; }
    public Dictionary<string, object> GameState { get; }
    
    public GameSession(IClientSession clientSession)
    {
        ClientSession = clientSession;
        StartTime = DateTime.UtcNow;
        GameState = new Dictionary<string, object>();
    }
}
