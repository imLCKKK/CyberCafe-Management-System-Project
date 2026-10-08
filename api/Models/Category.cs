using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("categories")]
public class Category
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string CategoryName { get; set; } = string.Empty;

    [Column(TypeName = "tinyint(1)")]
    public bool IsActive { get; set; } = true;

    // ----- Navigation: one-to-many -----
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
