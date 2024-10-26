using ChildAllowanceManager.Common.Interfaces;

namespace ChildAllowanceManager.Services;

public class TenantNotificationService: ITenantNotificationService, IDisposable
{
    public TenantNotificationService(
        ICurrentContextService currentContextService,
        IGlobalNotificationService globalNotificationService,
        ILogger<TenantNotificationService> logger
    )
    {
        _currentContextService = currentContextService;
        _globalNotificationService = globalNotificationService;
        _logger = logger;
        _globalNotificationService.ChildStateChanged += OnChildStateChanged;
    }
    
    private readonly ICurrentContextService _currentContextService;
    private readonly IGlobalNotificationService _globalNotificationService;
    private readonly ILogger<TenantNotificationService> _logger;
    
    public event EventHandler<IGlobalNotificationService.ChildStateChangedEventArgs>? ChildStateChanged;
    
    private void OnChildStateChanged(object? sender, IGlobalNotificationService.ChildStateChangedEventArgs e)
    {
        OnChildStateChanged(e.ChildId, e.TenantId, e.NotificationMessage);
    }
    
    public void OnChildStateChanged(string childId, string tenantId, string notificationMessage)
    {
        if (tenantId == _currentContextService.GetCurrentTenant())
        {
            ChildStateChanged?.Invoke(this, new IGlobalNotificationService.ChildStateChangedEventArgs
            {
                ChildId = childId,
                TenantId = tenantId,
                NotificationMessage = notificationMessage
            });
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _globalNotificationService.ChildStateChanged -= OnChildStateChanged;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}