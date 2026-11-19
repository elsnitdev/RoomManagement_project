import test from "node:test";
import assert from "node:assert/strict";
import {
  buildTenantOverviewModel,
  needsContractConfirmation,
} from "./tenantOverviewModel.js";

test("trả về trạng thái trống gọn khi người thuê chưa có hợp đồng hoặc hóa đơn", () => {
  const model = buildTenantOverviewModel({
    contracts: [],
    stays: [],
    invoices: [],
    requests: [],
    unreadNotifications: 0,
    rules: [],
    todayIso: "2026-11-18",
  });

  assert.equal(model.home, null);
  assert.equal(model.billing.totalAmountDue, 0);
  assert.equal(model.billing.tone, "success");
  assert.equal(model.activity.activeContractsCount, 0);
  assert.equal(model.activity.openRequestsCount, 0);
  assert.equal(model.activity.unreadNotificationsCount, 0);
  assert.deepEqual(model.attentionItems, []);
});

test("tổng hợp chính xác phòng đang ở, tổng dư nợ, quá hạn và giới hạn tối đa 3 việc cần chú ý", () => {
  const model = buildTenantOverviewModel({
    contracts: [
      {
        id: "c-active",
        contractCode: "HD-101",
        roomNumber: "101",
        status: "Active",
        monthlyRent: 4200000,
        paymentDueDay: 5,
        startDate: "2026-01-01",
        endDate: "2026-12-31",
        workflow: "Legacy",
      },
      {
        id: "c-active-2",
        contractCode: "HD-102",
        roomNumber: "102",
        status: "Active",
        monthlyRent: 3800000,
        paymentDueDay: 5,
        startDate: "2026-02-01",
        workflow: "Invitation",
        offerVersion: 2,
        tenants: [{ leaveDate: null, invitationAcceptedOfferVersion: 2 }],
      },
      {
        id: "c-pending",
        contractCode: "HD-201",
        roomNumber: "201",
        status: "PendingTenantConfirmation",
        monthlyRent: 4500000,
        startDate: "2026-12-01",
        workflow: "Invitation",
        offerVersion: 1,
        tenants: [
          {
            leaveDate: null,
            isContactPerson: true,
            invitationAcceptedOfferVersion: 0,
          },
        ],
      },
    ],
    stays: [
      {
        contractId: "c-active",
        propertyId: "p-1",
        propertyName: "Nhà Trọ Xanh Cơ Sở 1",
        roomNumber: "101",
      },
    ],
    invoices: [
      {
        id: "inv-1",
        code: "HDN-10",
        status: "Issued",
        displayStatus: "Overdue",
        amountDue: 4500000,
        dueDate: "2026-11-05",
      },
      {
        id: "inv-2",
        code: "HDN-11",
        status: "Issued",
        displayStatus: "Unpaid",
        amountDue: 1200000,
        dueDate: "2026-11-25",
      },
      {
        id: "inv-cancelled",
        code: "HDN-00",
        status: "Cancelled",
        displayStatus: "Cancelled",
        amountDue: 999999,
        dueDate: "2026-10-05",
      },
    ],
    requests: [
      { id: "r-1", status: "Open" },
      { id: "r-2", status: "InProgress" },
      { id: "r-3", status: "Resolved" },
    ],
    unreadNotifications: 4,
    rules: [
      { contractId: "c-active", versionId: "v1", acceptedCurrentVersion: false },
    ],
    todayIso: "2026-11-18",
  });

  assert.equal(model.home.roomNumber, "101");
  assert.equal(model.home.propertyName, "Nhà Trọ Xanh Cơ Sở 1");
  assert.equal(model.home.statusLabel, "Đang ở");
  assert.equal(model.home.extraRoomsCount, 1);

  assert.equal(model.billing.totalAmountDue, 5700000);
  assert.equal(model.billing.overdueCount, 1);
  assert.equal(model.billing.unpaidCount, 2);
  assert.equal(model.billing.nearestDueDate, "2026-11-05");
  assert.equal(model.billing.tone, "danger");

  assert.equal(model.activity.activeContractsCount, 2);
  assert.equal(model.activity.pendingConfirmCount, 1);
  assert.equal(model.activity.openRequestsCount, 2);
  assert.equal(model.activity.unreadNotificationsCount, 4);

  assert.equal(model.attentionItems.length, 3);
  assert.deepEqual(
    model.attentionItems.map((i) => i.id),
    ["overdue-invoices", "pending-contracts", "unaccepted-rules"],
  );
});

test("hiển thị trạng thái đã thanh toán đủ và không hiện khối cần chú ý khi không có việc tồn đọng", () => {
  const model = buildTenantOverviewModel({
    contracts: [
      {
        id: "c-1",
        roomNumber: "305",
        status: "PendingStart",
        monthlyRent: 3500000,
        startDate: "2026-12-01",
        workflow: "Invitation",
        offerVersion: 1,
        tenants: [
          {
            leaveDate: null,
            isContactPerson: true,
            invitationAcceptedOfferVersion: 1,
          },
        ],
      },
    ],
    stays: [],
    invoices: [
      {
        id: "inv-paid",
        status: "Issued",
        displayStatus: "Paid",
        amountDue: 0,
        creditToRefund: 150000,
      },
    ],
    requests: [{ id: "r-done", status: "Closed" }],
    unreadNotifications: 0,
    rules: [],
    todayIso: "2026-11-18",
  });

  assert.equal(model.home.roomNumber, "305");
  assert.equal(model.home.statusLabel, "Chờ hiệu lực");
  assert.equal(model.billing.totalAmountDue, 0);
  assert.equal(model.billing.totalCredit, 150000);
  assert.equal(model.billing.tone, "success");
  assert.equal(model.activity.pendingConfirmCount, 0);
  assert.equal(model.activity.openRequestsCount, 0);
  assert.deepEqual(model.attentionItems, []);
});

test("needsContractConfirmation xử lý đúng cả luồng Invitation và Legacy", () => {
  assert.equal(
    needsContractConfirmation({
      workflow: "Legacy",
      status: "PendingTenantConfirmation",
    }),
    true,
  );
  assert.equal(
    needsContractConfirmation({
      workflow: "Invitation",
      status: "Active",
      offerVersion: 3,
      tenants: [
        {
          leaveDate: null,
          isContactPerson: false,
          invitationAcceptedOfferVersion: 2,
        },
      ],
    }),
    true,
  );
  assert.equal(
    needsContractConfirmation({
      workflow: "Invitation",
      status: "Active",
      offerVersion: 3,
      tenants: [
        {
          leaveDate: null,
          isContactPerson: false,
          invitationAcceptedOfferVersion: 3,
        },
      ],
    }),
    false,
  );
});
