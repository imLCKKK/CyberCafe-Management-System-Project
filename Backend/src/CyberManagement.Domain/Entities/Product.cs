namespace CyberManagement.Domain.Entities;

public class Product
{
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    public string ProductName { get; set; } = null!;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;

    public Category Category { get; set; } = null!;
}
