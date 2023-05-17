using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode;

public partial class SettingsIndex : ComponentBase {
    [Parameter] public Guid InstanceId { get; set; }
    protected Type? Page { get; set; }
    protected Dictionary<string, object> Parameters = new();

    protected override void OnParametersSet() {
        Parameters.Add("InstanceId", InstanceId);
    }

    protected void ChangeSettingsPage(string pageToChangeTo) {
        Page = Type.GetType($"Sharenima.Client.Pages.Instance.SettingsPages.{pageToChangeTo}");
    }
}