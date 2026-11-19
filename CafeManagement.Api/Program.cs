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
    ");
}
catch (Exception ex)
{
    Console.WriteLine($"[DB INIT] Lỗi kiểm tra cấu trúc bảng: {ex.Message}");
}

app.Run();
