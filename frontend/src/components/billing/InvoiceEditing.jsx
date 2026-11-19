import { useState } from "react";
import { billingApi } from "../../services/api/billingApi";
import MoneyInput from "./MoneyInput";

const money = (value) =>
  new Intl.NumberFormat("vi-VN").format(Number(value || 0)) + " ₫";
const editableTypes = ["Service", "Damage", "Other", "Discount"];
const typeLabel = {
  Service: "Dịch vụ",
  Damage: "Sửa chữa",
  Other: "Khoản khác",
  Discount: "Giảm trừ",
  AdditionalCharge: "Phụ thu",
  Correction: "Điều chỉnh giảm",
  WriteOff: "Xóa nợ",
};
const newRow = () => ({
  key: crypto.randomUUID(),
  type: "Service",
  description: "",
  quantity: "1",
  unitPrice: "",
});
const rowAmount = (item) =>
  ((item.type === "Discount" ? -1 : 1) *
    Math.round(
      Number(item.quantity || 0) * Number(item.unitPrice || 0) * 100,
    )) /
  100;
const isMoney = (value) =>
  value !== "" &&
  Number.isFinite(Number(value)) &&
  Number(value) >= 0 &&
  Math.round(Number(value) * 100) === Number(value) * 100;
const isQuantity = (value) => isMoney(value) && Number(value) > 0;

