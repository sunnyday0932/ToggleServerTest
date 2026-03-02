using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ToggleServer.Core.Interfaces;
using ToggleServer.Core.Models;

namespace ToggleServer.Infrastructure.Data;

public class MongoFeatureToggleRepository : IFeatureToggleRepository
{
    private readonly IMongoCollection<FeatureToggle> _togglesCollection;
    private readonly IMongoCollection<ToggleAuditLog> _auditLogsCollection;

    public MongoFeatureToggleRepository(IOptions<MongoDbSettings> settings, IMongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
        _togglesCollection = database.GetCollection<FeatureToggle>("FeatureToggles");
        _auditLogsCollection = database.GetCollection<ToggleAuditLog>("ToggleAuditLogs");
    }

    public async Task<IEnumerable<FeatureToggle>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _togglesCollection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<FeatureToggle?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _togglesCollection.Find(x => x.Key == key).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateAsync(FeatureToggle toggle, CancellationToken cancellationToken = default)
    {
        await _togglesCollection.InsertOneAsync(toggle, new InsertOneOptions(), cancellationToken);
    }

    public async Task<bool> UpdateAsync(FeatureToggle toggle, CancellationToken cancellationToken = default)
    {
        // 實作樂觀鎖 (Optimistic Locking)
        // 條件：Key 必須符合，且目前資料庫中的 Version 必須等於傳進來更新「前」的 Version - 1
        // (因為進來前 Version 應該已經被 Service 加 1 了)
        var filter = Builders<FeatureToggle>.Filter.And(
            Builders<FeatureToggle>.Filter.Eq(x => x.Key, toggle.Key),
            Builders<FeatureToggle>.Filter.Eq(x => x.Version, toggle.Version - 1)
        );

        var result = await _togglesCollection.ReplaceOneAsync(filter, toggle, new ReplaceOptions(), cancellationToken);
        
        // 如果有改動到資料，代表樂觀鎖匹配成功
        return result.ModifiedCount > 0;
    }

    public async Task<IEnumerable<ToggleAuditLog>> GetAuditLogsAsync(string toggleKey, CancellationToken cancellationToken = default)
    {
        return await _auditLogsCollection.Find(x => x.ToggleKey == toggleKey)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAuditLogAsync(ToggleAuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await _auditLogsCollection.InsertOneAsync(auditLog, new InsertOneOptions(), cancellationToken);
    }
}
