import { useEffect, useState } from "react";
import { Droplets, Pencil, Plus, Zap } from "lucide-react";
import { billingApi } from "../../services/api/billingApi";
import InvoiceActionDialog from "./InvoiceActionDialog";

const today = () =>
  new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
const nextMonth = () => {
  const [y, m] = today().split("-").map(Number);
  return new Date(Date.UTC(y, m, 1)).toISOString().slice(0, 10);
};
const monthAfter = (date) => {
  const [y, m] = date.split("-").map(Number);
  return new Date(Date.UTC(y, m, 1)).toISOString().slice(0, 10);
};
const fixedRateUnit = (type) => (type === "Electricity" ? "kWh" : "m³");
const SERVICE_UNITS = ["tháng", "người", "khác"];
const rateDraft = (type) => ({
  type,
  unitPrice: "",
  unit: fixedRateUnit(type),
  effectiveFrom: nextMonth(),
});
const serviceDraft = () => ({
  name: "",
  description: "",
  unit: "tháng",
  defaultPrice: "",
  isActive: true,
});
const assignmentDraft = () => ({
  serviceId: "",
  customPrice: "",
  quantity: "1",
  startDate: nextMonth(),
});
const money = (n) =>
  new Intl.NumberFormat("vi-VN").format(Number(n || 0)) + " ₫";
const nameOf = (type) => (type === "Electricity" ? "Điện" : "Nước");
const activeRate = (rates, type) =>
  rates
    .filter(
      (r) =>
        r.type === type &&
        r.effectiveFrom <= today() &&
        (!r.effectiveTo || r.effectiveTo >= today()),
    )
    .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0];
const futureRate = (rates, type) =>
  rates
    .filter((r) => r.type === type && r.effectiveFrom > today())
    .sort((a, b) => a.effectiveFrom.localeCompare(b.effectiveFrom))[0];

