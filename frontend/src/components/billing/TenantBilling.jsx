import { useEffect, useState } from "react";
import { billingApi } from "../../services/api/billingApi";
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

const money = (value) =>
  new Intl.NumberFormat("vi-VN").format(Number(value || 0)) + " ₫";
const newPayment = () => ({
  amount: "",
  method: "BankTransfer",
  paidAt: vietnamToday(),
  transactionCode: "",
  note: "",
  idempotencyKey: crypto.randomUUID(),
});
const paymentStatus = {
  Pending: "Chờ chủ trọ xác nhận",
  Success: "Đã xác nhận",
  Failed: "Không được xác nhận",
  Cancelled: "Đã hủy",
};

export default function TenantBilling() {
  const [page, setPage] = useState(1);
  const [invoices, setInvoices] = useState([]);
  const [meta, setMeta] = useState(null);
  const [selectedId, setSelectedId] = useState("");
  const [detail, setDetail] = useState(null);
  const [payments, setPayments] = useState([]);
  const [account, setAccount] = useState(null);
  const [form, setForm] = useState(newPayment);
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

  useEffect(() => {
    const controller = new AbortController();
    setListLoading(true);
    setListError("");
    billingApi
      .myInvoices(page, controller.signal)
      .then((response) => {
        if (controller.signal.aborted) return;
        const rows = response.data || [];
        setInvoices(rows);
        setMeta(response.meta);
        setSelectedId((current) =>
          rows.some((item) => item.id === current)
            ? current
            : rows[0]?.id || "",
        );
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setListError(cause.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setListLoading(false);
      });
    return () => controller.abort();
  }, [page, revision]);

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

  function chooseInvoice(id) {
    setSelectedId(id);
    setForm(newPayment());
    setSuccess("");
    setSubmitError("");
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
    } catch (cause) {
      setSubmitError(cause.message);
      setRevision((value) => value + 1);
    } finally {
      setBusy(false);
    }
  }

  const summary = detail?.summary;
  const pendingCount = payments.filter(
    (item) => item.status === "Pending",
  ).length;
  const hasAccount = Boolean(
    account?.bankName && account?.accountNumber && account?.accountHolder,
  );
  return (
    <div className="bill-page bill-page--tenant">
      <header className="bill-header">
        <div>
          <span>TÀI CHÍNH</span>
          <h1>Hóa đơn của tôi</h1>
          <p>
            Xem hóa đơn, khai khoản đã giao hoặc chuyển và theo dõi xác nhận của
            chủ trọ.
          </p>
        </div>
      </header>
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
        <p role="status">Đang tải hóa đơn…</p>
      ) : invoices.length ? (
        <div className="bill-grid bill-grid--two">
          <section className="bill-card">
            <h2>Hóa đơn đã phát hành</h2>
            <ul className="bill-list bill-list--select">
              {invoices.map((item) => (
                <li key={item.id}>
                  <button
                    type="button"
                    className={selectedId === item.id ? "is-active" : ""}
                    onClick={() => chooseInvoice(item.id)}
                    aria-pressed={selectedId === item.id}
                  >
                    <strong>{item.code}</strong>
                    <small>
                      {item.type === "Deposit"
                        ? "Tiền cọc"
                        : item.billingMonth
                          ? `Kỳ ${item.billingMonth}/${item.billingYear}`
                          : "Hóa đơn khác"}{" "}
                      · Hạn {item.dueDate}
                    </small>
                    <span>Còn phải thu: {money(item.amountDue)}</span>
                  </button>
                </li>
              ))}
            </ul>
            <div className="bill-pagination">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => {
                  setPage((value) => value - 1);
                  setSelectedId("");
                }}
              >
                Trang trước
              </button>
              <span>
                Trang {page} / {Math.max(1, meta?.totalPages || 1)}
              </span>
              <button
                type="button"
                disabled={page >= (meta?.totalPages || 1)}
                onClick={() => {
                  setPage((value) => value + 1);
                  setSelectedId("");
                }}
              >
                Trang sau
              </button>
            </div>
          </section>
          <div className="bill-grid">
            <section
              className="bill-card"
              aria-labelledby="tenant-invoice-detail"
            >
              <h2 id="tenant-invoice-detail">Chi tiết hóa đơn</h2>
              {detailLoading ? (
                <p role="status">Đang tải chi tiết…</p>
              ) : detailError ? (
                <p className="bill-error" role="alert">
                  {detailError}{" "}
                  <button
                    className="bill-link"
                    type="button"
                    onClick={() => setRevision((value) => value + 1)}
                  >
                    Thử lại
                  </button>
                </p>
              ) : (
                summary && (
                  <>
                    <div className="bill-flow-hero">
                      <span>
                        {summary.type === "Deposit" ? "TIỀN CỌC" : "HÓA ĐƠN"}
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
                          ? "Tiền dư cần đối soát"
                          : Number(summary.amountDue) > 0
                            ? "Còn phải thu"
                            : "Đã thanh toán đủ"}
                      </small>
                      {pendingCount > 0 && (
                        <em>
                          {pendingCount} khoản chờ xác nhận, chưa tính vào số đã
                          thu
                        </em>
                      )}
                    </div>
                    <InvoiceSummary summary={summary} />
                    <InvoiceItems items={detail.items} />
                    {!!detail.adjustments?.length && (
                      <div>
                        <h3>Phụ thu và giảm trừ sau phát hành</h3>
                        <InvoiceAdjustmentHistory adjustments={detail.adjustments} />
                      </div>
                    )}
                  </>
                )
              )}
            </section>
            {summary && <InvoiceTenants tenants={detail.tenants} selfOnly />}
            {summary && (
              <section className="bill-card bill-account-card">
                <h2>Thông tin chuyển khoản</h2>
                {accountLoading ? (
                  <p role="status">Đang tải tài khoản nhận tiền…</p>
                ) : accountError ? (
                  <p className="bill-error" role="alert">
                    Không tải được tài khoản nhận tiền: {accountError}{" "}
                    <button
                      className="bill-link"
                      type="button"
                      onClick={() => setAccountRevision((value) => value + 1)}
                    >
                      Thử lại
                    </button>
                  </p>
                ) : hasAccount ? (
                  <dl className="bill-summary">
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
                      <dd>{account.accountNumber}</dd>
                    </div>
                    <div>
                      <dt>Chủ tài khoản</dt>
                      <dd>{account.accountHolder}</dd>
                    </div>
                  </dl>
                ) : (
                  <p>
                    Chủ trọ chưa cấu hình tài khoản nhận chuyển khoản cho khu
                    trọ này. Vui lòng liên hệ chủ trọ để lấy thông tin trước khi
                    chuyển.
                  </p>
                )}
              </section>
            )}
            {summary &&
              !accountLoading &&
              !accountError &&
              account?.canDeclarePayment &&
              Number(summary.amountDue) > 0 && (
                <section className="bill-card">
                  <h2>Khai khoản đã thanh toán</h2>
                  <p>
                    Chọn phương thức bạn đã dùng. Hóa đơn chỉ cập nhật số đã thu
                    sau khi chủ trọ xác nhận.
                  </p>
                  <form
                    className="bill-form bill-payment-form"
                    onSubmit={submit}
                  >
                    <label>
                      Số tiền đã giao/chuyển *
                      <input
                        type="number"
                        min="0.01"
                        step="any"
                        required
                        aria-invalid={
                          Number(form.amount) > Number(summary.amountDue)
                        }
                        value={form.amount}
                        onChange={(event) =>
                          update("amount", event.target.value)
                        }
                      />
                      <small>
                        Còn phải thu: {money(summary.amountDue)}. Được khai một
                        phần, không vượt số này.
                      </small>
                    </label>
                    {Number(form.amount) > Number(summary.amountDue) && (
                      <p className="bill-error bill-field--wide" role="alert">
                        Số tiền khai vượt số còn phải thu. Hãy nhập lại; nếu đã
                        chuyển dư, vui lòng liên hệ chủ trọ.
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
                        <option value="BankTransfer">Đã chuyển khoản</option>
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
                          value={form.transactionCode}
                          onChange={(event) =>
                            update("transactionCode", event.target.value)
                          }
                        />
                      </label>
                    )}
                    <label className="bill-field--wide">
                      Ghi chú
                      <textarea
                        maxLength={500}
                        value={form.note}
                        onChange={(event) => update("note", event.target.value)}
                      />
                    </label>
                    {submitError && (
                      <p className="bill-error bill-field--wide" role="alert">
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
            {summary &&
              !accountLoading &&
              !accountError &&
              !account?.canDeclarePayment && (
                <p className="bill-empty bill-payment-alert">
                  Bạn vẫn xem được hóa đơn và lịch sử của mình. Chỉ thành viên
                  còn thuộc hợp đồng mới được khai khoản thanh toán mới.
                </p>
              )}
            {summary &&
              account?.canDeclarePayment &&
              Number(summary.amountDue) <= 0 && (
                <p className="bill-empty bill-payment-alert">
                  Hóa đơn đã thanh toán đủ; không còn khoản phải thu để khai.
                </p>
              )}
            {summary && (
              <section className="bill-card">
                <h2>Khoản tôi đã khai</h2>
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
                        <strong>
                          {money(item.claimedAmount)} ·{" "}
                          {item.method === "Cash" ? "Tiền mặt" : "Chuyển khoản"}
                        </strong>
                        <span
                          className="bill-payment-status"
                          data-status={item.status}
                        >
                          {paymentStatus[item.status] || item.status}
                        </span>
                        {item.verifiedAmount != null && (
                          <span>
                            Chủ trọ xác nhận thực nhận:{" "}
                            {money(item.verifiedAmount)}
                          </span>
                        )}
                        <small>
                          {item.paidAt ? formatVietnamDate(item.paidAt) : ""}
                          {item.transactionCode &&
                            ` · Mã ${item.transactionCode}`}
                        </small>
                        {item.reviewReason && (
                          <small>Lý do duyệt: {item.reviewReason}</small>
                        )}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="bill-empty">
                    Bạn chưa khai khoản thanh toán nào cho hóa đơn này.
                  </p>
                )}
              </section>
            )}
          </div>
        </div>
      ) : (
        !listError && (
          <p className="bill-empty">Bạn chưa có hóa đơn đã phát hành.</p>
        )
      )}
    </div>
  );
}
