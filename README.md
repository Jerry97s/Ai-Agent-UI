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
- **로컬 HTTP만** 전제라 TLS·API 키 노출 방지·원격 배포 시 보안 설계가 비어 있음.
- 긴 응답·대용량 파일 처리는 **메모리·타임아웃** 튜닝이 필요할 수 있음.
- WPF + 트레이·핫키는 **Windows 전용**이다.

### 기술 점수 (주관적 10점 만점)

| 항목 | 점수 | 근거 |
|------|:----:|------|
| 아키텍처·코드 구조 | **8** | MVVM·서비스 추상화가 일관적 |
| UI/UX 완성도 | **8** | 탭·지속성·단축키 등 데스크톱 앱으로서 요소 풍부 |
| 백엔드(에이전트) 완성도 | **6** | API 뼈대는 양호, 실제 AI 로직은 연동 대기 |
| 보안·배포 | **6** | 로컬 프로토타입에는 적합, 프로덕션 가드는 미흡 |
| 테스트·문서 | **6** | 자동 테스트·API 문서는 최소 수준 |

**종합 (가중 평균 개념): 약 7 / 10** — *데스크톱 에이전트 클라이언트 뼈대로는 상위권, “완성된 제품 서비스”까지는 백엔드·보안·운영 항목 보강 필요.*

### 확장성

- **모델**: Python `agent_server.py`의 `_run_agent`에 LangChain/LlamaIndex/OpenAI 등 연결.
- **프로토콜**: 동일 REST에 스트리밍(`/chat/stream`)、도구 호출 필드 추가 시 클라이언트 DTO·UI만 확장.
- **인증**: 클라이언트에 설정·헤더 주입, 서버에 API 키 검증.
- **크로스 플랫폼**: UI를 Avalonia/MAUI로 이전하거나 웹 클라이언트를 추가해 동일 API 사용 가능.

## 저장소 레이아웃

```
AI_Agent_UI/
├── AiAgentUi/          # C# WPF 클라이언트
├── python/             # FastAPI 에이전트
├── COMMITS.md          # 커밋 이력 (CI로 자동 갱신)
└── .github/workflows/  # COMMITS.md 자동 업데이트
```

## 라이선스

별도 명시 없음. 필요 시 저장소 소유자가 `LICENSE`를 추가하세요.
