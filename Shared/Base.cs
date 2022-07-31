using System.ComponentModel.DataAnnotations;

namespace Sharenima.Shared; 

public class Base {
    [Key]
    public Guid Id { get; set; }
}