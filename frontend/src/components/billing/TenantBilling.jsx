import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertCircle,
  Banknote,
  Check,
  CheckCircle2,
  Clock,
  Copy,
  CreditCard,
  FileText,
  History,
  ReceiptText,
  RefreshCw,
  Search,
  Wallet,
  X,
} from "lucide-react";
import { billingApi } from "../../services/api/billingApi";
import InvoiceCodeBox, { invoiceTitle } from "./InvoiceCodeBox";
import {
  vietnamToday,
  paymentDateToIso,
  formatVietnamDate,
} from "./paymentDate";
import {
  InvoiceItems,
  InvoiceSummary,
  InvoiceTenants,
} from "./LandlordBilling";
import { InvoiceAdjustmentHistory } from "./InvoiceEditing";
import "../../styles/billing-live.css";

const PAGE_SIZE = 10;

const money = (value) =>
  new Intl.NumberFormat("vi-VN").format(Number(value || 0)) + " ₫";

const formatDateOnly = (value) => {
  if (!value) return "—";
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value.split("-").reverse().join("/");
  }
  return value;
};

const newPayment = () => ({
  amount: "",
  method: "BankTransfer",
  paidAt: vietnamToday(),
  transactionCode: "",
  note: "",
  idempotencyKey: crypto.randomUUID(),
});

const invoiceStatusLabel = {
  Draft: "Bản nháp",
  Issued: "Đã phát hành",
  Cancelled: "Đã hủy",
  Paid: "Đã thanh toán",
  Partial: "Đã thu một phần",
  Unpaid: "Chưa thanh toán",
  Overdue: "Quá hạn",
  Credit: "Dư tiền",
};

const invoiceTypeLabel = (type) => {
  if (type === "Deposit") return "Tiền cọc";
  if (type === "Monthly") return "Hóa đơn hàng tháng";
  if (type === "OneOff") return "Hóa đơn phát sinh";
  return "Hóa đơn khác";
};

const periodLabel = (item) => {
  if (!item) return "";
  if (item.billingMonth && item.billingYear) {
    return `Kỳ ${item.billingMonth}/${item.billingYear}`;
  }
  return invoiceTypeLabel(item.type);
};

const paymentStatus = {
  Pending: "Chờ chủ trọ xác nhận",
  Success: "Đã xác nhận",
  Failed: "Không được xác nhận",
  Cancelled: "Đã hủy",
};

const isUnpaidInvoice = (item) =>
  item.status !== "Cancelled" &&
  (Number(item.amountDue) > 0 ||
    ["Unpaid", "Partial", "Overdue"].includes(item.displayStatus));

const isOverdueInvoice = (item) =>
  item.status !== "Cancelled" && item.displayStatus === "Overdue";

const isPaidInvoice = (item) =>
  item.status !== "Cancelled" &&
  !isUnpaidInvoice(item) &&
  Number(item.creditToRefund || 0) <= 0 &&
  item.displayStatus !== "Credit";

