using ClosedXML.Excel;

namespace AssetTracking.Web.Helpers;

/// <summary>
/// مُصدِّر Excel عام (ClosedXML) — يبني ورقة RTL بترويسة ملوّنة
/// وصفوف بيانات من قائمة أعمدة مُعرَّفة بدوال.
/// </summary>
public static class ExcelExporter
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public record Column<T>(string Header, Func<T, object?> Value, string? Format = null, double Width = 18);

    public static byte[] Build<T>(string sheetName, string title, IEnumerable<T> rows,
        IReadOnlyList<Column<T>> columns, IReadOnlyList<(string Label, string Value)>? summary = null)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(Sanitize(sheetName));
        ws.RightToLeft = true;

        var lastCol = columns.Count;
        var r = 1;

        // ── العنوان ──
        var titleRange = ws.Range(r, 1, r, lastCol).Merge();
        titleRange.Value = title;
        titleRange.Style.Font.SetBold().Font.SetFontSize(14);
        titleRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0f5132"));
        titleRange.Style.Font.SetFontColor(XLColor.White);
        titleRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(r).Height = 24;
        r++;

        var stamp = ws.Range(r, 1, r, lastCol).Merge();
        stamp.Value = $"تاريخ التصدير: {DateTime.Now:yyyy/MM/dd HH:mm}";
        stamp.Style.Font.SetItalic().Font.SetFontSize(9);
        stamp.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        r += 2;

        // ── الملخّص (اختياري) ──
        if (summary is { Count: > 0 })
        {
            foreach (var (label, value) in summary)
            {
                ws.Cell(r, 1).Value = label;
                ws.Cell(r, 1).Style.Font.SetBold();
                ws.Cell(r, 2).Value = value;
                r++;
            }
            r++;
        }

        // ── ترويسة الجدول ──
        var headerRow = r;
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#d1e7dd"));
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Column(c + 1).Width = columns[c].Width;
        }
        r++;

        // ── البيانات ──
        var dataStart = r;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(r, c + 1);
                SetValue(cell, columns[c].Value(row));
                if (columns[c].Format != null) cell.Style.NumberFormat.Format = columns[c].Format;
            }
            r++;
        }
        var dataEnd = r - 1;

        if (dataEnd >= dataStart)
        {
            var table = ws.Range(headerRow, 1, dataEnd, lastCol);
            table.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.SheetView.FreezeRows(headerRow);
            ws.Range(headerRow, 1, dataEnd, lastCol).SetAutoFilter();
        }
        else
        {
            ws.Cell(r, 1).Value = "لا توجد بيانات مطابقة للفلاتر المحددة.";
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetValue(IXLCell cell, object? v)
    {
        switch (v)
        {
            case null:
                cell.Value = "—";
                break;
            case string s:
                cell.Value = s;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy/mm/dd";
                break;
            case decimal d:
                cell.Value = d;
                break;
            case double db:
                cell.Value = db;
                break;
            case int i:
                cell.Value = i;
                break;
            case bool b:
                cell.Value = b ? "نعم" : "لا";
                break;
            default:
                cell.Value = v.ToString();
                break;
        }
    }

    /// <summary>أسماء أوراق Excel لا تقبل هذه الرموز ولا تتجاوز ٣١ حرفاً</summary>
    private static string Sanitize(string name)
    {
        foreach (var ch in new[] { '\\', '/', '*', '?', ':', '[', ']' })
            name = name.Replace(ch, '-');
        return name.Length > 31 ? name[..31] : name;
    }
}
