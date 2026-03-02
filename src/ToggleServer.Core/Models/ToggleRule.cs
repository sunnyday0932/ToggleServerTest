namespace ToggleServer.Core.Models;

public class ToggleRule
{
    public string Name { get; set; } = string.Empty; // 規則名稱 (例如: "20% 灰度發布")
    public List<ToggleCondition> Conditions { get; set; } = new(); // AND 邏輯
    public bool Serve { get; set; } // 若條件皆符合，要回傳的狀態 (通常為 true)
}
