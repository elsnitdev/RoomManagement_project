const UPCOMING_CONTRACT_STATUSES = new Set([
  "PendingStart",
  "PendingOperatorConfirmation",
  "PendingTenantConfirmation",
  "Draft",
]);

const CONTRACT_STATUS_META = {
  Active: { label: "Đang ở", tone: "success" },
  PendingStart: { label: "Chờ hiệu lực", tone: "info" },
  PendingTenantConfirmation: { label: "Chờ xác nhận", tone: "warning" },
  PendingOperatorConfirmation: { label: "Chờ chủ trọ duyệt", tone: "info" },
  Draft: { label: "Bản nháp", tone: "neutral" },
  PendingMoveOut: { label: "Chờ bàn giao", tone: "warning" },
  PendingSettlement: { label: "Chờ tất toán", tone: "warning" },
  PendingCancelSettlement: { label: "Chờ xử lý hủy", tone: "warning" },
  Expired: { label: "Hết hạn", tone: "neutral" },
  Terminated: { label: "Đã kết thúc", tone: "neutral" },
  Cancelled: { label: "Đã hủy", tone: "neutral" },
};

export function formatCurrency(value) {
  return (
    new Intl.NumberFormat("vi-VN", {
      maximumFractionDigits: 0,
    }).format(Number(value) || 0) + " ₫"
  );
}

