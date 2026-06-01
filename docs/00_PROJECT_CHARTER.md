# 00. Project Charter

## 프로젝트명

`VisionCell Manufacturing Studio`

## 한 줄 정의

WPF HMI에서 가상 반도체/SMT 머신비전 검사장비를 셋업하고, Motor/I/O/Teaching/Recipe/Inspection/Offline Debug까지 수행하는 고품질 장비 소프트웨어 포트폴리오.

## 배경

채용공고의 업무는 단순 웹 CRUD가 아니라 다음에 가깝다.

- 검사 장비 Setting: PC, 제어기, 모터, I/O
- Teaching: Motor, Robot 좌표 등록
- 검사 장비 시뮬레이션 테스트
- 검사 장비 구동 테스트
- Visual Studio C++/C# 기반 개발/셋업 역량

따라서 포트폴리오는 웹앱보다 WPF 기반 장비 HMI가 맞다.

## 목표

- 실제 장비가 없어도 장비 셋업 업무의 핵심 흐름을 재현한다.
- WPF UI 품질, MVVM 구조, 도메인 분리, 테스트 가능성을 보여준다.
- 검사장비 소프트웨어의 Safety, Sequence, Traceability, Debug 흐름을 보여준다.
- GitHub Issue/PR/CI/Codex Review 기반 개발 프로세스를 보여준다.

## 비목표

- 실제 PLC, 실제 카메라, 실제 모터 드라이버 연결은 1차 목표가 아니다.
- 상용 검사 알고리즘 수준의 정확도는 목표가 아니다.
- 회사 내부 기술/이미지/데이터를 복제하지 않는다.
- 특정 회사의 비공개 UI/제품을 그대로 모사하지 않는다.

## 성공 기준

| 구분 | 성공 기준 |
|---|---|
| HMI | 장비용 데스크톱 앱처럼 보이고 동작한다. |
| Motion | Home, Jog, Absolute Move, Teaching, Timeout, Alarm 처리가 된다. |
| I/O | 센서/도어/비상정지/진공 등 가상 I/O를 실시간 표시한다. |
| Recipe | Teaching 좌표, ROI, 검사 파라미터를 제품별로 저장한다. |
| Inspection | 가상 이미지/HeightMap 검사 후 Pass/Fail과 Overlay를 저장한다. |
| Offline Debug | 과거 검사 데이터를 불러와 재검사하고 파라미터 비교가 가능하다. |
| Traceability | 모든 작업이 이벤트 로그/DB/결과 파일로 추적 가능하다. |
| Process | 요구사항 ID → Issue → PR → Test → Release Notes 흐름이 있다. |

## 핵심 사용자

### Setup Engineer

- 장비를 연결하고 원점복귀를 수행한다.
- Jog로 좌표를 맞추고 Teaching Point를 저장한다.
- 센서/I/O 상태를 확인한다.
- 검사 레시피를 만들고 시운전한다.

### Process Engineer

- 검사 조건과 ROI를 조정한다.
- 불량 유형과 검사 파라미터를 비교한다.
- Offline Debug에서 과거 결과를 재현한다.

### Reviewer / Interviewer

- WPF, C#, 장비 제어, 시퀀스, 테스트, GitHub 운영 능력을 확인한다.

## 제품 원칙

1. **현장 장비 느낌**: 멋진 웹 대시보드가 아니라 실제 장비 HMI 같은 흐름.
2. **안전 우선**: Door, Emergency, Servo, Homing 상태 없이는 위험 동작 금지.
3. **추적성**: 모든 명령, 상태변화, 검사결과는 로그/DB로 남김.
4. **재현성**: Offline Debug에서 같은 입력/레시피로 같은 결과가 나와야 함.
5. **확장성**: Simulator를 실제 장비 드라이버로 교체할 수 있게 인터페이스 분리.
6. **Codex 친화성**: 파일명, 모듈 책임, 요구사항 ID가 명확해서 AI가 이어서 개발 가능.
