using ToggleServer.Core.Models;

namespace ToggleServer.Core.Interfaces;

public interface IFeatureToggleRepository
{
    Task<IEnumerable<FeatureToggle>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FeatureToggle?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    
    // 預期在新增時，同時寫入 AuditLog (或在 Service 層透過 Transaction 處理)
    Task CreateAsync(FeatureToggle toggle, CancellationToken cancellationToken = default);
    
    // 必須實作樂觀鎖 (檢查 Version)，若更新成功回傳 true，若發生併發衝突回傳 false，同時寫入 AuditLog
    Task<bool> UpdateAsync(FeatureToggle toggle, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ToggleAuditLog>> GetAuditLogsAsync(string toggleKey, CancellationToken cancellationToken = default);
    Task InsertAuditLogAsync(ToggleAuditLog auditLog, CancellationToken cancellationToken = default);
}