export function formatShortDate(value) {
  if (!value) return "Chưa xác định";
  const str = String(value);
  if (/^\d{4}-\d{2}-\d{2}/.test(str)) {
    const [year, month, day] = str.slice(0, 10).split("-");
    return `${day}/${month}/${year}`;
  }
  const date = new Date(str);
  if (Number.isNaN(date.getTime())) return str;
  return new Intl.DateTimeFormat("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

export function needsContractConfirmation(contract) {
  if (!contract) return false;
  if (contract.workflow === "Invitation") {
    const ownLink = contract.tenants?.find((tenant) => !tenant.leaveDate);
    if (!ownLink) return false;
    const isRepresentative = Boolean(ownLink.isContactPerson);
    const accepted =
      ownLink.invitationAcceptedOfferVersion === contract.offerVersion;
    const canConfirm = isRepresentative
      ? contract.status === "PendingTenantConfirmation"
      : ["PendingTenantConfirmation", "PendingStart", "Active"].includes(
          contract.status,
        );
    return !accepted && canConfirm;
  }
  return contract.status === "PendingTenantConfirmation";
}

function getDateMillis(value) {
  if (!value) return Number.POSITIVE_INFINITY;
  const iso = String(value).slice(0, 10);
  const ts = Date.parse(`${iso}T00:00:00Z`);
  return Number.isNaN(ts) ? Number.POSITIVE_INFINITY : ts;
}

function isUnpaidInvoice(invoice) {
  if (!invoice || invoice.status === "Cancelled") return false;
  return (
    Number(invoice.amountDue || 0) > 0 ||
    ["Unpaid", "Partial", "Overdue"].includes(invoice.displayStatus)
  );
}

function isOverdueInvoice(invoice) {
  return Boolean(
    invoice &&
      invoice.status !== "Cancelled" &&
      invoice.displayStatus === "Overdue",
  );
}

/**
 * Chuẩn hóa toàn bộ dữ liệu từ các API thành cấu trúc tóm tắt gọn cho Trang Tổng quan Người thuê.
 */
export function buildTenantOverviewModel({
  contracts = [],
  stays = [],
  invoices = [],
  requests = [],
  unreadNotifications = 0,
  rules = [],
  todayIso,
}) {
  const stayByContractId = new Map();
  for (const stay of stays || []) {
    if (stay?.contractId) {
      stayByContractId.set(stay.contractId, stay);
    }
  }

  const activeContracts = (contracts || []).filter(
    (contract) => contract.status === "Active",
  );
  const upcomingContracts = (contracts || []).filter((contract) =>
    UPCOMING_CONTRACT_STATUSES.has(contract.status),
  );
  const pendingConfirmCount = (contracts || []).filter(
    needsContractConfirmation,
  ).length;

  const currentPool =
    activeContracts.length > 0 ? activeContracts : upcomingContracts;
  const primaryContract = currentPool[0] || null;
  const primaryStay = primaryContract
    ? stayByContractId.get(primaryContract.id) ||
      (stays || []).find(
        (stay) =>
          stay?.roomNumber &&
          primaryContract.roomNumber &&
          String(stay.roomNumber) === String(primaryContract.roomNumber),
      )
    : (stays || [])[0] || null;

  let home = null;
  if (primaryContract || primaryStay) {
    const statusKey = primaryContract?.status || "Active";
    const statusMeta = CONTRACT_STATUS_META[statusKey] || {
      label: statusKey,
      tone: "neutral",
    };
    const extraRoomsCount =
      currentPool.length > 1
        ? currentPool.length - 1
        : Math.max(0, (stays || []).length - 1);

    home = {
      roomNumber: primaryContract?.roomNumber || primaryStay?.roomNumber || "",
      propertyName:
        primaryStay?.propertyName ||
        primaryContract?.propertyName ||
        "Khu trọ của bạn",
      statusLabel: statusMeta.label,
      statusTone: statusMeta.tone,
      monthlyRent: primaryContract?.monthlyRent ?? null,
      paymentDueDay: primaryContract?.paymentDueDay ?? null,
      startDate: primaryContract?.startDate || null,
      endDate: primaryContract?.endDate || null,
      contractCode: primaryContract?.contractCode || "",
      extraRoomsCount,
    };
  }

  const nonCancelledInvoices = (invoices || []).filter(
    (item) => item.status !== "Cancelled",
  );
  const unpaidInvoices = nonCancelledInvoices.filter(isUnpaidInvoice);
  const overdueInvoices = unpaidInvoices.filter(isOverdueInvoice);

  const totalAmountDue = nonCancelledInvoices.reduce(
    (sum, item) => sum + Math.max(0, Number(item.amountDue || 0)),
    0,
  );
  const overdueAmount = overdueInvoices.reduce(
    (sum, item) => sum + Math.max(0, Number(item.amountDue || 0)),
    0,
  );
  const totalCredit = nonCancelledInvoices.reduce(
    (sum, item) => sum + Math.max(0, Number(item.creditToRefund || 0)),
    0,
  );

  const sortedUnpaidByDue = [...unpaidInvoices].sort(
    (a, b) => getDateMillis(a.dueDate) - getDateMillis(b.dueDate),
  );
  const nearestDueInvoice =
    sortedUnpaidByDue.find((item) => Boolean(item.dueDate)) ||
    sortedUnpaidByDue[0] ||
    null;

  const billing = {
    totalInvoices: nonCancelledInvoices.length,
    unpaidCount: unpaidInvoices.length,
    overdueCount: overdueInvoices.length,
    totalAmountDue,
    overdueAmount,
    totalCredit,
    nearestDueDate: nearestDueInvoice?.dueDate || null,
    tone:
      overdueInvoices.length > 0
        ? "danger"
        : totalAmountDue > 0
          ? "warning"
          : "success",
  };

  const openRequestsCount = (requests || []).filter(
    (item) => item.status === "Open" || item.status === "InProgress",
  ).length;

  const unacceptedRulesCount = (rules || []).filter(
    (rule) => rule.versionId && !rule.acceptedCurrentVersion,
  ).length;

  const attentionItems = [];

  if (overdueInvoices.length > 0) {
    attentionItems.push({
      id: "overdue-invoices",
      tone: "danger",
      title: `Có ${overdueInvoices.length} hóa đơn quá hạn thanh toán`,
      detail: `Tổng còn nợ ${formatCurrency(overdueAmount)} · Cần thanh toán sớm để tránh gián đoạn dịch vụ.`,
      actionLabel: "Thanh toán ngay",
      target: "tenant/billing",
    });
  }

  if (pendingConfirmCount > 0) {
    attentionItems.push({
      id: "pending-contracts",
      tone: "warning",
      title: `Có ${pendingConfirmCount} hợp đồng chờ bạn xác nhận`,
      detail:
        "Kiểm tra điều khoản phòng thuê và xác nhận để hợp đồng sớm có hiệu lực.",
      actionLabel: "Xem hợp đồng",
      target: "tenant/contracts",
    });
  }

  if (unacceptedRulesCount > 0) {
    attentionItems.push({
      id: "unaccepted-rules",
      tone: "warning",
      title: `Có ${unacceptedRulesCount} nội quy khu trọ chưa chấp thuận`,
      detail: "Vui lòng đọc và xác nhận nội quy trong mục Hợp đồng.",
      actionLabel: "Mở hợp đồng",
      target: "tenant/contracts",
    });
  }

  if (overdueInvoices.length === 0 && totalAmountDue > 0) {
    const dueLabel = nearestDueInvoice?.dueDate
      ? `Hạn gần nhất ${formatShortDate(nearestDueInvoice.dueDate)}`
      : "Chưa thanh toán";
    const isSoon = (() => {
      if (!todayIso || !nearestDueInvoice?.dueDate) return true;
      const diffDays = Math.round(
        (getDateMillis(nearestDueInvoice.dueDate) - getDateMillis(todayIso)) /
          86400000,
      );
      return diffDays <= 14;
    })();
    if (isSoon) {
      attentionItems.push({
        id: "unpaid-invoices",
        tone: "info",
        title: `Bạn có ${unpaidInvoices.length} hóa đơn cần thanh toán`,
        detail: `${formatCurrency(totalAmountDue)} · ${dueLabel}.`,
        actionLabel: "Xem hóa đơn",
        target: "tenant/billing",
      });
    }
  }

  return {
    home,
    billing,
    activity: {
      activeContractsCount: activeContracts.length,
      pendingConfirmCount,
      totalContractsCount: (contracts || []).length,
      openRequestsCount,
      unreadNotificationsCount: Math.max(0, Number(unreadNotifications || 0)),
    },
    attentionItems: attentionItems.slice(0, 3),
  };
}
