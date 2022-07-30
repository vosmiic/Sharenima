using System.ComponentModel.DataAnnotations;

namespace Sharenima.Server.Models; 

public class Base {
    [Key]
    public Guid Id { get; set; }
}