export default function TenantBilling({ initialInvoiceId = "" }) {
  const [invoices, setInvoices] = useState([]);
  const [selectedId, setSelectedId] = useState(initialInvoiceId);
  const [detail, setDetail] = useState(null);
  const [payments, setPayments] = useState([]);
  const [account, setAccount] = useState(null);
  const [form, setForm] = useState(newPayment);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [yearFilter, setYearFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [activeTab, setActiveTab] = useState("detail");
  const [copiedKey, setCopiedKey] = useState("");

  const [listLoading, setListLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [accountLoading, setAccountLoading] = useState(false);
  const [listError, setListError] = useState("");
  const [detailError, setDetailError] = useState("");
  const [historyError, setHistoryError] = useState("");
  const [accountError, setAccountError] = useState("");
  const [submitError, setSubmitError] = useState("");
  const [success, setSuccess] = useState("");
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const [accountRevision, setAccountRevision] = useState(0);

  const detailRef = useRef(null);
  const amountInputRef = useRef(null);
  const copyTimerRef = useRef(null);

  useEffect(() => () => clearTimeout(copyTimerRef.current), []);

  useEffect(() => {
    const controller = new AbortController();
    setListLoading(true);
    setListError("");
    billingApi
      .allMyInvoices(controller.signal)
      .then((response) => {
        if (controller.signal.aborted) return;
        const rows = response.data || [];
        setInvoices(rows);
        setSelectedId((current) => {
          if (current && rows.some((item) => item.id === current)) return current;
          const firstActionable =
            rows.find(isOverdueInvoice) || rows.find(isUnpaidInvoice) || rows[0];
          return firstActionable?.id || "";
        });
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setListError(cause.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setListLoading(false);
      });
    return () => controller.abort();
  }, [revision]);

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      return;
    }
    const controller = new AbortController();
    setDetailLoading(true);
    setDetailError("");
    setDetail(null);
    billingApi
      .myInvoice(selectedId, controller.signal)
      .then((invoice) => {
        if (!controller.signal.aborted) setDetail(invoice);
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setDetailError(cause.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setDetailLoading(false);
      });
    return () => controller.abort();
  }, [selectedId, revision]);

  useEffect(() => {
    if (!selectedId) {
      setPayments([]);
      return;
    }
    const controller = new AbortController();
    setHistoryLoading(true);
    setHistoryError("");
    setPayments([]);
    billingApi
      .myPayments(selectedId, controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted) setPayments(rows || []);
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setHistoryError(cause.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setHistoryLoading(false);
      });
    return () => controller.abort();
  }, [selectedId, revision]);

  useEffect(() => {
    if (!selectedId) {
      setAccount(null);
      return;
    }
    const controller = new AbortController();
    setAccountLoading(true);
    setAccountError("");
    setAccount(null);
    billingApi
      .myPaymentAccount(selectedId, controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) setAccount(value);
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setAccountError(cause.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setAccountLoading(false);
      });
    return () => controller.abort();
  }, [selectedId, accountRevision]);

  const stats = useMemo(() => {
    let totalDue = 0;
    let totalCredit = 0;
    let unpaidCount = 0;
    let overdueCount = 0;
    let paidCount = 0;
    let otherCount = 0;

    for (const item of invoices) {
      if (item.status !== "Cancelled") {
        totalDue += Math.max(0, Number(item.amountDue || 0));
        totalCredit += Math.max(0, Number(item.creditToRefund || 0));
      }
      if (isOverdueInvoice(item)) overdueCount += 1;
      if (isUnpaidInvoice(item)) unpaidCount += 1;
      else if (isPaidInvoice(item)) paidCount += 1;
      else otherCount += 1;
    }

    return {
      totalDue,
      totalCredit,
      unpaidCount,
      overdueCount,
      paidCount,
      otherCount,
    };
  }, [invoices]);

  const availableYears = useMemo(() => {
    const years = new Set();
    for (const item of invoices) {
      if (item.billingYear) years.add(String(item.billingYear));
    }
    return Array.from(years).sort((a, b) => Number(b) - Number(a));
  }, [invoices]);

  const availableTypes = useMemo(() => {
    const types = new Set();
    for (const item of invoices) {
      if (item.type) types.add(item.type);
    }
    return Array.from(types);
  }, [invoices]);

  const filteredInvoices = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return invoices.filter((item) => {
      if (statusFilter === "unpaid" && !isUnpaidInvoice(item)) return false;
      if (statusFilter === "overdue" && !isOverdueInvoice(item)) return false;
      if (statusFilter === "paid" && !isPaidInvoice(item)) return false;
      if (
        statusFilter === "other" &&
        (isUnpaidInvoice(item) || isPaidInvoice(item))
      ) {
        return false;
      }
      if (typeFilter !== "all" && item.type !== typeFilter) return false;
      if (yearFilter !== "all" && String(item.billingYear || "") !== yearFilter) {
        return false;
      }
      if (!keyword) return true;
      const haystack = [
        item.code,
        item.billingMonth && item.billingYear
          ? `${item.billingMonth}/${item.billingYear}`
          : "",
        invoiceTypeLabel(item.type),
        invoiceStatusLabel[item.displayStatus] || item.displayStatus,
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(keyword);
    });
  }, [invoices, search, statusFilter, typeFilter, yearFilter]);

  const totalPages = Math.max(1, Math.ceil(filteredInvoices.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pagedInvoices = useMemo(() => {
    const start = (safePage - 1) * PAGE_SIZE;
    return filteredInvoices.slice(start, start + PAGE_SIZE);
  }, [filteredInvoices, safePage]);

  useEffect(() => {
    setPage(1);
  }, [search, statusFilter, typeFilter, yearFilter]);

  useEffect(() => {
    if (!filteredInvoices.length) return;
    if (!filteredInvoices.some((item) => item.id === selectedId)) {
      setSelectedId(filteredInvoices[0].id);
    }
  }, [filteredInvoices, selectedId]);

  function chooseInvoice(id) {
    setSelectedId(id);
    setForm(newPayment());
    setSuccess("");
    setSubmitError("");
    setActiveTab("detail");
    if (window.innerWidth <= 1050 && detailRef.current) {
      detailRef.current.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }

  function clearFilters() {
    setSearch("");
    setStatusFilter("all");
    setTypeFilter("all");
    setYearFilter("all");
    setPage(1);
  }

  function copyValue(key, text) {
    if (!text || !navigator?.clipboard) return;
    navigator.clipboard
      .writeText(String(text))
      .then(() => {
        setCopiedKey(key);
        clearTimeout(copyTimerRef.current);
        copyTimerRef.current = setTimeout(() => setCopiedKey(""), 2200);
      })
      .catch(() => {});
  }

  function openPaymentTab(prefillAmount) {
    if (prefillAmount) {
      setForm((current) => ({
        ...current,
        amount: String(prefillAmount),
        idempotencyKey: crypto.randomUUID(),
      }));
    }
    setActiveTab("payment");
    setTimeout(() => {
      amountInputRef.current?.focus();
    }, 60);
  }

  function update(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
      idempotencyKey: crypto.randomUUID(),
    }));
  }

  async function submit(event) {
    event.preventDefault();
    setSubmitError("");
    setSuccess("");
    const amount = Number(form.amount);
    if (
      !Number.isFinite(amount) ||
      amount <= 0 ||
      amount > Number(detail?.summary?.amountDue)
    ) {
      setSubmitError(
        "Số tiền khai phải lớn hơn 0 và không vượt số còn phải thu. Nếu đã chuyển dư, vui lòng liên hệ chủ trọ để đối soát.",
      );
      return;
    }
    if (
      !window.confirm(
        `Bạn xác nhận đã ${form.method === "Cash" ? "giao tiền mặt" : "chuyển khoản"} ${money(amount)}? Hãy kiểm tra đúng số tiền thực tế trước khi gửi.`,
      )
    )
      return;
    setBusy(true);
    try {
      await billingApi.submitPayment(selectedId, {
        ...form,
        amount: Number(form.amount),
        paidAt: paymentDateToIso(form.paidAt),
        transactionCode:
          form.method === "BankTransfer"
            ? form.transactionCode.trim() || null
            : null,
        note: form.note.trim() || null,
      });
      setSuccess(
        "Đã gửi thông tin thanh toán. Chủ trọ sẽ kiểm tra và xác nhận.",
      );
      setForm(newPayment());
      setRevision((value) => value + 1);
      setActiveTab("history");
    } catch (cause) {
      setSubmitError(cause.message);
      setRevision((value) => value + 1);
    } finally {
      setBusy(false);
    }
  }

  const summary = detail?.summary;
  const selectedRow = invoices.find((item) => item.id === selectedId) || null;
  const pendingCount = payments.filter((item) => item.status === "Pending").length;
  const hasAccount = Boolean(
    account?.bankName && account?.accountNumber && account?.accountHolder,
  );
  const hasActiveFilters =
    Boolean(search.trim()) ||
    statusFilter !== "all" ||
    typeFilter !== "all" ||
    yearFilter !== "all";
  const canDeclare = Boolean(
    summary &&
      !accountLoading &&
      !accountError &&
      account?.canDeclarePayment &&
      Number(summary.amountDue) > 0,
  );

  const filterChips = [
    { id: "all", label: "Tất cả", count: invoices.length },
    { id: "unpaid", label: "Cần trả", count: stats.unpaidCount },
    { id: "overdue", label: "Quá hạn", count: stats.overdueCount },
    { id: "paid", label: "Đã xong", count: stats.paidCount },
    ...(stats.otherCount > 0
      ? [{ id: "other", label: "Khác", count: stats.otherCount }]
      : []),
  ];

  return (
    <div className="bill-page bill-page--tenant">
      <header className="bill-header tb-header">
        <div>
          <span>TÀI CHÍNH</span>
          <h1>Hóa đơn của tôi</h1>
          <p>
            Theo dõi công nợ, xem chi tiết từng khoản thu, khai báo chuyển khoản
            và kiểm tra lịch sử xác nhận.
          </p>
        </div>
        <button
          type="button"
          className="bill-button bill-button--secondary tb-refresh-btn"
          disabled={listLoading}
          onClick={() => setRevision((value) => value + 1)}
        >
          <RefreshCw size={15} aria-hidden="true" />
          Làm mới
        </button>
      </header>

      {!listLoading && invoices.length > 0 && (
        <section className="tb-kpis" aria-label="Tổng quan hóa đơn">
          <div className="tb-kpi-card tb-kpi-card--balance">
            <span className="tb-kpi-icon" aria-hidden="true">
              <Wallet size={19} />
            </span>
            <div>
              <small>Tổng còn phải trả</small>
              <strong>{money(stats.totalDue)}</strong>
              <em>
                {stats.totalCredit > 0
                  ? `Có ${money(stats.totalCredit)} tiền dư chờ đối soát`
                  : stats.totalDue > 0
                    ? "Tổng số dư các hóa đơn chưa xong"
                    : "Tất cả hóa đơn đã hoàn tất"}
              </em>
            </div>
          </div>

          <button
            type="button"
            className={`tb-kpi-card tb-kpi-card--button ${statusFilter === "unpaid" ? "is-active" : ""}`}
            onClick={() =>
              setStatusFilter((cur) => (cur === "unpaid" ? "all" : "unpaid"))
            }
          >
            <span className="tb-kpi-icon tb-kpi-icon--warn" aria-hidden="true">
              <Clock size={19} />
            </span>
            <div>
              <small>Cần thanh toán</small>
              <strong>{stats.unpaidCount} hóa đơn</strong>
              <em>
                {stats.unpaidCount > 0
                  ? "Nhấn để lọc hóa đơn chưa xong"
                  : "Không có hóa đơn chờ trả"}
              </em>
            </div>
          </button>

          <button
            type="button"
            className={`tb-kpi-card tb-kpi-card--button ${statusFilter === "overdue" ? "is-active" : ""}`}
            onClick={() =>
              setStatusFilter((cur) => (cur === "overdue" ? "all" : "overdue"))
            }
          >
            <span
              className={`tb-kpi-icon ${stats.overdueCount > 0 ? "tb-kpi-icon--danger" : "tb-kpi-icon--muted"}`}
              aria-hidden="true"
            >
              <AlertCircle size={19} />
            </span>
            <div>
              <small>Quá hạn thanh toán</small>
              <strong>{stats.overdueCount} hóa đơn</strong>
              <em>
                {stats.overdueCount > 0
                  ? "Cần ưu tiên thanh toán sớm"
                  : "Không có hóa đơn quá hạn"}
              </em>
            </div>
          </button>

          <button
            type="button"
            className={`tb-kpi-card tb-kpi-card--button ${statusFilter === "paid" ? "is-active" : ""}`}
            onClick={() =>
              setStatusFilter((cur) => (cur === "paid" ? "all" : "paid"))
            }
          >
            <span className="tb-kpi-icon tb-kpi-icon--ok" aria-hidden="true">
              <CheckCircle2 size={19} />
            </span>
            <div>
              <small>Tổng hóa đơn</small>
              <strong>{invoices.length} hóa đơn</strong>
              <em>{stats.paidCount} hóa đơn đã thanh toán đủ</em>
            </div>
          </button>
        </section>
      )}

      {success && (
        <p className="bill-success" role="status">
          {success}
        </p>
      )}

      {listError && (
        <p className="bill-error" role="alert">
          Không tải được danh sách hóa đơn: {listError}{" "}
          <button
            className="bill-link"
            type="button"
            onClick={() => setRevision((value) => value + 1)}
          >
            Thử lại
          </button>
        </p>
      )}

      {listLoading ? (
        <section className="bill-card tb-state" role="status">
          <ReceiptText size={28} aria-hidden="true" />
          <strong>Đang tải danh sách hóa đơn…</strong>
          <p>Vui lòng chờ trong giây lát.</p>
        </section>
      ) : invoices.length ? (
        <div className="tb-workspace">
          <section
            className="bill-card tb-list-panel"
            aria-label="Danh sách hóa đơn"
          >
            <div className="tb-list-head">
              <div>
                <h2>Hóa đơn đã phát hành</h2>
                <small>
                  Hiển thị {filteredInvoices.length} / {invoices.length} hóa đơn
                </small>
              </div>
            </div>

            <div className="tb-search-box">
              <Search size={16} aria-hidden="true" />
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Tìm mã hóa đơn, kỳ (vd 10/2026)…"
                aria-label="Tìm kiếm hóa đơn"
              />
              {search && (
                <button
                  type="button"
                  className="tb-search-clear"
                  onClick={() => setSearch("")}
                  aria-label="Xóa từ khóa tìm kiếm"
                >
                  <X size={14} aria-hidden="true" />
                </button>
              )}
            </div>

            <div
              className="tb-filter-chips"
              role="group"
              aria-label="Lọc theo trạng thái hóa đơn"
            >
              {filterChips.map((chip) => (
                <button
                  key={chip.id}
                  type="button"
                  className={statusFilter === chip.id ? "is-active" : ""}
                  onClick={() => setStatusFilter(chip.id)}
                  aria-pressed={statusFilter === chip.id}
                >
                  {chip.label}
                  <span>{chip.count}</span>
                </button>
              ))}
            </div>

            {(availableTypes.length > 1 || availableYears.length > 1) && (
              <div className="tb-extra-filters">
                {availableTypes.length > 1 && (
                  <select
                    aria-label="Lọc theo loại hóa đơn"
                    value={typeFilter}
                    onChange={(event) => setTypeFilter(event.target.value)}
                  >
                    <option value="all">Tất cả loại</option>
                    {availableTypes.map((type) => (
                      <option key={type} value={type}>
                        {invoiceTypeLabel(type)}
                      </option>
                    ))}
                  </select>
                )}
                {availableYears.length > 1 && (
                  <select
                    aria-label="Lọc theo năm"
                    value={yearFilter}
                    onChange={(event) => setYearFilter(event.target.value)}
                  >
                    <option value="all">Tất cả năm</option>
                    {availableYears.map((year) => (
                      <option key={year} value={year}>
                        Năm {year}
                      </option>
                    ))}
                  </select>
                )}
              </div>
            )}

            {pagedInvoices.length ? (
              <>
                <ul className="tb-invoice-list">
                  {pagedInvoices.map((item) => {
                    const active = selectedId === item.id;
                    const due = Number(item.amountDue || 0);
                    const credit = Number(item.creditToRefund || 0);
                    return (
                      <li key={item.id}>
                        <button
                          type="button"
                          className={`tb-invoice-item ${active ? "is-active" : ""}`}
                          onClick={() => chooseInvoice(item.id)}
                          aria-pressed={active}
                        >
                          <div className="tb-invoice-item__top">
                            <strong>{item.code}</strong>
                            <span
                              className="bill-payment-status"
                              data-status={item.displayStatus}
                            >
                              {invoiceStatusLabel[item.displayStatus] ||
                                item.displayStatus}
                            </span>
                          </div>
                          <div className="tb-invoice-item__meta">
                            <span>{periodLabel(item)}</span>
                            <span
                              className={
                                isOverdueInvoice(item) ? "tb-due-overdue" : ""
                              }
                            >
                              Hạn {formatDateOnly(item.dueDate)}
                            </span>
                          </div>
                          <div className="tb-invoice-item__bottom">
                            <small>Tổng {money(item.effectiveTotal)}</small>
                            {credit > 0 ? (
                              <strong className="tb-amount-credit">
                                Dư {money(credit)}
                              </strong>
                            ) : due > 0 ? (
                              <strong className="tb-amount-due">
                                Còn {money(due)}
                              </strong>
                            ) : (
                              <strong className="tb-amount-paid">
                                Đã thanh toán
                              </strong>
                            )}
                          </div>
                        </button>
                      </li>
                    );
                  })}
                </ul>

                {totalPages > 1 && (
                  <div className="bill-pagination tb-pagination">
                    <button
                      type="button"
                      disabled={safePage <= 1}
                      onClick={() => setPage((value) => value - 1)}
                    >
                      Trang trước
                    </button>
                    <span>
                      Trang {safePage} / {totalPages}
                    </span>
                    <button
                      type="button"
                      disabled={safePage >= totalPages}
                      onClick={() => setPage((value) => value + 1)}
                    >
                      Trang sau
                    </button>
                  </div>
                )}
              </>
            ) : (
              <div className="tb-filter-empty">
                <ReceiptText size={24} aria-hidden="true" />
                <strong>Không có hóa đơn phù hợp</strong>
                <p>Thử thay đổi từ khóa tìm kiếm hoặc bộ lọc trạng thái.</p>
                {hasActiveFilters && (
                  <button
                    type="button"
                    className="bill-button bill-button--secondary bill-btn-compact"
                    onClick={clearFilters}
                  >
                    Xóa bộ lọc
                  </button>
                )}
              </div>
            )}
          </section>

          <div className="tb-detail-column" ref={detailRef}>
            {!selectedId || (!filteredInvoices.length && hasActiveFilters) ? (
              <section className="bill-card tb-state">
                <ReceiptText size={28} aria-hidden="true" />
                <strong>Chọn một hóa đơn để xem chi tiết</strong>
                <p>
                  {hasActiveFilters && !filteredInvoices.length
                    ? "Không có hóa đơn khớp bộ lọc hiện tại."
                    : "Nhấn vào hóa đơn ở danh sách bên trái để kiểm tra khoản thu và khai báo thanh toán."}
                </p>
              </section>
            ) : detailLoading ? (
              <section className="bill-card tb-state" role="status">
                <ReceiptText size={28} aria-hidden="true" />
                <strong>Đang tải chi tiết hóa đơn…</strong>
              </section>
            ) : detailError ? (
              <section className="bill-card tb-state" role="alert">
                <AlertCircle size={28} aria-hidden="true" />
                <strong>Không thể tải chi tiết hóa đơn</strong>
                <p>{detailError}</p>
                <button
                  className="bill-button bill-button--secondary"
                  type="button"
                  onClick={() => setRevision((value) => value + 1)}
                >
                  Thử lại
                </button>
              </section>
            ) : summary ? (
              <section
                className="bill-card tb-detail-card"
                aria-labelledby="tenant-invoice-detail"
              >
                <div className="tb-detail-header">
                  <div>
                    <div className="tb-detail-badges">
                      <span className="tb-type-pill">
                        {invoiceTypeLabel(summary.type)}
                      </span>
                      <span
                        className="bill-payment-status"
                        data-status={summary.displayStatus}
                      >
                        {invoiceStatusLabel[summary.displayStatus] ||
                          summary.displayStatus}
                      </span>
                    </div>
                    <h2
                      id="tenant-invoice-detail"
                      className="tb-detail-title"
                    >
                      {invoiceTitle(summary)}
                    </h2>
                    <p className="tb-detail-subtitle">
                      {periodLabel(summary)} · Phát hành{" "}
                      {formatDateOnly(summary.issueDate)} · Hạn thanh toán{" "}
                      <strong>{formatDateOnly(summary.dueDate)}</strong>
                    </p>
                  </div>
                  <InvoiceCodeBox code={summary.code} />
                </div>

                <div
                  className={`bill-flow-hero tb-hero ${
                    Number(summary.creditToRefund) > 0
                      ? "tb-hero--credit"
                      : Number(summary.amountDue) > 0
                        ? "tb-hero--due"
                        : "tb-hero--paid"
                  }`}
                >
                  <div>
                    <span>
                      {Number(summary.creditToRefund) > 0
                        ? "TIỀN DƯ CẦN ĐỐI SOÁT"
                        : Number(summary.amountDue) > 0
                          ? "SỐ TIỀN CÒN PHẢI THU"
                          : "TRẠNG THÁI THANH TOÁN"}
                    </span>
                    <strong>
                      {money(
                        Number(summary.creditToRefund) > 0
                          ? summary.creditToRefund
                          : Math.max(0, Number(summary.amountDue)),
                      )}
                    </strong>
                    <small>
                      {Number(summary.creditToRefund) > 0
                        ? "Khoản thanh toán vượt tổng hiệu lực, liên hệ chủ trọ để hoàn/cấn trừ."
                        : Number(summary.amountDue) > 0
                          ? `Tổng hiệu lực ${money(summary.effectiveTotal)} · Đã ghi nhận ${money(summary.totalPaid)}`
                          : "Hóa đơn đã được thanh toán đủ."}
                    </small>
                  </div>
                  <div className="tb-hero-actions">
                    {pendingCount > 0 && (
                      <em className="tb-pending-note">
                        {pendingCount} khoản chờ chủ trọ xác nhận (chưa trừ vào
                        số dư)
                      </em>
                    )}
                    {canDeclare && activeTab !== "payment" && (
                      <button
                        type="button"
                        className="bill-button"
                        onClick={() => openPaymentTab()}
                      >
                        <CreditCard size={16} aria-hidden="true" />
                        Khai báo thanh toán
                      </button>
                    )}
                  </div>
                </div>

                <div
                  className="bill-tabs tb-detail-tabs"
                  role="tablist"
                  aria-label="Các mục chi tiết hóa đơn"
                >
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeTab === "detail"}
                    className={activeTab === "detail" ? "is-active" : ""}
                    onClick={() => setActiveTab("detail")}
                  >
                    <FileText size={15} aria-hidden="true" />
                    Chi tiết & Khoản thu
                  </button>
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeTab === "payment"}
                    className={activeTab === "payment" ? "is-active" : ""}
                    onClick={() => setActiveTab("payment")}
                  >
                    <Banknote size={15} aria-hidden="true" />
                    Thanh toán & Chuyển khoản
                    {canDeclare && <span className="tb-tab-dot" aria-hidden="true" />}
                  </button>
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeTab === "history"}
                    className={activeTab === "history" ? "is-active" : ""}
                    onClick={() => setActiveTab("history")}
                  >
                    <History size={15} aria-hidden="true" />
                    Lịch sử khai báo ({payments.length})
                    {pendingCount > 0 && (
                      <span className="tb-tab-badge">{pendingCount} chờ</span>
                    )}
                  </button>
                </div>

                {activeTab === "detail" && (
                  <div className="tb-tab-panel" role="tabpanel">
                    <section className="tb-subpanel">
                      <h3>Tổng hợp hóa đơn</h3>
                      <InvoiceSummary summary={summary} />
                    </section>

                    <section className="tb-subpanel">
                      <h3>Các khoản thu kỳ này</h3>
                      <InvoiceItems items={detail.items} />
                    </section>

                    {!!detail.adjustments?.length && (
                      <section className="tb-subpanel">
                        <h3>Phụ thu và giảm trừ sau phát hành</h3>
                        <InvoiceAdjustmentHistory
                          adjustments={detail.adjustments}
                        />
                      </section>
                    )}

                    <div className="tb-tenants-wrap">
                      <InvoiceTenants tenants={detail.tenants} selfOnly />
                    </div>
                  </div>
                )}

                {activeTab === "payment" && (
                  <div className="tb-tab-panel" role="tabpanel">
                    <section className="bill-card bill-account-card tb-account-box">
                      <div className="tb-account-head">
                        <div>
                          <h3>Thông tin chuyển khoản</h3>
                          <p>
                            Chuyển đúng số tài khoản dưới đây và giữ lại mã giao
                            dịch để khai báo nhanh hơn.
                          </p>
                        </div>
                      </div>

                      {accountLoading ? (
                        <p role="status">Đang tải tài khoản nhận tiền…</p>
                      ) : accountError ? (
                        <p className="bill-error" role="alert">
                          Không tải được tài khoản nhận tiền: {accountError}{" "}
                          <button
                            className="bill-link"
                            type="button"
                            onClick={() =>
                              setAccountRevision((value) => value + 1)
                            }
                          >
                            Thử lại
                          </button>
                        </p>
                      ) : hasAccount ? (
                        <dl className="bill-summary tb-account-grid">
                          <div>
                            <dt>Khu trọ</dt>
                            <dd>{account.propertyName}</dd>
                          </div>
                          <div>
                            <dt>Ngân hàng</dt>
                            <dd>{account.bankName}</dd>
                          </div>
                          <div>
                            <dt>Số tài khoản</dt>
                            <dd className="tb-copyable-dd">
                              <span>{account.accountNumber}</span>
                              <button
                                type="button"
                                className="tb-copy-chip"
                                onClick={() =>
                                  copyValue("stk", account.accountNumber)
                                }
                              >
                                {copiedKey === "stk" ? (
                                  <>
                                    <Check size={12} aria-hidden="true" /> Đã chép
                                  </>
                                ) : (
                                  <>
                                    <Copy size={12} aria-hidden="true" /> Sao chép
                                  </>
                                )}
                              </button>
                            </dd>
                          </div>
                          <div>
                            <dt>Chủ tài khoản</dt>
                            <dd>{account.accountHolder}</dd>
                          </div>
                          <div>
                            <dt>Nội dung CK gợi ý</dt>
                            <dd className="tb-copyable-dd">
                              <span>{summary.code}</span>
                              <button
                                type="button"
                                className="tb-copy-chip"
                                onClick={() => copyValue("memo", summary.code)}
                              >
                                {copiedKey === "memo" ? (
                                  <>
                                    <Check size={12} aria-hidden="true" /> Đã chép
                                  </>
                                ) : (
                                  <>
                                    <Copy size={12} aria-hidden="true" /> Sao chép
                                  </>
                                )}
                              </button>
                            </dd>
                          </div>
                        </dl>
                      ) : (
                        <p className="bill-empty">
                          Chủ trọ chưa cấu hình tài khoản nhận chuyển khoản cho
                          khu trọ này. Vui lòng liên hệ chủ trọ để lấy thông tin
                          trước khi chuyển.
                        </p>
                      )}
                    </section>

                    {!accountLoading &&
                      !accountError &&
                      account?.canDeclarePayment &&
                      Number(summary.amountDue) > 0 && (
                        <section className="tb-subpanel tb-declare-box">
                          <h3>Khai khoản đã thanh toán</h3>
                          <p className="bill-hint">
                            Chọn phương thức bạn đã dùng. Hóa đơn chỉ cập nhật
                            số đã thu sau khi chủ trọ xác nhận.
                          </p>
                          <form
                            className="bill-form bill-payment-form"
                            onSubmit={submit}
                          >
                            <label>
                              Số tiền đã giao/chuyển *
                              <div className="tb-amount-input-row">
                                <input
                                  ref={amountInputRef}
                                  type="number"
                                  min="0.01"
                                  step="any"
                                  required
                                  aria-invalid={
                                    Number(form.amount) >
                                    Number(summary.amountDue)
                                  }
                                  value={form.amount}
                                  onChange={(event) =>
                                    update("amount", event.target.value)
                                  }
                                />
                                <button
                                  type="button"
                                  className="bill-button bill-button--secondary bill-btn-compact"
                                  onClick={() =>
                                    update("amount", String(summary.amountDue))
                                  }
                                >
                                  Điền đủ {money(summary.amountDue)}
                                </button>
                              </div>
                              <small>
                                Còn phải thu: {money(summary.amountDue)}. Được
                                khai một phần, không vượt số này.
                              </small>
                            </label>
                            {Number(form.amount) > Number(summary.amountDue) && (
                              <p
                                className="bill-error bill-field--wide"
                                role="alert"
                              >
                                Số tiền khai vượt số còn phải thu. Hãy nhập lại;
                                nếu đã chuyển dư, vui lòng liên hệ chủ trọ.
                              </p>
                            )}
                            <label>
                              Phương thức
                              <select
                                value={form.method}
                                onChange={(event) =>
                                  update("method", event.target.value)
                                }
                              >
                                <option value="BankTransfer">
                                  Đã chuyển khoản
                                </option>
                                <option value="Cash">Đã giao tiền mặt</option>
                              </select>
                            </label>
                            <label>
                              Ngày đã giao/chuyển *
                              <input
                                type="date"
                                max={vietnamToday()}
                                required
                                value={form.paidAt}
                                onChange={(event) =>
                                  update("paidAt", event.target.value)
                                }
                              />
                            </label>
                            {form.method === "BankTransfer" && (
                              <label>
                                Mã giao dịch (nếu có)
                                <input
                                  maxLength={150}
                                  placeholder="VD: FT26318..."
                                  value={form.transactionCode}
                                  onChange={(event) =>
                                    update(
                                      "transactionCode",
                                      event.target.value,
                                    )
                                  }
                                />
                              </label>
                            )}
                            <label className="bill-field--wide">
                              Ghi chú
                              <textarea
                                maxLength={500}
                                placeholder="Thông tin thêm để chủ trọ dễ đối soát…"
                                value={form.note}
                                onChange={(event) =>
                                  update("note", event.target.value)
                                }
                              />
                            </label>
                            {submitError && (
                              <p
                                className="bill-error bill-field--wide"
                                role="alert"
                              >
                                {submitError}
                              </p>
                            )}
                            <button
                              className="bill-button"
                              type="submit"
                              disabled={busy || !form.amount}
                            >
                              {busy ? "Đang gửi…" : "Gửi yêu cầu xác nhận"}
                            </button>
                          </form>
                        </section>
                      )}

                    {!accountLoading &&
                      !accountError &&
                      !account?.canDeclarePayment && (
                        <p className="bill-empty bill-payment-alert">
                          Bạn vẫn xem được hóa đơn và lịch sử của mình. Chỉ thành
                          viên còn thuộc hợp đồng mới được khai khoản thanh toán
                          mới.
                        </p>
                      )}

                    {account?.canDeclarePayment &&
                      Number(summary.amountDue) <= 0 && (
                        <p className="bill-empty bill-payment-alert">
                          Hóa đơn đã thanh toán đủ; không còn khoản phải thu để
                          khai.
                        </p>
                      )}
                  </div>
                )}

                {activeTab === "history" && (
                  <div className="tb-tab-panel" role="tabpanel">
                    <div className="tb-history-head">
                      <h3>Khoản tôi đã khai</h3>
                      {canDeclare && (
                        <button
                          type="button"
                          className="bill-button bill-button--secondary bill-btn-compact"
                          onClick={() => openPaymentTab()}
                        >
                          Khai thêm khoản mới
                        </button>
                      )}
                    </div>
                    {historyLoading ? (
                      <p role="status">Đang tải lịch sử…</p>
                    ) : historyError ? (
                      <p className="bill-error" role="alert">
                        {historyError}{" "}
                        <button
                          className="bill-link"
                          type="button"
                          onClick={() => setRevision((value) => value + 1)}
                        >
                          Thử lại
                        </button>
                      </p>
                    ) : payments.length ? (
                      <ul className="bill-payment-list">
                        {payments.map((item) => (
                          <li key={item.id}>
                            <div className="tb-payment-row-top">
                              <strong>
                                {money(item.claimedAmount)} ·{" "}
                                {item.method === "Cash"
                                  ? "Tiền mặt"
                                  : "Chuyển khoản"}
                              </strong>
                              <span
                                className="bill-payment-status"
                                data-status={item.status}
                              >
                                {paymentStatus[item.status] || item.status}
                              </span>
                            </div>
                            {item.verifiedAmount != null && (
                              <span>
                                Chủ trọ xác nhận thực nhận:{" "}
                                <strong>{money(item.verifiedAmount)}</strong>
                              </span>
                            )}
                            <small>
                              {item.paidAt
                                ? `Ngày ${formatVietnamDate(item.paidAt)}`
                                : ""}
                              {item.transactionCode &&
                                ` · Mã GD: ${item.transactionCode}`}
                            </small>
                            {item.note && <small>Ghi chú: {item.note}</small>}
                            {item.reviewReason && (
                              <small>Lý do duyệt: {item.reviewReason}</small>
                            )}
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <div className="tb-filter-empty">
                        <History size={24} aria-hidden="true" />
                        <strong>Chưa có khai báo thanh toán</strong>
                        <p>
                          Bạn chưa khai khoản thanh toán nào cho hóa đơn{" "}
                          {selectedRow?.code || summary.code}.
                        </p>
                        {canDeclare && (
                          <button
                            type="button"
                            className="bill-button bill-btn-compact"
                            onClick={() => openPaymentTab()}
                          >
                            Khai báo thanh toán ngay
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                )}
              </section>
            ) : null}
          </div>
        </div>
      ) : (
        !listError && (
          <section className="bill-card tb-state">
            <ReceiptText size={28} aria-hidden="true" />
            <strong>Bạn chưa có hóa đơn đã phát hành</strong>
            <p>
              Khi chủ trọ phát hành hóa đơn tiền cọc hoặc tiền phòng hằng tháng,
              thông tin sẽ hiển thị tại đây.
            </p>
          </section>
        )
      )}
    </div>
  );
}
