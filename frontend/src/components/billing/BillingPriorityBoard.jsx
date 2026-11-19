import { useState } from "react";
import MonthlyBillingActions from "./MonthlyBillingActions";
import {
  AlertCircle,
  ArrowRight,
  Check,
  ChevronRight,
  Clock,
  Receipt,
  Search,
  X,
  Zap,
} from "lucide-react";

const money = (value) =>
  new Intl.NumberFormat("vi-VN").format(Number(value || 0)) + " ₫";

const workPriority = {
  MissingInputs: 0,
  NeedsAttention: 1,
  ReadyToPreview: 2,
  Draft: 3,
  Issued: 4,
};

const lastClosedMonth = () => {
  const current = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 7);
  const [year, month] = current.split("-").map(Number);
  return new Date(Date.UTC(year, month - 2, 1)).toISOString().slice(0, 7);
};

export default function BillingPriorityBoard({
  propertyId,
  onBillingChanged,
  month,
  onMonthChange,
  worklist,
  worklistError,
  worklistLoading,
  attention,
  attentionError,
  attentionLoading,
  roomName,
  onOpenMeters,
  onOpenUtilityInputs,
  onFocusUnpaid,
  onOpenInvoice,
}) {
  const [paymentQuery, setPaymentQuery] = useState("");

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

  const missingReadingCount = needsInput.filter((item) =>
    item.missingInputs?.some(
      (code) => code === "ElectricityReading" || code === "WaterReading"
    )
  ).length;
  const missingRateOrOtherCount = needsInput.length - missingReadingCount;

  const pending = attention?.pendingPayments || [];
  const totalPendingAmount = pending.reduce(
    (sum, item) => sum + Number(item.claimedAmount || 0),
    0
  );

  const normalizedQuery = paymentQuery.trim().toLowerCase();
  const filteredPending = normalizedQuery
    ? pending.filter((payment) => {
        const roomText = String(roomName(payment.roomId) || "").toLowerCase();
        const codeText = String(payment.invoiceCode || "").toLowerCase();
        const submitterText = String(payment.submittedByName || "").toLowerCase();
        return (
          roomText.includes(normalizedQuery) ||
          codeText.includes(normalizedQuery) ||
          submitterText.includes(normalizedQuery)
        );
      })
    : pending;

  const unpaid = (attention?.unpaid || []).filter(
    (item) => Number(item.amountDue) > 0
  );

  const formattedMonth = month ? month.split("-").reverse().join("/") : "này";

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
            <span>Kỳ {formattedMonth}</span>
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

        {/* Thẻ 3: Hóa đơn còn phải thu -> cuộn xuống Danh sách hóa đơn */}
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
            if (onFocusUnpaid) {
              onFocusUnpaid();
            } else {
              document
                .getElementById("bill-invoices-list")
                ?.scrollIntoView({ behavior: "smooth", block: "start" });
            }
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
                ? "Xem tại danh sách bên dưới"
                : "Tất cả đã thanh toán"}
            </span>
            <ChevronRight size={14} className="bill-stat-card__arrow" />
          </div>
        </button>
      </div>

      {/* Tầng 2: Trung tâm Xử lý (Bố cục 2 Cột Bento gọn) */}
      <div className="bill-priority-bento">
        {/* Cột trái: Chốt số điện nước (Tóm tắt + Link sang trang nhập chỉ số) */}
        <section
          className="bill-priority-card bill-priority-card--meters"
          id="bill-meters-hub"
          aria-labelledby="bill-worklist-title"
        >
          <div className="bill-bento-head">
            <div className="bill-bento-title">
              <h2 id="bill-worklist-title">Chốt số điện nước</h2>
              <p>
                Kỳ {formattedMonth} để lập hóa đơn đầu tháng.
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

          {!worklistLoading && (
            <div
              className={`bill-meters-summary ${
                needsInput.length > 0
                  ? "bill-meters-summary--attention"
                  : "bill-meters-summary--success"
              }`}
            >
              <div className="bill-meters-summary__top">
                <strong className="bill-meters-summary__count">
                  {needsInput.length > 0
                    ? `${needsInput.length} phòng cần xử lý`
                    : `Đã đủ dữ liệu kỳ ${formattedMonth}`}
                </strong>
                {needsInput.length > 0 ? (
                  <span className="bill-badge bill-badge--urgent">
                    Chưa chốt đủ
                  </span>
                ) : (
                  <span className="bill-badge bill-badge--ready">
                    Sẵn sàng
                  </span>
                )}
              </div>

              <p className="bill-meters-summary__breakdown">
                {needsInput.length > 0 ? (
                  <>
                    {missingReadingCount > 0 && (
                      <span>{missingReadingCount} phòng thiếu chỉ số</span>
                    )}
                    {missingReadingCount > 0 && missingRateOrOtherCount > 0 && " · "}
                    {missingRateOrOtherCount > 0 && (
                      <span>{missingRateOrOtherCount} phòng thiếu đơn giá/dữ liệu khác</span>
                    )}
                    {otherWork.length > 0 && (
                      <span> · {otherWork.length} phòng sẵn sàng/nháp</span>
                    )}
                  </>
                ) : (
                  <>
                    Không có phòng thiếu chỉ số điện nước.
                    {otherWork.length > 0 && ` (${otherWork.length} phòng sẵn sàng/nháp)`}
                  </>
                )}
              </p>

              <div className="bill-meters-cta">
                <button
                  type="button"
                  className={`bill-button bill-btn-compact ${
                    needsInput.length > 0
                      ? "bill-button--primary"
                      : "bill-button--ghost"
                  }`}
                  disabled={!propertyId || !month}
                  onClick={() => onOpenMeters?.()}
                >
                  <span>
                    {needsInput.length > 0
                      ? "Nhập chỉ số điện nước"
                      : "Xem / sửa chỉ số"}
                  </span>
                  <ArrowRight size={14} />
                </button>
                {missingRateOrOtherCount > 0 && onOpenUtilityInputs && (
                  <button
                    type="button"
                    className="bill-button bill-button--ghost bill-btn-compact"
                    onClick={() => onOpenUtilityInputs()}
                  >
                    Kiểm tra đơn giá
                  </button>
                )}
              </div>
            </div>
          )}

          <div className="bill-meters-actions">
            <MonthlyBillingActions
              propertyId={propertyId}
              month={month}
              work={work}
              loading={worklistLoading}
              onChanged={onBillingChanged}
            />
          </div>
        </section>

        {/* Cột phải: Thanh toán chờ xác nhận (Compact & có scroll khi nhiều) */}
        <section
          className="bill-priority-card bill-priority-card--payments"
          id="bill-payment-hub"
          aria-labelledby="bill-payment-queue-title"
        >
          <div className="bill-bento-head">
            <div className="bill-bento-title">
              <h2 id="bill-payment-queue-title">Thanh toán chờ xác nhận</h2>
              <p>Chứng từ người thuê đã gửi, đang chờ đối chiếu.</p>
            </div>
            {!attentionLoading && !attentionError && (
              <div className="bill-queue-summary" aria-label="Tổng hợp chờ duyệt">
                <span className="bill-pill-badge">{pending.length} khoản</span>
                {pending.length > 0 && (
                  <strong className="bill-amount-tabular">
                    {money(totalPendingAmount)}
                  </strong>
                )}
              </div>
            )}
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
              {!pending.length ? (
                <p className="bill-empty">
                  Không có chứng từ thanh toán nào đang chờ xác nhận.
                </p>
              ) : (
                <>
                  {pending.length > 5 && (
                    <div className="bill-queue-search">
                      <Search size={14} className="bill-queue-search__icon" />
                      <input
                        type="text"
                        value={paymentQuery}
                        onChange={(event) => setPaymentQuery(event.target.value)}
                        placeholder="Tìm phòng, mã hóa đơn hoặc người nộp…"
                        aria-label="Tìm kiếm yêu cầu thanh toán"
                      />
                      {paymentQuery && (
                        <button
                          type="button"
                          className="bill-queue-search__clear"
                          aria-label="Xóa từ khóa tìm kiếm"
                          onClick={() => setPaymentQuery("")}
                        >
                          <X size={13} />
                        </button>
                      )}
                    </div>
                  )}

                  {!filteredPending.length ? (
                    <div className="bill-queue-empty-filter">
                      <p>Không tìm thấy thanh toán khớp với "{paymentQuery}".</p>
                      <button
                        type="button"
                        className="bill-button bill-button--ghost bill-btn-compact"
                        onClick={() => setPaymentQuery("")}
                      >
                        Xóa bộ lọc
                      </button>
                    </div>
                  ) : (
                    <ul className="bill-action-list bill-action-list--compact">
                      {filteredPending.map((payment) => (
                        <li
                          className="bill-action-row bill-action-row--compact"
                          key={payment.paymentId}
                        >
                          <div className="bill-action-row__main">
                            <strong>
                              Phòng {roomName(payment.roomId)} · {payment.invoiceCode}
                            </strong>
                            <small>
                              {payment.submittedByName || "Người thuê"} nộp chứng từ
                            </small>
                          </div>
                          <div className="bill-action-row__right">
                            <strong className="bill-amount-tabular">
                              {money(payment.claimedAmount)}
                            </strong>
                            <button
                              className="bill-button bill-button--ghost bill-btn-compact"
                              type="button"
                              onClick={() => onOpenInvoice(payment.invoiceId)}
                            >
                              Đối chiếu
                            </button>
                          </div>
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
