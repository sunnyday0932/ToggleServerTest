
# Feature Toggle Server 核心規格書

## 1. 資料結構設計 (C# Models for MongoDB)

我們將建立兩個主要的 Collection 模型：`FeatureToggle`（當前狀態）與 `ToggleAuditLog`（稽核日誌）。為了維持 C# 的強型別特性，我們會將 JSON 結構對應成 C# 的類別（Class）。

### 1.1 當前狀態模型 (FeatureToggle)

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

public class FeatureToggle
{
    // 在 MongoDB 中作為 _id，直接使用 Toggle Key (例如: "new_checkout_flow")
    [BsonId]
    public string Key { get; set; } 

    public string Description { get; set; }
    
    // 總開關 (Kill Switch)
    public bool Enabled { get; set; } 
    
    // 樂觀鎖版本號，每次 Update 必須 +1
    public int Version { get; set; } 
    
    // 規則陣列 (由上往下評估，First Match Wins)
    public List<ToggleRule> Rules { get; set; } = new List<ToggleRule>(); 
    
    // 預設退場值
    public bool DefaultServe { get; set; } 
    
    public string LastUpdatedBy { get; set; } // 操作者 ID 或名稱
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}

public class ToggleRule
{
    public string Name { get; set; } // 規則名稱 (例如: "20% 灰度發布")
    public List<ToggleCondition> Conditions { get; set; } = new List<ToggleCondition>(); // AND 邏輯
    public bool Serve { get; set; } // 若條件皆符合，要回傳的狀態 (通常為 true)
}

public class ToggleCondition
{
    public string Attribute { get; set; } // 要比對的屬性 (如: "email", "userId", "country")
    
    [BsonRepresentation(BsonType.String)]
    public ConditionOperator Operator { get; set; } // 比對運算子
    
    // 為了相容多種型別 (字串陣列、數字陣列等)，使用 dynamic 或 object 陣列，
    // 或自訂 BsonSerializer 來處理。這裡為求單純先用 List<string> 示範。
    public List<string> Values { get; set; } = new List<string>(); 
}

public enum ConditionOperator
{
    EQUALS,
    NOT_EQUALS,
    IN,
    NOT_IN,
    STARTS_WITH,
    ENDS_WITH,
    MATCHES_REGEX,
    PERCENTAGE_ROLLOUT // 針對 Hash 的數值做小於比對
}

```

### 1.2 稽核與歷史版本模型 (ToggleAuditLog)

這張表是 Append-Only（只新增不修改），用於追蹤與 Rollback。

```csharp
public class ToggleAuditLog
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    public string ToggleKey { get; set; } // 關聯的 Toggle Key
    
    public int Version { get; set; } // 異動後的版本號
    
    [BsonRepresentation(BsonType.String)]
    public AuditAction Action { get; set; }
    
    public string OperatorId { get; set; }
    public string OperatorName { get; set; }
    
    // 異動前的完整狀態 (JSON 序列化字串或 BsonDocument)
    public FeatureToggle PreviousConfiguration { get; set; } 
    
    // 異動後的完整狀態
    public FeatureToggle NewConfiguration { get; set; } 
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}

public enum AuditAction
{
    CREATE,
    UPDATE,
    ENABLE,    // 透過緊急按鈕開啟
    DISABLE,   // 透過緊急按鈕關閉
    ROLLBACK
}

