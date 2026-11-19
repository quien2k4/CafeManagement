using Microsoft.EntityFrameworkCore;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Data;

public class CafeDbContext : DbContext
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<BranchInventory> BranchInventories => Set<BranchInventory>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeVersion> RecipeVersions => Set<RecipeVersion>();
    public DbSet<RecipeDetail> RecipeDetails => Set<RecipeDetail>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemTopping> OrderItemToppings => Set<OrderItemTopping>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderReceipt> OrderReceipts => Set<OrderReceipt>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleBranchAssignment> UserRoleBranchAssignments => Set<UserRoleBranchAssignment>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    public CafeDbContext(DbContextOptions<CafeDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId);

        modelBuilder.Entity<ProductVariant>()
            .HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId);

        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId);

        modelBuilder.Entity<OrderItemTopping>()
            .HasOne(t => t.OrderItem)
            .WithMany(i => i.Toppings)
            .HasForeignKey(t => t.OrderItemId);

        modelBuilder.Entity<BranchInventory>()
            .HasOne(bi => bi.Ingredient)
            .WithMany()
            .HasForeignKey(bi => bi.IngredientId);

        modelBuilder.Entity<RecipeDetail>()
            .HasOne(rd => rd.Ingredient)
            .WithMany()
            .HasForeignKey(rd => rd.IngredientId);

        modelBuilder.Entity<InventoryTransaction>()
            .HasOne(it => it.Ingredient)
            .WithMany()
            .HasForeignKey(it => it.IngredientId);
    }
}
