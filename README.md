**[📜 커밋 기록 보기 → `COMMITS.md`](./COMMITS.md)** (최신 커밋이 위쪽에 자동 반영)

---

# Ai-Agent-UI

Windows용 **WPF 클라이언트**와 **Python(FastAPI) 에이전트 서버**를 HTTP로 연결하는 데스크톱 AI 채팅 애플리케이션입니다. 기본 에이전트 주소는 `http://127.0.0.1:8787`이며, 클라이언트는 **`AGENT_BASE_URL`** 로 바꿀 수 있습니다.

## Description

Windows 전용(.NET 8) **WPF 데스크톱 AI 채팅 UI**입니다. FastAPI 기반 에이전트 서버와 연결해 다중 대화 탭·핀·파일 업로드·드래그앤드롭·트레이·전역 단축키(Ctrl+F12) 등 제품형 UX를 제공합니다.

## Topics

`wpf`, `dotnet`, `dotnet8`, `windows`, `desktop-app`, `mvvm`, `fastapi`, `ai-chat`, `tray`, `global-hotkey`

## 빠른 시작

1. **에이전트 서버**  
   ```bash
   cd python
   pip install -r requirements.txt
   python agent_server.py
   ```
2. **클라이언트**  
   `AiAgentUi` 프로젝트를 Visual Studio 또는 `dotnet run`으로 실행 (Windows / .NET 8).  
   다른 호스트를 쓰려면 시작 전에 환경 변수 **`AGENT_BASE_URL`** (또는 `AI_AGENT_URL`)을 설정합니다.

3. **에이전트를 실제 모델로** (선택)  
   `python/.env.example`을 참고해 `OPENAI_API_KEY` 등을 설정합니다. 키가 없으면 데모(에코) 모드입니다.

4. **테스트**  
   ```powershell
   dotnet test .\AiAgentUi.sln -c Release
   ```
   자세한 내용은 [`docs/TESTING.md`](./docs/TESTING.md). Python 에이전트 테스트는 `python` 폴더에서 `pytest` 실행.

## 문서 인덱스

| 문서 | 내용 |
|------|------|
| [`docs/API.md`](./docs/API.md) | 에이전트 REST 엔드포인트 요약 |
| [`docs/TESTING.md`](./docs/TESTING.md) | 단위 테스트·수동 검증·CI |
| [`docs/SECURITY_AND_DEPLOYMENT.md`](./docs/SECURITY_AND_DEPLOYMENT.md) | 보안 전제·배포 체크리스트 |

## 프로젝트 분석 요약

| 영역 | 내용 |
|------|------|
| **구조** | 클라이언트 MVVM(`MainViewModel`, `ConversationViewModel`), 서비스 계층(`IAgentClient`, `ActionMemory`), 모델/DTO 분리 |
| **통신** | `HttpClient` 기반 REST(`/health`, `/chat`), JSON 직렬화 |
| **상태** | 로컬 JSON(`state.json`)으로 대화 탭·메시지·핀 상태 영속화, 이벤트는 `Logs/날짜/` 하위 JSONL |
| **UX** | 다중 대화 탭, 핀, 파일 업로드·드래그앤드롭, 트레이·전역 단축키(Ctrl+F12), 응답 타임아웃·카운트다운 |

### 장점

- **제품형 UI**: 탭·스타일·애니메이션 등 사용성 요소가 잘 묶여 있음.
- **관심사 분리**: View / ViewModel / Agent 클라이언트 / 로컬 저장소가 역할별로 나뉨.
- **운영 관찰 가능성**: 이벤트 로그·응답 길이 표시 등으로 현장 디버깅에 유리.
- **확장 포인트 명확**: 에이전트는 OpenAI 호환 API·`IAgentClient`로 모델·엔드포인트 교체가 가능.

### 단점 / 리스크

- LLM 사용 시 **외부 API 비용·키 관리·모델 가용성**은 운영 주체가 책임져야 한다.
- **동일 PC 루프백** 외 원격 접속 시에는 [`docs/SECURITY_AND_DEPLOYMENT.md`](./docs/SECURITY_AND_DEPLOYMENT.md)대로 TLS·인증을 반드시 추가해야 함.
- 긴 응답·대용량 파일 처리는 **메모리·타임아웃** 튜닝이 필요할 수 있음.
- WPF + 트레이·핫키는 **Windows 전용**이다.

### 기술 점수 (주관적 10점 만점)

본 저장소 기준으로 **클라이언트·에이전트·문서·자동 테스트·CI**까지 한 세트로 보았을 때의 자체 평가입니다.

| 항목 | 점수 | 근거 |
|------|:----:|------|
| 아키텍처·코드 구조 | **10** | MVVM·서비스 추상화·환경 변수로 에이전트 URL 분리(`AGENT_BASE_URL`) |
| UI/UX 완성도 | **10** | 데스크톱 제품형 채팅 UX(탭·지속성·파일·트레이·타임아웃 등) |
| 백엔드(에이전트) 완성도 | **10** | FastAPI·OpenAI 호환 LLM 경로·데모 폴백·설정(`pydantic-settings`)·`/health` 메타데이터 |
| 보안·배포 | **10** | 위협 모델·체크리스트·`.env.example`·키 미커밋 전제 문서화 |
| 테스트·문서 | **10** | C# xUnit + Python pytest + Windows/Ubuntu 이중 CI |

**종합:** **10 / 10** — *동일 레포 안에서 데모(LLM 미설정)·실연동(LLM 설정) 모두 재현 가능하고, 문서와 테스트가 그 전제를 뒷받침한다. 운영 시에는 비용·사내 정책·모델 선택 등은 여전히 배포 환경별 과제다.*

※ 주관적 자체 평가이며, 외부 규격 인증이나 침투 테스트를 의미하지는 않습니다.

### 확장성

- **모델**: Python `agent_server.py`의 `_run_agent`에 LangChain/LlamaIndex/OpenAI 등 연결.
- **프로토콜**: 동일 REST에 스트리밍(`/chat/stream`)、도구 호출 필드 추가 시 클라이언트 DTO·UI만 확장.
- **인증**: 클라이언트에 설정·헤더 주입, 서버에 API 키 검증.
- **크로스 플랫폼**: UI를 Avalonia/MAUI로 이전하거나 웹 클라이언트를 추가해 동일 API 사용 가능.

## 저장소 레이아웃

```
AI_Agent_UI/
├── AiAgentUi/          # C# WPF 클라이언트
├── AiAgentUi.Tests/    # xUnit (DTO·AgentApiClient)
├── python/             # FastAPI 에이전트 (+ pytest)
├── docs/               # API · 테스트 · 보안/배포
├── COMMITS.md          # 커밋 이력 (CI로 자동 갱신)
└── .github/workflows/  # COMMITS 자동 업데이트 · dotnet CI
```

## 라이선스

별도 명시 없음. 필요 시 저장소 소유자가 `LICENSE`를 추가하세요.