export default function BillingInputs({
  propertyId,
  rooms = [],
  onChanged,
  initialRoomId = "",
}) {
  const [roomId, setRoomId] = useState(initialRoomId);
  const [rates, setRates] = useState([]);
  const [services, setServices] = useState([]);
  const [assignments, setAssignments] = useState([]);
  const [rateForm, setRateForm] = useState(null);
  const [serviceForm, setServiceForm] = useState(null);
  const [assignmentForm, setAssignmentForm] = useState(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    setRoomId(initialRoomId);
    setRates([]);
    setServices([]);
    setAssignments([]);
    setRateForm(null);
    setServiceForm(null);
    setAssignmentForm(null);
    setError("");
  }, [propertyId, initialRoomId]);

  useEffect(() => {
    if (roomId && !rooms.some((r) => String(r.id) === String(roomId)))
      setRoomId("");
  }, [roomId, rooms]);

  useEffect(() => {
    setAssignmentForm(null);
    setError("");
  }, [roomId]);

  useEffect(() => {
    if (!propertyId) return;
    const controller = new AbortController();
    setLoading(true);
    Promise.all([
      billingApi.rates(propertyId, controller.signal),
      billingApi.propertyServices(propertyId, controller.signal),
      roomId
        ? billingApi.roomServices(propertyId, roomId, controller.signal)
        : Promise.resolve([]),
    ])
      .then(([r, s, a]) => {
        setRates(r || []);
        setServices(s || []);
        setAssignments(a || []);
      })
      .catch((e) => {
        if (e.name !== "AbortError")
          setError(e.message || "Không tải được dữ liệu.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, roomId, revision]);

  async function submit(event, action, reset, message) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await action();
      reset();
      setSuccess(message);
      setRevision((v) => v + 1);
      onChanged?.();
    } catch (e) {
      setError(e.message || "Không thể hoàn tất thao tác.");
    } finally {
      setBusy(false);
    }
  }

  function openRateModal(type) {
    const existing = activeRate(rates, type) || futureRate(rates, type);
    setError("");
    setServiceForm(null);
    setAssignmentForm(null);
    setRateForm({
      ...rateDraft(type),
      unitPrice: existing?.unitPrice ?? "",
      unit: fixedRateUnit(type),
      initial: !rates.some((r) => r.type === type),
      effectiveFrom: existing ? nextMonth() : today().slice(0, 7) + "-01",
    });
  }

  function closeRateModal() {
    setRateForm(null);
    setError("");
  }

  function openServiceModal(service = null) {
    setError("");
    setRateForm(null);
    setAssignmentForm(null);
    setServiceForm(
      service
        ? {
            ...service,
            defaultPrice: service.defaultPrice ?? "",
          }
        : serviceDraft(),
    );
  }

  function closeServiceModal() {
    setServiceForm(null);
    setError("");
  }

  function openAssignmentModal(assignment = null) {
    setError("");
    setRateForm(null);
    setServiceForm(null);
    if (assignment) {
      const earliestStartDate =
        monthAfter(assignment.startDate) > nextMonth()
          ? monthAfter(assignment.startDate)
          : nextMonth();
      setAssignmentForm({
        ...assignmentDraft(),
        id: assignment.id,
        serviceId: assignment.serviceId,
        quantity: String(assignment.quantity || 1),
        customPrice: assignment.unitPrice ?? "",
        startDate: earliestStartDate,
        earliestStartDate,
      });
      return;
    }
    setAssignmentForm(assignmentDraft());
  }

  function closeAssignmentModal() {
    setAssignmentForm(null);
    setError("");
  }

  function saveRate(event) {
    const body = {
      type: rateForm.type,
      unitPrice: Number(rateForm.unitPrice),
      unit: fixedRateUnit(rateForm.type),
      effectiveFrom: rateForm.effectiveFrom,
      effectiveTo: null,
    };
    submit(
      event,
      () =>
        rateForm.initial
          ? billingApi.createRate(propertyId, body)
          : billingApi.reviseRate(propertyId, body),
      closeRateModal,
      "Đã lưu đơn giá.",
    );
  }

  function saveService(event) {
    const body = {
      name: serviceForm.name.trim(),
      description: serviceForm.description?.trim() || null,
      unit: serviceForm.unit.trim(),
      defaultPrice:
        serviceForm.defaultPrice === ""
          ? null
          : Number(serviceForm.defaultPrice),
      isActive: serviceForm.isActive,
    };
    submit(
      event,
      () =>
        serviceForm.id
          ? billingApi.updateService(propertyId, serviceForm.id, body)
          : billingApi.createPropertyService(propertyId, body),
      closeServiceModal,
      "Đã lưu dịch vụ của khu trọ.",
    );
  }

  function saveAssignment(event) {
    const body = {
      serviceId: assignmentForm.serviceId,
      customPrice:
        assignmentForm.customPrice === ""
          ? null
          : Number(assignmentForm.customPrice),
      quantity: Number(assignmentForm.quantity),
      startDate: assignmentForm.startDate,
      endDate: null,
    };
    submit(
      event,
      () =>
        assignmentForm.id
          ? billingApi.reviseRoomService(
              propertyId,
              roomId,
              assignmentForm.id,
              body,
            )
          : billingApi.assignService(propertyId, roomId, body),
      closeAssignmentModal,
      "Đã lưu dịch vụ. Thay đổi áp dụng từ tháng sau.",
    );
  }

  const chosen = services.find(
    (s) => String(s.id) === String(assignmentForm?.serviceId),
  );
  const unitPrice =
    assignmentForm?.customPrice !== ""
      ? Number(assignmentForm?.customPrice)
      : Number(chosen?.defaultPrice || 0);
  const quantity = Number(assignmentForm?.quantity || 0);
  const earliestAssignmentDate =
    assignmentForm?.earliestStartDate || nextMonth();
  const hasOpenModal = Boolean(rateForm || serviceForm || assignmentForm);
  const selectedRoom = rooms.find((r) => String(r.id) === String(roomId));
  const selectedRoomLabel =
    selectedRoom?.roomNumber || selectedRoom?.number || roomId;

  const isRateDateInvalid =
    rateForm && !rateForm.initial && !rateForm.effectiveFrom.endsWith("-01");
  const isAssignmentInvalid =
    assignmentForm &&
    (!assignmentForm.serviceId ||
      (chosen?.defaultPrice == null && assignmentForm.customPrice === "") ||
      !Number.isInteger(quantity) ||
      quantity < 1 ||
      assignmentForm.startDate < earliestAssignmentDate ||
      !assignmentForm.startDate.endsWith("-01"));

  if (!propertyId)
    return (
      <p className="bill-empty">Chọn khu trọ để nhập dữ liệu tính hóa đơn.</p>
    );

  return (
    <div className="bill-inputs">
      {/* Tầng 1: Giá điện nước hiện hành */}
      <section className="bill-card">
        <div className="bill-section-head">
          <div>
            <h2>Giá điện nước hiện hành</h2>
            <p>
              Giá được áp dụng theo khu trọ. Chỉ mở biểu mẫu khi muốn thay đổi.
            </p>
          </div>
        </div>
        <div className="bill-rate-grid">
          {["Electricity", "Water"].map((type) => {
            const current = activeRate(rates, type);
            const upcoming = futureRate(rates, type);
            const Icon = type === "Electricity" ? Zap : Droplets;
            return (
              <div className="bill-rate-card" key={type}>
                <div className="bill-rate-card__info">
                  <span className="bill-rate-card__label">
                    <Icon size={14} aria-hidden="true" />
                    {nameOf(type)}
                  </span>
                  <strong className="bill-rate-card__price">
                    {current
                      ? `${money(current.unitPrice)}/${current.unit}`
                      : "Chưa có đơn giá hiện hành"}
                  </strong>
                  {current && (
                    <small>Áp dụng từ {current.effectiveFrom}</small>
                  )}
                  {upcoming && (
                    <small className="bill-rate-card__upcoming">
                      Giá từ {upcoming.effectiveFrom}:{" "}
                      {money(upcoming.unitPrice)}/{upcoming.unit}
                    </small>
                  )}
                </div>
                <button
                  type="button"
                  className="bill-button bill-button--secondary"
                  onClick={() => openRateModal(type)}
                  disabled={busy}
                >
                  <Pencil size={14} aria-hidden="true" />
                  {rates.some((r) => r.type === type)
                    ? "Chỉnh giá"
                    : "Thêm giá ban đầu"}
                </button>
              </div>
            );
          })}
        </div>
      </section>

      {/* Tầng 2: Danh mục dịch vụ khu trọ + Dịch vụ của phòng */}
      <div className="bill-grid bill-grid--two">
        <section className="bill-card bill-inputs-card">
          <div className="bill-section-head">
            <div>
              <h2>Danh mục dịch vụ của khu trọ</h2>
              <p>Mỗi khu trọ có danh mục và giá mặc định riêng.</p>
            </div>
            <button
              type="button"
              className="bill-button bill-button--secondary"
              onClick={() => openServiceModal()}
            >
              <Plus size={15} aria-hidden="true" />
              Thêm dịch vụ
            </button>
          </div>

          {services.length ? (
            <ul className="bill-list bill-list--compact">
              {services.map((s) => (
                <li key={s.id} className="bill-inputs-item">
                  <div className="bill-inputs-item__info">
                    <strong>
                      {s.name} ·{" "}
                      {s.defaultPrice == null
                        ? "Chưa có giá"
                        : `${money(s.defaultPrice)}/${s.unit}`}
                    </strong>
                    <small>
                      <span
                        className={`bill-service-status ${
                          s.isActive ? "is-active" : "is-inactive"
                        }`}
                      >
                        {s.isActive ? "Đang sử dụng" : "Ngừng sử dụng"}
                      </span>
                      {s.description ? ` · ${s.description}` : ""}
                    </small>
                  </div>
                  <button
                    type="button"
                    className="bill-link bill-link--compact"
                    aria-label={`Sửa ${s.name}`}
                    onClick={() => openServiceModal(s)}
                  >
                    <Pencil size={13} aria-hidden="true" />
                    Sửa
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="bill-empty">Khu trọ chưa có dịch vụ.</p>
          )}
        </section>

        <section className="bill-card bill-inputs-card">
          <div className="bill-section-head bill-section-head--room">
            <div>
              <h2>Dịch vụ của phòng</h2>
              <p>
                Nhập số lượng theo phòng. Dịch vụ mới và thay đổi bắt đầu từ
                tháng sau.
              </p>
            </div>
            <div className="bill-room-controls">
              <select
                aria-label="Chọn phòng"
                className="bill-room-select"
                value={roomId}
                onChange={(e) => setRoomId(e.target.value)}
              >
                <option value="">Chọn phòng</option>
                {rooms.map((r) => (
                  <option key={r.id} value={r.id}>
                    Phòng {r.roomNumber || r.number || r.id}
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="bill-button bill-button--secondary"
                disabled={!roomId || !services.some((s) => s.isActive)}
                onClick={() => openAssignmentModal()}
              >
                <Plus size={15} aria-hidden="true" />
                Gán dịch vụ
              </button>
            </div>
          </div>

          {!roomId ? (
            <p className="bill-empty">
              Chọn phòng ở góc trên để xem và gán dịch vụ áp dụng riêng.
            </p>
          ) : assignments.length ? (
            <ul className="bill-list bill-list--compact">
              {assignments.map((a) => (
                <li key={a.id} className="bill-inputs-item">
                  <div className="bill-inputs-item__info">
                    <strong>
                      {a.serviceName} · {Number(a.quantity || 1)} ×{" "}
                      {money(a.unitPrice)} ={" "}
                      {money(
                        Number(a.quantity || 1) * Number(a.unitPrice || 0),
                      )}
                    </strong>
                    <small>
                      {a.startDate} → {a.endDate || "Chưa kết thúc"}
                    </small>
                  </div>
                  {!a.endDate &&
                    services.find((s) => String(s.id) === String(a.serviceId))
                      ?.isActive && (
                      <button
                        type="button"
                        className="bill-link bill-link--compact"
                        onClick={() => openAssignmentModal(a)}
                      >
                        <Pencil size={13} aria-hidden="true" />
                        Chỉnh số lượng / giá
                      </button>
                    )}
                </li>
              ))}
            </ul>
          ) : (
            <p className="bill-empty">Phòng chưa được gán dịch vụ.</p>
          )}
        </section>
      </div>

      {loading && <p role="status">Đang tải dữ liệu...</p>}
      {!hasOpenModal && error && (
        <p className="bill-error" role="alert">
          {error}
        </p>
      )}
      {success && (
        <p className="bill-success" role="status">
          {success}
        </p>
      )}

      {/* Popup 1: Chỉnh / Thêm giá điện nước */}
      {rateForm && (
        <InvoiceActionDialog
          title={
            rateForm.initial
              ? `Thêm giá ${nameOf(rateForm.type).toLowerCase()} ban đầu`
              : `Chỉnh giá ${nameOf(rateForm.type).toLowerCase()}`
          }
          onClose={closeRateModal}
          footer={
            <div className="bill-dialog__footer-actions bill-dialog__footer-actions--end">
              <button
                type="button"
                className="bill-button bill-button--secondary"
                onClick={closeRateModal}
                disabled={busy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="bill-rate-form"
                className="bill-button"
                disabled={busy || isRateDateInvalid}
              >
                {busy ? "Đang lưu..." : "Lưu giá"}
              </button>
            </div>
          }
        >
          <form
            id="bill-rate-form"
            className="bill-form bill-modal-form"
            onSubmit={saveRate}
          >
            <p className="bill-hint bill-field--wide bill-modal-hint">
              {rateForm.initial
                ? "Giá ban đầu có thể áp dụng từ đầu tháng hiện tại."
                : "Giá sửa đổi có hiệu lực từ ngày đầu tháng sau hoặc muộn hơn (ngày 01)."}
            </p>
            <label>
              Đơn giá (₫)
              <input
                type="number"
                min="0"
                step="0.01"
                required
                value={rateForm.unitPrice}
                onChange={(e) =>
                  setRateForm({ ...rateForm, unitPrice: e.target.value })
                }
              />
            </label>
            <label>
              Đơn vị (cố định)
              <input
                type="text"
                value={fixedRateUnit(rateForm.type)}
                readOnly
                disabled
                aria-readonly="true"
              />
            </label>
            <label className="bill-field--wide">
              Ngày bắt đầu
              <input
                type="date"
                required
                min={
                  rateForm.initial ? today().slice(0, 7) + "-01" : nextMonth()
                }
                value={rateForm.effectiveFrom}
                onChange={(e) =>
                  setRateForm({ ...rateForm, effectiveFrom: e.target.value })
                }
              />
            </label>
            {isRateDateInvalid && (
              <p className="bill-hint bill-field--wide">
                Ngày bắt đầu giá mới phải là ngày mùng 1 của tháng (ví dụ:
                YYYY-MM-01).
              </p>
            )}
            {error && (
              <p className="bill-error bill-field--wide" role="alert">
                {error}
              </p>
            )}
          </form>
        </InvoiceActionDialog>
      )}

      {/* Popup 2: Thêm / Sửa dịch vụ khu trọ */}
      {serviceForm && (
        <InvoiceActionDialog
          title={
            serviceForm.id
              ? `Sửa dịch vụ: ${serviceForm.name || ""}`
              : "Thêm dịch vụ khu trọ"
          }
          onClose={closeServiceModal}
          footer={
            <div className="bill-dialog__footer-actions bill-dialog__footer-actions--end">
              <button
                type="button"
                className="bill-button bill-button--secondary"
                onClick={closeServiceModal}
                disabled={busy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="bill-service-form"
                className="bill-button"
                disabled={busy}
              >
                {busy ? "Đang lưu..." : "Lưu dịch vụ"}
              </button>
            </div>
          }
        >
          <form
            id="bill-service-form"
            className="bill-form bill-modal-form"
            onSubmit={saveService}
          >
            <p className="bill-hint bill-field--wide bill-modal-hint">
              Thay đổi gán cho phòng bắt đầu từ tháng sau.
            </p>
            <label>
              Tên dịch vụ
              <input
                required
                maxLength="100"
                value={serviceForm.name}
                onChange={(e) =>
                  setServiceForm({ ...serviceForm, name: e.target.value })
                }
              />
            </label>
            <label>
              Đơn vị tính
              <select
                required
                value={serviceForm.unit}
                onChange={(e) =>
                  setServiceForm({ ...serviceForm, unit: e.target.value })
                }
              >
                <option value="tháng">Tháng</option>
                <option value="người">Người</option>
                <option value="khác">Khác</option>
                {serviceForm.unit &&
                  !SERVICE_UNITS.includes(serviceForm.unit) && (
                    <option value={serviceForm.unit}>{serviceForm.unit}</option>
                  )}
              </select>
            </label>
            <label>
              Giá mặc định
              <input
                type="number"
                min="0"
                step="0.01"
                value={serviceForm.defaultPrice}
                onChange={(e) =>
                  setServiceForm({
                    ...serviceForm,
                    defaultPrice: e.target.value,
                  })
                }
              />
            </label>
            {serviceForm.id && (
              <label className="bill-checkbox">
                <input
                  type="checkbox"
                  checked={serviceForm.isActive}
                  onChange={(e) =>
                    setServiceForm({
                      ...serviceForm,
                      isActive: e.target.checked,
                    })
                  }
                />
                Đang sử dụng
              </label>
            )}
            <label className="bill-field--wide">
              Mô tả
              <textarea
                maxLength="500"
                rows={3}
                value={serviceForm.description || ""}
                onChange={(e) =>
                  setServiceForm({
                    ...serviceForm,
                    description: e.target.value,
                  })
                }
              />
            </label>
            {error && (
              <p className="bill-error bill-field--wide" role="alert">
                {error}
              </p>
            )}
          </form>
        </InvoiceActionDialog>
      )}

      {/* Popup 3: Gán / Cập nhật dịch vụ phòng */}
      {assignmentForm && (
        <InvoiceActionDialog
          title={`${
            assignmentForm.id ? "Cập nhật dịch vụ" : "Gán dịch vụ"
          } — Phòng ${selectedRoomLabel}`}
          onClose={closeAssignmentModal}
          footer={
            <div className="bill-dialog__footer-actions bill-dialog__footer-actions--end">
              <button
                type="button"
                className="bill-button bill-button--secondary"
                onClick={closeAssignmentModal}
                disabled={busy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="bill-assignment-form"
                className="bill-button"
                disabled={busy || isAssignmentInvalid}
              >
                {busy ? "Đang lưu..." : "Lưu dịch vụ"}
              </button>
            </div>
          }
        >
          <form
            id="bill-assignment-form"
            className="bill-form bill-modal-form"
            onSubmit={saveAssignment}
          >
            <p className="bill-hint bill-field--wide bill-modal-hint">
              Ngày bắt đầu phải là ngày 1 của tháng sau hoặc muộn hơn.
            </p>
            <label>
              Dịch vụ
              <select
                required
                disabled={Boolean(assignmentForm.id)}
                value={assignmentForm.serviceId}
                onChange={(e) =>
                  setAssignmentForm({
                    ...assignmentForm,
                    serviceId: e.target.value,
                  })
                }
              >
                <option value="">Chọn dịch vụ</option>
                {services
                  .filter(
                    (s) =>
                      s.isActive ||
                      String(s.id) === String(assignmentForm.serviceId),
                  )
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              Số lượng
              <input
                type="number"
                min="1"
                step="1"
                required
                value={assignmentForm.quantity}
                onChange={(e) =>
                  setAssignmentForm({
                    ...assignmentForm,
                    quantity: e.target.value,
                  })
                }
              />
            </label>
            <label>
              Giá riêng (để trống dùng giá mặc định)
              <input
                type="number"
                min="0"
                step="0.01"
                value={assignmentForm.customPrice}
                onChange={(e) =>
                  setAssignmentForm({
                    ...assignmentForm,
                    customPrice: e.target.value,
                  })
                }
              />
            </label>
            <label>
              Ngày bắt đầu
              <input
                type="date"
                min={earliestAssignmentDate}
                required
                value={assignmentForm.startDate}
                onChange={(e) =>
                  setAssignmentForm({
                    ...assignmentForm,
                    startDate: e.target.value,
                  })
                }
              />
            </label>
            <p
              className="bill-assignment-total bill-field--wide"
              aria-live="polite"
            >
              Dự kiến:
              <strong>
                {chosen && quantity > 0
                  ? `${quantity} × ${money(unitPrice)} = ${money(
                      quantity * unitPrice,
                    )}`
                  : "Chọn dịch vụ và nhập số lượng"}
              </strong>
            </p>
            {error && (
              <p className="bill-error bill-field--wide" role="alert">
                {error}
              </p>
            )}
          </form>
        </InvoiceActionDialog>
      )}
    </div>
  );
}
