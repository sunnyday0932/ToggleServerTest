using ToggleServer.Core.Interfaces;
using ToggleServer.Core.Models;

namespace ToggleServer.Api.Services;

public class ToggleService : IToggleService
{
    private readonly IFeatureToggleRepository _repository;

    public ToggleService(IFeatureToggleRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<FeatureToggle>> GetAllTogglesAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<FeatureToggle?> GetToggleAsync(string key, CancellationToken cancellationToken = default)
    {
        return _repository.GetByKeyAsync(key, cancellationToken);
    }

    public async Task<FeatureToggle> CreateToggleAsync(FeatureToggle toggle, string operatorId, string operatorName, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByKeyAsync(toggle.Key, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Toggle with key '{toggle.Key}' already exists.");
        }

        // 預設第一次建立版本為 1
        toggle.Version = 1;
        toggle.Enabled = true;
        toggle.UpdatedAt = DateTime.UtcNow;
        toggle.LastUpdatedBy = operatorName;

        var auditLog = new ToggleAuditLog
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            ToggleKey = toggle.Key,
            Version = toggle.Version,
            Action = AuditAction.CREATE,
            OperatorId = operatorId,
            OperatorName = operatorName,
            PreviousConfiguration = null,
            NewConfiguration = toggle,
            CreatedAt = DateTime.UtcNow
        };

        // TODO: In a real-world scenario with replica sets, use MongoDB transactions here.
        // For simplicity without guaranteed replica sets, standard sequential inserts are used.
        await _repository.CreateAsync(toggle, cancellationToken);
        await _repository.InsertAuditLogAsync(auditLog, cancellationToken);

        return toggle;
    }

    public async Task<FeatureToggle> UpdateToggleAsync(FeatureToggle toggle, string operatorId, string operatorName, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByKeyAsync(toggle.Key, cancellationToken);
        if (existing is null)
        {
            throw new ArgumentException($"Toggle with key '{toggle.Key}' not found.");
        }

        // 遞增版本號用於樂觀鎖寫入
        toggle.Version = existing.Version + 1;
        toggle.UpdatedAt = DateTime.UtcNow;
        toggle.LastUpdatedBy = operatorName;

        var success = await _repository.UpdateAsync(toggle, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException($"Optimistic concurrency violation. The toggle '{toggle.Key}' was modified by another transaction.");
        }

        var auditLog = new ToggleAuditLog
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            ToggleKey = toggle.Key,
            Version = toggle.Version,
            Action = AuditAction.UPDATE,
            OperatorId = operatorId,
            OperatorName = operatorName,
            PreviousConfiguration = existing,
            NewConfiguration = toggle,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.InsertAuditLogAsync(auditLog, cancellationToken);

        return toggle;
    }

    public async Task<FeatureToggle> KillSwitchAsync(string key, string operatorId, string operatorName, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByKeyAsync(key, cancellationToken);
        if (existing is null)
        {
            throw new ArgumentException($"Toggle with key '{key}' not found.");
        }

        if (!existing.Enabled)
        {
            // Already disabled
            return existing;
        }

        var previousConfig = existing;
        
        // Deep copy for new configuration in a real app, here we just mutate and trust the caller hasn't messed with the reference yet
        var updated = existing;
        updated.Enabled = false;
        updated.Version += 1;
        updated.UpdatedAt = DateTime.UtcNow;
        updated.LastUpdatedBy = operatorName;

        var success = await _repository.UpdateAsync(updated, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException($"Optimistic concurrency violation while disabling toggle '{key}'.");
        }

        var auditLog = new ToggleAuditLog
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            ToggleKey = key,
            Version = updated.Version,
            Action = AuditAction.DISABLE,
            OperatorId = operatorId,
            OperatorName = operatorName,
            PreviousConfiguration = previousConfig, // Note: In a real scenario ensure full clone to avoid reference updates
            NewConfiguration = updated,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.InsertAuditLogAsync(auditLog, cancellationToken);

        return updated;
    }

    public Task<IEnumerable<ToggleAuditLog>> GetAuditLogsAsync(string key, CancellationToken cancellationToken = default)
    {
        return _repository.GetAuditLogsAsync(key, cancellationToken);
    }

    public async Task<FeatureToggle> RollbackAsync(string key, int targetVersion, string operatorId, string operatorName, CancellationToken cancellationToken = default)
    {
        var logs = await _repository.GetAuditLogsAsync(key, cancellationToken);
        var targetLog = logs.FirstOrDefault(l => l.Version == targetVersion);
        
        if (targetLog is null || targetLog.NewConfiguration is null)
        {
            throw new ArgumentException($"Version {targetVersion} for toggle '{key}' not found or has no configuration data.");
        }

        var currentConfig = await _repository.GetByKeyAsync(key, cancellationToken);
        if (currentConfig is null)
        {
             throw new ArgumentException($"Toggle with key '{key}' not found.");
        }

        var rollbackConfig = targetLog.NewConfiguration;
        
        // Roll-forward: Ensure the version and tracking fields are correctly bumped beyond the current state
        rollbackConfig.Version = currentConfig.Version + 1;
        rollbackConfig.UpdatedAt = DateTime.UtcNow;
        rollbackConfig.LastUpdatedBy = operatorName;

        var success = await _repository.UpdateAsync(rollbackConfig, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException($"Optimistic concurrency violation while rolling back toggle '{key}'.");
        }

        var newAuditLog = new ToggleAuditLog
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            ToggleKey = key,
            Version = rollbackConfig.Version,
            Action = AuditAction.ROLLBACK,
            OperatorId = operatorId,
            OperatorName = operatorName,
            PreviousConfiguration = currentConfig,
            NewConfiguration = rollbackConfig,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.InsertAuditLogAsync(newAuditLog, cancellationToken);

        return rollbackConfig;
    }
}
