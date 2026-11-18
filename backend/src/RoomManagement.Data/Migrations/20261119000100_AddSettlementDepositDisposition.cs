using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261119000100_AddSettlementDepositDisposition")]
public sealed class AddSettlementDepositDisposition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_ContractSettlements_Disposition", "ContractSettlements", "dbo");
        migrationBuilder.AddCheckConstraint("CK_ContractSettlements_Disposition", "ContractSettlements",
            "(DepositDisposition IN (N'Refunded', N'PartialRefund', N'Forfeited', N'Offset', N'NoDeposit', N'CancelRefunded', N'CancelPartialRefund', N'CancelForfeited'))", "dbo");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_ContractSettlements_Disposition", "ContractSettlements", "dbo");
        migrationBuilder.AddCheckConstraint("CK_ContractSettlements_Disposition", "ContractSettlements",
            "(DepositDisposition IN (N'Refunded', N'Offset', N'NoDeposit', N'CancelRefunded', N'CancelPartialRefund', N'CancelForfeited'))", "dbo");
    }
}
