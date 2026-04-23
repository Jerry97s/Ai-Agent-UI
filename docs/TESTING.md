# 테스트 가이드

## 자동 테스트 (.NET / xUnit)

저장소 루트에서:

```powershell
dotnet test .\AiAgentUi.sln -c Release
```

또는 테스트 프로젝트만:

```powershell
dotnet test .\AiAgentUi.Tests\AiAgentUi.Tests.csproj
```

### 포함 범위

| 영역 | 설명 |
|------|------|
| `ChatDtosJsonTests` | `/chat` 요청·응답 JSON 형식과 DTO 매핑 |
| `AgentApiClientTests` | HTTP 클라이언트 동작(모의 핸들러): `HealthAsync`, `SendMessageAsync` |

UI(WPF)·전역 핫키·트레이는 단위 테스트 비용이 커서 현재 범위에서 제외했습니다. 필요 시 UI 자동화(WinAppDriver 등)를 별도 스위트로 두는 것을 권장합니다.

## 에이전트 서버 수동 확인

에이전트 실행 후:

```bash
curl -s http://127.0.0.1:8787/health
curl -s -X POST http://127.0.0.1:8787/chat -H "Content-Type: application/json" -d "{\"message\":\"ping\"}"
```

## Python (pytest)

저장소 루트에서:

```powershell
cd python
py -m pip install -r requirements.txt
py -m pytest -q
```

또는 `pip`/`pytest`가 PATH에 있으면 동일하게 실행합니다. CI(Ubuntu)에서는 Python 3.12에서 동작을 검증합니다.

## CI

GitHub에 푸시 시 다음 워크플로가 실행됩니다.

- `.github/workflows/dotnet-ci.yml` — `dotnet build` / `dotnet test` (Windows)
- `.github/workflows/python-ci.yml` — `pytest` (Ubuntu, 데모 모드)