```

---

## 2. API Endpoint 設計與職責

為了職責分離（Separation of Concerns），我們將 API 分為「Client API（給各微服務 SDK 呼叫的）」與「Management API（給 BackOffice 呼叫的）」。

### 2.1 Client API (高併發、極致讀取效能)

* **`GET /api/v1/client/toggles`**
* **職責：** 讓各個微服務的 SDK 在啟動時，或背景定時任務（Polling）時拉取「所有的」規則。
* **回應：** 回傳 `FeatureToggle` 陣列。
* **備註：** 這支 API 流量會最大，未來可考慮在它前面加上 Redis Cache，或利用 ETag 來減少傳輸量（回傳 `304 Not Modified`）。



### 2.2 Management API (BackOffice 專用，著重業務邏輯與一致性)

* **`GET /api/v1/management/toggles`**
* **職責：** 列表頁使用，支援分頁、關鍵字搜尋（搜 Key 或 Description）。


* **`GET /api/v1/management/toggles/{key}`**
* **職責：** 取得單一 Toggle 的詳細設定，供編輯頁面渲染。


* **`POST /api/v1/management/toggles`**
* **職責：** 建立新的 Feature Toggle。預設 `Version` 為 1，並寫入一筆 `CREATE` 的 Audit Log。


* **`PUT /api/v1/management/toggles/{key}`**
* **職責：** 更新 Toggle 的規則。
* **關鍵邏輯：** Request Body 必須帶上前端看到的 `Version`。後端需實作樂觀鎖驗證，若成功更新，則寫入一筆 `UPDATE` 的 Audit Log。


* **`POST /api/v1/management/toggles/{key}/disable` (Kill Switch)**
* **職責：** 一鍵緊急關閉。直接將 `Enabled` 設為 `false`，寫入一筆 `DISABLE` 的 Audit Log。


* **`GET /api/v1/management/toggles/{key}/audit-logs`**
* **職責：** 取得該 Toggle 的歷史異動軌跡，按時間倒序排列（最新在上）。


* **`POST /api/v1/management/toggles/{key}/rollback/{version}`**
* **職責：** 將 Toggle 狀態回滾至指定的歷史版本。
* **關鍵邏輯：** 將目標版本的 `NewConfiguration` 覆寫為最新狀態，`Version` 遞增，並產生一筆 `ROLLBACK` 的 Audit Log (Roll Forward 機制)。



---

## 3. BackOffice UI 介面設計規劃

BackOffice 是非技術人員（如 PM、營運人員）與 Toggle Server 互動的橋樑，UX（使用者體驗）與防呆機制是設計重點。建議包含以下三個核心畫面：

### 3.1 Dashboard 總覽頁 (List View)

* **狀態指示燈：** 清楚標示每個 Toggle 目前是「Global On（全開）」、「Global Off（全關）」還是「Targeting（部分條件開啟，如灰度）」。
* **搜尋與過濾器：** 支援用 Key、名稱、建立者或標籤進行篩選。
* **快速操作按鈕 (Quick Actions)：**
* 針對每個 Toggle 提供一個紅色的「Kill Switch（強制關閉）」按鈕，點擊後需跳出二次確認視窗（"您確定要關閉此功能嗎？這將影響線上所有流量"）。



### 3.2 詳細設定與編輯頁 (Toggle Editor)

* **基本資訊區：** Key (不可改)、Description (可編輯)。
* **總開關 (Master Switch)：** 一個大大的 Toggle Button，控制整體的 `Enabled` 狀態。
* **規則引擎編輯器 (Rule Builder UI)：** 這是最複雜也最重要的元件。
* **區塊化設計：** 每一個 `ToggleRule` 是一個獨立的卡片區塊。支援拖拉（Drag and Drop）來調整優先順序。
* **條件設定表單：** 提供下拉選單選擇 `Attribute`（如 email）、`Operator`（如 ENDS_WITH），並提供 Input 欄位輸入 `Values`。
* **百分比滑桿：** 當選擇 `PERCENTAGE_ROLLOUT` 時，顯示一個 0~100 的滑桿 UI，讓非技術人員直覺操作。


* **預設回傳值設定：** 若都不符合規則時的行為設定。
* **儲存按鈕：** 儲存時，如果觸發樂觀鎖衝突（409），UI 要彈出提示並重新載入最新設定。

### 3.3 歷史軌跡與時光機 (Audit & Diff Viewer)

* **時間軸 (Timeline)：** 左側顯示垂直的時間軸，列出所有異動紀錄（例如："John Doe 在 14:00 更新了規則", "System 在 15:00 執行了 Rollback"）。
* **Diff 差異比較器：** 點擊任何一筆紀錄時，右側畫面要顯示類似 GitHub Pull Request 的 "Code Diff" 或 "UI Diff" 畫面。以紅綠色塊清楚標示「刪除了什麼條件」、「新增了什麼條件」。
* **一鍵回滾按鈕：** 在過去的某個穩定版本紀錄旁，提供一個「回滾至此版本 (Restore to this version)」按鈕。

---