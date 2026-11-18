using System.IO.Compression;
using System.Xml.Linq;
using RoomManagement.API.Infrastructure;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;

namespace RoomManagement.Tests;

public sealed class MeterImportWorkbookTests
{
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly Guid PropertyId = Guid.Parse("01111111-1111-4111-8111-111111111111");
    private static readonly Guid RoomId = Guid.Parse("02222222-2222-4222-8222-222222222222");

    [Fact]
    public void RoundTripsTwoContractsForTheSameRoom()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var cell = new MeterImportCell("Missing", 100m, null);
        using var workbook = new MemoryStream(MeterImportWorkbook.Create(new MeterImportResult(PropertyId, 2026, 11,
        [new MeterImportRow(RoomId, "P.101", null, cell, cell, first, "HD-1"),
         new MeterImportRow(RoomId, "P.101", null, cell, cell, second, "HD-2")])));
        var rows = MeterImportWorkbook.Read(workbook, PropertyId, 2026, 11);
        Assert.Equal(new Guid?[] { first, second }, rows.Select(x => x.Input.ContractId).ToArray());
    }

    [Fact]
    public void ReadsZeroAsAnEnteredReadingAndLeavesBlankWaterMissing()
    {
        using var workbook = EditedWorkbook(row =>
            SetCell(row, "D5", new XElement(Sheet + "v", "0")));

        var parsed = Assert.Single(MeterImportWorkbook.Read(workbook, PropertyId, 2026, 11));
        Assert.Equal(RoomId, parsed.Input.RoomId);
        Assert.Equal(5, parsed.ExcelRow);
        Assert.Equal(0m, parsed.Input.Electricity);
        Assert.Null(parsed.Input.Water);
        Assert.Null(parsed.ElectricityError);
        Assert.Null(parsed.WaterError);
    }

    [Fact]
    public void RejectsFormulaEvenWithCachedValueButKeepsValidOtherReading()
    {
        using var workbook = EditedWorkbook(row =>
        {
            SetCell(row, "D5", new XElement(Sheet + "f", "1+2"), new XElement(Sheet + "v", "3"));
            SetCell(row, "F5", new XElement(Sheet + "v", "12.25"));
        });

        var parsed = Assert.Single(MeterImportWorkbook.Read(workbook, PropertyId, 2026, 11));
        Assert.NotNull(parsed.ElectricityError);
        Assert.Null(parsed.Input.Electricity);
        Assert.Equal(12.25m, parsed.Input.Water);
        Assert.Null(parsed.WaterError);
    }

    [Fact]
    public void ReadsReorderedInputColumnsAndIgnoresExtraNotes()
    {
        using var workbook = EditedWorkbook(row =>
        {
            var header = row.ElementsBeforeSelf(Sheet + "row").Single(x => (string?)x.Attribute("r") == "4");
            var electricity = header.Elements(Sheet + "c").Single(x => (string?)x.Attribute("r") == "D4");
            electricity.Remove();
            electricity.SetAttributeValue("r", "H4");
            header.Add(electricity);
            SetCell(header, "D4", new XElement(Sheet + "v", "99"));
            SetCell(row, "H5", new XElement(Sheet + "v", "17.25"));
            SetCell(row, "D5", new XElement(Sheet + "v", "99"));
        });

        var parsed = Assert.Single(MeterImportWorkbook.Read(workbook, PropertyId, 2026, 11));
        Assert.Equal(17.25m, parsed.Input.Electricity);
    }

    [Fact]
    public void RejectsATemplateForAnotherPropertyOrPeriod()
    {
        using var otherProperty = new MemoryStream(TemplateBytes());
        using var otherMonth = new MemoryStream(TemplateBytes());
        Assert.Throws<BusinessException>(() => MeterImportWorkbook.Read(otherProperty, Guid.NewGuid(), 2026, 11));
        Assert.Throws<BusinessException>(() => MeterImportWorkbook.Read(otherMonth, PropertyId, 2026, 12));
    }

    private static byte[] TemplateBytes()
    {
        var emptyReading = new MeterImportCell("Missing", 0m, null);
        return MeterImportWorkbook.Create(new MeterImportResult(PropertyId, 2026, 11,
            [new MeterImportRow(RoomId, "P.101", null, emptyReading, emptyReading)]));
    }

    private static MemoryStream EditedWorkbook(Action<XElement> edit)
    {
        using var template = new MemoryStream(TemplateBytes());
        using var input = new ZipArchive(template, ZipArchiveMode.Read);
        var buffer = new MemoryStream();
        using (var output = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var source = entry.Open();
                using var destination = output.CreateEntry(entry.FullName).Open();
                if (entry.FullName == "xl/worksheets/sheet1.xml")
                {
                    var document = XDocument.Load(source);
                    edit(document.Descendants(Sheet + "row").Single(row => (string?)row.Attribute("r") == "5"));
                    document.Save(destination);
                }
                else
                {
                    source.CopyTo(destination);
                }
            }
        }
        buffer.Position = 0;
        return buffer;
    }

    private static void SetCell(XElement row, string address, params XElement[] contents)
    {
        var cell = row.Elements(Sheet + "c").SingleOrDefault(item => (string?)item.Attribute("r") == address);
        if (cell is null)
        {
            cell = new XElement(Sheet + "c", new XAttribute("r", address));
            var next = row.Elements(Sheet + "c").FirstOrDefault(item =>
                string.CompareOrdinal((string?)item.Attribute("r"), address) > 0);
            if (next is null)
                row.Add(cell);
            else
                next.AddBeforeSelf(cell);
        }
        cell.SetAttributeValue("t", "n");
        cell.ReplaceNodes(contents);
    }
}
