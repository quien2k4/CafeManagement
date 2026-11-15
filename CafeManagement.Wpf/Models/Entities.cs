using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeManagement.Wpf.Models;

[Table("Branches")]
public class Branch
{
    [Key]
    public int BranchId { get; set; }

    [Required]
    [MaxLength(20)]
    public string BranchCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string BranchName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("ProductCategories")]
public class ProductCategory
{
    [Key]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(20)]
    public string CategoryCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public List<Product> Products { get; set; } = [];
}

[Table("Products")]
public class Product
{
    [Key]
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    [Required]
    [MaxLength(30)]
    public string ProductCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BasePrice { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(CategoryId))]
    public ProductCategory? Category { get; set; }

    public List<ProductVariant> Variants { get; set; } = [];
}

[Table("ProductVariants")]
public class ProductVariant
{
    [Key]
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SkuCode { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Size { get; set; } = "M"; // S, M, L

    [MaxLength(10)]
    public string Temperature { get; set; } = "ICED"; // ICED, HOT

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraPrice { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }
}

[Table("Toppings")]
public class Topping
{
    [Key]
    public int ToppingId { get; set; }

    [Required]
    [MaxLength(20)]
    public string ToppingCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ToppingName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("Ingredients")]
public class Ingredient
{
    [Key]
    public int IngredientId { get; set; }

    [Required]
    [MaxLength(30)]
    public string IngredientCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string IngredientName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string BaseUom { get; set; } = "g";

    [Column(TypeName = "decimal(12,3)")]
    public decimal MinStockAlert { get; set; } = 0;

    [MaxLength(255)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("BranchInventory")]
public class BranchInventory
{
    [Key]
    public int BranchInventoryId { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal QuantityOnHand { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal ReservedQuantity { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(BranchId))]
    public Branch? Branch { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient? Ingredient { get; set; }
}

[Table("Recipes")]
public class Recipe
{
    [Key]
    public int RecipeId { get; set; }
    public int VariantId { get; set; }
    public string? Description { get; set; }

    [ForeignKey(nameof(VariantId))]
    public ProductVariant? Variant { get; set; }

    public List<RecipeVersion> Versions { get; set; } = [];
}

[Table("RecipeVersions")]
public class RecipeVersion
{
    [Key]
    public int RecipeVersionId { get; set; }
    public int RecipeId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; } = true;
    public string? Note { get; set; }

    [ForeignKey(nameof(RecipeId))]
    public Recipe? Recipe { get; set; }

    public List<RecipeDetail> Details { get; set; } = [];
}

[Table("RecipeDetails")]
public class RecipeDetail
{
    [Key]
    public int RecipeDetailId { get; set; }
    public int RecipeVersionId { get; set; }
    public int IngredientId { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    public string Uom { get; set; } = "g";

    [ForeignKey(nameof(RecipeVersionId))]
    public RecipeVersion? RecipeVersion { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient? Ingredient { get; set; }
}

[Table("InventoryTransactions")]
public class InventoryTransaction
{
    [Key]
    public long TransactionId { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }

    [MaxLength(30)]
    public string TransactionType { get; set; } = "SALE_CONSUMPTION"; // GOODS_RECEIPT, SALE_CONSUMPTION, WASTAGE

    [Column(TypeName = "decimal(12,3)")]
    public decimal QuantityChange { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal BalanceAfter { get; set; }

    [MaxLength(30)]
    public string ReferenceType { get; set; } = "ORDER";

    [MaxLength(50)]
    public string ReferenceId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; } = 1;

    [ForeignKey(nameof(IngredientId))]
    public Ingredient? Ingredient { get; set; }
}

[Table("Orders")]
public class Order
{
    [Key]
    public long OrderId { get; set; }

    [Required]
    [MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    public int BranchId { get; set; }
    public int CashierUserId { get; set; }

    [MaxLength(20)]
    public string OrderType { get; set; } = "TAKE_AWAY"; // DINE_IN, TAKE_AWAY

    [MaxLength(20)]
    public string OrderStatus { get; set; } = "SUBMITTED"; 

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalAmount { get; set; }

    [MaxLength(255)]
    public string? Note { get; set; }

    public DateTime? HeldAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(BranchId))]
    public Branch? Branch { get; set; }

    public List<OrderItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

[Table("OrderItems")]
public class OrderItem
{
    [Key]
    public long OrderItemId { get; set; }

    public long OrderId { get; set; }
    public int VariantId { get; set; }
    public int? RecipeVersionId { get; set; }

    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [MaxLength(10)]
    public string SugarLevel { get; set; } = "100%";

    [MaxLength(10)]
    public string IceLevel { get; set; } = "100%";

    [MaxLength(20)]
    public string ItemStatus { get; set; } = "QUEUED"; 

    [MaxLength(255)]
    public string? Note { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [ForeignKey(nameof(VariantId))]
    public ProductVariant? Variant { get; set; }

    public List<OrderItemTopping> Toppings { get; set; } = [];
}

[Table("OrderItemToppings")]
public class OrderItemTopping
{
    [Key]
    public long OrderItemToppingId { get; set; }

    public long OrderItemId { get; set; }
    public int ToppingId { get; set; }
    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [ForeignKey(nameof(OrderItemId))]
    public OrderItem? OrderItem { get; set; }

    [ForeignKey(nameof(ToppingId))]
    public Topping? Topping { get; set; }
}

[Table("Payments")]
public class Payment
{
    [Key]
    public long PaymentId { get; set; }

    public long OrderId { get; set; }

    [MaxLength(20)]
    public string PaymentMethod { get; set; } = "CASH"; // CASH, VIETQR, MOMO, CARD

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "SUCCESS";

    [MaxLength(100)]
    public string? TransactionCode { get; set; }

    public int CashierUserId { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

[Table("OrderReceipts")]
public class OrderReceipt
{
    [Key]
    public long ReceiptId { get; set; }

    [Required]
    [MaxLength(30)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public long OrderId { get; set; }
    public int BranchId { get; set; }
    public int CashierUserId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ChangeAmount { get; set; }

    public string? ReceiptSnapshotJson { get; set; }
}

[Table("UserAccounts")]
public class UserAccount
{
    [Key]
    public int UserId { get; set; }

    public int EmployeeId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string PasswordSalt { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
}

[Table("Employees")]
public class Employee
{
    [Key]
    public int EmployeeId { get; set; }

    [Required]
    [MaxLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = "0909000000";

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? CitizenId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    public DateTime HireDate { get; set; } = DateTime.UtcNow;
}

[Table("Roles")]
public class Role
{
    [Key]
    public int RoleId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RoleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }
}

[Table("UserRoleBranchAssignments")]
public class UserRoleBranchAssignment
{
    [Key]
    public int AssignmentId { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public int BranchId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public UserAccount? User { get; set; }

    [ForeignKey(nameof(RoleId))]
    public Role? Role { get; set; }

    [ForeignKey(nameof(BranchId))]
    public Branch? Branch { get; set; }
}
