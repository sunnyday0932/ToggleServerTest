namespace ToggleServer.Core.Models;

public enum AuditAction
{
    CREATE,
    UPDATE,
    ENABLE,    // 透過緊急按鈕開啟
    DISABLE,   // 透過緊急按鈕關閉
    ROLLBACK
}
