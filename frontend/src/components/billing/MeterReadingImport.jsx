import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { AlertTriangle, Download, Droplets, FileSpreadsheet, Upload, Zap } from "lucide-react";
import { billingApi } from "../../services/api/billingApi";
import InvoiceActionDialog from "./InvoiceActionDialog";
import "./meter-reading-import.css";

const utilities = [
  { key: "electricity", title: "Điện", unit: "kWh", expectedPrevious: "expectedElectricityPrevious", expectedCurrent: "expectedElectricityCurrent" },
  { key: "water", title: "Nước", unit: "m³", expectedPrevious: "expectedWaterPrevious", expectedCurrent: "expectedWaterCurrent" },
];
const roomStatusLabels = {
  updated: "Đã cập nhật",
  partial: "Cập nhật một phần",
  missing: "Chưa cập nhật",
  review: "Cần xử lý",
};
const importStatusLabels = {
  Edited: "Chưa kiểm tra",
  Missing: "Chưa nhập",
  Ready: "Hợp lệ",
  Error: "Có lỗi",
  NeedsReview: "Cần xử lý",
  AlreadyRecorded: "Đã có dữ liệu",
  Saved: "Đã lưu",
};
const pageSize = 25;
const maxFileBytes = 4 * 1024 * 1024;
const number = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });
const format = (value) => value == null ? "—" : number.format(value);
const isRecorded = (cell) =>
  ["AlreadyRecorded", "Saved"].includes(cell.status) && cell.currentValue != null;
const canEditReading = (cell) =>
  cell.canEnterOpening || (cell.previousValue != null && cell.status !== "NeedsReview");
const rowKey = (row) => `${row.roomId}:${row.contractId || ""}`;
const editKey = (row, key) => `${rowKey(row)}:${key}`;

function parseReading(raw) {
  const value = raw.trim();
  if (!value) return { value: null };
  if (!/^\d+(?:[.,]\d{1,2})?$/.test(value)) return { error: "Nhập số không âm, tối đa hai chữ số thập phân." };
  const normalized = value.replace(",", ".");
  const amount = Number(normalized);
  const [whole, fraction = ""] = normalized.split(".");
  const exact = `${whole.replace(/^0+(?=\d)/, "")}.${fraction.padEnd(2, "0")}`;
  if (!Number.isFinite(amount) || amount.toFixed(2) !== exact)
    return { error: "Chỉ số vượt giới hạn có thể nhập." };
  return { value: amount };
}

function roomStatus(row) {
  const recorded = utilities.filter(({ key }) => isRecorded(row[key])).length;
  if (recorded === utilities.length) return "updated";
  if (recorded) return "partial";
  if (utilities.some(({ key }) => ["Error", "NeedsReview"].includes(row[key].status))) return "review";
  return "missing";
}

function makeCommitBatch(rows, period, edits) {
  return {
    ...period,
    rows: rows.flatMap((row) => {
      const input = { roomId: row.roomId, contractId: row.contractId ?? null };
      for (const utility of utilities) {
        const cell = row[utility.key];
        if (cell.status !== "Ready" || edits.has(editKey(row, utility.key))) continue;
        input[utility.key] = cell.currentValue;
        input[utility.expectedPrevious] = cell.previousValue;
        input[utility.expectedCurrent] = cell.recordedValue ?? null;
        if (cell.canEnterOpening) input[initialField(utility.key)] = cell.previousValue;
        if (cell.manual) input[`${utility.key}Manual`] = true;
      }
      return utilities.some(({ key }) => input[key] != null) ? [input] : [];
    }),
  };
}

function saveFile(blob, filename) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function Pagination({ current, total, label, onChange }) {
  if (total <= 1) return null;
  return (
    <nav className="bill-pagination meter-import__pagination" aria-label={label}>
      <button className="bill-button bill-button--secondary" type="button" disabled={current <= 1}
        onClick={() => onChange(current - 1)}>Trang trước</button>
      <span>Trang {current} / {total}</span>
      <button className="bill-button bill-button--secondary" type="button" disabled={current >= total}
        onClick={() => onChange(current + 1)}>Trang sau</button>
    </nav>
  );
}

