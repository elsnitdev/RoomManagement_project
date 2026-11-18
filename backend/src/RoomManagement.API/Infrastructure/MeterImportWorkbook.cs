using System.IO.Compression;
using System.Globalization;
using System.Xml;
using ClosedXML.Excel;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;

namespace RoomManagement.API.Infrastructure;

public static class MeterImportWorkbook
{
    private const string DataSheet = "Chi so";
    private const string MetaSheet = "_meta";
    private const int HeaderRow = 4;
    private const string ContractIdHeader = "Mã định danh hợp đồng";
    private const string ContractCodeHeader = "Hợp đồng";
    private const int MaxExpandedBytes = 16 * 1024 * 1024;
    private static readonly string[] Headers = [
        "Phòng", "Mã phòng", "Điện đầu kỳ (kWh)", "Điện cuối kỳ (kWh)",
        "Nước đầu kỳ (m³)", "Nước cuối kỳ (m³)", "Trạng thái"
    ];

    public static byte[] Create(MeterImportResult result)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add(DataSheet);
        sheet.Cell("A1").Value = $"Nhập chỉ số điện nước kỳ {result.Month:D2}/{result.Year}";
        sheet.Cell("A2").Value = $"Mã khu trọ: {result.PropertyId}";
        sheet.Cell("A3").Value = "Giữ mã phòng, mã hợp đồng và tiêu đề. Mỗi dòng ứng với một hợp đồng; điền chỉ số cuối kỳ.";
        sheet.Range("A1:G1").Merge().Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Range("A2:G2").Merge();
        sheet.Range("A3:G3").Merge().Style.Alignment.SetWrapText();
        sheet.Row(3).Height = 28;
        for (var c = 0; c < Headers.Length; c++)
            sheet.Cell(HeaderRow, c + 1).Value = Headers[c];
        sheet.Range(HeaderRow, 1, HeaderRow, Headers.Length).Style.Font.SetBold();
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Column(1).Width = 18;
        sheet.Column(2).Hide();
        sheet.Columns(3, 6).Width = 23;
        sheet.Column(7).Width = 48;
        sheet.Cell(HeaderRow, 9).Value = ContractCodeHeader;
        sheet.Cell(HeaderRow, 10).Value = ContractIdHeader;
        sheet.Column(9).Width = 25;
        sheet.Column(10).Hide();

        for (var i = 0; i < result.Rows.Count; i++)
        {
            var row = result.Rows[i];
            var index = HeaderRow + 1 + i;
            sheet.Cell(index, 1).Value = row.RoomNumber;
            sheet.Cell(index, 2).Value = row.RoomId.ToString("D");
            if (row.ContractId is { } contractId) sheet.Cell(index, 10).Value = contractId.ToString("D");
            sheet.Cell(index, 9).Value = row.ContractCode ?? "";
            WriteReference(sheet, index, 3, row.Electricity);
            WriteReference(sheet, index, 5, row.Water);
            sheet.Cell(index, 7).Value = string.Join(" • ", StatusText("Điện", row.Electricity)
                .Concat(StatusText("Nước", row.Water)));
            sheet.Cell(index, 7).Style.Alignment.WrapText = true;
        }

        var meta = book.Worksheets.Add(MetaSheet);
        meta.Cell("A1").Value = 1;
        meta.Cell("B1").Value = result.PropertyId.ToString("D");
        meta.Cell("C1").Value = result.Year;
        meta.Cell("D1").Value = result.Month;
        meta.Cell("E1").Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        meta.Hide();

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<ParsedMeterImportRow> Read(Stream stream, Guid propertyId, short year, byte month)
    {
        try
        {
            CheckArchive(stream);
            using var book = new XLWorkbook(stream);
            if (!book.Worksheets.TryGetWorksheet(DataSheet, out var sheet) ||
                !book.Worksheets.TryGetWorksheet(MetaSheet, out var meta) ||
                !MetadataMatches(meta, propertyId, year, month))
                throw Validation("METER_IMPORT_TEMPLATE_MISMATCH", "File không đúng mẫu, khu trọ hoặc kỳ đã chọn.");

            var roomColumn = FindColumn(sheet, Headers[1]);
            var electricityColumn = FindColumn(sheet, Headers[3]);
            var waterColumn = FindColumn(sheet, Headers[5]);
            var electricityReferenceColumn = FindColumn(sheet, Headers[2], required: false);
            var waterReferenceColumn = FindColumn(sheet, Headers[4], required: false);
            var contractColumn = FindColumn(sheet, ContractIdHeader, required: false);
            var inputColumns = new[] { roomColumn, electricityColumn, waterColumn };
            var lastRow = inputColumns.Max(c => sheet.Column(c).LastCellUsed()?.Address.RowNumber ?? HeaderRow);
            if (lastRow - HeaderRow > MeterImportService.MaxRows)
                throw Validation("METER_IMPORT_LIMIT", "File vượt giới hạn số dòng.");

            var result = new List<ParsedMeterImportRow>();
            var seen = new HashSet<(Guid RoomId, Guid? ContractId)>();
            for (var row = HeaderRow + 1; row <= lastRow; row++)
            {
                if (inputColumns.Select(c => sheet.Cell(row, c)).All(x => x.IsEmpty() && !x.HasFormula))
                    continue;
                var idCell = sheet.Cell(row, roomColumn);
                if (idCell.HasFormula || idCell.DataType != XLDataType.Text ||
                    !Guid.TryParseExact(idCell.GetString(), "D", out var roomId) || roomId == Guid.Empty)
                    throw Validation("METER_IMPORT_ROOM_INVALID", $"Dòng {row} thiếu mã phòng hợp lệ.");
                Guid? contractId = null;
                if (contractColumn > 0 && !sheet.Cell(row, contractColumn).IsEmpty())
                {
                    var contractCell = sheet.Cell(row, contractColumn);
                    if (contractCell.HasFormula || contractCell.DataType != XLDataType.Text ||
                        !Guid.TryParseExact(contractCell.GetString(), "D", out var parsedId) || parsedId == Guid.Empty)
                        throw Validation("METER_IMPORT_CONTRACT_INVALID", $"Dòng {row} có mã hợp đồng không hợp lệ.");
                    contractId = parsedId;
                }
                if (!seen.Add((roomId, contractId)))
                    throw Validation("METER_IMPORT_DUPLICATE_ROOM", $"Phòng và hợp đồng tại dòng {row} bị trùng.");

                var (electricity, electricError) = ReadValue(sheet.Cell(row, electricityColumn));
                var (water, waterError) = ReadValue(sheet.Cell(row, waterColumn));
                var electricityReference = electricityReferenceColumn > 0 ? ReadValue(sheet.Cell(row, electricityReferenceColumn)).Value : null;
                var waterReference = waterReferenceColumn > 0 ? ReadValue(sheet.Cell(row, waterReferenceColumn)).Value : null;
                result.Add(new ParsedMeterImportRow(
                    new MeterImportRowInput(roomId, electricity, water, electricityReference, waterReference, ContractId: contractId),
                    row, electricError, waterError));
            }
            return result;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or XmlException or FormatException or ArgumentException or
            InvalidOperationException or OverflowException or KeyNotFoundException or NotSupportedException or
            DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            throw new BusinessException(BusinessErrorType.Validation, "METER_IMPORT_FILE_INVALID",
                "Không thể đọc file Excel. Hãy tải lại file mẫu và thử lại.", ex);
        }
    }

