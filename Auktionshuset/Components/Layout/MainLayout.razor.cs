using Microsoft.AspNetCore.Components;

namespace Auktionshuset.Components.Layout;

public partial class MainLayout
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    private bool IsAdministrationSection =>
        NavigationManager
            .ToBaseRelativePath(NavigationManager.Uri)
            .StartsWith("admin", StringComparison.OrdinalIgnoreCase);
}
