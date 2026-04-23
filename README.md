**[📜 커밋 기록 보기 → `COMMITS.md`](./COMMITS.md)** (최신 커밋이 위쪽에 자동 반영)

---

# Ai-Agent-UI

Windows용 **WPF 클라이언트**와 **Python(FastAPI) 에이전트 서버**를 HTTP로 연결하는 데스크톱 AI 채팅 애플리케이션입니다. 에이전트 주소는 `http://127.0.0.1:8787`로 고정되어 있습니다.

## 빠른 시작

1. **에이전트 서버**  
   ```bash
   cd python
   pip install -r requirements.txt
   python agent_server.py
   ```
2. **클라이언트**  
   `AiAgentUi` 프로젝트를 Visual Studio 또는 `dotnet run`으로 실행 (Windows / .NET 8).

3. **테스트**  
   ```powershell
   dotnet test .\AiAgentUi.sln -c Release
   ```
   자세한 내용은 [`docs/TESTING.md`](./docs/TESTING.md).

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
- **확장 포인트 명확**: `_run_agent`·`IAgentClient`만 갈아끼우면 백엔드 모델 교체 가능.

### 단점 / 리스크

- 에이전트 서버 샘플은 **에코/플레이스홀더**라 실제 LLM 연동·인증·과금은 별도 구현 필요.
- **동일 PC 루프백** 외 원격 접속 시에는 [`docs/SECURITY_AND_DEPLOYMENT.md`](./docs/SECURITY_AND_DEPLOYMENT.md)대로 TLS·인증을 반드시 추가해야 함.
- 긴 응답·대용량 파일 처리는 **메모리·타임아웃** 튜닝이 필요할 수 있음.
- WPF + 트레이·핫키는 **Windows 전용**이다.

### 기술 점수 (주관적 10점 만점)

| 항목 | 점수 | 근거 |
|------|:----:|------|
| 아키텍처·코드 구조 | **8** | MVVM·서비스 추상화가 일관적 |
| UI/UX 완성도 | **8** | 탭·지속성·단축키 등 데스크톱 앱으로서 요소 풍부 |
| 백엔드(에이전트) 완성도 | **6** | API 뼈대는 양호, 실제 AI 로직은 연동 대기 |
| 보안·배포 | **8** | 위협 모델·체크리스트·환경 변수 안내 ([`SECURITY_AND_DEPLOYMENT`](./docs/SECURITY_AND_DEPLOYMENT.md)); 원격 시 TLS/인증은 여전히 구현 과제 |
| 테스트·문서 | **8** | xUnit 자동 테스트·[`API`](./docs/API.md)·[`TESTING`](./docs/TESTING.md); GitHub Actions에서 `dotnet test` |

**종합 (가중 평균 개념): 약 7.3 / 10** — *클라이언트·문서·CI는 제품형에 가깝게 정리되었으며, 에이전트 비즈니스 로직(LLM)·원격 배포 세부 보안은 프로젝트별로 채워 넣어야 한다.*

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
├── python/             # FastAPI 에이전트
├── docs/               # API · 테스트 · 보안/배포
├── COMMITS.md          # 커밋 이력 (CI로 자동 갱신)
└── .github/workflows/  # COMMITS 자동 업데이트 · dotnet CI
```

## 라이선스

별도 명시 없음. 필요 시 저장소 소유자가 `LICENSE`를 추가하세요.
