import { useState } from "react";
import { billingApi } from "../../services/api/billingApi";

const money = (value) => new Intl.NumberFormat("vi-VN", {
  style: "currency", currency: "VND", maximumFractionDigits: 2,
}).format(value);

export default function MonthlyBillingActions({ propertyId, month, work, loading, onChanged }) {
  const [busy, setBusy] = useState(false);
  const [reviews, setReviews] = useState({});
  const [selected, setSelected] = useState([]);
  const [report, setReport] = useState(null);
  const [error, setError] = useState("");
  const drafts = work.filter((item) => item.status === "Draft" && item.invoiceId);
  const reviewed = drafts.filter((item) => reviews[item.invoiceId]?.reviewVersion);
  const approved = reviewed.filter((item) => selected.includes(item.invoiceId));
  const [year, billingMonth] = month.split("-").map(Number);
  const period = { year, month: billingMonth };

  async function run(request, onSuccess) {
    setBusy(true);
    setError("");
    try {
      onSuccess(await request());
    } catch (cause) {
      setError(cause.message || "Không thể xử lý hóa đơn. Vui lòng thử lại.");
    } finally {
      setBusy(false);
    }
  }

  function showResults(results, successStatus, successLabel) {
    const byContract = new Map(work.map((item) => [item.contractId, item]));
    const byInvoice = new Map(work.map((item) => [item.invoiceId, item]));
    const created = results.filter((item) => item.status === successStatus).length;
    const existing = results.filter((item) => item.status === "AlreadyExists").length;
    const issues = results.filter((item) => item.status === "NeedsReview" || item.status === "Retry");
    setReport({
      summary: [`${successLabel} ${created} hóa đơn`, existing ? `${existing} đã có hóa đơn` : "",
        issues.length ? `${issues.length} cần xử lý` : ""].filter(Boolean).join(" · "),
      issues: issues.map((item) => ({
        id: item.invoiceId || item.contractId,
        room: (byInvoice.get(item.invoiceId) || byContract.get(item.contractId))?.roomNumber,
        message: item.message,
      })),
    });
    onChanged();
  }

  function issue() {
    if (!window.confirm(`Phát hành ${approved.length} hóa đơn đã chọn? Người thuê sẽ nhận được thông báo.`)) return;
    const invoices = approved.map(({ invoiceId }) => ({ invoiceId, version: reviews[invoiceId].reviewVersion }));
    run(() => billingApi.issueMonth(propertyId, { ...period, invoices }), (results) => {
      setSelected([]);
      setReviews({});
      showResults(results, "Issued", "Đã phát hành");
    });
  }

  async function loadReviews() {
    await run(async () => {
      const pending = drafts.filter((item) => !reviews[item.invoiceId]);
      const loaded = {};
      let failed = 0;
      for (let index = 0; index < pending.length; index += 8) {
        const batch = pending.slice(index, index + 8);
        const results = await Promise.allSettled(batch.map(({ invoiceId }) => billingApi.invoice(invoiceId)));
        results.forEach((result, offset) => {
          if (result.status === "fulfilled" && result.value.reviewVersion) {
            loaded[batch[offset].invoiceId] = result.value;
          } else {
            failed += 1;
          }
        });
      }
      return { loaded, failed };
    }, ({ loaded, failed }) => {
      setReviews((current) => ({ ...current, ...loaded }));
      if (failed) {
        setError(`Không tải được ${failed} hóa đơn nháp. Hãy thử lại.`);
        onChanged();
      }
    });
  }

  return (
    <div>
      <div className="bill-actions">
        <button type="button" className="bill-button bill-button--primary"
          disabled={busy || loading || !month || !work.some((item) => !item.invoiceId)}
          onClick={() => run(() => billingApi.closeMonth(propertyId, period),
            (results) => showResults(results, "DraftCreated", "Đã tạo nháp"))}>
          Chốt kỳ và tạo nháp
        </button>
      </div>
      {error && <p className="bill-error" role="alert">{error}</p>}
      {report && (
        <div role="status">
          <p>{report.summary}</p>
          {!!report.issues.length && <ul>{report.issues.map((item) => (
            <li key={item.id}>{item.room ? `Phòng ${item.room} · ` : ""}{item.message}</li>
          ))}</ul>}
        </div>
      )}
      {!!drafts.length && (
        <details className="bill-secondary-work">
          <summary>{drafts.length} hóa đơn nháp chờ duyệt</summary>
          <div className="bill-actions">
            {reviewed.length < drafts.length && (
              <button type="button" className="bill-button bill-button--secondary" disabled={busy}
                onClick={loadReviews}>Tải các khoản tiền để duyệt</button>
            )}
            {!!reviewed.length && (
              <button type="button" className="bill-button bill-button--secondary" disabled={busy}
                onClick={() => setSelected(reviewed.slice(0, 100).map(({ invoiceId }) => invoiceId))}>
                {reviewed.length > 100 ? "Chọn 100 hóa đơn đã tải đầu tiên" : "Chọn mọi hóa đơn đã tải"}
              </button>
            )}
          </div>
          <ul className="bill-action-list">
            {drafts.map(({ invoiceId, roomNumber, contractCode }) => {
              const review = reviews[invoiceId];
              return (
                <li key={invoiceId} className="bill-card">
                  <strong>Phòng {roomNumber} · {contractCode}</strong>
                  {review ? (
                    <>
                      <p>Tổng {money(review.summary.baseTotal)} · Hạn {review.summary.dueDate}</p>
                      <ul>{review.items.map((item) => (
                        <li key={item.id}>{item.description} · {money(item.amount)}</li>
                      ))}</ul>
                      <label>
                        <input type="checkbox" checked={selected.includes(invoiceId)}
                          disabled={busy || (!selected.includes(invoiceId) && approved.length >= 100)}
                          onChange={(event) => setSelected(event.target.checked
                            ? [...selected, invoiceId] : selected.filter((id) => id !== invoiceId))} />
                        Chọn hóa đơn phòng {roomNumber}
                      </label>
                    </>
                  ) : (
                    <button type="button" className="bill-button bill-button--ghost" disabled={busy}
                      onClick={() => run(() => billingApi.invoice(invoiceId), (detail) => {
                        if (!detail.reviewVersion) {
                          setError("Hóa đơn đã đổi trạng thái. Danh sách đang được cập nhật.");
                          onChanged();
                          return;
                        }
                        setReviews((current) => ({ ...current, [invoiceId]: detail }));
                      })}>
                      Xem các khoản tiền
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
          <button type="button" className="bill-button bill-button--primary"
            disabled={busy || loading || !approved.length} onClick={issue}>
            Phát hành {approved.length || ""} hóa đơn đã chọn
          </button>
        </details>
      )}
    </div>
  );
}
