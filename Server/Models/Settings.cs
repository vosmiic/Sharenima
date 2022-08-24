using Sharenima.Shared;

namespace Sharenima.Server.Models;

public class Settings : Base {
    public SettingKey Key { get; set; }
    public string Value { get; set; }
}

public enum SettingKey {
    DownloadLocation = 0
}