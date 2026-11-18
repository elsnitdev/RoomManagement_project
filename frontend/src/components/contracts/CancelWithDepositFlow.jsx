import { useEffect, useState } from "react";
import { contractApi } from "../../services/api/contractApi";

const money = (value) =>
  `${Number(value || 0).toLocaleString("vi-VN")} đ`;

const DISPOSITIONS = [
  {
    value: "CancelRefunded",
    label: "Hoàn trả toàn bộ tiền cọc",
    hint: "Hoàn lại 100% số tiền cọc đã thu cho người thuê.",
  },
  {
    value: "CancelPartialRefund",
    label: "Hoàn trả một phần & giữ lại một phần",
    hint: "Hoàn lại một phần cho người thuê, phần còn lại chủ trọ giữ lại theo thỏa thuận.",
  },
  {
    value: "CancelForfeited",
    label: "Không hoàn (giữ lại toàn bộ cọc)",
    hint: "Chủ trọ giữ lại 100% số tiền cọc đã thu.",
  },
];

export default function CancelWithDepositFlow({
  propertyId,
  contract,
  depositInvoice,
  open,
  onClose,
  onChanged,
}) {
  const isPendingCancel = contract?.status === "PendingCancelSettlement";
  const held = Number(contract?.depositHeld || 0);
  const totalContractDeposit = Number(contract?.depositAmount || 0);
  const unpaidDeposit = Math.max(
    0,
    Number(depositInvoice?.amountDue ?? totalContractDeposit - held),
  );
  const isPartialPaid =
    held > 0 && (unpaidDeposit > 0 || held < totalContractDeposit);

  const [step, setStep] = useState(isPendingCancel ? 2 : 1);
  const [reason, setReason] = useState(contract?.closureReason || "");
  const [disposition, setDisposition] = useState("CancelRefunded");
  const [partialRefund, setPartialRefund] = useState("");
  const [note, setNote] = useState("");
  const [evidenceFile, setEvidenceFile] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [completed, setCompleted] = useState(false);
  const [pendingEvidenceRetry, setPendingEvidenceRetry] = useState(null);

  useEffect(() => {
    if (!open) return;
    setStep(contract?.status === "PendingCancelSettlement" ? 2 : 1);
    setReason(contract?.closureReason || "");
    setDisposition("CancelRefunded");
    setPartialRefund(held > 0 ? String(Math.round(held / 2)) : "");
    setNote("");
    setEvidenceFile(null);
    setError("");
    setSuccess("");
    setCompleted(false);
    setPendingEvidenceRetry(null);
  }, [open, contract?.id, contract?.status, contract?.closureReason, held]);

  if (!open || !contract) return null;

  const refundAmount =
    disposition === "CancelRefunded"
      ? held
      : disposition === "CancelForfeited"
        ? 0
        : Number(partialRefund || 0);
  const forfeitedAmount = Math.max(
    0,
    Math.round((held - refundAmount) * 100) / 100,
  );

  const validStep2 =
    held > 0 &&
    (disposition === "CancelRefunded" ||
      disposition === "CancelForfeited" ||
      (disposition === "CancelPartialRefund" &&
        refundAmount > 0 &&
        refundAmount < held));

  async function handleRequestCancel(event) {
    event.preventDefault();
    if (!reason.trim()) {
      setError("Vui lòng nhập lý do hủy hợp đồng.");
      return;
    }
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await contractApi.requestCancel(propertyId, contract.id, reason.trim());
      await onChanged?.();
      setSuccess("Đã xác nhận yêu cầu hủy. Tiếp tục xử lý khoản cọc.");
      setStep(2);
    } catch (cause) {
      setError(cause.message || "Không thể ghi nhận yêu cầu hủy hợp đồng.");
    } finally {
      setBusy(false);
    }
  }

  async function handleCompleteCancel() {
    if (completed) return;
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await contractApi.completeCancel(propertyId, contract.id, {
        depositDisposition: disposition,
        refundAmount,
        forfeitedAmount,
        note: note.trim() || null,
      });
      setCompleted(true);
      if (refundAmount > 0 && evidenceFile) {
        const selection = { id: crypto.randomUUID(), file: evidenceFile };
        try {
          await contractApi.uploadSettlementRefundEvidence(
            propertyId,
            contract.id,
            selection,
          );
          setSuccess("Đã hoàn tất hủy hợp đồng và lưu ảnh bằng chứng hoàn cọc.");
        } catch (cause) {
          setPendingEvidenceRetry(selection);
          setError(
            `Đã hoàn tất hủy hợp đồng nhưng chưa tải được ảnh bằng chứng: ${cause.message}. Vui lòng thử tải lại ảnh.`,
          );
          await onChanged?.();
          return;
        }
      } else {
        setSuccess("Đã hoàn tất hủy hợp đồng và xử lý khoản cọc.");
      }
      await onChanged?.();
    } catch (cause) {
      setError(cause.message || "Không thể hoàn tất hủy hợp đồng.");
    } finally {
      setBusy(false);
    }
  }

  async function retryUploadEvidence() {
    if (!pendingEvidenceRetry) return;
    setBusy(true);
    setError("");
    try {
      await contractApi.uploadSettlementRefundEvidence(
        propertyId,
        contract.id,
        pendingEvidenceRetry,
      );
      setPendingEvidenceRetry(null);
      setSuccess("Đã lưu ảnh bằng chứng hoàn cọc.");
      await onChanged?.();
    } catch (cause) {
      setError(`Chưa tải được ảnh bằng chứng: ${cause.message}. Hãy thử lại.`);
    } finally {
      setBusy(false);
    }
  }

  const selectedLabel =
    DISPOSITIONS.find((item) => item.value === disposition)?.label || "";

  return (
    <div
      className="ct-exit-overlay"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !busy) onClose?.();
      }}
    >
      <div
        className="ct-exit-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="ct-cancel-deposit-title"
      >
        <header>
          <div>
            <span>HỢP ĐỒNG {contract.contractCode}</span>
            <h2 id="ct-cancel-deposit-title">Hủy hợp đồng có cọc</h2>
          </div>
          <button
            type="button"
            aria-label="Đóng quy trình"
            disabled={busy}
            onClick={onClose}
          >
            ×
          </button>
        </header>

        <ol className="ct-exit-steps">
          <li className={step === 1 ? "is-current" : step > 1 ? "is-done" : ""}>
            1. Xác nhận yêu cầu hủy
          </li>
          <li className={step === 2 ? "is-current" : step > 2 ? "is-done" : ""}>
            2. Xử lý khoản cọc
          </li>
          <li className={step === 3 ? "is-current" : completed ? "is-done" : ""}>
            3. Hoàn tất hủy
          </li>
        </ol>

        <div className="ct-exit-dialog__body">
          {error && (
            <p className="contract-message contract-message--error" role="alert">
              {error}
            </p>
          )}
          {success && (
            <p className="contract-message" role="status">
              {success}
            </p>
          )}

          {step === 1 && (
            <form className="ct-exit-stage ct-exit-form" onSubmit={handleRequestCancel}>
              <h3>Xác nhận yêu cầu hủy hợp đồng</h3>
              <div className="ct-exit-invoice-card">
                <p>
                  Tiền cọc theo hợp đồng: <strong>{money(totalContractDeposit)}</strong>
                </p>
                <p>
                  Tiền cọc đã thu:{" "}
                  <strong>{money(held)}</strong>{" "}
                  ({isPartialPaid ? "Thanh toán một phần" : "Thanh toán đủ"})
                </p>
                {unpaidDeposit > 0 && (
                  <p>
                    Phần cọc chưa thu: <strong>{money(unpaidDeposit)}</strong> (sẽ tự động đóng khi hoàn tất hủy)
                  </p>
                )}
              </div>

              {!isPendingCancel ? (
                <>
                  <label>
                    Lý do hủy hợp đồng
                    <textarea
                      rows={3}
                      maxLength={1000}
                      required
                      placeholder="Ví dụ: Hai bên thỏa thuận hủy trước ngày vào ở..."
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                    />
                  </label>
                  <div className="ct-exit-actions">
                    <button
                      type="button"
                      className="contract-button contract-button--ghost"
                      disabled={busy}
                      onClick={onClose}
                    >
                      Đóng
                    </button>
                    <button
                      type="submit"
                      className="contract-button contract-button--primary"
                      disabled={busy || !reason.trim()}
                    >
                      {busy ? "Đang xác nhận..." : "Xác nhận yêu cầu hủy"}
                    </button>
                  </div>
                </>
              ) : (
                <>
                  <p>
                    Lý do hủy đã ghi nhận: <strong>{contract.closureReason || reason}</strong>
                  </p>
                  <div className="ct-exit-actions">
                    <button
                      type="button"
                      className="contract-button contract-button--primary"
                      onClick={() => setStep(2)}
                    >
                      Tiếp tục xử lý khoản cọc
                    </button>
                  </div>
                </>
              )}
            </form>
          )}

          {step === 2 && (
            <div className="ct-exit-stage ct-exit-form">
              <h3>Xử lý khoản cọc đang giữ ({money(held)})</h3>
              <div className="ct-exit-invoice-card">
                {DISPOSITIONS.map((item) => (
                  <label key={item.value} style={{ display: "block", marginBottom: 10 }}>
                    <input
                      type="radio"
                      name="cancelDepositDisposition"
                      value={item.value}
                      checked={disposition === item.value}
                      onChange={(event) => setDisposition(event.target.value)}
                      style={{ width: "auto", marginRight: 8 }}
                    />
                    <strong>{item.label}</strong>
                    <div style={{ fontSize: "0.85em", opacity: 0.8, marginLeft: 22 }}>
                      {item.hint}
                    </div>
                  </label>
                ))}
              </div>

              {disposition === "CancelPartialRefund" && (
                <label>
                  Số tiền hoàn trả người thuê (VNĐ)
                  <input
                    type="number"
                    min="1"
                    max={held - 1}
                    step="1000"
                    required
                    value={partialRefund}
                    onChange={(event) => setPartialRefund(event.target.value)}
                  />
                  <small>
                    Số tiền chủ trọ giữ lại: <strong>{money(forfeitedAmount)}</strong>
                  </small>
                </label>
              )}

              {refundAmount > 0 && (
                <label>
                  Ảnh bằng chứng chuyển khoản hoàn cọc (tùy chọn)
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    onChange={(event) => setEvidenceFile(event.target.files?.[0] || null)}
                  />
                </label>
              )}

              <label>
                Ghi chú xử lý cọc
                <input
                  type="text"
                  maxLength={1000}
                  placeholder="Ghi chú thêm về thỏa thuận hoàn/giữ cọc..."
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                />
              </label>

              <div className="ct-exit-actions">
                <button
                  type="button"
                  className="contract-button contract-button--ghost"
                  onClick={() => setStep(1)}
                >
                  Xem lại bước 1
                </button>
                <button
                  type="button"
                  className="contract-button contract-button--primary"
                  disabled={!validStep2}
                  onClick={() => {
                    setError("");
                    setStep(3);
                  }}
                >
                  Tiếp tục đến hoàn tất hủy
                </button>
              </div>
            </div>
          )}

          {step === 3 && (
            <div className="ct-exit-stage">
              <h3>Hoàn tất hủy hợp đồng</h3>
              <div className="ct-exit-invoice-card">
                <p>
                  Mã hợp đồng: <strong>{contract.contractCode}</strong>
                </p>
                <p>
                  Lý do hủy: <strong>{contract.closureReason || reason}</strong>
                </p>
                <p>
                  Phương án xử lý cọc: <strong>{selectedLabel}</strong>
                </p>
                <p>
                  Hoàn trả người thuê: <strong>{money(refundAmount)}</strong>
                </p>
                <p>
                  Giữ lại: <strong>{money(forfeitedAmount)}</strong>
                </p>
                {unpaidDeposit > 0 && (
                  <p>
                    Phần cọc chưa thu ({money(unpaidDeposit)}):{" "}
                    <strong>Tự động đóng công nợ</strong>
                  </p>
                )}
                {note.trim() && (
                  <p>
                    Ghi chú: <strong>{note.trim()}</strong>
                  </p>
                )}
                {refundAmount > 0 && evidenceFile && (
                  <p>
                    Ảnh bằng chứng: <strong>{evidenceFile.name}</strong>
                  </p>
                )}
              </div>

              {pendingEvidenceRetry && (
                <div className="ct-exit-upload-retry">
                  <p>Hợp đồng đã hủy xong. Hãy thử tải lại ảnh bằng chứng hoàn cọc.</p>
                  <button
                    type="button"
                    className="contract-button contract-button--secondary"
                    disabled={busy}
                    onClick={retryUploadEvidence}
                  >
                    Thử tải lại ảnh hoàn cọc
                  </button>
                </div>
              )}

              <div className="ct-exit-actions">
                {!completed && (
                  <button
                    type="button"
                    className="contract-button contract-button--ghost"
                    disabled={busy}
                    onClick={() => setStep(2)}
                  >
                    Quay lại sửa
                  </button>
                )}
                {!completed ? (
                  <button
                    type="button"
                    className="contract-button contract-button--danger"
                    disabled={busy}
                    onClick={handleCompleteCancel}
                  >
                    {busy ? "Đang hoàn tất..." : "Hoàn tất hủy hợp đồng"}
                  </button>
                ) : (
                  <button
                    type="button"
                    className="contract-button contract-button--primary"
                    disabled={busy}
                    onClick={onClose}
                  >
                    Đóng
                  </button>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
