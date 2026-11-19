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

app.Run();
