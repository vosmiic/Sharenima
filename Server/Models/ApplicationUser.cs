using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Identity;

namespace Sharenima.Server.Models;

public class ApplicationUser : IdentityUser {
    private ICollection<AdvancedRole> _advancedRoles;
    //public virtual ICollection<AdvancedRole> Roles { get; set; }
    public virtual ICollection<AdvancedRole> Roles {
        get => _advancedRoles ?? (_advancedRoles = new List<AdvancedRole>());
        protected set => _advancedRoles = value;
    }
    
    public string? StreamKey { get; set; }
}