function RoomReadings({ propertyId, year, month, revision, onEditRoom, saving }) {
  const [result, setResult] = useState(null);
  const [retry, setRetry] = useState(0);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const controller = new AbortController();
    billingApi.meterImportReadings(propertyId, { year, month }, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ rows: data.rows, revision, retry });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        const message = error.status === 404 && !error.details?.code
          ? "Backend đang chạy chưa có API danh sách chỉ số. Hãy khởi động lại backend rồi chọn Thử lại."
          : error.message || "Không thể tải chỉ số.";
        setResult({ error: message, revision, retry });
      });
    return () => controller.abort();
  }, [propertyId, year, month, revision, retry]);

  const rows = result?.rows;
  const current = result?.revision === revision && result?.retry === retry;
  const visible = useMemo(() => {
    const query = search.trim().toLocaleLowerCase("vi");
    return (rows ?? []).filter((row) =>
      `${row.roomNumber} ${row.contractCode || ""}`.toLocaleLowerCase("vi").includes(query) && (filter === "all" || roomStatus(row) === filter));
  }, [rows, search, filter]);
  const totalPages = Math.max(1, Math.ceil(visible.length / pageSize));
  const currentPage = Math.min(page, totalPages);

  return (
    <section className="meter-import__list" aria-labelledby="meter-list-title" aria-busy={!current}>
      <div className="meter-import__toolbar">
        <h3 id="meter-list-title">Chỉ số theo phòng</h3>
        {rows?.length > 0 && (
          <div className="meter-import__filters">
            <label className="bill-field meter-import__field">
              Tìm phòng
              <input type="search" value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} />
            </label>
            <label className="bill-field meter-import__field">
              Trạng thái
              <select value={filter} onChange={(event) => { setFilter(event.target.value); setPage(1); }}>
                <option value="all">Tất cả</option>
                {Object.entries(roomStatusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </label>
          </div>
        )}
      </div>
      {!current && <p className="bill-empty" role="status">Đang tải chỉ số theo phòng…</p>}
      {current && result?.error && (
        <div className="meter-import__retry">
          <p className="bill-error" role="alert">{result.error}</p>
          <button className="bill-button bill-button--secondary" type="button" onClick={() => setRetry((value) => value + 1)}>Thử lại</button>
        </div>
      )}
      {current && rows && (
        <>
          {visible.length ? (
            <div className="bill-table-wrap meter-import__table-wrap" tabIndex={0} role="region" aria-label="Bảng chỉ số theo phòng">
              <table className="bill-table meter-import__table meter-import__table--list">
                <thead>
                  <tr>
                    <th scope="col">Phòng</th>
                    {utilities.map(({ key, title, unit }) => <th key={key} scope="col">{title} ({unit})</th>)}
                    <th scope="col">Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.slice((currentPage - 1) * pageSize, currentPage * pageSize).map((row) => {
                    const status = roomStatus(row);
                    const reason = status === "partial"
                      ? "Thiếu " + utilities.filter(({ key }) => !isRecorded(row[key])).map(({ title }) => title.toLocaleLowerCase("vi")).join(", ")
                      : status === "review"
                        ? utilities.map(({ key }) => row[key]).find((cell) => ["Error", "NeedsReview"].includes(cell.status))?.message
                        : null;
                    return (
                      <tr key={rowKey(row)}>
                        <th scope="row" className="meter-import__room">{row.roomNumber}{row.contractCode && <small>Hợp đồng {row.contractCode}</small>}</th>
                        {utilities.map(({ key }) => (
                          <td className="meter-import__value" key={key}>
                            {format(isRecorded(row[key]) ? row[key].currentValue : null)}
                            {!isRecorded(row[key]) && row[key].previousValue != null && <small>Đầu kỳ: {format(row[key].previousValue)}</small>}
                          </td>
                        ))}
                        <td className="meter-import__state">
                          <span className="meter-import__state-label" data-state={status}>{roomStatusLabels[status]}</span>
                          {reason && <small>{reason}</small>}
                          {utilities.some(({ key }) => canEditReading(row[key])) && (
                            <button type="button" className="bill-link" disabled={saving}
                              onClick={() => onEditRoom(row)}
                              aria-label={`Nhập hoặc sửa chỉ số, phòng ${row.roomNumber}${row.contractCode ? `, hợp đồng ${row.contractCode}` : ""}`}>Nhập / sửa chỉ số</button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : <p className="bill-empty">{rows.length ? "Không tìm thấy phòng phù hợp." : "Kỳ này không có phòng đang cho thuê."}</p>}
          <Pagination current={currentPage} total={totalPages} label="Trang chỉ số theo phòng" onChange={setPage} />
        </>
      )}
    </section>
  );
}

const initialField = (key) => key === "electricity" ? "initialElectricityPrevious" : "initialWaterPrevious";

function ImportReadingCell({ cell, room, utility, draft, disabled, onEdit }) {
  const editable = canEditReading(cell);
  const localError = draft && Object.values(draft).map(parseReading).find(({ error }) => error)?.error;
  const status = draft === undefined ? cell.status : localError ? "Error" : "Edited";
  const messageId = `meter-cell-${room.roomId}-${room.contractId || "room"}-${utility.key}`;
  const input = (field, label, value) => (
    <label className="meter-import__previous">
      {label}
      <input className="meter-import__reading-input" type="text" inputMode="decimal" maxLength={24}
        value={draft?.[field] ?? value ?? ""}
      aria-label={`Chỉ số ${utility.title.toLowerCase()} ${label.toLowerCase()}, phòng ${room.roomNumber}${room.contractCode ? `, hợp đồng ${room.contractCode}` : ""}`}
        aria-describedby={messageId} aria-invalid={status === "Error" || undefined}
        disabled={disabled} onChange={(event) => onEdit(room, utility.key, field, event.target.value)} />
    </label>
  );
  return (
    <td className="meter-import__reading" data-state={status}>
      <div className="meter-import__reading-main">
        {cell.recordedValue != null && (
          <span className="meter-import__previous">Đã lưu trước đó <strong>{format(cell.recordedValue)}</strong></span>
        )}
        {cell.canEnterOpening && input("opening", "Đầu kỳ", cell.previousValue)}
        {editable ? input("closing", "Cuối kỳ", cell.currentValue) : (
          <span className="meter-import__previous">Cuối kỳ <strong>{format(cell.currentValue)}</strong></span>
        )}
      </div>
      <div className="meter-import__cell-meta" id={messageId}>
        <strong>{importStatusLabels[status]}</strong>
        {!cell.canEnterOpening && cell.previousValue != null && <span> · Đầu kỳ {format(cell.previousValue)}</span>}
        {!draft && cell.consumption != null && <span> · Tiêu thụ {format(cell.consumption)} {utility.unit}</span>}
        {localError && <small>{localError}</small>}
        {draft === undefined && cell.message && cell.code !== "METER_IMPORT_VALUE_MISSING" && <small>{cell.message}</small>}
      </div>
    </td>
  );
}

export default function MeterReadingImport({ propertyId, month, onMonthChange, saving, onSavingChange }) {
  return <MeterPeriod key={propertyId + ":" + month} propertyId={propertyId} month={month}
    onMonthChange={onMonthChange} saving={saving} onSavingChange={onSavingChange} />;
}

function ManualMeterModal({ propertyId, period, room, onClose, onSavingChange, onChanged, onSuccess }) {
  const [drafts, setDrafts] = useState(() => ({
    electricity: {
      opening: room.electricity?.previousValue != null ? String(room.electricity.previousValue) : "",
      closing: room.electricity?.currentValue != null ? String(room.electricity.currentValue) : "",
    },
    water: {
      opening: room.water?.previousValue != null ? String(room.water.previousValue) : "",
      closing: room.water?.currentValue != null ? String(room.water.currentValue) : "",
    },
  }));
  const [busy, setBusy] = useState(false);
  const [apiError, setApiError] = useState("");
  const [confirmState, setConfirmState] = useState(null);
  const pending = useRef(null);

  useLayoutEffect(() => () => pending.current?.abort(), []);

  function updateField(key, field, value) {
    setApiError("");
    setConfirmState(null);
    setDrafts((current) => ({
      ...current,
      [key]: { ...current[key], [field]: value },
    }));
  }

  const parsedByUtility = useMemo(() => {
    const result = {};
    for (const utility of utilities) {
      const cell = room[utility.key];
      const editable = canEditReading(cell);
      const draft = drafts[utility.key];
      const closingParsed = editable ? parseReading(draft.closing) : { value: cell.currentValue };
      const openingParsed = cell.canEnterOpening
        ? parseReading(draft.opening)
        : { value: cell.previousValue };

      let rangeError = "";
      if (
        !closingParsed.error &&
        !openingParsed.error &&
        closingParsed.value != null &&
        openingParsed.value != null &&
        closingParsed.value < openingParsed.value
      ) {
        rangeError = "Chỉ số cuối kỳ phải lớn hơn hoặc bằng đầu kỳ.";
      }

      const missingOpeningError =
        cell.canEnterOpening &&
        closingParsed.value != null &&
        openingParsed.value == null &&
        !openingParsed.error
          ? "Vui lòng nhập chỉ số đầu kỳ."
          : "";

      const consumption =
        !closingParsed.error &&
        !openingParsed.error &&
        !rangeError &&
        closingParsed.value != null &&
        openingParsed.value != null
          ? Number((closingParsed.value - openingParsed.value).toFixed(2))
          : null;

      result[utility.key] = {
        cell,
        editable,
        openingValue: openingParsed.value,
        openingError: openingParsed.error || missingOpeningError,
        closingValue: closingParsed.value,
        closingError: closingParsed.error || rangeError,
        consumption,
      };
    }
    return result;
  }, [room, drafts]);

  const hasValidationError = utilities.some(
    ({ key }) => parsedByUtility[key].openingError || parsedByUtility[key].closingError,
  );
  const hasAnyInput = utilities.some(
    ({ key }) => parsedByUtility[key].editable && parsedByUtility[key].closingValue != null,
  );

  async function executeCommit(commitBody, controller) {
    const commitResult = await billingApi.meterImportCommit(propertyId, commitBody, controller.signal);
    if (controller.signal.aborted) return;

    const returnedRow = commitResult.rows?.find((r) => rowKey(r) === rowKey(room));
    const sentRow = commitBody.rows[0] || {};
    const outcome = { saved: 0, existing: 0, failed: 0 };
    const failureMessages = [];

    for (const utility of utilities) {
      if (sentRow[utility.key] == null) continue;
      const cell = returnedRow?.[utility.key];
      if (cell?.status === "Saved") outcome.saved += 1;
      else if (cell?.status === "AlreadyRecorded") outcome.existing += 1;
      else {
        outcome.failed += 1;
        if (cell?.message) failureMessages.push(`${utility.title}: ${cell.message}`);
      }
    }

    onChanged();
    if (outcome.failed > 0) {
      setConfirmState(null);
      setApiError(failureMessages.join(" ") || "Không thể lưu một số chỉ số. Vui lòng kiểm tra lại.");
      return;
    }

    onSuccess(
      `Phòng ${room.roomNumber}: đã lưu ${outcome.saved} chỉ số${outcome.existing ? `, giữ nguyên ${outcome.existing} chỉ số đã có` : ""}.`,
    );
    onClose();
  }

  async function handleSave() {
    if (pending.current || hasValidationError || !hasAnyInput) return;

    const input = { roomId: room.roomId, contractId: room.contractId ?? null };
    for (const { key } of utilities) {
      const info = parsedByUtility[key];
      if (!info.editable || info.closingValue == null) continue;
      input[key] = info.closingValue;
      if (info.cell.canEnterOpening) {
        input[initialField(key)] = info.openingValue;
      }
    }

    const controller = new AbortController();
    pending.current = controller;
    setBusy(true);
    setApiError("");
    onSavingChange(true);

    try {
      const validateRes = await billingApi.meterImportValidate(
        propertyId,
        { ...period, rows: [input] },
        controller.signal,
      );
      if (controller.signal.aborted) return;

      const validatedRow = validateRes.rows?.find((r) => rowKey(r) === rowKey(room));
      if (!validatedRow) {
        setApiError("Không nhận được kết quả kiểm tra từ máy chủ.");
        return;
      }

      const errorParts = [];
      let replacingCount = 0;
      let draftImpactCount = 0;
      const commitRow = { roomId: room.roomId, contractId: room.contractId ?? null };

      for (const utility of utilities) {
        if (input[utility.key] == null) continue;
        const vCell = validatedRow[utility.key];
        if (!vCell) continue;
        if (vCell.status === "Error" || vCell.status === "NeedsReview") {
          errorParts.push(`${utility.title}: ${vCell.message || "Giá trị không hợp lệ."}`);
          continue;
        }
        if (vCell.status === "Ready") {
          commitRow[utility.key] = vCell.currentValue;
          commitRow[utility.expectedPrevious] = vCell.previousValue;
          commitRow[utility.expectedCurrent] = vCell.recordedValue ?? null;
          if (vCell.canEnterOpening) commitRow[initialField(utility.key)] = vCell.previousValue;
          commitRow[`${utility.key}Manual`] = true;
          if (vCell.recordedValue != null) replacingCount += 1;
          if (vCell.message) draftImpactCount += 1;
        }
      }

      if (errorParts.length > 0) {
        setApiError(errorParts.join(" "));
        return;
      }

      if (!utilities.some(({ key }) => commitRow[key] != null)) {
        onSuccess(`Phòng ${room.roomNumber}: chỉ số không thay đổi.`);
        onClose();
        return;
      }

      const commitBody = { ...period, rows: [commitRow] };
      if (replacingCount > 0 || draftImpactCount > 0) {
        setConfirmState({ commitBody, replacingCount, draftImpactCount });
        return;
      }

      await executeCommit(commitBody, controller);
    } catch (cause) {
      if (!controller.signal.aborted) {
        setApiError(cause.message || "Không thể lưu chỉ số. Vui lòng thử lại.");
      }
    } finally {
      pending.current = null;
      if (!controller.signal.aborted) {
        setBusy(false);
        onSavingChange(false);
      }
    }
  }

  async function handleConfirmCommit() {
    if (pending.current || !confirmState) return;
    const controller = new AbortController();
    pending.current = controller;
    setBusy(true);
    setApiError("");
    onSavingChange(true);
    try {
      await executeCommit(confirmState.commitBody, controller);
    } catch (cause) {
      if (!controller.signal.aborted) {
        onChanged();
        setApiError(cause.message || "Không thể lưu chỉ số. Vui lòng thử lại.");
      }
    } finally {
      pending.current = null;
      if (!controller.signal.aborted) {
        setBusy(false);
        onSavingChange(false);
      }
    }
  }

  function cardBadge(info) {
    if (!info.editable) return { label: "Đã khóa", tone: "neutral" };
    if (info.closingError || info.openingError) return { label: "Cần kiểm tra", tone: "danger" };
    if (info.cell.status === "NeedsReview" || info.cell.status === "Error") {
      return { label: "Cần kiểm tra", tone: "warning" };
    }
    if (isRecorded(info.cell)) return { label: "Đã lưu", tone: "success" };
    return { label: "Chưa nhập", tone: "neutral" };
  }

  return (
    <InvoiceActionDialog
      title={`Nhập / sửa chỉ số — Phòng ${room.roomNumber}${room.contractCode ? ` · ${room.contractCode}` : ""}`}
      onClose={() => { if (!busy) onClose(); }}
      footer={
        <div className="bill-dialog__footer-actions bill-dialog__footer-actions--end">
          {confirmState ? (
            <>
              <button type="button" className="bill-button bill-button--secondary" disabled={busy}
                onClick={() => setConfirmState(null)}>
                Quay lại
              </button>
              <button type="button" className="bill-button" disabled={busy} onClick={handleConfirmCommit}>
                {busy ? "Đang lưu…" : "Xác nhận lưu"}
              </button>
            </>
          ) : (
            <>
              <button type="button" className="bill-button bill-button--secondary" disabled={busy} onClick={onClose}>
                Hủy
              </button>
              <button type="button" className="bill-button"
                disabled={busy || hasValidationError || !hasAnyInput} onClick={handleSave}>
                {busy ? "Đang lưu…" : "Lưu chỉ số"}
              </button>
            </>
          )}
        </div>
      }
    >
      <div className="meter-manual">
        <p className="meter-manual__subtitle">
          Kỳ <strong>Tháng {period.month}/{period.year}</strong> · Nhập chỉ số cuối kỳ thực tế của phòng.
        </p>

        <div className="meter-manual-grid">
          {utilities.map((utility) => {
            const info = parsedByUtility[utility.key];
            const badge = cardBadge(info);
            const draft = drafts[utility.key];
            const IconComp = utility.key === "electricity" ? Zap : Droplets;
            const showImpactNote =
              (info.cell.recordedValue != null &&
                info.closingValue != null &&
                info.closingValue !== info.cell.recordedValue) ||
              (info.cell.message && info.cell.code !== "METER_IMPORT_VALUE_MISSING");

            return (
              <section className="meter-manual-card" key={utility.key} aria-label={`Chỉ số ${utility.title}`}>
                <header className="meter-manual-card__head">
                  <div className="meter-manual-card__title">
                    <span className={`meter-manual-card__icon meter-manual-card__icon--${utility.key}`}>
                      <IconComp size={17} aria-hidden="true" />
                    </span>
                    <div>
                      <strong>{utility.title}</strong>
                      <small>Đơn vị: {utility.unit}</small>
                    </div>
                  </div>
                  <span className={`meter-manual-badge meter-manual-badge--${badge.tone}`}>
                    {badge.label}
                  </span>
                </header>

                {info.cell.recordedValue != null && (
                  <p className="meter-manual-ref">
                    Đã lưu kỳ này: <strong>{format(info.cell.recordedValue)} {utility.unit}</strong>
                  </p>
                )}

                <div className="meter-manual-fields">
                  {info.cell.canEnterOpening ? (
                    <label className="meter-manual-field">
                      <span>Chỉ số đầu kỳ ({utility.unit})</span>
                      <input
                        type="text"
                        inputMode="decimal"
                        maxLength={24}
                        value={draft.opening}
                        disabled={busy}
                        aria-invalid={Boolean(info.openingError) || undefined}
                        placeholder="Ví dụ: 1200"
                        onChange={(e) => updateField(utility.key, "opening", e.target.value)}
                      />
                      {info.openingError && <span className="meter-manual-error">{info.openingError}</span>}
                    </label>
                  ) : (
                    <div className="meter-manual-readonly">
                      <span>Đầu kỳ</span>
                      <strong>{format(info.cell.previousValue)} {utility.unit}</strong>
                    </div>
                  )}

                  {info.editable ? (
                    <label className="meter-manual-field">
                      <span>Chỉ số cuối kỳ ({utility.unit})</span>
                      <input
                        type="text"
                        inputMode="decimal"
                        maxLength={24}
                        value={draft.closing}
                        disabled={busy}
                        aria-invalid={Boolean(info.closingError) || undefined}
                        placeholder="Nhập số mới"
                        onChange={(e) => updateField(utility.key, "closing", e.target.value)}
                      />
                      {info.closingError && <span className="meter-manual-error">{info.closingError}</span>}
                    </label>
                  ) : (
                    <div className="meter-manual-readonly meter-manual-readonly--locked">
                      <span>Cuối kỳ (Đã khóa)</span>
                      <strong>{format(info.cell.currentValue)} {utility.unit}</strong>
                      {info.cell.message && <small>{info.cell.message}</small>}
                    </div>
                  )}
                </div>

                {info.consumption != null && (
                  <div className="meter-manual-preview">
                    <span>Tiêu thụ dự kiến:</span>
                    <strong>{format(info.consumption)} {utility.unit}</strong>
                  </div>
                )}

                {showImpactNote && (
                  <div className="meter-manual-note">
                    <AlertTriangle size={15} aria-hidden="true" />
                    <span>
                      {info.cell.message && info.cell.code !== "METER_IMPORT_VALUE_MISSING"
                        ? info.cell.message
                        : "Giá trị mới sẽ thay thế chỉ số đã lưu trước đó."}
                    </span>
                  </div>
                )}
              </section>
            );
          })}
        </div>

        {confirmState && (
          <div className="meter-manual-confirm" role="status">
            <strong>Xác nhận cập nhật chỉ số</strong>
            {confirmState.replacingCount > 0 && (
              <p>Có {confirmState.replacingCount} chỉ số sẽ thay thế số đã lưu trước đó.</p>
            )}
            {confirmState.draftImpactCount > 0 && (
              <p>Có {confirmState.draftImpactCount} chỉ số sẽ cập nhật tiền điện nước trong hóa đơn nháp.</p>
            )}
          </div>
        )}

        {apiError && <p className="bill-error" role="alert">{apiError}</p>}
      </div>
    </InvoiceActionDialog>
  );
}

function MeterPeriod({ propertyId, month, onMonthChange, saving, onSavingChange }) {
  const [year, billingMonth] = month.split("-").map(Number);
  const validPeriod = year >= 2000 && year <= 2100 && billingMonth >= 1 && billingMonth <= 12;
  const [open, setOpen] = useState(false);
  const [manualRow, setManualRow] = useState(null);
  const [manualSuccess, setManualSuccess] = useState("");
  const [revision, setRevision] = useState(0);

  function refreshReadings() {
    setRevision((value) => value + 1);
  }

  return (
    <section className="bill-card meter-import" aria-labelledby="meter-import-title">
      <div className="bill-section-head meter-import__heading">
        <h2 id="meter-import-title">Chỉ số điện nước</h2>
        <div className="meter-import__heading-actions">
          <label className="bill-field meter-import__field">
            Kỳ ghi chỉ số
            <input type="month" min="2000-01" max="2100-12" value={month}
              onChange={(event) => onMonthChange(event.target.value)} disabled={saving} />
          </label>
          <button className="bill-button" type="button" aria-expanded={open} aria-controls="meter-excel-panel"
            disabled={saving || !validPeriod} onClick={() => { setManualRow(null); setOpen((value) => !value); }}>
            <FileSpreadsheet size={17} aria-hidden="true" />
            {open ? "Đóng nhập Excel" : "Nhập từ Excel"}
          </button>
        </div>
      </div>
      {manualSuccess && <p className="bill-success" role="status">{manualSuccess}</p>}
      <div id="meter-excel-panel" hidden={!open}>
        {open && <MeterReadingEditor propertyId={propertyId} month={month} period={{ year, month: billingMonth }}
          onSavingChange={onSavingChange} onChanged={refreshReadings} />}
      </div>
      {manualRow && (
        <ManualMeterModal
          key={rowKey(manualRow)}
          propertyId={propertyId}
          period={{ year, month: billingMonth }}
          room={manualRow}
          onClose={() => setManualRow(null)}
          onSavingChange={onSavingChange}
          onChanged={refreshReadings}
          onSuccess={(msg) => setManualSuccess(msg)}
        />
      )}
      {validPeriod
        ? <RoomReadings propertyId={propertyId} year={year} month={billingMonth} revision={revision} saving={saving}
          onEditRoom={(row) => { setOpen(false); setManualSuccess(""); setManualRow(row); }} />
        : <p className="bill-empty">Chọn kỳ để xem hoặc nhập chỉ số.</p>}
    </section>
  );
}

function MeterReadingEditor({ propertyId, month, period, onSavingChange, onChanged, manualRow, onClose }) {
  const [file, setFile] = useState(null);
  const [rows, setRows] = useState(() => manualRow ? [manualRow] : null);
  const [edits, setEdits] = useState(() => new Map());
  const [page, setPage] = useState(1);
  const [issuesOnly, setIssuesOnly] = useState(false);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [confirming, setConfirming] = useState(false);
  const pending = useRef(null);
  const heading = useRef(null);
  const closeConfirm = useCallback(() => setConfirming(false), []);

  useLayoutEffect(() => () => pending.current?.abort(), []);
  useEffect(() => {
    if (manualRow) heading.current?.focus();
  }, [manualRow]);

  async function run(action, request, accepted) {
    if (pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setBusy(action);
    setError("");
    setSuccess("");
    if (action === "save") onSavingChange(true);
    try {
      const result = await request(controller.signal);
      if (!controller.signal.aborted) accepted(result);
    } catch (cause) {
      if (!controller.signal.aborted) {
        if (action === "save") onChanged();
        setError(cause.message || "Không thể xử lý chỉ số. Vui lòng thử lại.");
      }
    } finally {
      pending.current = null;
      if (!controller.signal.aborted) {
        setBusy("");
        if (action === "save") onSavingChange(false);
      }
    }
  }

  function chooseFile(event) {
    const next = event.target.files?.[0];
    if (!next) return;
    event.target.value = "";
    setFile(null);
    setRows(null);
    setEdits(new Map());
    setPage(1);
    setIssuesOnly(false);
    setError("");
    setSuccess("");
    if (!/\.xlsx$/i.test(next.name) || next.size <= 0 || next.size > maxFileBytes) {
      setError("Chọn tệp Excel .xlsx có dữ liệu và dung lượng tối đa 4 MB.");
      return;
    }
    setFile(next);
  }

  function preview() {
    setRows(null);
    setEdits(new Map());
    run("preview", (signal) => billingApi.meterImportPreview(propertyId, period, file, signal), (result) => {
      setRows(result.rows);
      setPage(1);
      setIssuesOnly(false);
    });
  }

  function edit(row, key, field, value) {
    const id = editKey(row, key);
    setEdits((current) => new Map(current).set(id, { ...current.get(id), [field]: value }));
  }

  function validateEdits() {
    const checked = new Map();
    const inputs = rows.flatMap((row) => {
      const input = { roomId: row.roomId, contractId: row.contractId ?? null };
      for (const { key } of utilities) {
        const id = editKey(row, key);
        if (!edits.has(id)) continue;
        const draft = edits.get(id);
        const cell = row[key];
        const closing = draft.closing === undefined ? { value: cell.currentValue } : parseReading(draft.closing);
        const opening = draft.opening === undefined ? { value: cell.previousValue } : parseReading(draft.opening);
        if (closing.error || opening.error) continue;
        checked.set(id, closing.value);
        input[key] = closing.value;
        if (cell.canEnterOpening) input[initialField(key)] = opening.value;
      }
      return utilities.some(({ key }) => Object.hasOwn(input, key)) ? [input] : [];
    });
    if (!inputs.length) return;
    run("validate", (signal) => billingApi.meterImportValidate(propertyId, { ...period, rows: inputs }, signal), (result) => {
      const byRoom = new Map(result.rows.map((row) => [rowKey(row), row]));
      const remaining = new Map(edits);
      setRows(rows.map((row) => {
        const next = { ...row };
        for (const { key } of utilities) {
          const id = editKey(row, key);
          const cell = byRoom.get(rowKey(row))?.[key];
          if (!checked.has(id) || !cell) continue;
          next[key] = { ...cell, manual: true };
          remaining.delete(id);
        }
        return next;
      }));
      setEdits(remaining);
      setSuccess("Đã kiểm tra các ô vừa sửa.");
    });
  }

  function commit() {
    const body = makeCommitBatch(rows, period, edits);
    if (!body.rows.length) return;
    closeConfirm();
    run("save", (signal) => billingApi.meterImportCommit(propertyId, body, signal), (result) => {
      const sent = new Map(body.rows.map((row) => [rowKey(row), row]));
      const returned = new Map(result.rows.map((row) => [rowKey(row), row]));
      const outcome = { saved: 0, existing: 0, failed: 0 };
      const updated = rows.map((row) => {
        const next = { ...row };
        for (const utility of utilities) {
          if (sent.get(rowKey(row))?.[utility.key] == null) continue;
          const cell = returned.get(rowKey(row))?.[utility.key];
          next[utility.key] = cell ?? { ...row[utility.key], status: "Error", message: "Chưa nhận được kết quả lưu." };
          if (cell?.status === "Saved") outcome.saved += 1;
          else if (cell?.status === "AlreadyRecorded") outcome.existing += 1;
          else outcome.failed += 1;
        }
        return next;
      });
      setRows(updated);
      setIssuesOnly(outcome.failed > 0);
      setPage(1);
      setSuccess(`Đã lưu ${outcome.saved} chỉ số · Đã có dữ liệu ${outcome.existing} · Lưu không thành công ${outcome.failed}.`);
      onChanged();
    });
  }

  const counts = useMemo(() => {
    const total = { ready: 0, missing: 0, issues: 0, existing: 0, replacing: 0, draftImpact: 0 };
    for (const row of rows ?? []) {
      for (const { key } of utilities) {
        const cell = row[key];
        if (edits.has(editKey(row, key))) total.issues += 1;
        else if (cell.status === "Ready") {
          total.ready += 1;
          if (cell.recordedValue != null) total.replacing += 1;
          if (cell.message) total.draftImpact += 1;
        } else if (cell.status === "Missing") total.missing += 1;
        else if (isRecorded(cell)) total.existing += 1;
        else total.issues += 1;
      }
    }
    return total;
  }, [rows, edits]);

  const visible = (rows ?? []).filter((row) =>
    !issuesOnly || utilities.some(({ key }) => edits.has(editKey(row, key)) ||
      ["Missing", "Error", "NeedsReview"].includes(row[key].status)));
  const totalPages = Math.max(1, Math.ceil(visible.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const fileLabel = file ? "Chọn lại tệp Excel" : "Chọn tệp Excel";

  return (
    <section className="meter-import__excel" aria-labelledby="meter-excel-title">
      <div className="meter-import__toolbar">
        <h3 id="meter-excel-title" ref={heading} tabIndex={manualRow ? -1 : undefined}>
          {manualRow ? `Nhập hoặc sửa chỉ số · Phòng ${manualRow.roomNumber}` : "Nhập chỉ số từ Excel"}
        </h3>
        {manualRow && <button type="button" className="bill-button bill-button--secondary" disabled={!!busy}
          onClick={onClose}>Đóng bảng nhập</button>}
      </div>
      <p className="meter-import__summary">{manualRow
        ? "Nhập chỉ số cuối kỳ. Nếu đây là lần ghi đầu tiên, hãy điền thêm chỉ số đầu kỳ thực tế."
        : "Mỗi dòng trong mẫu tương ứng với một hợp đồng. Điền chỉ số cuối kỳ rồi tải lên; bạn có thể kiểm tra và sửa trên bảng xem trước."}</p>
      {!manualRow && (
      <div className="meter-import__controls">
        <button className="bill-button bill-button--secondary" type="button" disabled={!!busy}
          onClick={() => run("download", (signal) => billingApi.meterImportTemplate(propertyId, period, signal),
            (blob) => saveFile(blob, `chi-so-dien-nuoc-${month}.xlsx`))}>
          <Download size={17} aria-hidden="true" /> {busy === "download" ? "Đang tải mẫu…" : "Tải mẫu Excel"}
        </button>
        <div className="meter-import__file">
          <label htmlFor="meter-excel-file" className="bill-button bill-button--secondary" aria-disabled={!!busy}>{fileLabel}</label>
          <input className="meter-import__file-input" id="meter-excel-file" type="file" accept=".xlsx"
            aria-label="Tệp Excel đã chỉnh" aria-describedby="meter-file-limit" disabled={!!busy} onChange={chooseFile} />
          <small id="meter-file-limit">{file ? file.name : ".xlsx, tối đa 4 MB"}</small>
        </div>
        <button className="bill-button" type="button" disabled={!file || !!busy} onClick={preview}>
          <Upload size={17} aria-hidden="true" /> {busy === "preview" ? "Đang kiểm tra…" : "Kiểm tra tệp"}
        </button>
      </div>
      )}
      {error && <p className="bill-error" role="alert">{error}</p>}
      {success && <p className="bill-success" role="status">{success}</p>}
      {rows && (
        <section className="meter-import__preview" aria-labelledby="meter-preview-title" aria-busy={!!busy}>
          <div className="meter-import__toolbar">
            <div>
              <h3 id="meter-preview-title">{manualRow ? "Chỉ số cần kiểm tra" : "Kết quả kiểm tra tệp"}</h3>
              <p className="meter-import__summary" aria-live="polite">
                {counts.ready} hợp lệ · {counts.missing} chưa nhập · {counts.issues} cần xử lý · {counts.existing} đã lưu
              </p>
            </div>
            <label className="meter-import__check">
              <input type="checkbox" checked={issuesOnly} onChange={(event) => { setIssuesOnly(event.target.checked); setPage(1); }} />
              Chỉ hiện ô thiếu hoặc cần xử lý
            </label>
          </div>
          {visible.length ? (
            <div className="bill-table-wrap meter-import__table-wrap" tabIndex={0} role="region"
              aria-label={manualRow ? "Bảng nhập chỉ số trực tiếp" : "Bảng kiểm tra tệp Excel"}>
              <table className="bill-table meter-import__table meter-import__table--preview">
                <thead>
                  <tr>
                    <th scope="col">Phòng</th>
                    {utilities.map(({ key, title, unit }) => <th scope="col" key={key}>{title} ({unit})</th>)}
                  </tr>
                </thead>
                <tbody>
                  {visible.slice((currentPage - 1) * pageSize, currentPage * pageSize).map((row) => (
                    <tr key={rowKey(row)}>
                      <th scope="row" className="meter-import__room">
                        {row.roomNumber || "Không xác định"}
                        {row.contractCode && <small>Hợp đồng {row.contractCode}</small>}
                        {row.excelRow && <small>Dòng {row.excelRow} trong Excel</small>}
                      </th>
                      {utilities.map((utility) => <ImportReadingCell key={utility.key} cell={row[utility.key]}
                        room={row} utility={utility} draft={edits.get(editKey(row, utility.key))}
                        disabled={!!busy} onEdit={edit} />)}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : <p className="bill-empty">{issuesOnly ? "Không có ô thiếu hoặc cần xử lý." : "Tệp không có phòng để kiểm tra."}</p>}
          <Pagination current={currentPage} total={totalPages} label="Trang kết quả kiểm tra tệp" onChange={setPage} />
          <div className="meter-import__actions">
            <button className="bill-button bill-button--secondary" type="button"
              disabled={!!busy || ![...edits.values()].some((draft) =>
                Object.values(draft).every((value) => !parseReading(value).error))}
              onClick={validateEdits}>{busy === "validate" ? "Đang kiểm tra…" : "Kiểm tra lại ô đã sửa"}</button>
            <button className="bill-button" type="button" disabled={!!busy || !counts.ready}
              onClick={() => setConfirming(true)}>{busy === "save" ? "Đang lưu…" : "Lưu chỉ số hợp lệ"}</button>
          </div>
        </section>
      )}
      {confirming && (
        <InvoiceActionDialog title="Xác nhận lưu chỉ số" onClose={closeConfirm} footer={
          <div className="bill-dialog__footer-actions">
            <button type="button" className="bill-button bill-button--secondary" onClick={closeConfirm}>Quay lại</button>
            <button type="button" className="bill-button" onClick={commit}>Xác nhận lưu</button>
          </div>
        }>
          <p>Lưu {counts.ready} chỉ số hợp lệ cho kỳ {period.month}/{period.year}.</p>
          {counts.replacing > 0 && <p>Trong số đó có {counts.replacing} chỉ số sẽ thay thế số đã lưu.</p>}
          {counts.draftImpact > 0 && <p>Có {counts.draftImpact} chỉ số sẽ cập nhật tiền điện nước trong hóa đơn nháp.</p>}
          {counts.missing + counts.issues > 0 && <p>Còn {counts.missing + counts.issues} chỉ số cần xử lý hoặc kiểm tra lại.</p>}
        </InvoiceActionDialog>
      )}
    </section>
  );
}
