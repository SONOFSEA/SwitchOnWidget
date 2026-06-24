# Architecture

SwitchOnWidget은 작은 WPF 애플리케이션이지만, UI 코드에 도메인 로직과 저장 로직이 몰리지 않도록 역할별로 분리했습니다. 이 문서는 프로젝트의 설계 의도와 주요 기술적 결정을 설명합니다.

## 1. 구성 요소

```text
Presentation
├─ MainWindow             오늘의 식단, 체크, 체중, 트레이 제어
├─ CalendarWindow         42일 계획 조회와 날짜별 기록 편집
└─ WeightLogWindow        체중 표, 통계, Canvas 그래프, CSV

Domain Models
├─ DietDay                계산된 날짜별 식단
├─ DailyRecord            사용자가 입력한 수행 기록
├─ UserProfile            시작 체중과 목표 설정
└─ AppSettings            창 위치와 실행 환경 설정

Application Services
├─ DietPlanService        DateOnly 기반 42일 계획 생성
├─ WeightProjectionService 선형 보간 기반 3개 예상선
├─ StorageService         JSON 초기화, 복구, 직렬화, 안전 저장
├─ StartupService         HKCU Run 등록과 제거
└─ CsvExportService       기록의 이식 가능한 CSV 변환
```

## 2. 주요 데이터 흐름

### 앱 시작

1. `App`이 이름 있는 `Mutex`를 획득해 중복 실행을 차단합니다.
2. `StorageService.InitializeAsync`가 AppData 폴더와 세 JSON 파일을 준비합니다.
3. 저장된 프로필·기록·설정을 로드하고, 손상된 파일은 백업 후 기본값으로 복구합니다.
4. `MainWindow`가 Windows 로컬 날짜에서 `DateOnly`를 만들고 해당 날짜 계획을 표시합니다.
5. 저장된 창 위치, 항상 위 옵션, 자동실행 상태를 UI에 반영합니다.

### 기록 저장

1. UI에서 입력 범위와 형식을 검증합니다.
2. 체크 상태를 `DailyRecord`에 반영합니다.
3. JSON 전체를 같은 폴더의 `.tmp` 파일에 비동기로 기록합니다.
4. 쓰기와 flush가 끝난 임시 파일을 실제 JSON 경로로 교체합니다.
5. `SemaphoreSlim`이 동시에 발생한 자동 저장을 직렬화합니다.

## 3. 날짜 설계

날짜는 시각과 타임존이 필요 없는 도메인 값입니다. 모든 계획 날짜, 기록 키, Day 계산에 `DateOnly`를 사용합니다.

```csharp
public static readonly DateOnly StartDate = new(2026, 6, 22);
int day = date.DayNumber - StartDate.DayNumber + 1;
```

현재 날짜만 `DateOnly.FromDateTime(DateTime.Now)`로 Windows 로컬 시각에서 얻습니다. UTC 변환, Unix timestamp, `DateTimeOffset` 왕복이 없으므로 한국 시간대에서 전날로 바뀌는 경로가 없습니다.

## 4. 데이터 안정성

- **기본값 생성:** 최초 실행 시 세 JSON 파일을 자동 생성합니다.
- **손상 격리:** 역직렬화 실패 파일을 `.corrupt-yyyyMMddHHmmss`로 보관합니다.
- **안전 저장:** 임시 파일 작성이 완료된 후 원본 경로와 교체합니다.
- **입력 검증:** 실제 체중은 30~200kg 범위만 허용합니다.
- **종료 저장:** 완전 종료 전에 기록, 창 위치, 실행 설정을 저장합니다.

## 5. Windows 통합

| 기능 | 구현 |
|---|---|
| 시스템 트레이 | `System.Windows.Forms.NotifyIcon` |
| 단일 인스턴스 | 이름 있는 `System.Threading.Mutex` |
| 자동실행 | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| 항상 위 | WPF `Window.Topmost` |
| 완전 종료 | 트레이 리소스 해제 후 `Application.Shutdown()` |

HKCU만 수정하므로 관리자 권한이 필요하지 않습니다. 창 닫기와 최소화는 트레이 숨김으로 처리하고, 사용자가 명시적으로 `정지`를 선택했을 때만 프로세스를 종료합니다.

## 6. 체중 예상선

Day 1과 Day 42의 목표 사이를 41개 구간으로 선형 보간합니다.

```text
ratio = (day - 1) / 41
expected = start + (target - start) × ratio
```

보수 74.0kg, 표준 71.5kg, 도전 68.0kg의 세 목표를 동일한 방식으로 계산하고 소수점 첫째 자리까지 표시합니다. 실제 체중은 계산값과 분리해 저장합니다.

## 7. 의도적인 트레이드오프

- 개인용 오프라인 위젯이므로 데이터베이스 대신 사람이 읽을 수 있는 JSON을 선택했습니다.
- 배포 크기와 유지보수 부담을 줄이기 위해 MVVM 프레임워크와 차트 패키지를 추가하지 않았습니다.
- 그래프는 WPF `Canvas`, `Polyline`, `Ellipse`로 직접 그려 외부 의존성을 제거했습니다.
- 자동실행은 바로가기 COM 자동화보다 단순하고 관리자 권한이 필요 없는 HKCU Run 키를 사용했습니다.

## 8. 확장 방향

- 서비스 계층에 대한 xUnit 단위 테스트 추가
- 시작일과 식단을 UI에서 편집하는 계획 설정 화면
- JSON 스키마 버전과 마이그레이션 전략
- 접근성 개선과 다국어 리소스 분리
- GitHub Releases를 통한 서명된 self-contained 배포
