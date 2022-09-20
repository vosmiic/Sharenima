namespace Sharenima.Shared;

public class InstancePermissions {
    public List<Permissions.Permission>? Permissions { get; set; }
    public List<UserPermissions>? UserPermissions { get; set; }
}
public class UserPermissions {
    public string Username { get; set; }
    public List<Permissions.Permission> Permissions { get; set; }
}