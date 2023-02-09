using MatBlazor;
using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode;

public partial class ChatMenu : ComponentBase {
    [Inject] protected StreamService StreamService { get; set; }
    [Parameter] public string Username { get; set; }
    protected ForwardRef buttonForwardRef = new ForwardRef();
    protected MatMenu Menu { get; set; }

    protected void OnMenuButtonClick() {
        Menu.OpenAsync();
    }

    protected async Task WatchStreamClicked() {
        await StreamService.CallWatchStream(Username);
    }
}