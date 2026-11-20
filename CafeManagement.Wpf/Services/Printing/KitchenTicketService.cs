using System.IO;
using System.IO.Compression;
using System.Text;
using CafeManagement.Core.DTOs.Orders;

namespace CafeManagement.Wpf.Services.Printing;

public class KitchenTicketCupModel
{
    public int CupIndex { get; set; }
    public int TotalCups { get; set; }
    public string CupNumberDisplay => $"Ly {CupIndex}/{TotalCups}";

    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = "M";
    public string TitleDisplay => $"{ProductName.ToUpper()} (SIZE {Size.ToUpper()})";

    public string SugarLevel { get; set; } = "100%";
    public string IceLevel { get; set; } = "100%";
    public string CustomizationDisplay => $"Đường {SugarLevel}, Đá {IceLevel}";

    public string ToppingsDisplay { get; set; } = "Không";
    public string NoteDisplay { get; set; } = "Không";

    public bool IsCompleted { get; set; } = false;
    public string CheckDisplay => IsCompleted ? "[✓] Đã pha chế" : "[  ] Đã pha chế";
}

public static class KitchenTicketService
{
    public static List<KitchenTicketCupModel> BuildCups(OrderDto order)
    {
        var list = new List<KitchenTicketCupModel>();
        int totalCups = order.Items.Sum(i => Math.Max(1, i.Quantity));
        if (totalCups == 0) totalCups = 1;
        int currentCup = 1;

        foreach (var item in order.Items)
        {
            int count = Math.Max(1, item.Quantity);
            string topStr = item.Toppings != null && item.Toppings.Count > 0
                ? string.Join(", ", item.Toppings.Select(t => $"+ {t.Quantity}x {t.ToppingName}"))
                : "Không";

            string noteStr = !string.IsNullOrWhiteSpace(item.Note) ? item.Note.Trim() : "Không";

            for (int c = 0; c < count; c++)
            {
                list.Add(new KitchenTicketCupModel
                {
                    CupIndex = currentCup++,
                    TotalCups = totalCups,
                    ProductName = item.ProductName,
                    Size = item.Size,
                    SugarLevel = string.IsNullOrWhiteSpace(item.SugarLevel) ? "100%" : item.SugarLevel,
                    IceLevel = string.IsNullOrWhiteSpace(item.IceLevel) ? "100%" : item.IceLevel,
                    ToppingsDisplay = topStr,
                    NoteDisplay = noteStr,
                    IsCompleted = false
                });
            }
        }

        return list;
    }

    public static string GetOrderTypeDisplay(string? orderType)
    {
        return (orderType?.ToUpper()) switch
        {
            "DINE_IN" => "TẠI BÀN (DINE-IN)",
            _ => "MANG VỀ (TAKE-AWAY)"
        };
    }

    public static string GenerateDocx(OrderDto order, string? outputPath = null)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CafeManagement_Tickets");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string safeNumber = order.OrderNumber.Replace("#", "").Trim();
        string filePath = outputPath ?? Path.Combine(dir, $"PhieuPhaChe_{safeNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");

        if (File.Exists(filePath))
        {
            try { File.Delete(filePath); } catch { }
        }

        var cups = BuildCups(order);
        string orderTypeDisplay = GetOrderTypeDisplay(order.OrderType);
        string branchName = !string.IsNullOrWhiteSpace(order.BranchName) ? order.BranchName : "Chi nhánh";
        string cashierName = !string.IsNullOrWhiteSpace(order.CashierName) ? order.CashierName : "Thu ngân";
        string orderTime = order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            // 1. [Content_Types].xml
            var entryContentTypes = archive.CreateEntry("[Content_Types].xml");
            using (var sw = new StreamWriter(entryContentTypes.Open(), Encoding.UTF8))
            {
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
</Types>");
            }

            // 2. _rels/.rels
            var entryRels = archive.CreateEntry("_rels/.rels");
            using (var sw = new StreamWriter(entryRels.Open(), Encoding.UTF8))
            {
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");
            }

