using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ToggleServer.Core.Models;

public class ToggleAuditLog
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    public string ToggleKey { get; set; } = string.Empty; // 關聯的 Toggle Key
    
    public int Version { get; set; } // 異動後的版本號
    
    [BsonRepresentation(BsonType.String)]
    public AuditAction Action { get; set; }
    
    public string OperatorId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    
    // 異動前的完整狀態 (JSON 序列化字串或 BsonDocument)
    public FeatureToggle? PreviousConfiguration { get; set; } 
    
    // 異動後的完整狀態
    public FeatureToggle? NewConfiguration { get; set; } 
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}
