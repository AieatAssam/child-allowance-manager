using ChildAllowanceManager.Common.Interfaces;

namespace ChildAllowanceManager.Services;

public class GlobalNotificationService: IGlobalNotificationService
{
    public event EventHandler<IGlobalNotificationService.ChildStateChangedEventArgs>? ChildStateChanged;

    public virtual void OnChildStateChanged(string childId, string tenantId, string notificationMessage)
    {
        var args = new IGlobalNotificationService.ChildStateChangedEventArgs
        {
            ChildId = childId,
            TenantId = tenantId,
            NotificationMessage = notificationMessage
        };
        ChildStateChanged?.Invoke(this, args);
    }
}