using ToggleServer.Core.Models;

namespace ToggleServer.Core.Interfaces;

public interface IToggleService
{
    Task<IEnumerable<FeatureToggle>> GetAllTogglesAsync(CancellationToken cancellationToken = default);
    Task<FeatureToggle?> GetToggleAsync(string key, CancellationToken cancellationToken = default);
    
    Task<FeatureToggle> CreateToggleAsync(FeatureToggle toggle, string operatorId, string operatorName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新 Toggle
    /// </summary>
    /// <returns>更新後的 Toggle，若發生版本衝突 (Optimistic Concurrency) 則丟出異常或回傳 null/錯誤代表</returns>
    Task<FeatureToggle> UpdateToggleAsync(FeatureToggle toggle, string operatorId, string operatorName, CancellationToken cancellationToken = default);
    
    Task<FeatureToggle> KillSwitchAsync(string key, string operatorId, string operatorName, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ToggleAuditLog>> GetAuditLogsAsync(string key, CancellationToken cancellationToken = default);
    
    Task<FeatureToggle> RollbackAsync(string key, int targetVersion, string operatorId, string operatorName, CancellationToken cancellationToken = default);
}
