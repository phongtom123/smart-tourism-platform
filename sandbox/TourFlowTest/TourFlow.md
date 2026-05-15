# Tour Flow Diagrams

Các sơ đồ Mermaid dưới đây có thể paste trực tiếp vào draw.io qua menu **Arrange > Insert > Advanced > Mermaid** (hoặc dùng plugin Mermaid trong VS Code/Notion).

---

## 1. Sequence — du khách đi 1 tour từ đầu đến cuối

```mermaid
sequenceDiagram
    autonumber
    participant U as Du khách (App MAUI)
    participant Geo as GeofenceEngine
    participant API as Backend .NET
    participant Q as PoiVisitQueue
    participant DB as MySQL

    U->>API: GET /api/tour
    API->>DB: SELECT tour active
    DB-->>API: List tours
    API-->>U: [{ idTour, ten, soStop, ... }]

    U->>API: GET /api/tour/{id}
    API->>DB: JOIN tour + tour_diem + gianhang + ghnn
    DB-->>API: Tour + stops (lat/lon, audioUrl)
    API-->>U: TourDetailDto

    Note over U,Geo: Visitor chọn tour → app khởi động lazy prefetch<br/>cho 3 stop đầu theo thứ tự

    loop Mỗi stop
        U->>Geo: location update (GPS tick)
        Geo->>Geo: Prioritize(triggers, tourNextStopBoothId)
        Note right of Geo: Stop kế tiếp luôn thắng<br/>dù MonthlyFee thấp
        Geo-->>U: Audio booth thắng phát

        U->>API: POST /api/tour/{id}/advance<br/>X-Device-Id, idGianHangVuaDen
        API->>DB: SELECT tour_diem.thuTu
        API->>DB: UPSERT tour_tien_do GREATEST(step)
        DB-->>API: OK
        API-->>U: { stepKeTiep, idGianHangKeTiep, isCompleted }

        U->>Q: POST /api/poi/{idGh}/visit (queued)
        Q-->>DB: batch flush mỗi 5s/50 items
    end

    Note over U: stepKeTiep == soStop → isCompleted=true
    U->>U: Hiển thị màn "Hoàn thành tour"
```

---

## 2. State machine — trạng thái tour của 1 thiết bị

```mermaid
stateDiagram-v2
    [*] --> ChuaBatDau: visitor chọn tour
    ChuaBatDau --> DangDi: POST advance lần đầu (step=1)
    DangDi --> DangDi: advance đến stop tiếp theo<br/>(step++, GREATEST chống lùi)
    DangDi --> DaSkip: nhảy stop (vd 1→4)<br/>các stop giữa được đánh dấu skipped
    DaSkip --> DangDi: tiếp tục advance
    DangDi --> HoanThanh: thuTu == totalStops
    DaSkip --> HoanThanh: thuTu == totalStops
    HoanThanh --> [*]

    note right of DangDi
        stepHienTai = thuTu_vua_den + 1
        startedAt = NOW (chỉ set lần đầu)
    end note
    note right of HoanThanh
        completedAt = UTC_NOW
        Hiển thị màn congrats
    end note
```

---

## 3. Priority logic — khi nào tour stop thắng audio

```mermaid
flowchart TD
    A[Tick GPS update] --> B[Lấy mọi booth visitor đang inside]
    B --> C{Đang trong tour?}
    C -->|Không| D[Prioritize:<br/>distance/radius ASC<br/>MonthlyFee DESC<br/>Id ASC]
    C -->|Có| E[Prioritize với override:<br/>tourNextStopBoothId DESC<br/>distance/radius ASC<br/>MonthlyFee DESC<br/>Id ASC]
    D --> F[Winner = booth ratio thấp nhất]
    E --> G{Visitor đang inside<br/>stop kế tiếp?}
    G -->|Có| H[Winner = stop kế tiếp<br/>dù fee thấp hơn]
    G -->|Không| F
    F --> I[ScheduleAutoPlayAsync winner]
    H --> I
    I --> J[Audio phát + ghi visit qua queue]
```

---

## 4. Admin CRUD flow

```mermaid
sequenceDiagram
    autonumber
    participant Admin as Admin (Browser)
    participant PHP as CS_admin/admin/tour.php
    participant API as Backend .NET
    participant Auth as AccountAccessService
    participant DB as MySQL

    Admin->>PHP: GET ?usecase=tour
    Note over PHP: Guard:<br/>if accountRole != 'admin' redirect
    PHP->>API: GET /api/admin/tour?idTaiKhoan=X
    API->>Auth: IsAdminAsync(X)
    Auth->>DB: SELECT loaiTaiKhoan
    Auth-->>API: true
    API->>DB: SELECT tour + COUNT(stops)
    DB-->>API: List
    API-->>PHP: JSON
    PHP-->>Admin: Render bảng + stat cards

    Admin->>PHP: Click "Tạo tour mới"
    PHP-->>Admin: Form HTML + Sortable.js<br/>+ list gian hàng

    Admin->>PHP: Drag-drop sắp xếp + Submit
    PHP->>API: POST /api/admin/tour?idTaiKhoan=X<br/>{ ten, stops[] }
    API->>Auth: IsAdminAsync
    Auth-->>API: true
    API->>DB: BEGIN TX
    API->>DB: INSERT tour
    API->>DB: INSERT tour_diem (multi)
    API->>DB: COMMIT
    DB-->>API: OK
    API-->>PHP: { success: true, idTour }
    PHP-->>Admin: Redirect + flash success

    rect rgb(255, 235, 235)
        Note over Admin,Auth: Non-admin (chu_quan_ly) thử truy cập
        Admin->>PHP: GET ?usecase=tour
        Note over PHP: Guard: redirect ra store
        Admin->>API: GET /api/admin/tour?idTaiKhoan=Y
        API->>Auth: IsAdminAsync(Y)
        Auth-->>API: false
        API-->>Admin: 403 Forbidden
    end
```

---

## 5. Tổng quan kiến trúc tour

```mermaid
flowchart LR
    subgraph Client[App MAUI]
        Map[PoiMapPage<br/>+ Map polyline]
        Geo[GeofenceEngine<br/>+ priority override]
        Audio[AudioCacheService<br/>+ tour-aware lazy prefetch]
        Sel[TourSelectionPage]
    end

    subgraph Web[Admin Web - PHP]
        TourPage[tour.php<br/>list + form drag-drop]
        Sidebar[sidebar.php<br/>admin-only link]
    end

    subgraph Backend[.NET]
        TC[TourController<br/>+ admin CRUD endpoints]
        TS[TourService<br/>+ FlushVisitBatchAsync<br/>+ AdvanceAsync]
        AAS[AccountAccessService<br/>IsAdminAsync]
        Q[PoiVisitQueue<br/>+ Worker batch]
    end

    subgraph DBlayer[MySQL]
        DB1[(tour)]
        DB2[(tour_diem)]
        DB3[(tour_tien_do)]
        DB4[(gianhang + ghnn)]
        DB5[(luot_truy_cap_*)]
    end

    Sel --> TC
    Map --> TC
    Geo --> TC
    Audio --> Geo
    TourPage --> TC
    Sidebar --> TourPage

    TC --> AAS
    TC --> TS
    TC --> Q
    TS --> DB1
    TS --> DB2
    TS --> DB3
    TS --> DB4
    Q --> DB5

    style Client fill:#e9fcfb
    style Web fill:#fff1e6
    style Backend fill:#eef2ff
    style DBlayer fill:#f3f4f6
```
