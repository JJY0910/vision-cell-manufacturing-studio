# 01. Requirements Specification

## 문서 목적

이 문서는 `VisionCell Manufacturing Studio`의 회사 수준 요구사항 정의서다. 모든 구현은 요구사항 ID에 연결되어야 하며, Codex는 작업 전 관련 요구사항을 명시해야 한다.

## 우선순위 정의

| Priority | 의미 |
|---|---|
| P0 | 포트폴리오 핵심. 없으면 프로젝트 가치가 크게 떨어짐. |
| P1 | 고품질 포트폴리오를 만드는 주요 기능. |
| P2 | 완성도/편의성/확장성 향상. |
| P3 | 시간이 남으면 구현. |

## 상태 정의

| Status | 의미 |
|---|---|
| Planned | 계획됨 |
| In Progress | 구현 중 |
| Implemented | 구현됨 |
| Verified | 테스트/데모 확인됨 |
| Deferred | 보류 |

## 시스템 모드

| Mode | 설명 |
|---|---|
| Offline | 장비 미연결. 레시피 편집/Offline Debug 가능. |
| Manual | 수동 셋업. Jog/Teaching/I/O 확인 가능. |
| Auto | 자동 검사 시퀀스 실행. 위험 수동 조작 제한. |
| Alarm | 에러 발생. Reset 전 동작 제한. |
| EmergencyStop | 비상정지. Servo Off 및 모든 명령 차단. |

## Functional Requirements

### A. Application Shell / UX

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-001 | P0 | WPF Shell은 Top status bar, Left navigation, Main workspace, Bottom event log를 제공해야 한다. | 앱 실행 시 모든 영역이 표시되고 창 크기 변경에도 깨지지 않는다. |
| FR-002 | P0 | 화면 전환은 MVVM navigation service로 수행해야 한다. | Code-behind 직접 화면 생성 금지, ViewModel 테스트 가능. |
| FR-003 | P0 | 전역 장비 상태는 모든 화면에서 동일하게 표시되어야 한다. | Connected/Mode/Alarm/EStop/Recipe/Operator/Cycle 표시. |
| FR-004 | P1 | 모든 주요 명령 버튼은 Enabled/Disabled 조건을 시각적으로 표시해야 한다. | EStop 시 Motion/Inspection 버튼 비활성화. |
| FR-005 | P1 | Bottom event log는 최근 이벤트를 시간순으로 표시해야 한다. | Event type, severity, source, message, timestamp 표시. |
| FR-006 | P1 | UI는 1920x1080 기준으로 최적화하고 1366x768에서도 스크롤 없이 핵심 조작이 가능해야 한다. | 기본 화면에서 핵심 패널 잘림 없음. |
| FR-007 | P2 | Light/Dark theme 또는 장비용 Dark theme를 지원해야 한다. | Theme resource 변경으로 전체 스타일 변경 가능. |
| FR-008 | P2 | 키보드 단축키를 지원해야 한다. | F5 Start, F6 Stop, F8 Reset, Ctrl+S Recipe Save. |

### B. Equipment Connection / Controller

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-020 | P0 | Simulator Controller에 Connect/Disconnect 할 수 있어야 한다. | Connect 상태와 latency가 Dashboard에 표시된다. |
| FR-021 | P0 | Controller 상태는 polling 또는 event stream으로 갱신되어야 한다. | UI가 1초 이내 상태 변화를 반영한다. |
| FR-022 | P0 | Controller command는 timeout/cancellation을 가져야 한다. | Timeout 발생 시 Alarm event와 error code가 생성된다. |
| FR-023 | P1 | Connection profile을 저장/불러올 수 있어야 한다. | Local simulator, virtual cell profile 선택 가능. |
| FR-024 | P1 | 연결 실패/재연결/heartbeat loss를 처리해야 한다. | Heartbeat loss 시 Auto mode 진입 금지. |
| FR-025 | P2 | Simulator latency, jitter, failure rate를 설정할 수 있어야 한다. | 설정 변경 후 command 응답 특성이 바뀐다. |

