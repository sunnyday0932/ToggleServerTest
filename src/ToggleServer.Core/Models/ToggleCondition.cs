using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ToggleServer.Core.Models;

public class ToggleCondition
{
    public string Attribute { get; set; } = string.Empty; // 要比對的屬性 (如: "email", "userId", "country")
    
    [BsonRepresentation(BsonType.String)]
    public ConditionOperator Operator { get; set; } // 比對運算子
    
    // 為了相容多種型別 (字串陣列、數字陣列等)，這裡為求單純先用 List<string>
    public List<string> Values { get; set; } = new(); 
}
