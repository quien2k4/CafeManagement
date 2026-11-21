using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Procurement;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Procurement;

public class ProcurementService : IProcurementService
{
    private readonly CafeDbContext _db;
    private static bool _tablesVerified = false;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public ProcurementService(CafeDbContext db)
    {
        _db = db;
    }

    private async Task EnsureProcurementTablesExistAsync()
    {
        if (_tablesVerified) return;
        await _initLock.WaitAsync();
        try
        {
            if (_tablesVerified) return;

            await _db.Database.ExecuteSqlRawAsync(@"
                -- 1. BẢNG NHÀ CUNG CẤP (SUPPLIERS)
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
                BEGIN
                    CREATE TABLE dbo.Suppliers (
                        SupplierId INT IDENTITY(1,1) PRIMARY KEY,
                        SupplierCode NVARCHAR(30) NOT NULL UNIQUE,
                        SupplierName NVARCHAR(150) NOT NULL,
                        ContactPerson NVARCHAR(100) NULL,
                        Phone NVARCHAR(30) NULL,
                        Email NVARCHAR(100) NULL,
                        Address NVARCHAR(255) NULL,
                        TaxCode NVARCHAR(50) NULL,
                        Note NVARCHAR(500) NULL,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE()
                    );
                END
                ELSE
                BEGIN
                    -- Tự động bổ sung các cột còn thiếu nếu bảng đã tồn tại từ trước
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'SupplierCode')
                        ALTER TABLE dbo.Suppliers ADD SupplierCode NVARCHAR(30) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'ContactPerson')
                        ALTER TABLE dbo.Suppliers ADD ContactPerson NVARCHAR(100) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'Email')
                        ALTER TABLE dbo.Suppliers ADD Email NVARCHAR(100) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'Address')
                        ALTER TABLE dbo.Suppliers ADD Address NVARCHAR(255) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'TaxCode')
                        ALTER TABLE dbo.Suppliers ADD TaxCode NVARCHAR(50) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'Note')
                        ALTER TABLE dbo.Suppliers ADD Note NVARCHAR(500) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'IsActive')
                        ALTER TABLE dbo.Suppliers ADD IsActive BIT NOT NULL DEFAULT 1;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Suppliers') AND name = 'CreatedAt')
                        ALTER TABLE dbo.Suppliers ADD CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE();
                END

                -- Chuẩn hóa SupplierCode không bị rỗng
                UPDATE dbo.Suppliers 
                SET SupplierCode = 'NCC-' + RIGHT('000' + CAST(SupplierId AS NVARCHAR(10)), 3)
                WHERE SupplierCode IS NULL OR SupplierCode = '';

                -- ==============================================================
                -- TỰ ĐỘNG DỌN DẸP BẢNG CŨ NẾU THIẾU CỘT KHÓA CHÍNH (POID, DETAILID, RECEIPTID)
                -- ==============================================================
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

                -- 2. BẢNG MẶT HÀNG & BẢNG GIÁ CỦA NCC (SUPPLIERINGREDIENTS)
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SupplierIngredients')
                BEGIN
                    CREATE TABLE dbo.SupplierIngredients (
                        SupplierIngredientId INT IDENTITY(1,1) PRIMARY KEY,
                        SupplierId INT NOT NULL,
                        IngredientId INT NOT NULL,
                        SupplyPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                        PackageUom NVARCHAR(30) NOT NULL DEFAULT 'Gói',
                        ConversionFactor DECIMAL(12,3) NOT NULL DEFAULT 1.0,
                        MinOrderQuantity DECIMAL(12,3) NOT NULL DEFAULT 1.0,
                        LeadTimeDays INT NOT NULL DEFAULT 1,
                        IsPreferred BIT NOT NULL DEFAULT 0,
                        CONSTRAINT FK_SI_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId) ON DELETE CASCADE,
                        CONSTRAINT FK_SI_Ingredient FOREIGN KEY (IngredientId) REFERENCES dbo.Ingredients(IngredientId)
                    );
                END

                -- 3. BẢNG ĐƠN ĐẶT HÀNG MUA (PURCHASEORDERS)
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
                BEGIN
                    CREATE TABLE dbo.PurchaseOrders (
                        PoId INT IDENTITY(1,1) PRIMARY KEY,
                        PoCode NVARCHAR(30) NOT NULL UNIQUE,
                        SupplierId INT NOT NULL,
                        BranchId INT NOT NULL,
                        Status NVARCHAR(30) NOT NULL DEFAULT 'DRAFT',
                        OrderDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        ExpectedDeliveryDate DATETIME NULL,
                        CreatedByUserId INT NOT NULL DEFAULT 1,
                        SubTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
                        TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        Note NVARCHAR(500) NULL,
                        CONSTRAINT FK_PO_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
                        CONSTRAINT FK_PO_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(BranchId)
                    );
                END

                -- 4. BẢNG CHI TIẾT ĐƠN ĐẶT HÀNG MUA (PURCHASEORDERDETAILS)
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderDetails')
                BEGIN
                    CREATE TABLE dbo.PurchaseOrderDetails (
                        PoDetailId INT IDENTITY(1,1) PRIMARY KEY,
                        PoId INT NOT NULL,
                        IngredientId INT NOT NULL,
                        OrderedQuantity DECIMAL(12,3) NOT NULL,
                        ReceivedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        UnitPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                        SubTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
                        Uom NVARCHAR(30) NOT NULL DEFAULT 'g',
                        CONSTRAINT FK_POD_PO FOREIGN KEY (PoId) REFERENCES dbo.PurchaseOrders(PoId) ON DELETE CASCADE,
                        CONSTRAINT FK_POD_Ingredient FOREIGN KEY (IngredientId) REFERENCES dbo.Ingredients(IngredientId)
                    );
                END

                -- 5. BẢNG PHIẾU TIẾP NHẬN HÀNG HÓA (GOODSRECEIPTS)
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceipts')
                BEGIN
                    CREATE TABLE dbo.GoodsReceipts (
                        ReceiptId INT IDENTITY(1,1) PRIMARY KEY,
                        ReceiptCode NVARCHAR(30) NOT NULL UNIQUE,
                        PoId INT NOT NULL,
                        BranchId INT NOT NULL,
                        ReceivedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        ReceivedByUserId INT NOT NULL DEFAULT 1,
                        DeliveryPerson NVARCHAR(100) NULL,
                        InvoiceNumber NVARCHAR(50) NULL,
                        TotalAcceptedAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        Note NVARCHAR(500) NULL,
                        CONSTRAINT FK_GR_PO FOREIGN KEY (PoId) REFERENCES dbo.PurchaseOrders(PoId),
                        CONSTRAINT FK_GR_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(BranchId)
                    );
                END

                -- 6. BẢNG CHI TIẾT TIẾP NHẬN & QC (GOODSRECEIPTDETAILS)
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GoodsReceiptDetails')
                BEGIN
                    CREATE TABLE dbo.GoodsReceiptDetails (
                        ReceiptDetailId INT IDENTITY(1,1) PRIMARY KEY,
                        ReceiptId INT NOT NULL,
                        IngredientId INT NOT NULL,
                        DeliveredQuantity DECIMAL(12,3) NOT NULL,
                        AcceptedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        QuarantinedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        RejectedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        RejectionReason NVARCHAR(255) NULL,
                        UnitPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                        Uom NVARCHAR(30) NOT NULL DEFAULT 'g',
                        CONSTRAINT FK_GRD_Receipt FOREIGN KEY (ReceiptId) REFERENCES dbo.GoodsReceipts(ReceiptId) ON DELETE CASCADE,
                        CONSTRAINT FK_GRD_Ingredient FOREIGN KEY (IngredientId) REFERENCES dbo.Ingredients(IngredientId)
                    );
                END
            ");

            // Tạo nhà cung cấp mẫu nếu bảng Suppliers đang trống
            var count = await _db.Suppliers.CountAsync();
            if (count == 0)
            {
                var s1 = new Supplier
                {
                    SupplierCode = "NCC-CP01",
                    SupplierName = "Công Ty TNHH Cà Phê Cao Nguyên",
                    ContactPerson = "Nguyễn Văn Hùng",
                    Phone = "0901234567",
                    Email = "supply@caonguyen.vn",
                    Address = "Km 12, QL 14, TP. Buôn Ma Thuột, Đắk Lắk",
                    TaxCode = "6001234567",
                    Note = "Cung cấp cà phê nhân, hạt mộc Robusta & Arabica",
                    IsActive = true
                };
                var s2 = new Supplier
                {
                    SupplierCode = "NCC-SL02",
                    SupplierName = "Công Ty Cổ Phần Sữa Tươi Dalat Fresh",
                    ContactPerson = "Trần Thị Mai",
                    Phone = "0918765432",
                    Email = "kinhdoanh@dalatfresh.vn",
                    Address = "25 Đường 3/4, TP. Đà Lạt, Lâm Đồng",
                    TaxCode = "5801987654",
                    Note = "Sữa tươi thanh trùng, sữa đặc, kem béo",
                    IsActive = true
                };
                var s3 = new Supplier
                {
                    SupplierCode = "NCC-BB03",
                    SupplierName = "Công Ty Bao Bì & Phụ Liệu F&B Sài Gòn",
                    ContactPerson = "Lê Hoàng Quân",
                    Phone = "0982334455",
                    Email = "sales@baobifnb.com",
                    Address = "150 Kinh Dương Vương, Q. Bình Tân, TP. HCM",
                    TaxCode = "0312456789",
                    Note = "Ly giấy, ống hút sinh học, màng bọc, túi chữ T",
                    IsActive = true
                };

                _db.Suppliers.AddRange(s1, s2, s3);
                await _db.SaveChangesAsync();

                // Gán mặt hàng cung cấp mẫu nếu có nguyên liệu
                var ingredients = await _db.Ingredients.Take(6).ToListAsync();
                if (ingredients.Count > 0)
                {
                    var siList = new List<SupplierIngredient>();
                    if (ingredients.Count > 0)
                    {
                        siList.Add(new SupplierIngredient
                        {
                            SupplierId = s1.SupplierId,
                            IngredientId = ingredients[0].IngredientId,
                            SupplyPrice = 145000,
                            PackageUom = "Bao 25kg",
                            ConversionFactor = 25000,
                            MinOrderQuantity = 1,
                            LeadTimeDays = 2,
                            IsPreferred = true
                        });
                    }
                    if (ingredients.Count > 1)
                    {
                        siList.Add(new SupplierIngredient
                        {
                            SupplierId = s2.SupplierId,
                            IngredientId = ingredients[1].IngredientId,
                            SupplyPrice = 32000,
                            PackageUom = "Thùng 12 hộp 1L",
                            ConversionFactor = 12000,
                            MinOrderQuantity = 2,
                            LeadTimeDays = 1,
                            IsPreferred = true
                        });
                    }
                    if (siList.Count > 0)
                    {
                        _db.SupplierIngredients.AddRange(siList);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            _tablesVerified = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcurementService] Lỗi khởi tạo bảng mua hàng: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    #region 1. Quản lý Nhà Cung Cấp
    public async Task<List<SupplierDto>> GetSuppliersAsync(bool activeOnly = false)
    {
        await EnsureProcurementTablesExistAsync();

        var query = _db.Suppliers
            .AsNoTracking()
            .Include(s => s.SupplierIngredients)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        var list = await query.OrderBy(s => s.SupplierName).ToListAsync();

        return list.Select(s => new SupplierDto
        {
            SupplierId = s.SupplierId,
            SupplierCode = s.SupplierCode,
            SupplierName = s.SupplierName,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            TaxCode = s.TaxCode,
            Note = s.Note,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            ItemCount = s.SupplierIngredients.Count
        }).ToList();
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(int supplierId)
    {
        await EnsureProcurementTablesExistAsync();

        var s = await _db.Suppliers
            .AsNoTracking()
            .Include(s => s.SupplierIngredients)
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

        if (s == null) return null;

        return new SupplierDto
        {
            SupplierId = s.SupplierId,
            SupplierCode = s.SupplierCode,
            SupplierName = s.SupplierName,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            TaxCode = s.TaxCode,
            Note = s.Note,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            ItemCount = s.SupplierIngredients.Count
        };
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateOrUpdateSupplierRequest req)
    {
        await EnsureProcurementTablesExistAsync();

        string code = string.IsNullOrWhiteSpace(req.SupplierCode)
            ? $"NCC-{DateTime.UtcNow:yyMMddHHmm}"
            : req.SupplierCode.Trim().ToUpper();

        var supplier = new Supplier
        {
            SupplierCode = code,
            SupplierName = req.SupplierName.Trim(),
            ContactPerson = req.ContactPerson?.Trim(),
            Phone = req.Phone?.Trim(),
            Email = req.Email?.Trim(),
            Address = req.Address?.Trim(),
            TaxCode = req.TaxCode?.Trim(),
            Note = req.Note?.Trim(),
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        return new SupplierDto
        {
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxCode = supplier.TaxCode,
            Note = supplier.Note,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            ItemCount = 0
        };
    }

    public async Task<SupplierDto?> UpdateSupplierAsync(int supplierId, CreateOrUpdateSupplierRequest req)
    {
        await EnsureProcurementTablesExistAsync();

        var supplier = await _db.Suppliers.FindAsync(supplierId);
        if (supplier == null) return null;

        if (!string.IsNullOrWhiteSpace(req.SupplierCode))
            supplier.SupplierCode = req.SupplierCode.Trim().ToUpper();

        supplier.SupplierName = req.SupplierName.Trim();
        supplier.ContactPerson = req.ContactPerson?.Trim();
        supplier.Phone = req.Phone?.Trim();
        supplier.Email = req.Email?.Trim();
        supplier.Address = req.Address?.Trim();
        supplier.TaxCode = req.TaxCode?.Trim();
        supplier.Note = req.Note?.Trim();
        supplier.IsActive = req.IsActive;

        await _db.SaveChangesAsync();

        int count = await _db.SupplierIngredients.CountAsync(si => si.SupplierId == supplierId);

        return new SupplierDto
        {
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxCode = supplier.TaxCode,
            Note = supplier.Note,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            ItemCount = count
        };
    }

    public async Task<bool> DeleteSupplierAsync(int supplierId)
    {
        await EnsureProcurementTablesExistAsync();

        var supplier = await _db.Suppliers.FindAsync(supplierId);
        if (supplier == null) return false;

        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync();
        return true;
    }
    #endregion

    #region 2. Bảng giá & Mặt hàng cung ứng
    public async Task<List<SupplierIngredientDto>> GetSupplierIngredientsAsync(int supplierId)
    {
        await EnsureProcurementTablesExistAsync();

        var list = await _db.SupplierIngredients
            .AsNoTracking()
            .Include(si => si.Ingredient)
            .Where(si => si.SupplierId == supplierId)
            .OrderBy(si => si.Ingredient!.IngredientName)
            .ToListAsync();

        return list.Select(si => new SupplierIngredientDto
        {
            SupplierIngredientId = si.SupplierIngredientId,
            SupplierId = si.SupplierId,
            IngredientId = si.IngredientId,
            IngredientCode = si.Ingredient?.IngredientCode ?? string.Empty,
            IngredientName = si.Ingredient?.IngredientName ?? "Nguyên liệu",
            BaseUom = si.Ingredient?.BaseUom ?? "g",
            SupplyPrice = si.SupplyPrice,
            PackageUom = si.PackageUom,
            ConversionFactor = si.ConversionFactor,
            MinOrderQuantity = si.MinOrderQuantity,
            LeadTimeDays = si.LeadTimeDays,
            IsPreferred = si.IsPreferred
        }).ToList();
    }

    public async Task<SupplierIngredientDto> SetSupplierIngredientAsync(SetSupplierIngredientRequest req)
    {
        await EnsureProcurementTablesExistAsync();

        var item = await _db.SupplierIngredients
            .Include(si => si.Ingredient)
            .FirstOrDefaultAsync(si => si.SupplierId == req.SupplierId && si.IngredientId == req.IngredientId);

        if (item == null)
        {
            item = new SupplierIngredient
            {
                SupplierId = req.SupplierId,
                IngredientId = req.IngredientId,
                SupplyPrice = req.SupplyPrice,
                PackageUom = string.IsNullOrWhiteSpace(req.PackageUom) ? "Gói" : req.PackageUom.Trim(),
                ConversionFactor = req.ConversionFactor > 0 ? req.ConversionFactor : 1.0m,
                MinOrderQuantity = req.MinOrderQuantity > 0 ? req.MinOrderQuantity : 1.0m,
                LeadTimeDays = req.LeadTimeDays >= 0 ? req.LeadTimeDays : 1,
                IsPreferred = req.IsPreferred
            };
            _db.SupplierIngredients.Add(item);
        }
        else
        {
            item.SupplyPrice = req.SupplyPrice;
            item.PackageUom = string.IsNullOrWhiteSpace(req.PackageUom) ? "Gói" : req.PackageUom.Trim();
            item.ConversionFactor = req.ConversionFactor > 0 ? req.ConversionFactor : 1.0m;
            item.MinOrderQuantity = req.MinOrderQuantity > 0 ? req.MinOrderQuantity : 1.0m;
            item.LeadTimeDays = req.LeadTimeDays >= 0 ? req.LeadTimeDays : 1;
            item.IsPreferred = req.IsPreferred;
        }

        await _db.SaveChangesAsync();

        if (item.Ingredient == null)
        {
            await _db.Entry(item).Reference(i => i.Ingredient).LoadAsync();
        }

        return new SupplierIngredientDto
        {
            SupplierIngredientId = item.SupplierIngredientId,
            SupplierId = item.SupplierId,
            IngredientId = item.IngredientId,
            IngredientCode = item.Ingredient?.IngredientCode ?? string.Empty,
            IngredientName = item.Ingredient?.IngredientName ?? "Nguyên liệu",
            BaseUom = item.Ingredient?.BaseUom ?? "g",
            SupplyPrice = item.SupplyPrice,
            PackageUom = item.PackageUom,
            ConversionFactor = item.ConversionFactor,
            MinOrderQuantity = item.MinOrderQuantity,
            LeadTimeDays = item.LeadTimeDays,
            IsPreferred = item.IsPreferred
        };
    }

    public async Task<bool> DeleteSupplierIngredientAsync(int supplierIngredientId)
    {
        await EnsureProcurementTablesExistAsync();

        var item = await _db.SupplierIngredients.FindAsync(supplierIngredientId);
        if (item == null) return false;

        _db.SupplierIngredients.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
    #endregion

    #region 3. Đơn Đặt Hàng Mua (Purchase Orders - PO)
    public async Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(int? branchId = null, string status = "ALL")
    {
        await EnsureProcurementTablesExistAsync();

        var query = _db.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Supplier)
            .Include(po => po.Branch)
            .Include(po => po.Details)
                .ThenInclude(d => d.Ingredient)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(po => po.BranchId == branchId.Value);
        }

        if (!string.IsNullOrEmpty(status) && status != "ALL")
        {
            query = query.Where(po => po.Status == status);
        }

        var list = await query.OrderByDescending(po => po.OrderDate).ToListAsync();

        var userIds = list.Select(po => po.CreatedByUserId).Distinct().ToList();
        var users = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Employee?.FullName ?? u.Username);

        return list.Select(po => MapPoToDto(po, users)).ToList();
    }

    public async Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(int poId)
    {
        await EnsureProcurementTablesExistAsync();

        var po = await _db.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Branch)
            .Include(p => p.Details)
                .ThenInclude(d => d.Ingredient)
            .FirstOrDefaultAsync(p => p.PoId == poId);

        if (po == null) return null;

        var user = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == po.CreatedByUserId);

        var userDict = new Dictionary<int, string>();
        if (user != null)
        {
            userDict[user.UserId] = user.Employee?.FullName ?? user.Username;
        }

        return MapPoToDto(po, userDict);
    }

    public async Task<PurchaseOrderDto> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest req)
    {
        await EnsureProcurementTablesExistAsync();

        if (req.Items == null || req.Items.Count == 0)
        {
            throw new ArgumentException("Đơn đặt hàng phải có ít nhất 1 mặt hàng nguyên liệu.");
        }

        string datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        int countToday = await _db.PurchaseOrders.CountAsync(p => p.PoCode.StartsWith($"PO-{datePrefix}"));
        string poCode = $"PO-{datePrefix}-{(countToday + 1):D3}";

        decimal subTotal = req.Items.Sum(i => i.OrderedQuantity * i.UnitPrice);
        decimal totalAmount = Math.Max(0, subTotal - req.DiscountAmount + req.TaxAmount);

        var po = new PurchaseOrder
        {
            PoCode = poCode,
            SupplierId = req.SupplierId,
            BranchId = req.BranchId,
            Status = "ORDERED", // Khởi tạo ở trạng thái đã đặt hàng
            OrderDate = DateTime.UtcNow,
            ExpectedDeliveryDate = req.ExpectedDeliveryDate,
            CreatedByUserId = req.CreatedByUserId > 0 ? req.CreatedByUserId : 1,
            SubTotal = subTotal,
            DiscountAmount = req.DiscountAmount,
            TaxAmount = req.TaxAmount,
            TotalAmount = totalAmount,
            Note = req.Note?.Trim()
        };

        foreach (var item in req.Items)
        {
            po.Details.Add(new PurchaseOrderDetail
            {
                IngredientId = item.IngredientId,
                OrderedQuantity = item.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitPrice = item.UnitPrice,
                SubTotal = item.OrderedQuantity * item.UnitPrice,
                Uom = string.IsNullOrWhiteSpace(item.Uom) ? "g" : item.Uom
            });
        }

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        return (await GetPurchaseOrderByIdAsync(po.PoId))!;
    }

    public async Task<bool> UpdatePoStatusAsync(int poId, string newStatus)
    {
        await EnsureProcurementTablesExistAsync();

        var po = await _db.PurchaseOrders.FindAsync(poId);
        if (po == null) return false;

        po.Status = newStatus;
        await _db.SaveChangesAsync();
        return true;
    }

    private static PurchaseOrderDto MapPoToDto(PurchaseOrder po, Dictionary<int, string> users)
    {
        string userName = users.TryGetValue(po.CreatedByUserId, out var name) ? name : $"NV #{po.CreatedByUserId}";

        decimal totalOrdered = po.Details.Sum(d => d.OrderedQuantity);
        decimal totalReceived = po.Details.Sum(d => d.ReceivedQuantity);
        double pct = totalOrdered > 0 ? (double)(totalReceived / totalOrdered) * 100.0 : 0.0;

        return new PurchaseOrderDto
        {
            PoId = po.PoId,
            PoCode = po.PoCode,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.SupplierName ?? "Nhà cung cấp",
            BranchId = po.BranchId,
            BranchName = po.Branch?.BranchName ?? "Chi nhánh",
            Status = po.Status,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            CreatedByUserId = po.CreatedByUserId,
            CreatedByName = userName,
            SubTotal = po.SubTotal,
            TaxAmount = po.TaxAmount,
            DiscountAmount = po.DiscountAmount,
            TotalAmount = po.TotalAmount,
            Note = po.Note,
            ItemCount = po.Details.Count,
            ReceivedPercent = Math.Round(Math.Min(100.0, pct), 1),
            Details = po.Details.Select(d => new PurchaseOrderDetailDto
            {
                PoDetailId = d.PoDetailId,
                PoId = d.PoId,
                IngredientId = d.IngredientId,
                IngredientCode = d.Ingredient?.IngredientCode ?? string.Empty,
                IngredientName = d.Ingredient?.IngredientName ?? "Nguyên liệu",
                OrderedQuantity = d.OrderedQuantity,
                ReceivedQuantity = d.ReceivedQuantity,
                UnitPrice = d.UnitPrice,
                SubTotal = d.SubTotal,
                Uom = d.Uom
            }).ToList()
        };
    }
    #endregion

    #region 4. Quy trình Tiếp Nhận Hàng Hóa 2 Bước & QC
    public async Task<List<GoodsReceiptDto>> GetGoodsReceiptsAsync(int? branchId = null, int? poId = null)
    {
        await EnsureProcurementTablesExistAsync();

        var query = _db.GoodsReceipts
            .AsNoTracking()
            .Include(gr => gr.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(gr => gr.Branch)
            .Include(gr => gr.Details)
                .ThenInclude(d => d.Ingredient)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(gr => gr.BranchId == branchId.Value);
        }

        if (poId.HasValue && poId.Value > 0)
        {
            query = query.Where(gr => gr.PoId == poId.Value);
        }

        var list = await query.OrderByDescending(gr => gr.ReceivedDate).ToListAsync();

        var userIds = list.Select(gr => gr.ReceivedByUserId).Distinct().ToList();
        var users = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Employee?.FullName ?? u.Username);

        return list.Select(gr => MapReceiptToDto(gr, users)).ToList();
    }

    public async Task<GoodsReceiptDto?> GetGoodsReceiptByIdAsync(int receiptId)
    {
        await EnsureProcurementTablesExistAsync();

        var gr = await _db.GoodsReceipts
            .AsNoTracking()
            .Include(g => g.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(g => g.Branch)
            .Include(g => g.Details)
                .ThenInclude(d => d.Ingredient)
            .FirstOrDefaultAsync(g => g.ReceiptId == receiptId);

        if (gr == null) return null;

        var user = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == gr.ReceivedByUserId);

        var userDict = new Dictionary<int, string>();
        if (user != null)
        {
            userDict[user.UserId] = user.Employee?.FullName ?? user.Username;
        }

        return MapReceiptToDto(gr, userDict);
    }

    public async Task<GoodsReceiptDto> ProcessGoodsReceiptAsync(CreateGoodsReceiptRequest req)
    {
        await EnsureProcurementTablesExistAsync();

        var po = await _db.PurchaseOrders
            .Include(p => p.Details)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PoId == req.PoId);

        if (po == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy đơn đặt hàng ID #{req.PoId}");
        }

        string datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        int countToday = await _db.GoodsReceipts.CountAsync(r => r.ReceiptCode.StartsWith($"GRN-{datePrefix}"));
        string receiptCode = $"GRN-{datePrefix}-{(countToday + 1):D3}";

        decimal totalAcceptedAmount = 0;

        var receipt = new GoodsReceipt
        {
            ReceiptCode = receiptCode,
            PoId = req.PoId,
            BranchId = req.BranchId,
            ReceivedDate = DateTime.UtcNow,
            ReceivedByUserId = req.ReceivedByUserId > 0 ? req.ReceivedByUserId : 1,
            DeliveryPerson = req.DeliveryPerson?.Trim(),
            InvoiceNumber = req.InvoiceNumber?.Trim(),
            Note = req.Note?.Trim()
        };

        // Bước 1 & Bước 2: Kiểm đếm đối chiếu & Ghi sổ kho
        foreach (var item in req.Items)
        {
            // Kiểm tra số lượng phân loại hợp lệ
            decimal accepted = Math.Max(0, item.AcceptedQuantity);
            decimal quarantined = Math.Max(0, item.QuarantinedQuantity);
            decimal rejected = Math.Max(0, item.RejectedQuantity);

            decimal delivered = item.DeliveredQuantity > 0 ? item.DeliveredQuantity : (accepted + quarantined + rejected);
            decimal unitPrice = item.UnitPrice;

            totalAcceptedAmount += accepted * unitPrice;

            receipt.Details.Add(new GoodsReceiptDetail
            {
                IngredientId = item.IngredientId,
                DeliveredQuantity = delivered,
                AcceptedQuantity = accepted,
                QuarantinedQuantity = quarantined,
                RejectedQuantity = rejected,
                RejectionReason = string.IsNullOrWhiteSpace(item.RejectionReason) ? null : item.RejectionReason.Trim(),
                UnitPrice = unitPrice,
                Uom = string.IsNullOrWhiteSpace(item.Uom) ? "g" : item.Uom
            });

            // Cập nhật số lượng đã nhận lũy kế trên dòng PO tương ứng
            var poDetail = po.Details.FirstOrDefault(d => d.IngredientId == item.IngredientId);
            if (poDetail != null)
            {
                poDetail.ReceivedQuantity += accepted;
            }

            // Ghi sổ kho: Chỉ cộng Hàng Đạt Chuẩn (AcceptedQuantity) vào tồn kho chi nhánh
            if (accepted > 0)
            {
                var stock = await _db.BranchInventories
                    .FirstOrDefaultAsync(bi => bi.BranchId == req.BranchId && bi.IngredientId == item.IngredientId);

                if (stock == null)
                {
                    stock = new BranchInventory
                    {
                        BranchId = req.BranchId,
                        IngredientId = item.IngredientId,
                        QuantityOnHand = accepted,
                        ReservedQuantity = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.BranchInventories.Add(stock);
                }
                else
                {
                    stock.QuantityOnHand += accepted;
                    stock.UpdatedAt = DateTime.UtcNow;
                }

                string qcNote = $"Nhập kho QC từ đơn {po.PoCode}: Đạt {accepted} {item.Uom}";
                if (rejected > 0)
                {
                    qcNote += $", Trả lại {rejected} {item.Uom} ({item.RejectionReason ?? "Hư hỏng"})";
                }

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BranchId = req.BranchId,
                    IngredientId = item.IngredientId,
                    TransactionType = "GOODS_RECEIPT",
                    QuantityChange = accepted,
                    BalanceAfter = stock.QuantityOnHand,
                    ReferenceType = "GOODS_RECEIPT_QC",
                    ReferenceId = receiptCode,
                    Note = qcNote,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        receipt.TotalAcceptedAmount = totalAcceptedAmount;
        _db.GoodsReceipts.Add(receipt);

        // Đánh giá trạng thái hoàn thành của đơn PO
        bool allFulfilled = po.Details.All(d => d.ReceivedQuantity >= d.OrderedQuantity);
        po.Status = allFulfilled ? "COMPLETED" : "PARTIALLY_RECEIVED";

        await _db.SaveChangesAsync();

        return (await GetGoodsReceiptByIdAsync(receipt.ReceiptId))!;
    }

    private static GoodsReceiptDto MapReceiptToDto(GoodsReceipt gr, Dictionary<int, string> users)
    {
        string userName = users.TryGetValue(gr.ReceivedByUserId, out var name) ? name : $"NV #{gr.ReceivedByUserId}";

        return new GoodsReceiptDto
        {
            ReceiptId = gr.ReceiptId,
            ReceiptCode = gr.ReceiptCode,
            PoId = gr.PoId,
            PoCode = gr.PurchaseOrder?.PoCode ?? $"PO #{gr.PoId}",
            SupplierName = gr.PurchaseOrder?.Supplier?.SupplierName ?? "Nhà cung cấp",
            BranchId = gr.BranchId,
            BranchName = gr.Branch?.BranchName ?? "Chi nhánh",
            ReceivedDate = gr.ReceivedDate,
            ReceivedByUserId = gr.ReceivedByUserId,
            ReceivedByName = userName,
            DeliveryPerson = gr.DeliveryPerson,
            InvoiceNumber = gr.InvoiceNumber,
            TotalAcceptedAmount = gr.TotalAcceptedAmount,
            Note = gr.Note,
            Details = gr.Details.Select(d => new GoodsReceiptDetailDto
            {
                ReceiptDetailId = d.ReceiptDetailId,
                ReceiptId = d.ReceiptId,
                IngredientId = d.IngredientId,
                IngredientCode = d.Ingredient?.IngredientCode ?? string.Empty,
                IngredientName = d.Ingredient?.IngredientName ?? "Nguyên liệu",
                DeliveredQuantity = d.DeliveredQuantity,
                AcceptedQuantity = d.AcceptedQuantity,
                QuarantinedQuantity = d.QuarantinedQuantity,
                RejectedQuantity = d.RejectedQuantity,
                RejectionReason = d.RejectionReason,
                UnitPrice = d.UnitPrice,
                Uom = d.Uom
            }).ToList()
        };
    }
    #endregion
}
