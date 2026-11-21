using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Authentication;
using CafeManagement.Api.Services.Branches;
using CafeManagement.Api.Services.Categories;
using CafeManagement.Api.Services.Customers;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Api.Services.Orders;
using CafeManagement.Api.Services.Products;
using CafeManagement.Api.Services.Promotions;
using CafeManagement.Api.Services.Recipes;
using CafeManagement.Api.Services.WorkShifts;
using CafeManagement.Api.Services.KDS;
using CafeManagement.Api.Services.Inventory;
using CafeManagement.Api.Services.Employees;
using CafeManagement.Api.Services.Reports;
using CafeManagement.Api.Services.Procurement;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext
builder.Services.AddDbContext<CafeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Business Services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IBranchesService, BranchService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IKdsService, KdsService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IProcurementService, ProcurementService>();

// Cấu hình Controllers và tuần tự hóa JSON tránh vòng lặp tham chiếu
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// Cấu hình CORS cho Client kết nối nội bộ
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "Healthy", service = "CafeManagement.Api", time = DateTime.UtcNow }));

// Đảm bảo cấu trúc CSDL mở rộng luôn sẵn sàng khi API khởi động
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
    db.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderRefunds')
        BEGIN
            CREATE TABLE dbo.OrderRefunds (
                RefundId BIGINT IDENTITY(1,1) PRIMARY KEY,
                OrderId BIGINT NOT NULL,
                PaymentId BIGINT NULL,
                RefundAmount DECIMAL(18,2) NOT NULL,
                RefundMethod VARCHAR(20) NOT NULL DEFAULT 'CASH',
                RefundReason NVARCHAR(255) NOT NULL,
                Status VARCHAR(20) NOT NULL DEFAULT 'COMPLETED',
                ProcessedByUserId INT NOT NULL DEFAULT 1,
                ProcessedByName NVARCHAR(100) NOT NULL DEFAULT '',
                RefundedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_OrderRefunds_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId) ON DELETE CASCADE
            );
        END

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ToppingRecipeDetails')
        BEGIN
            CREATE TABLE dbo.ToppingRecipeDetails (
                ToppingRecipeDetailId INT IDENTITY(1,1) PRIMARY KEY,
                ToppingId INT NOT NULL,
                IngredientId INT NOT NULL,
                Quantity DECIMAL(12,3) NOT NULL,
                Uom NVARCHAR(20) NOT NULL DEFAULT 'g',
                CONSTRAINT FK_TRD_Toppings FOREIGN KEY (ToppingId) REFERENCES dbo.Toppings(ToppingId) ON DELETE CASCADE,
                CONSTRAINT FK_TRD_Ingredients FOREIGN KEY (IngredientId) REFERENCES dbo.Ingredients(IngredientId)
            );
        END

        -- Tự động dọn dẹp cấu trúc bảng điều chuyển cũ nếu thiếu cột chuẩn
        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfers')
           AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.StockTransfers') AND name = 'FromBranchId')
        BEGIN
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferDetails') DROP TABLE dbo.StockTransferDetails;
            DROP TABLE dbo.StockTransfers;
        END

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferDetails')
           AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.StockTransferDetails') AND name = 'DetailId')
        BEGIN
            DROP TABLE dbo.StockTransferDetails;
        END

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfers')
        BEGIN
            CREATE TABLE dbo.StockTransfers (
                TransferId INT IDENTITY(1,1) PRIMARY KEY,
                TransferCode NVARCHAR(30) NOT NULL UNIQUE,
                FromBranchId INT NOT NULL,
                ToBranchId INT NOT NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT 'REQUESTED',
                RequestedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
                RequestedByUserId INT NOT NULL DEFAULT 1,
                ApprovedDate DATETIME NULL,
                ApprovedByUserId INT NULL,
                DispatchedDate DATETIME NULL,
                DispatchedByUserId INT NULL,
                ReceivedDate DATETIME NULL,
                ReceivedByUserId INT NULL,
                RequestNote NVARCHAR(255) NULL,
                ApprovalNote NVARCHAR(255) NULL,
                ReconciliationNote NVARCHAR(500) NULL,
                CONSTRAINT FK_StockTransfers_FromBranch FOREIGN KEY (FromBranchId) REFERENCES dbo.Branches(BranchId),
                CONSTRAINT FK_StockTransfers_ToBranch FOREIGN KEY (ToBranchId) REFERENCES dbo.Branches(BranchId)
            );
        END

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferDetails')
        BEGIN
            CREATE TABLE dbo.StockTransferDetails (
                DetailId INT IDENTITY(1,1) PRIMARY KEY,
                TransferId INT NOT NULL,
                IngredientId INT NOT NULL,
                RequestedQuantity DECIMAL(12,3) NOT NULL,
                ApprovedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                DispatchedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                ReceivedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                DiscrepancyQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                DiscrepancyReason NVARCHAR(255) NULL,
                Uom NVARCHAR(20) NOT NULL DEFAULT 'g',
                CONSTRAINT FK_STD_Transfers FOREIGN KEY (TransferId) REFERENCES dbo.StockTransfers(TransferId) ON DELETE CASCADE,
                CONSTRAINT FK_STD_Ingredients FOREIGN KEY (IngredientId) REFERENCES dbo.Ingredients(IngredientId)
            );
        END

        -- Tự động bổ sung các cột còn thiếu cho bảng Suppliers nếu đã có sẵn từ trước
        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
        BEGIN
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'SupplierCode')
                ALTER TABLE dbo.Suppliers ADD SupplierCode NVARCHAR(30) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'TaxCode')
                ALTER TABLE dbo.Suppliers ADD TaxCode NVARCHAR(50) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'Note')
                ALTER TABLE dbo.Suppliers ADD Note NVARCHAR(500) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'IsActive')
                ALTER TABLE dbo.Suppliers ADD IsActive BIT NOT NULL DEFAULT 1;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'CreatedAt')
                ALTER TABLE dbo.Suppliers ADD CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE();
        END

        -- Tự động dọn dẹp bảng PurchaseOrders cũ nếu thiếu cột PoId chuẩn
        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
           AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseOrders') AND name = 'PoId')
        BEGIN
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceiptDetails') DROP TABLE dbo.GoodsReceiptDetails;
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceipts') DROP TABLE dbo.GoodsReceipts;
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderDetails') DROP TABLE dbo.PurchaseOrderDetails;
            DROP TABLE dbo.PurchaseOrders;
        END

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderDetails')
           AND (NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseOrderDetails') AND name = 'PoDetailId')
                OR NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseOrderDetails') AND name = 'PoId'))
        BEGIN
            DROP TABLE dbo.PurchaseOrderDetails;
        END

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceipts')
           AND (NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GoodsReceipts') AND name = 'ReceiptId')
                OR NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GoodsReceipts') AND name = 'PoId'))
        BEGIN
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceiptDetails') DROP TABLE dbo.GoodsReceiptDetails;
            DROP TABLE dbo.GoodsReceipts;
        END

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceiptDetails')
           AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GoodsReceiptDetails') AND name = 'ReceiptDetailId')
        BEGIN
            DROP TABLE dbo.GoodsReceiptDetails;
        END

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SupplierIngredients')
           AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SupplierIngredients') AND name = 'SupplierIngredientId')
        BEGIN
            DROP TABLE dbo.SupplierIngredients;
        END
    ");
}
catch (Exception ex)
{
    Console.WriteLine($"[DB INIT] Lỗi kiểm tra cấu trúc bảng: {ex.Message}");
}

app.Run();
