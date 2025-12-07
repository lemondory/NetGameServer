# NetGameServer

.NET 기반의 고성능 TCP 게임 서버입니다. MMO RPG 게임 서버를 목표로 하며, 네트워크 처리와 게임 로직을 분리한 구조로 설계되었습니다.

## 주요 특징

- **네트워크 스레드 분리**: I/O와 게임 로직 분리
- **패킷 처리 워커 풀**: 병렬 패킷 처리
- **공간 분할**: 효율적인 오브젝트 조회 (Grid 기반)
- **Interest Management**: 시야 범위 내 오브젝트만 동기화
- **메모리 풀링**: ArrayPool 및 Object Pooling으로 GC 압력 감소
- **AI 최적화**: 상태별 업데이트 빈도 조절

## 프로젝트 구조

```
src/
├── NetGameServer.Common/    # 패킷 정의, 맵 데이터
├── NetGameServer.Network/   # TCP 서버, 세션 관리
├── NetGameServer.Auth/      # 인증 서비스
├── NetGameServer.Game/      # 게임 로직
├── NetGameServer/           # 메인 서버
└── NetGameServer.TestClient/# 테스트 클라이언트
```

## 빌드 및 실행

```bash
# 서버 실행
cd src/NetGameServer
dotnet run

# 테스트 클라이언트 실행
cd src/NetGameServer.TestClient
dotnet run
```

기본 포트: **8888**

## 기술 스택

- .NET 9.0
- TCP/IP 소켓 통신
- 비동기 I/O (async/await)
- Serilog (로깅)

## 라이선스

MIT

