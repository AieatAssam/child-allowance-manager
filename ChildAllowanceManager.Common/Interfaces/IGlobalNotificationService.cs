namespace ChildAllowanceManager.Common.Interfaces;

public interface IGlobalNotificationService
{
    public event EventHandler<ChildStateChangedEventArgs> ChildStateChanged;

    public void OnChildStateChanged(string childId, string tenantId, string notificationMessage);


    public class ChildStateChangedEventArgs : EventArgs
    {
        public string TenantId { get; set; } = string.Empty;
        public string ChildId { get; set; } = string.Empty;
        public string NotificationMessage { get; set; } = string.Empty;
    }
}
