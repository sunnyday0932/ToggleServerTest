# Feature Toggle Server 架構設計與技術選型

基於前期的規格需求與討論，本文件定義了 Feature Toggle Server 的後端技術選型與系統架構設計。

---

## 1. 核心技術與框架選型

* **語言與運行環境**: C# / .NET 10
* **專案類型**: Web API (純粹的 REST API)
* **API 撰寫風格**: Minimal API
  * **理由**: 本系統的端點結構相對單純（CRUD 與少數操作如 Rollback、Disable），使用 Minimal API 能夠提供更簡潔的程式碼結構、降低儀式感（Boilerplate），並具備優異的啟動效能，非常適合輕量級的微服務架構。
  * **邏輯組織**: 採用類似 Clean Architecture 的基礎分層。Minimal API 僅負責路由解析與 HTTP 回應，將業務邏輯（如版本控制、Audit Log、樂觀鎖）抽離至獨立的 Service 層（如 `IToggleService`），以利於關注點分離與單元測試。
* **資料驗證**: 引入 `FluentValidation` 對傳入的 API Payload 進行資料驗證，確保寫入資料庫前的資料正確性。

## 2. 資料儲存機制

* **主要資料庫**: MongoDB
  * **理由**: 依據規格單中的模型定義（如 `BsonId`、`ObjectId`），Feature Toggle 的規則 (Rules/Conditions) 結構可能具備高度彈性與巢狀特性，選擇 Document 型 NoSQL 資料庫 (MongoDB) 能提供最大的靈活性與最好的讀寫效能。
* **並行控制 (Concurrency Control)**:
  * 透過文件內的 `Version` 欄位實作 **樂觀鎖 (Optimistic Locking)**，確保在多位營運人員同時編輯同一個 Toggle 時不會發生資料覆寫的問題。

## 3. 專案與目錄架構規劃

為了維持 Minimal API 的輕量特性，同時不失去可維護性，建議將職責做適度的切分：

```text
/ToggleServer
├── /src
│   ├── ToggleServer.Api         # Minimal API Endpoint 路由、DI 註冊、中介軟體 (Middleware) 設定
│   ├── ToggleServer.Core        # 領域模型 (FeatureToggle, ToggleAuditLog)、Enums 與介面定義
│   └── ToggleServer.Infrastructure # MongoDB Repository 實作、外部依賴整合
├── /tests
│   ├── ToggleServer.UnitTests   # 針對核心邏輯的單元測試
│   └── ToggleServer.IntegrationTests # 針對 API 與 DB 的整合測試
└── /docs                        # 文件存放區
```

## 4. 測試策略選型

高品質的基礎建設服務需要嚴謹的測試來保證穩定性：

* **單元測試 (Unit Tests)**:
  * **框架**: `xUnit`
  * **輔助套件**: `Moq` (用於 Mock 外部依賴)、`AwesomeAssertions` (提供更具語意的斷言，取代 FluentAssertions)。
  * **職責**: 針對核心邏輯（例如 Toggle 規則的過濾、版本號比對邏輯等）進行快速且隔離的測試。

* **整合測試 (Integration Tests)**:
  * **框架**: `xUnit` 搭配 `Testcontainers` (針對 .NET 的 `Testcontainers.MongoDb`)
  * **職責**: 基於 `WebApplicationFactory` 建立端到端 (End-to-End) 的 API 測試。透過 Testcontainers 在測試前自動啟動真實的 MongoDB Docker 容器，確保包含樂觀鎖、Audit Log 寫入、以及資料庫查詢的操作都經過真實環境的驗證。
  * **優點**: 避免使用 InMemory DB 導致行為與真實環境不符的問題，尤其在操作 MongoDB 特定的更新指令場景。

## 5. 其他架構考量

* **API 認證與授權 (Auth)**:
  * 建立自訂的 Middleware 攔截請求進行驗證。初期實作將採用 Mock 邏輯進行簡單的身分確認，不深入實作完整的 JWT 或 OAuth 流程，保留日後擴充空間。
* **即時更新機制 (Real-time Updates)**:
  * 現階段 Client Side 將維持定時輪詢 (Polling) 機制獲取最新 Toggle 狀態，暫不引入 SignalR 或 Server-Sent Events (SSE)。
* **分散式快取 (Redis)**: 
  * 針對 Client API (`GET /api/v1/client/toggles`) 高併發拉取的場景，架構上應將 Repository 介面化，保留未來掛載 Redis Cache 的彈性。初期可先透過 HTTP Header (`ETag`) 設計降低傳輸成本。
* **結構化日誌 (Structured Logging)**:
  * 整合 `Serilog` 增強輸出格式，取代內建的 Console Logger，方便日後串接集中化監控系統並提供更豐富的 log context。
