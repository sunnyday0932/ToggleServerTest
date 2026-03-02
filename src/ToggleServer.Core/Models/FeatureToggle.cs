using MongoDB.Bson.Serialization.Attributes;

namespace ToggleServer.Core.Models;

public class FeatureToggle
{
    // 在 MongoDB 中作為 _id，直接使用 Toggle Key (例如: "new_checkout_flow")
    [BsonId]
    public string Key { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    
    // 總開關 (Kill Switch)
    public bool Enabled { get; set; } 
    
    // 樂觀鎖版本號，每次 Update 必須 +1
    public int Version { get; set; } 
    
    // 規則陣列 (由上往下評估，First Match Wins)
    public List<ToggleRule> Rules { get; set; } = new(); 
    
    // 預設退場值
    public bool DefaultServe { get; set; } 
    
    public string LastUpdatedBy { get; set; } = string.Empty; // 操作者 ID 或名稱
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}
