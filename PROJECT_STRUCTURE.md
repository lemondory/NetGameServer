# 프로젝트 구조

## 폴더 구조

```
NetGameServer/
├── src/
│   ├── NetGameServer.Common/          # 공통 정의
│   │   ├── Packets/                   # 패킷 정의
│   │   └── MapData/                   # 맵 데이터 구조
│   │
│   ├── NetGameServer.Network/         # 네트워크 라이브러리
│   │   ├── Sessions/                  # 클라이언트 세션
│   │   ├── Servers/                   # TCP 서버
│   │   ├── Processing/                # 패킷 처리
│   │   └── Management/                # 연결 관리
│   │
│   ├── NetGameServer.Auth/            # 인증 서비스(WebServer로 변경 예정)
│   │
│   ├── NetGameServer.Game/            # 게임 로직
│   │   ├── Entities/                  # 게임 오브젝트 (Character, Monster)
│   │   ├── World/                     # 맵 및 월드 관리
│   │   ├── Spatial/                   # 공간 분할
│   │   ├── Synchronization/           # 오브젝트 동기화
│   │   ├── Pooling/                   # 오브젝트 풀링
│   │   └── Services/                  # 게임 서비스
│   │
│   ├── NetGameServer/                 # 메인 서버 애플리케이션
│   │
│   └── NetGameServer.TestClient/      # 테스트 클라이언트
│
├── docs/                              # 문서
├── Maps/                              # 맵 데이터 파일
└── UnityEditorScripts/                # Unity 에디터 스크립트
```

## 네임스페이스 구조

### NetGameServer.Common
- `NetGameServer.Common.Packets`: 패킷 정의
- `NetGameServer.Common.MapData`: 맵 데이터 구조

### NetGameServer.Network
- `NetGameServer.Network.Sessions`: 클라이언트 세션 인터페이스 및 구현
- `NetGameServer.Network.Servers`: TCP 서버 구현
- `NetGameServer.Network.Processing`: 패킷 버퍼 및 프로세서
- `NetGameServer.Network.Management`: 연결 관리

### NetGameServer.Game
- `NetGameServer.Game.Entities`: 게임 오브젝트 (Character, Monster, IGameObject)
- `NetGameServer.Game.World`: 맵 및 월드 관리 (GameMap, GameWorld)
- `NetGameServer.Game.Spatial`: 공간 분할 (ISpatialPartition, SpatialGrid)
- `NetGameServer.Game.Synchronization`: 오브젝트 동기화 (InterestManager, ObjectStateTracker)
- `NetGameServer.Game.Pooling`: 오브젝트 풀링 (ObjectPool, GameObjectPools)
- `NetGameServer.Game.Services`: 게임 서비스 (GameService, IGameService)

## 프로젝트 이름

**NetGameServer** - 현재 이름 그대로 사용 가능합니다.

- 명확하고 간결함
- .NET 기반 게임 서버임을 명확히 표현
- GitHub에서 검색하기 쉬움
- 변경 시 큰 작업 필요 없음 (나중에 변경 가능)