export function DraftItemsEditor({
  invoiceId,
  items,
  onChanged,
  initialOpen = false,
}) {
  const [open, setOpen] = useState(initialOpen);
  const [rows, setRows] = useState(() =>
    items
      .filter((item) => editableTypes.includes(item.type))
      .map((item) => ({
        ...item,
        key: item.id || crypto.randomUUID(),
        quantity: String(item.quantity),
        unitPrice: String(item.unitPrice),
      })),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const fixedTotal = items
    .filter((item) => !editableTypes.includes(item.type))
    .reduce((sum, item) => sum + Number(item.amount || 0), 0);
  const proposedTotal =
    fixedTotal + rows.reduce((sum, item) => sum + rowAmount(item), 0);

  function update(key, field, value) {
    setRows((current) =>
      current.map((row) =>
        row.key === key ? { ...row, [field]: value } : row,
      ),
    );
  }
  async function save(event) {
    event.preventDefault();
    if (
      rows.some(
        (row) =>
          !row.description.trim() ||
          !isQuantity(row.quantity) ||
          !isMoney(row.unitPrice),
      ) ||
      !Number.isFinite(proposedTotal) ||
      proposedTotal <= 0
    ) {
      setError("Kiểm tra mô tả, số lượng, đơn giá và tổng hóa đơn.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await billingApi.saveDraftItems(invoiceId, {
        items: rows.map((row) => ({
          ...(row.id ? { id: row.id } : {}),
          type: row.type,
          description: row.description.trim(),
          quantity: Number(row.quantity),
          unitPrice: Number(row.unitPrice),
        })),
      });
      setOpen(false);
      onChanged("Đã cập nhật các khoản của hóa đơn nháp.");
    } catch (cause) {
      setError(cause.message || "Không thể lưu thay đổi.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="bill-editor">
      <div className="bill-section-actions">
        <div>
          <h3>Chỉnh dịch vụ và các khoản kỳ này</h3>
          <p>Thay đổi chỉ áp dụng cho hóa đơn nháp này.</p>
        </div>
        <button
          type="button"
          className="bill-button bill-button--secondary"
          aria-expanded={open}
          onClick={() => setOpen((value) => !value)}
        >
          {open ? "Đóng chỉnh sửa" : "Chỉnh các khoản"}
        </button>
      </div>
      {open && (
        <form onSubmit={save}>
          {rows.map((row, index) => (
            <div
              className="bill-editor-row bill-editor-row--draft"
              key={row.key}
            >
              <label>
                Loại khoản {index + 1}
                <select
                  value={row.type}
                  disabled={Boolean(row.id)}
                  onChange={(event) =>
                    update(row.key, "type", event.target.value)
                  }
                >
                  {editableTypes.map((type) => (
                    <option key={type} value={type}>
                      {typeLabel[type]}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Mô tả *
                <input
                  required
                  maxLength={255}
                  value={row.description}
                  onChange={(event) =>
                    update(row.key, "description", event.target.value)
                  }
                />
              </label>
              <label>
                Số lượng *
                <input
                  required
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={row.quantity}
                  onChange={(event) =>
                    update(row.key, "quantity", event.target.value)
                  }
                />
              </label>
              <label>
                Đơn giá (₫) *
                <MoneyInput
                  required
                  allowDecimal
                  value={row.unitPrice}
                  onValueChange={(val) => update(row.key, "unitPrice", val)}
                  placeholder="0"
                />
              </label>
              <button
                className="bill-link"
                type="button"
                onClick={() =>
                  setRows((current) =>
                    current.filter((item) => item.key !== row.key),
                  )
                }
              >
                Bỏ khoản
              </button>
            </div>
          ))}
          <div className="bill-section-actions">
            <button
              className="bill-button bill-button--secondary"
              type="button"
              onClick={() => setRows((current) => [...current, newRow()])}
            >
              Thêm dịch vụ hoặc khoản thu/trừ
            </button>
            <strong>Tổng mới: {money(proposedTotal)}</strong>
          </div>
          {error && (
            <p className="bill-error" role="alert">
              {error}
            </p>
          )}
          <button
            className="bill-button"
            type="submit"
            disabled={busy || proposedTotal <= 0}
          >
            {busy ? "Đang lưu…" : "Lưu thay đổi"}
          </button>
        </form>
      )}
    </div>
  );
}

const adjustmentOptions = [
  {
    value: "AdditionalCharge",
    label: "Thu thêm tiền",
    badge: "Phụ thu",
    sign: "+",
    tone: "increase",
  },
  {
    value: "Discount",
    label: "Giảm trừ",
    badge: "Giảm trừ",
    sign: "−",
    tone: "decrease",
  },
  {
    value: "Correction",
    label: "Điều chỉnh giảm",
    badge: "Điều chỉnh giảm",
    sign: "−",
    tone: "decrease",
  },
  {
    value: "WriteOff",
    label: "Xóa nợ",
    badge: "Xóa nợ",
    sign: "−",
    tone: "decrease",
  },
];

export function InvoiceAdjustmentHistory({ adjustments = [] }) {
  if (!adjustments.length)
    return (
      <p className="bill-empty">Chưa có khoản cộng hoặc trừ sau phát hành.</p>
    );
  return (
    <ul className="bill-adjustment-list">
      {adjustments.map((item) => {
        const isIncrease = item.type === "AdditionalCharge";
        return (
          <li
            className={`bill-adjustment-row${item.voidedAt ? " bill-adjustment-row--void" : ""}`}
            key={item.id}
          >
            <div className="bill-adjustment-row__main">
              <div className="bill-adj-row-meta">
                <span
                  className={`bill-adj-badge bill-adj-badge--${isIncrease ? "increase" : "decrease"}`}
                >
                  {typeLabel[item.type] || item.type}
                </span>
                <small>
                  {item.createdAt
                    ? new Date(item.createdAt).toLocaleString("vi-VN", {
                        timeZone: "Asia/Ho_Chi_Minh",
                      })
                    : ""}
                  {item.voidedAt ? " · Đã hủy điều chỉnh" : ""}
                </small>
              </div>
              <p className="bill-adjustment-row__reason">{item.reason}</p>
            </div>
            <strong
              className={`bill-adjustment-row__amount${isIncrease ? "" : " bill-adjustment-row__amount--decrease"}`}
            >
              {isIncrease ? "+" : "−"}
              {money(item.amount)}
            </strong>
          </li>
        );
      })}
    </ul>
  );
}

export function InvoiceAdjustmentEditor({
  invoiceId,
  adjustments,
  summary,
  onChanged,
}) {
  const [type, setType] = useState("AdditionalCharge");
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState(() =>
    crypto.randomUUID(),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const isIncrease = type === "AdditionalCharge";
  const signed = isIncrease ? 1 : -1;
  const numericAmount = Number(amount || 0);
  const currentEffective = Number(summary.effectiveTotal || 0);
  const newEffective = currentEffective + signed * numericAmount;
  const isInvalidTotal = !Number.isFinite(newEffective) || newEffective < 0;

  async function submit(event) {
    event.preventDefault();
    if (
      !reason.trim() ||
      !isMoney(amount) ||
      numericAmount <= 0 ||
      isInvalidTotal
    ) {
      setError("Nhập số tiền hợp lệ và lý do; tổng phải thu không được âm.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await billingApi.addAdjustment(invoiceId, {
        type,
        amount: Number(amount),
        reason: reason.trim(),
        idempotencyKey,
      });
      setAmount("");
      setReason("");
      setIdempotencyKey(crypto.randomUUID());
      onChanged("Đã ghi khoản cộng/trừ và cập nhật số dư.");
    } catch (cause) {
      setError(cause.message || "Không thể ghi điều chỉnh.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="bill-card" aria-labelledby="bill-adjustment-title">
      <div className="bill-adj-head">
        <div>
          <h2 id="bill-adjustment-title">Phụ thu và giảm trừ</h2>
          <p>Mỗi khoản phát sinh sau phát hành được ghi riêng cùng lý do.</p>
        </div>
        <span className="bill-adj-current-pill">
          Tổng hiện tại: <strong>{money(currentEffective)}</strong>
        </span>
      </div>

      <form className="bill-editor bill-editor--adjustment" onSubmit={submit}>
        <div className="bill-adj-type-wrap">
          <span className="bill-adj-label">Loại điều chỉnh *</span>
          <div className="bill-adj-type-group" role="radiogroup" aria-label="Loại điều chỉnh">
            {adjustmentOptions.map((option) => {
              const active = type === option.value;
              return (
                <button
                  key={option.value}
                  type="button"
                  role="radio"
                  aria-checked={active}
                  className={`bill-adj-type-btn bill-adj-type-btn--${option.tone}${active ? " is-active" : ""}`}
                  onClick={() => setType(option.value)}
                >
                  <span className="bill-adj-type-sign">{option.sign}</span>
                  <span>{option.label}</span>
                </button>
              );
            })}
          </div>
        </div>

        <div className="bill-adj-fields">
          <label className="bill-adj-field">
            <span>Số tiền (₫) *</span>
            <MoneyInput
              required
              allowDecimal
              value={amount}
              onValueChange={setAmount}
              placeholder="Ví dụ: 50.000"
            />
          </label>
          <label className="bill-adj-field">
            <span>Lý do *</span>
            <input
              required
              maxLength={500}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Ví dụ: phí sửa khóa cửa hoặc giảm trừ kỳ này"
            />
          </label>
        </div>

        <div className="bill-adj-preview" aria-live="polite">
          <div className="bill-adj-preview-item">
            <span>Tổng hiện tại</span>
            <strong>{money(currentEffective)}</strong>
          </div>
          <div className="bill-adj-preview-item">
            <span>Điều chỉnh</span>
            <strong
              className={
                isIncrease
                  ? "bill-adj-delta bill-adj-delta--increase"
                  : "bill-adj-delta bill-adj-delta--decrease"
              }
            >
              {numericAmount > 0
                ? `${isIncrease ? "+" : "−"}${money(numericAmount)}`
                : "0 ₫"}
            </strong>
          </div>
          <div className="bill-adj-preview-item bill-adj-preview-item--result">
            <span>Tổng sau điều chỉnh</span>
            <strong className={isInvalidTotal ? "bill-adj-invalid" : ""}>
              {money(newEffective)}
            </strong>
          </div>
        </div>

        {error && (
          <p className="bill-error" role="alert">
            {error}
          </p>
        )}

        <div className="bill-adj-actions">
          {isInvalidTotal && (
            <span className="bill-adj-hint">
              Số tiền giảm không được vượt quá tổng hiện tại.
            </span>
          )}
          <button
            className="bill-button"
            type="submit"
            disabled={
              busy ||
              !isMoney(amount) ||
              numericAmount <= 0 ||
              !reason.trim() ||
              isInvalidTotal
            }
          >
            {busy ? "Đang lưu…" : "Ghi khoản điều chỉnh"}
          </button>
        </div>
      </form>

      <div className="bill-adj-history-head">
        <h3>Lịch sử điều chỉnh</h3>
        {adjustments?.length > 0 && (
          <span className="bill-adj-count">{adjustments.length} khoản</span>
        )}
      </div>
      <InvoiceAdjustmentHistory adjustments={adjustments} />
    </section>
  );
}