### C. Safety / Interlock

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-040 | P0 | Emergency Stop 상태에서는 모든 motion/camera/inspection command가 차단되어야 한다. | EStop On 후 명령 시도 시 CommandRejected event 발생. |
| FR-041 | P0 | Door Open 상태에서는 Auto inspection sequence가 시작되면 안 된다. | Start 클릭 시 SafetyInterlockFailed 표시. |
| FR-042 | P0 | Servo Off 상태에서는 Move/Jog/Home command가 실행되면 안 된다. | UI disabled + backend validation 둘 다 존재. |
| FR-043 | P1 | Alarm Reset은 원인/현재 상태를 검증한 뒤 수행해야 한다. | EStop On 상태에서 Reset 실패. |
| FR-044 | P1 | Safety summary panel을 제공해야 한다. | Door, EStop, Servo, Vacuum, AirPressure 상태 표시. |
| FR-045 | P2 | Interlock violation history를 DB에 저장해야 한다. | 날짜별 위반 내역 조회 가능. |

### D. Motion / Axis Control

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-060 | P0 | X/Y/Z/Theta 4축 상태를 표시해야 한다. | Position, Target, Homed, Servo, Moving, Alarm 표시. |
| FR-061 | P0 | 각 축 Home command를 지원해야 한다. | Home 완료 후 Homed=true, position=0 또는 configured origin. |
| FR-062 | P0 | 각 축 Jog +/− command를 지원해야 한다. | 누르고 있는 동안 또는 step 방식으로 이동 가능. |
| FR-063 | P0 | Absolute Move를 지원해야 한다. | 목표 좌표 입력 후 위치 이동 및 command log 저장. |
| FR-064 | P0 | Motion command 중복 실행을 방지해야 한다. | Moving 상태에서 같은 축 새 명령 차단 또는 queue 정책 적용. |
| FR-065 | P1 | Motion profile을 설정해야 한다. | velocity, acceleration, deceleration, jerk 저장. |
| FR-066 | P1 | Soft limit을 적용해야 한다. | 범위 초과 명령은 실행 전 거부. |
| FR-067 | P1 | Motion timeout/alarm/error injection을 지원해야 한다. | 시뮬레이터에서 timeout 주입 후 Alarm 발생. |
| FR-068 | P1 | Multi-axis move를 지원해야 한다. | Teaching point로 X/Y/Z/Theta 동시 이동. |
| FR-069 | P2 | Move history chart 또는 table을 제공해야 한다. | 최근 100개 이동 명령 조회. |

### E. I/O Monitor

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-080 | P0 | Digital Input/Output 상태를 grid로 표시해야 한다. | Sensor, Door, EStop, Vacuum, Air, Light, Buzzer 표시. |
| FR-081 | P0 | Simulator I/O를 UI에서 토글할 수 있어야 한다. | Door Open/Close, EStop On/Off 등 상태 변경. |
| FR-082 | P1 | I/O bit별 이름, address, direction, forced state를 표시해야 한다. | Tooltip 또는 detail panel 제공. |
| FR-083 | P1 | Output write는 권한과 mode에 따라 제한되어야 한다. | Auto mode에서 수동 output 변경 제한. |
| FR-084 | P2 | I/O 상태 변화 이벤트를 DB에 저장해야 한다. | 변화 timestamp와 source 기록. |

### F. Teaching

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-100 | P0 | 현재 axis position을 Teaching Point로 저장할 수 있어야 한다. | 이름/역할/좌표/메모가 저장된다. |
| FR-101 | P0 | Teaching Point 목록을 표시하고 선택 시 해당 좌표로 이동할 수 있어야 한다. | Go To Teaching Point command 동작. |
| FR-102 | P0 | Teaching Point role을 정의해야 한다. | Load, Camera, Inspection, Review, Safe, Park role 지원. |
| FR-103 | P1 | Teaching Point별 tolerance를 설정해야 한다. | 도착 위치 오차가 tolerance 초과 시 경고. |
| FR-104 | P1 | Teaching 수정 이력을 저장해야 한다. | 변경 전/후 좌표, 변경자, 시간 기록. |
| FR-105 | P2 | Teaching point import/export를 지원해야 한다. | JSON export/import 가능. |

