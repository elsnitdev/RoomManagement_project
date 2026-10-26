using RoomManagement.Domain.Enums;

namespace RoomManagement.Domain.Entities;

public sealed class Contract
{
    public Guid Id { get; set; }
    public string ContractCode { get; set; } = null!;
    public Guid RoomId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public byte? PaymentDueDay { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
