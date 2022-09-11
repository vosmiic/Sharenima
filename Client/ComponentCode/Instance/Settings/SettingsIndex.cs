using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode; 

public partial class SettingsIndex : ComponentBase {
    [Parameter] public string InstanceName { get; set; }
    protected Type? Page { get; set; }
    protected Dictionary<string, object> Parameters = new();

    protected override void OnParametersSet() {
        Parameters.Add("InstanceId", InstanceName);
    }

    protected void ChangeSettingsPage(string pageToChangeTo) {
        Page = Type.GetType($"Sharenima.Client.Pages.Instance.SettingsPages.{pageToChangeTo}");
    }
}