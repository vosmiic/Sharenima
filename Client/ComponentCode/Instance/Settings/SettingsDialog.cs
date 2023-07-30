using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode.Settings; 

public partial class SettingsDialog : ComponentBase {
    protected bool SettingsIsOpen = false;
    [Parameter] public Guid InstanceId { get; set; }

    protected void SettingsButtonClicked() =>
        SettingsIsOpen = true;
}