    private static int FindColumn(IXLWorksheet sheet, string header, bool required = true)
    {
        var matches = sheet.Row(HeaderRow).CellsUsed()
            .Where(x => !x.HasFormula && x.DataType == XLDataType.Text &&
                string.Equals(x.GetString().Trim(), header, StringComparison.OrdinalIgnoreCase))
            .Take(2).Select(x => x.Address.ColumnNumber).ToArray();
        if (matches.Length > 1 || required && matches.Length == 0)
            throw Validation("METER_IMPORT_HEADERS_INVALID", $"File phải có đúng một cột “{header}”.");
        return matches.SingleOrDefault();
    }

    private static bool MetadataMatches(IXLWorksheet meta, Guid propertyId, short year, byte month) =>
        meta.Range("A1:D1").Cells().All(x => !x.HasFormula) &&
        meta.Cell("A1").DataType == XLDataType.Number && meta.Cell("A1").GetDouble() == 1 &&
        meta.Cell("B1").DataType == XLDataType.Text &&
        Guid.TryParseExact(meta.Cell("B1").GetString(), "D", out var recordedId) && recordedId == propertyId &&
        meta.Cell("C1").DataType == XLDataType.Number && meta.Cell("C1").GetDouble() == year &&
        meta.Cell("D1").DataType == XLDataType.Number && meta.Cell("D1").GetDouble() == month;

    private static void CheckArchive(Stream stream)
    {
        if (!stream.CanSeek || stream.Length is <= 0 or > MeterImportService.MaxFileBytes)
            throw Validation("METER_IMPORT_FILE_SIZE", "File Excel thiếu dữ liệu hoặc lớn hơn 4 MB.");
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (zip.Entries.Count > 64 || zip.Entries.Any(e => e.Length > MaxExpandedBytes) ||
                zip.Entries.Sum(e => e.Length) > MaxExpandedBytes)
                throw Validation("METER_IMPORT_LIMIT", "File Excel vượt giới hạn xử lý.");

            // Read each entry with a byte budget before sending the ZIP package to the workbook reader.
            var buffer = new byte[8192];
            var remaining = MaxExpandedBytes;
            foreach (var entry in zip.Entries)
            {
                using var content = entry.Open();
                int count;
                while ((count = content.Read(buffer)) > 0)
                {
                    remaining -= count;
                    if (remaining < 0)
                        throw Validation("METER_IMPORT_LIMIT", "File Excel vượt giới hạn xử lý.");
                }
            }
        }
        stream.Position = 0;
    }

    private static (decimal? Value, string? Error) ReadValue(IXLCell cell)
    {
        if (cell.HasFormula) return (null, "Ô chỉ số phải là số, không dùng công thức.");
        if (cell.IsEmpty()) return (null, null);
        return cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out var number)
            ? (number, null) : (null, "Ô chỉ số phải là số.");
    }

    private static void WriteReference(IXLWorksheet sheet, int row, int referenceColumn, MeterImportCell cell)
    {
        if (cell.PreviousValue is { } previous)
            sheet.Cell(row, referenceColumn).Value = previous;
        var input = sheet.Cell(row, referenceColumn + 1);
        if (cell.Status == "AlreadyRecorded" && cell.CurrentValue is { } current)
            input.Value = current;
        if (cell.CanEnterOpening || cell.Status is "Missing" or "AlreadyRecorded")
            input.Style.Fill.BackgroundColor = XLColor.LightGoldenrodYellow;
        input.Style.NumberFormat.Format = "0.##";
    }

    private static IEnumerable<string> StatusText(string title, MeterImportCell cell) =>
        cell.Status == "Missing" || cell.Message is null ? [] : [$"{title}: {cell.Message}"];

    private static BusinessException Validation(string code, string message) =>
        new(BusinessErrorType.Validation, code, message);
}
