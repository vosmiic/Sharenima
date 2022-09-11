namespace Sharenima.Shared; 

public class LimitedUser {
    public string Username { get; set; }
    public List<Permissions.Permission> Permissions { get; set; }
}