            // 3. word/document.xml
            var entryDocument = archive.CreateEntry("word/document.xml");
            using (var sw = new StreamWriter(entryDocument.Open(), Encoding.UTF8))
            {
                var sb = new StringBuilder();
                sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>");

                // Header: Tên chuỗi quán & Chi nhánh
                sb.Append(MakeParagraph("CAFE CHAIN MANAGEMENT", bold: true, fontSizeHalfPoints: 34, align: "center", colorHex: "2C3E50"));
                sb.Append(MakeParagraph("☕ PHIẾU PHA CHẾ (BARISTA TICKET) ☕", bold: true, fontSizeHalfPoints: 26, align: "center", colorHex: "D35400"));
                sb.Append(MakeParagraph($"Chi nhánh: {EscapeXml(branchName)}", bold: true, fontSizeHalfPoints: 22, align: "center", colorHex: "34495E"));

                // Thông tin đơn hàng
                sb.Append(MakeDivider());
                sb.Append(MakeParagraph($"Số hóa đơn: #{EscapeXml(order.OrderNumber)}", bold: true, fontSizeHalfPoints: 24, align: "left", colorHex: "2980B9"));
                sb.Append(MakeParagraph($"Giờ đặt món: {EscapeXml(orderTime)}", bold: false, fontSizeHalfPoints: 20, align: "left", colorHex: "7F8C8D"));
                sb.Append(MakeParagraph($"Thu ngân thực hiện: {EscapeXml(cashierName)}", bold: false, fontSizeHalfPoints: 20, align: "left", colorHex: "7F8C8D"));
                sb.Append(MakeParagraph($"LOẠI ĐƠN: {EscapeXml(orderTypeDisplay)}", bold: true, fontSizeHalfPoints: 22, align: "left", colorHex: "C0392B"));
                sb.Append(MakeDivider());

                // Chi tiết từng ly đồ uống
                foreach (var cup in cups)
                {
                    // Tên món (in hoa đậm)
                    sb.Append(MakeParagraph($"[{cup.CupNumberDisplay}] {EscapeXml(cup.TitleDisplay)}", bold: true, fontSizeHalfPoints: 24, align: "left", colorHex: "1A202C"));
                    
                    // Tùy chỉnh: Đường, Đá
                    sb.Append(MakeBulletParagraph($"Tùy chỉnh: {EscapeXml(cup.CustomizationDisplay)}", fontSizeHalfPoints: 20));
                    
                    // Topping
                    sb.Append(MakeBulletParagraph($"Topping: {EscapeXml(cup.ToppingsDisplay)}", fontSizeHalfPoints: 20));
                    
                    // Ghi chú
                    sb.Append(MakeBulletParagraph($"Ghi chú: {EscapeXml(cup.NoteDisplay)}", fontSizeHalfPoints: 20, italic: true));
                    
                    // Ô kiểm soát
                    sb.Append(MakeParagraph($"Ô kiểm soát: [  ] Đã pha chế", bold: true, fontSizeHalfPoints: 20, align: "left", colorHex: "27AE60"));
                    
                    sb.Append(MakeSubDivider());
                }

                // Footer: Tổng số ly cần pha chế (STT ly / tổng ly)
                sb.Append(MakeDivider());
                sb.Append(MakeParagraph($"TỔNG SỐ LY CẦN PHA CHẾ: {cups.Count} LY (Ly 1..{cups.Count}/{cups.Count})", bold: true, fontSizeHalfPoints: 24, align: "center", colorHex: "C0392B"));
                sb.Append(MakeParagraph("Chúc Barista pha chế ngon miệng và chuẩn vị!", bold: false, fontSizeHalfPoints: 18, align: "center", colorHex: "95A5A6", italic: true));

                // Page setup: Khổ phiếu nhỏ gọn vừa máy in nhiệt và văn phòng
                sb.Append(@"<w:sectPr>
  <w:pgSz w:w=""11906"" w:h=""16838""/>
  <w:pgMar w:top=""720"" w:right=""720"" w:bottom=""720"" w:left=""720""/>
</w:sectPr>");

                sb.Append(@"  </w:body>
</w:document>");
                sw.Write(sb.ToString());
            }
        }

        return filePath;
    }

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }

    private static string MakeParagraph(string text, bool bold = false, int fontSizeHalfPoints = 20, string align = "left", string colorHex = "000000", bool italic = false)
    {
        var sb = new StringBuilder();
        sb.Append("<w:p><w:pPr>");
        if (!string.IsNullOrEmpty(align) && align != "left")
        {
            sb.Append($"<w:jc w:val=\"{align}\"/>");
        }
        sb.Append("<w:spacing w:line=\"280\" w:lineRule=\"auto\" w:before=\"40\" w:after=\"40\"/>");
        sb.Append("</w:pPr><w:r><w:rPr>");
        if (bold) sb.Append("<w:b/>");
        if (italic) sb.Append("<w:i/>");
        sb.Append($"<w:sz w:val=\"{fontSizeHalfPoints}\"/>");
        if (!string.IsNullOrEmpty(colorHex)) sb.Append($"<w:color w:val=\"{colorHex}\"/>");
        sb.Append($"</w:rPr><w:t xml:space=\"preserve\">{EscapeXml(text)}</w:t></w:r></w:p>");
        return sb.ToString();
    }

    private static string MakeBulletParagraph(string text, int fontSizeHalfPoints = 20, bool italic = false)
    {
        var sb = new StringBuilder();
        sb.Append("<w:p><w:pPr>");
        sb.Append("<w:ind w:left=\"280\"/>");
        sb.Append("<w:spacing w:line=\"260\" w:lineRule=\"auto\" w:before=\"20\" w:after=\"20\"/>");
        sb.Append("</w:pPr><w:r><w:rPr>");
        if (italic) sb.Append("<w:i/>");
        sb.Append($"<w:sz w:val=\"{fontSizeHalfPoints}\"/>");
        sb.Append("<w:color w:val=\"34495E\"/>");
        sb.Append($"</w:rPr><w:t xml:space=\"preserve\">• {EscapeXml(text)}</w:t></w:r></w:p>");
        return sb.ToString();
    }

    private static string MakeDivider()
    {
        return "<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"60\" w:after=\"60\"/></w:pPr><w:r><w:rPr><w:color w:val=\"BDC3C7\"/><w:sz w:val=\"18\"/></w:rPr><w:t>--------------------------------------------------------------------------------</w:t></w:r></w:p>";
    }

    private static string MakeSubDivider()
    {
        return "<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"40\" w:after=\"40\"/></w:pPr><w:r><w:rPr><w:color w:val=\"E2E8F0\"/><w:sz w:val=\"16\"/></w:rPr><w:t>- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -</w:t></w:r></w:p>";
    }
}