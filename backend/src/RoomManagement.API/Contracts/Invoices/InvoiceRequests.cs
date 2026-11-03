using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;

namespace RoomManagement.API.Contracts.Invoices;

public sealed record MonthlyInvoiceRequest(
    [property: JsonRequired] Guid ContractId,
    [property: JsonRequired][Range(2000, 2100)] short Year,
    [property: JsonRequired][Range(1, 12)] byte Month)
{
    public MonthlyInvoiceInput ToInput() => new(ContractId, Year, Month);
}
