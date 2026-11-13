import { useState } from "react";
import {
  AlertCircle,
  Check,
  ChevronRight,
  Clock,
  Receipt,
  Zap,
} from "lucide-react";

const money = (value) =>
  new Intl.NumberFormat("vi-VN").format(Number(value || 0)) + " ₫";

const missingLabel = {
  ElectricityReading: "Chỉ số điện",
  WaterReading: "Chỉ số nước",
  ElectricityRate: "Giá điện",
  WaterRate: "Giá nước",
};

const workPriority = {
  MissingInputs: 0,
  NeedsAttention: 1,
  ReadyToPreview: 2,
  Draft: 3,
  Issued: 4,
};
const debtPriority = { Overdue: 0, Partial: 1, Unpaid: 2 };
const lastClosedMonth = () => {
  const current = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 7);
  const [year, month] = current.split("-").map(Number);
  return new Date(Date.UTC(year, month - 2, 1)).toISOString().slice(0, 7);
};

export default function BillingPriorityBoard({
  month,
  onMonthChange,
  worklist,
  worklistError,
  worklistLoading,
  attention,
  attentionError,
  attentionLoading,
  roomName,
  onWorkItem,
  onOpenInvoice,
}) {
  const work = [...worklist].sort(
    (a, b) =>
      (workPriority[a.status] ?? 5) - (workPriority[b.status] ?? 5) ||
      String(a.roomNumber).localeCompare(String(b.roomNumber), "vi")
  );
  const needsInput = work.filter(
    (item) => item.status === "MissingInputs" || item.status === "NeedsAttention"
  );
  const otherWork = work.filter(
    (item) =>
      item.status !== "MissingInputs" &&
      item.status !== "NeedsAttention" &&
      item.status !== "Issued"
  );
  const pending = attention?.pendingPayments || [];
  const unpaid = [...(attention?.unpaid || [])]
    .filter((item) => Number(item.amountDue) > 0)
    .sort(
      (a, b) =>
        (debtPriority[a.displayStatus] ?? 3) -
          (debtPriority[b.displayStatus] ?? 3) ||
        String(a.dueDate).localeCompare(String(b.dueDate))
    );

  const [activePaymentTab, setActivePaymentTab] = useState("pending");
  const currentPaymentTab =
    activePaymentTab === "pending" && !pending.length && unpaid.length
      ? "unpaid"
      : activePaymentTab;

  return (
    <div className="bill-priority-board">
      {/* Tầng 1: 3 Thẻ Chỉ số Hành động (Actionable KPI Stat Cards) */}
      <div className="bill-stat-grid" aria-label="Tổng quan việc cần xử lý">
        {/* Thẻ 1: Chốt số điện nước */}
        <button
          type="button"
          className={`bill-stat-card ${
            needsInput.length > 0
              ? "bill-stat-card--attention"
              : "bill-stat-card--success"
          }`}
          onClick={() => {
            const el = document.getElementById("bill-meters-hub");
            el?.scrollIntoView({ behavior: "smooth", block: "start" });
          }}
        >
          <div className="bill-stat-card__top">
            <div className="bill-stat-card__icon bill-stat-card__icon--meters">
              <Zap size={20} strokeWidth={2.2} />
            </div>
            {needsInput.length > 0 ? (
              <span className="bill-stat-pill bill-stat-pill--warning">
                <AlertCircle size={12} strokeWidth={2.5} /> Cần chốt
              </span>
            ) : (
              <span className="bill-stat-pill bill-stat-pill--success">
                <Check size={12} strokeWidth={2.5} /> Đã hoàn tất
              </span>
            )}
          </div>
          <div className="bill-stat-card__body">
            <span className="bill-stat-card__label">Chốt số điện nước</span>
            <strong className="bill-stat-card__value">
              {worklistLoading ? "…" : needsInput.length}
            </strong>
          </div>
          <div className="bill-stat-card__footer">
            <span>
              Kỳ {month ? month.split("-").reverse().join("/") : "này"}
            </span>
            <ChevronRight size={14} className="bill-stat-card__arrow" />
          </div>
        </button>

        {/* Thẻ 2: Thanh toán chờ xác nhận */}
        <button
          type="button"
          className={`bill-stat-card ${
            pending.length > 0
              ? "bill-stat-card--attention"
              : "bill-stat-card--success"
          }`}
          onClick={() => {
            setActivePaymentTab("pending");
            const el = document.getElementById("bill-payment-hub");
            el?.scrollIntoView({ behavior: "smooth", block: "start" });
          }}
        >
          <div className="bill-stat-card__top">
            <div className="bill-stat-card__icon bill-stat-card__icon--payments">
              <Clock size={20} strokeWidth={2.2} />
            </div>
            {pending.length > 0 ? (
              <span className="bill-stat-pill bill-stat-pill--warning">
                <AlertCircle size={12} strokeWidth={2.5} /> Chờ đối chiếu
              </span>
            ) : (
              <span className="bill-stat-pill bill-stat-pill--success">
                <Check size={12} strokeWidth={2.5} /> Đã kiểm tra
              </span>
            )}
          </div>
          <div className="bill-stat-card__body">
            <span className="bill-stat-card__label">Thanh toán chờ xác nhận</span>
            <strong className="bill-stat-card__value">
              {attentionLoading ? "…" : pending.length}
            </strong>
          </div>
          <div className="bill-stat-card__footer">
            <span>
              {pending.length > 0
                ? "Người thuê đã nộp chứng từ"
                : "Không có khoản chờ"}
            </span>
            <ChevronRight size={14} className="bill-stat-card__arrow" />
          </div>
        </button>

        {/* Thẻ 3: Hóa đơn còn phải thu */}
        <button
          type="button"
          className={`bill-stat-card ${
            unpaid.some((i) => i.displayStatus === "Overdue")
              ? "bill-stat-card--danger"
              : unpaid.length > 0
              ? "bill-stat-card--attention"
              : "bill-stat-card--success"
          }`}
          onClick={() => {
            setActivePaymentTab("unpaid");
            const el = document.getElementById("bill-payment-hub");
            el?.scrollIntoView({ behavior: "smooth", block: "start" });
          }}
        >
          <div className="bill-stat-card__top">
            <div className="bill-stat-card__icon bill-stat-card__icon--debts">
              <Receipt size={20} strokeWidth={2.2} />
            </div>
            {unpaid.some((i) => i.displayStatus === "Overdue") ? (
              <span className="bill-stat-pill bill-stat-pill--danger">
                <AlertCircle size={12} strokeWidth={2.5} /> Có nợ quá hạn
              </span>
            ) : unpaid.length > 0 ? (
              <span className="bill-stat-pill bill-stat-pill--warning">
                Đang chờ thu
              </span>
            ) : (
              <span className="bill-stat-pill bill-stat-pill--success">
                <Check size={12} strokeWidth={2.5} /> Thu đủ
              </span>
            )}
          </div>
          <div className="bill-stat-card__body">
            <span className="bill-stat-card__label">Hóa đơn còn phải thu</span>
            <strong className="bill-stat-card__value">
              {attentionLoading ? "…" : unpaid.length}
            </strong>
          </div>
          <div className="bill-stat-card__footer">
            <span>
              {unpaid.length > 0
                ? "Bao gồm nợ và quá hạn"
                : "Tất cả đã thanh toán"}
            </span>
            <ChevronRight size={14} className="bill-stat-card__arrow" />
          </div>
        </button>
      </div>

      {/* Tầng 2: Trung tâm Xử lý Khẩn cấp (Action Hub - Bố cục 2 Cột Bento) */}
      <div className="bill-priority-bento">
        {/* Cột trái: Cần nhập điện nước */}
        <section
          className="bill-priority-card bill-priority-card--meters"
          id="bill-meters-hub"
          aria-labelledby="bill-worklist-title"
        >
          <div className="bill-bento-head">
            <div className="bill-bento-title">
              <h2 id="bill-worklist-title">Chốt số điện nước</h2>
              <p>
                Kỳ {month?.split("-").reverse().join("/")} để lập hóa đơn đầu tháng.
              </p>
            </div>
            <div className="bill-month-picker-compact">
              <label>
                Kỳ
                <input
                  type="month"
                  value={month}
                  min="2000-01"
                  max={lastClosedMonth()}
                  onChange={(event) => onMonthChange(event.target.value)}
                />
              </label>
            </div>
          </div>

          {worklistError && (
            <p className="bill-error" role="alert">
              {worklistError}
            </p>
          )}
          {worklistLoading && <p role="status">Đang tải việc cần chốt…</p>}
          {!worklistLoading && !worklistError && !needsInput.length && (
            <p className="bill-empty">
              Không có phòng thiếu dữ liệu điện nước trong kỳ này.
            </p>
          )}

          {!!needsInput.length && (
            <ul className="bill-action-list">
              {needsInput.map((item) => (
                <li className="bill-action-row" key={item.contractId}>
                  <div className="bill-action-row__main">
                    <strong>
                      Phòng {item.roomNumber || roomName(item.roomId)}
                    </strong>
                    <small>{item.contractCode}</small>
                  </div>
                  <div className="bill-action-row__center">
                    <span
                      className={`bill-badge ${
                        item.status === "NeedsAttention"
                          ? "bill-badge--pending"
                          : "bill-badge--urgent"
                      }`}
                    >
                      {item.status === "NeedsAttention"
                        ? "Cần kiểm tra"
                        : "Thiếu dữ liệu"}
                    </span>
                    <p className="bill-action-row__desc">
                      {item.missingInputs
                        ?.map((code) => missingLabel[code] || code)
                        .join(", ") || "Dữ liệu tính tiền cần kiểm tra"}
                    </p>
                  </div>
                  <button
                    className="bill-button bill-button--primary bill-btn-compact"
                    type="button"
                    onClick={() => onWorkItem(item)}
                  >
                    {item.status === "NeedsAttention"
                      ? "Kiểm tra dữ liệu"
                      : "Nhập chỉ số"}
                  </button>
                </li>
              ))}
            </ul>
          )}

          {!!otherWork.length && (
            <details className="bill-secondary-work">
              <summary>
                {otherWork.length} phòng đã có dữ liệu hoặc hóa đơn nháp
              </summary>
              <ul className="bill-action-list">
                {otherWork.map((item) => (
                  <li className="bill-action-row" key={item.contractId}>
                    <div className="bill-action-row__main">
                      <strong>
                        Phòng {item.roomNumber || roomName(item.roomId)}
                      </strong>
                      <small>{item.contractCode}</small>
                    </div>
                    <div className="bill-action-row__center">
                      <span className="bill-badge bill-badge--ready">
                        {item.status === "Draft"
                          ? "Nháp chờ phát hành"
                          : "Sẵn sàng xem trước"}
                      </span>
                    </div>
                    <button
                      type="button"
                      className="bill-button bill-button--ghost bill-btn-compact"
                      onClick={() => onWorkItem(item)}
                    >
                      {item.invoiceId ? "Xem hóa đơn" : "Xem trước"}
                    </button>
                  </li>
                ))}
              </ul>
            </details>
          )}
        </section>

        {/* Cột phải: Thanh toán & Thu nợ */}
        <section
          className="bill-priority-card bill-priority-card--payments"
          id="bill-payment-hub"
          aria-labelledby="bill-payment-queue-title"
        >
          <div className="bill-bento-head">
            <div className="bill-bento-title">
              <h2 id="bill-payment-queue-title">Thanh toán & Thu nợ</h2>
              <p>Chứng từ chờ đối chiếu và các hóa đơn còn tồn đọng.</p>
            </div>
            <div className="bill-subtab-pills" role="tablist">
              <button
                type="button"
                role="tab"
                aria-selected={currentPaymentTab === "pending"}
                className={`bill-subtab-pill ${
                  currentPaymentTab === "pending" ? "is-active" : ""
                }`}
                onClick={() => setActivePaymentTab("pending")}
              >
                Chờ xác nhận
                {pending.length > 0 && (
                  <span className="bill-pill-badge">{pending.length}</span>
                )}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={currentPaymentTab === "unpaid"}
                className={`bill-subtab-pill ${
                  currentPaymentTab === "unpaid" ? "is-active" : ""
                }`}
                onClick={() => setActivePaymentTab("unpaid")}
              >
                Còn phải thu
                {unpaid.length > 0 && (
                  <span
                    className={`bill-pill-badge ${
                      unpaid.some((i) => i.displayStatus === "Overdue")
                        ? "bill-pill-badge--danger"
                        : ""
                    }`}
                  >
                    {unpaid.length}
                  </span>
                )}
              </button>
            </div>
          </div>

          {attentionError && (
            <p className="bill-error" role="alert">
              {attentionError}
            </p>
          )}
          {attentionLoading && (
            <p role="status">Đang tải thanh toán cần xử lý…</p>
          )}

          {!attentionLoading && !attentionError && (
            <>
              {currentPaymentTab === "pending" && (
                <>
                  {!pending.length ? (
                    <p className="bill-empty">
                      Không có chứng từ thanh toán nào đang chờ xác nhận.
                    </p>
                  ) : (
                    <ul className="bill-action-list">
                      {pending.map((payment) => (
                        <li
                          className="bill-action-row"
                          key={payment.paymentId}
                        >
                          <div className="bill-action-row__main">
                            <strong>{payment.invoiceCode}</strong>
                            <small>Phòng {roomName(payment.roomId)}</small>
                          </div>
                          <div className="bill-action-row__center">
                            <span className="bill-badge bill-badge--pending">
                              Chờ đối chiếu
                            </span>
                            <p className="bill-action-row__desc">
                              {payment.submittedByName || "Người thuê"} khai{" "}
                              <strong className="bill-amount-tabular">
                                {money(payment.claimedAmount)}
                              </strong>
                            </p>
                          </div>
                          <button
                            className="bill-button bill-button--ghost bill-btn-compact"
                            type="button"
                            onClick={() => onOpenInvoice(payment.invoiceId)}
                          >
                            Xem hóa đơn
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              )}

              {currentPaymentTab === "unpaid" && (
                <>
                  {!unpaid.length ? (
                    <p className="bill-empty">
                      Tất cả hóa đơn đã được thanh toán đầy đủ.
                    </p>
                  ) : (
                    <ul className="bill-action-list">
                      {unpaid.map((invoice) => (
                        <li className="bill-action-row" key={invoice.id}>
                          <div className="bill-action-row__main">
                            <strong>{invoice.code}</strong>
                            <small>
                              Phòng {roomName(invoice.roomId)} · Hạn{" "}
                              {invoice.dueDate}
                            </small>
                          </div>
                          <div className="bill-action-row__center">
                            <span
                              className={`bill-badge ${
                                invoice.displayStatus === "Overdue"
                                  ? "bill-badge--urgent"
                                  : "bill-badge--pending"
                              }`}
                            >
                              {({
                                Overdue: "Quá hạn",
                                Partial: "Đã thu 1 phần",
                                Unpaid: "Chưa thanh toán",
                              })[invoice.displayStatus] || invoice.displayStatus}
                            </span>
                            <p className="bill-action-row__desc">
                              Còn nợ:{" "}
                              <strong className="bill-amount-tabular bill-amount-tabular--debt">
                                {money(invoice.amountDue)}
                              </strong>
                            </p>
                          </div>
                          <button
                            className="bill-button bill-button--ghost bill-btn-compact"
                            type="button"
                            onClick={() => onOpenInvoice(invoice.id)}
                          >
                            Xem hóa đơn
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  );
}
