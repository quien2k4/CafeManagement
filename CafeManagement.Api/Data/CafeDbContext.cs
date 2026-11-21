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
    public DbSet<ToppingRecipeDetail> ToppingRecipeDetails => Set<ToppingRecipeDetail>();
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
    public DbSet<OrderRefund> OrderRefunds => Set<OrderRefund>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferDetail> StockTransferDetails => Set<StockTransferDetail>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierIngredient> SupplierIngredients => Set<SupplierIngredient>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptDetail> GoodsReceiptDetails => Set<GoodsReceiptDetail>();

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

        modelBuilder.Entity<ToppingRecipeDetail>()
            .HasOne(trd => trd.Topping)
            .WithMany(t => t.RecipeDetails)
            .HasForeignKey(trd => trd.ToppingId);

        modelBuilder.Entity<ToppingRecipeDetail>()
            .HasOne(trd => trd.Ingredient)
            .WithMany()
            .HasForeignKey(trd => trd.IngredientId);

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

        modelBuilder.Entity<OrderRefund>()
            .HasOne(r => r.Order)
            .WithMany(o => o.Refunds)
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(t => t.FromBranch)
            .WithMany()
            .HasForeignKey(t => t.FromBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(t => t.ToBranch)
            .WithMany()
            .HasForeignKey(t => t.ToBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransferDetail>()
            .HasOne(d => d.StockTransfer)
            .WithMany(t => t.Details)
            .HasForeignKey(d => d.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StockTransferDetail>()
            .HasOne(d => d.Ingredient)
            .WithMany()
            .HasForeignKey(d => d.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Procurement & QC Relationships
        modelBuilder.Entity<SupplierIngredient>()
            .HasOne(si => si.Supplier)
            .WithMany(s => s.SupplierIngredients)
            .HasForeignKey(si => si.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupplierIngredient>()
            .HasOne(si => si.Ingredient)
            .WithMany()
            .HasForeignKey(si => si.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(po => po.Supplier)
            .WithMany()
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(po => po.Branch)
            .WithMany()
            .HasForeignKey(po => po.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderDetail>()
            .HasOne(pod => pod.PurchaseOrder)
            .WithMany(po => po.Details)
            .HasForeignKey(pod => pod.PoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseOrderDetail>()
            .HasOne(pod => pod.Ingredient)
            .WithMany()
            .HasForeignKey(pod => pod.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(gr => gr.PurchaseOrder)
            .WithMany(po => po.GoodsReceipts)
            .HasForeignKey(gr => gr.PoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(gr => gr.Branch)
            .WithMany()
            .HasForeignKey(gr => gr.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceiptDetail>()
            .HasOne(grd => grd.GoodsReceipt)
            .WithMany(gr => gr.Details)
            .HasForeignKey(grd => grd.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoodsReceiptDetail>()
            .HasOne(grd => grd.Ingredient)
            .WithMany()
            .HasForeignKey(grd => grd.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