### G. Recipe Management

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-120 | P0 | Recipe 생성/수정/복제/삭제를 지원해야 한다. | ProductCode, Version, Description 포함. |
| FR-121 | P0 | Recipe에는 Teaching, ROI, Vision Params, Safety Params가 포함되어야 한다. | JSON Schema validation 통과. |
| FR-122 | P0 | Active Recipe를 선택해야 inspection 가능하다. | No recipe 시 Start disabled. |
| FR-123 | P1 | Recipe versioning을 지원해야 한다. | v1.0.0 → v1.0.1 변경 기록. |
| FR-124 | P1 | Recipe validation 결과를 UI에 표시해야 한다. | 누락/범위초과 항목 리스트 표시. |
| FR-125 | P2 | Recipe diff view를 제공해야 한다. | 두 버전 파라미터 차이 표시. |

### H. Camera / Image Acquisition

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-140 | P0 | Camera simulator는 sample image를 grab해야 한다. | Grab command 후 이미지가 viewer에 표시된다. |
| FR-141 | P0 | Grab 실패/timeout을 시뮬레이션해야 한다. | CameraGrabFailed error code 발생. |
| FR-142 | P1 | Exposure/Gain/Light 설정을 recipe에 저장해야 한다. | 설정 변경 후 grab 결과 metadata에 저장. |
| FR-143 | P1 | Live preview mode를 지원해야 한다. | 일정 간격으로 simulator image 업데이트. |
| FR-144 | P2 | Image calibration placeholder를 제공해야 한다. | Pixel-to-mm factor 저장. |

### I. Vision Inspection

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-160 | P0 | ROI 기반 2D 검사를 수행해야 한다. | Missing, Offset, Scratch 중 2개 이상 판정. |
| FR-161 | P0 | 검사 결과는 Pass/Fail, defect type, score, overlay를 포함해야 한다. | 결과 JSON/DB에 저장. |
| FR-162 | P1 | Synthetic height map 기반 3D height 검사를 수행해야 한다. | Lift, Dent, LeadBent 유사 defect 판정. |
| FR-163 | P1 | 검사 알고리즘 파라미터를 recipe에서 읽어야 한다. | threshold/tolerance 변경 시 결과 변화. |
| FR-164 | P1 | Overlay image를 생성해야 한다. | ROI, bbox, defect label 시각화. |
| FR-165 | P2 | ONNX AI classifier 확장 지점을 제공해야 한다. | 인터페이스와 placeholder 구현. |
| FR-166 | P2 | C++ OpenCV native engine 연동 후보를 제공해야 한다. | native CLI/DLL 구조 문서화. |

### J. Inspection Sequence

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-180 | P0 | Auto inspection sequence를 실행해야 한다. | Load Recipe → Safety → Move → Grab → Inspect → Judge → Save. |
| FR-181 | P0 | Sequence step별 상태를 UI timeline에 표시해야 한다. | Pending/Running/Success/Fail/Skipped 표시. |
| FR-182 | P0 | Stop command는 안전하게 sequence를 중단해야 한다. | 현재 step 종료 또는 취소 후 Safe state. |
| FR-183 | P1 | Step별 cycle time을 측정해야 한다. | DB와 UI에 ms 단위 저장. |
| FR-184 | P1 | Error 발생 시 Alarm mode로 전환해야 한다. | Error code, recovery hint 표시. |
| FR-185 | P2 | Dry Run mode를 지원해야 한다. | 실제 저장 없이 sequence 검증. |

### K. Result Logging / Traceability

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-200 | P0 | 검사 결과를 SQLite에 저장해야 한다. | Lot, Recipe, ImagePath, Judge, Defect, Timing 저장. |
| FR-201 | P0 | System event log를 SQLite에 저장해야 한다. | severity/source/message/correlationId 저장. |
| FR-202 | P1 | 검사 결과 상세 화면을 제공해야 한다. | 이미지, overlay, params, timing, defect list 표시. |
| FR-203 | P1 | CSV export를 지원해야 한다. | 결과 목록을 CSV로 저장. |
| FR-204 | P2 | Lot summary report를 제공해야 한다. | Pass rate, defect Pareto, cycle time summary 표시. |

### L. Offline Debug Station

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-220 | P1 | 과거 검사 결과를 검색/선택할 수 있어야 한다. | 기간/recipe/judge/defect filter. |
| FR-221 | P1 | 선택 결과의 원본/overlay/height map/params를 표시해야 한다. | 검사 재현에 필요한 데이터 표시. |
| FR-222 | P1 | 현재 recipe 또는 기존 recipe로 Re-Inspect해야 한다. | 기존 판정 vs 재검사 판정 비교. |
| FR-223 | P2 | 파라미터 sensitivity test를 지원해야 한다. | threshold 변경 sweep 결과 표시. |

### M. Settings / Maintenance

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-240 | P1 | 장비 profile, path, DB, log retention 설정 화면을 제공해야 한다. | 설정 저장/재시작 후 유지. |
| FR-241 | P1 | Error code catalog를 제공해야 한다. | 코드, 원인, 복구방법 표시. |
| FR-242 | P2 | Self diagnosis screen을 제공해야 한다. | Controller/Camera/Motion/DB health check. |
| FR-243 | P2 | Sample data reset 기능을 제공해야 한다. | 개발/데모용 seed data 복원. |

### N. GitHub / Codex Workflow

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| FR-260 | P0 | 요구사항 ID 기반 Issue/PR 운영을 해야 한다. | PR title에 FR ID 포함. |
| FR-261 | P0 | GitHub Actions에서 Windows build/test를 실행해야 한다. | PR마다 build/test 결과 생성. |
| FR-262 | P1 | Codex review를 AGENTS.md 기준으로 수행해야 한다. | PR comment에서 `@codex review` 또는 자동 리뷰. |
| FR-263 | P1 | Codex가 자율 개선 backlog를 갱신해야 한다. | `docs/BACKLOG.md`, ADR 업데이트. |

## Non-functional Requirements

| ID | Priority | Requirement | Acceptance Criteria |
|---|---|---|---|
| NFR-001 | P0 | UI freeze 금지 | 500ms 이상 blocking command는 async/progress 처리. |
| NFR-002 | P0 | 모든 hardware command timeout/cancellation | timeoutMs, CancellationToken 전달. |
| NFR-003 | P0 | 테스트 가능성 | Core/Application/Motion/Vision 로직 단위테스트. |
| NFR-004 | P0 | 추적성 | command/event/result correlationId 저장. |
| NFR-005 | P1 | 성능 | 일반 inspection sequence 1초 내 완료; simulator 기준. |
| NFR-006 | P1 | 안정성 | 예외가 UI를 죽이지 않고 error event로 전환. |
| NFR-007 | P1 | 확장성 | Simulator 구현을 실제 장비 구현으로 교체 가능. |
| NFR-008 | P1 | 보안 | secrets 커밋 금지, path traversal 방지. |
| NFR-009 | P2 | 유지보수성 | feature별 module 분리, ADR 기록. |
| NFR-010 | P2 | 접근성 | 주요 command button tooltip, keyboard navigation. |

## Requirement Traceability Matrix

| Area | Requirements | Main Projects |
|---|---|---|
| Shell/UX | FR-001~019 | VisionCell.App |
| Equipment | FR-020~059 | VisionCell.Equipment, VisionCell.Simulator |
| Motion | FR-060~079 | VisionCell.Motion, VisionCell.Application |
| I/O | FR-080~099 | VisionCell.Equipment, VisionCell.App |
| Teaching | FR-100~119 | VisionCell.Motion, VisionCell.Persistence |
| Recipe | FR-120~139 | VisionCell.Application, VisionCell.Persistence |
| Camera | FR-140~159 | VisionCell.Equipment, VisionCell.Simulator |
| Vision | FR-160~179 | VisionCell.Vision |
| Sequence | FR-180~199 | VisionCell.Application |
| Result | FR-200~219 | VisionCell.Persistence, VisionCell.Telemetry |
| Offline Debug | FR-220~239 | VisionCell.App, VisionCell.Application |
| Workflow | FR-260~269 | GitHub, AGENTS, docs |
