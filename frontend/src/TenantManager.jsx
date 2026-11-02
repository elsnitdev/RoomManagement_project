import { useEffect, useRef, useState } from "react";
import { Badge, Button, Empty, Field, Heading, Icon, Info } from "./ui";
import { GENDER_OPTIONS, GENDER_LABELS } from "./constants/tenants";
import { validateTenantProfile } from "./utils/tenantValidators";
import { useApiQuery } from "./hooks/useApiQuery";
import {
  getLandlordTenants,
  getLandlordTenantById,
  createLandlordTenant,
  updateLandlordTenant,
  mapTenantApiError,
} from "./services/api";

const PAGE_SIZE = 5;

const emptyDraft = {
  fullName: "",
  phoneNumber: "",
  email: "",
  citizenId: "",
  dateOfBirth: "",
  gender: "",
  permanentAddress: "",
};

export default function TenantManager({
  notify,
  contracts = [],
  navigate,
}) {
  const [keyword, setKeyword] = useState("");
  const [debouncedKeyword, setDebouncedKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState(null);
  const [selectedSnapshot, setSelectedSnapshot] = useState(null);
  const [isEditing, setIsEditing] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const [fieldErrors, setFieldErrors] = useState({});
  const [generalError, setGeneralError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const detailRef = useRef(null);

  // Debounce từ khóa tìm kiếm (350ms) để tối ưu số lượng request gửi lên Backend
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedKeyword(keyword.trim());
      setPage(1);
    }, 350);
    return () => clearTimeout(timer);
  }, [keyword]);

  // 1. Truy vấn danh sách hồ sơ người thuê phân trang từ Backend API
  const listCacheKey = `landlord:tenants:list:${page}:${PAGE_SIZE}:${debouncedKeyword}`;
  const {
    data: listResponse,
    error: listError,
    isLoading: isListLoading,
    reload: reloadList,
  } = useApiQuery(listCacheKey, () =>
    getLandlordTenants({
      keyword: debouncedKeyword,
      page,
      pageSize: PAGE_SIZE,
    }),
  );

  const pagedTenants = Array.isArray(listResponse?.data) ? listResponse.data : [];
  const totalItems =
    typeof listResponse?.meta?.total === "number"
      ? listResponse.meta.total
      : typeof listResponse?.total === "number"
        ? listResponse.total
        : pagedTenants.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  // 2. Truy vấn chi tiết hồ sơ người thuê khi người dùng chọn một dòng
  const detailCacheKey = selectedId ? `landlord:tenant:${selectedId}` : null;
  const {
    data: detailResponse,
    error: detailError,
    isLoading: isDetailLoading,
    reload: reloadDetail,
  } = useApiQuery(
    detailCacheKey,
    () => getLandlordTenantById(selectedId),
    { enabled: Boolean(selectedId) },
  );

  const selectedTenant =
    detailResponse?.data ||
    pagedTenants.find((item) => item.id === selectedId) ||
    (selectedSnapshot?.id === selectedId ? selectedSnapshot : null);

  // Đồng bộ dữ liệu mới nhất vào form sửa nếu người dùng bấm "Sửa" trực tiếp từ bảng
  useEffect(() => {
    if (isEditing && detailResponse?.data && detailResponse.data.id === selectedId) {
      const fresh = detailResponse.data;
      setDraft({
        fullName: fresh.fullName || "",
        phoneNumber: fresh.phoneNumber || "",
        email: fresh.email || "",
        citizenId: fresh.citizenId || "",
        dateOfBirth: fresh.dateOfBirth ? String(fresh.dateOfBirth).split("T")[0] : "",
        gender: fresh.gender || "",
        permanentAddress: fresh.permanentAddress || "",
      });
    }
  }, [detailResponse, isEditing, selectedId]);

  // Lấy các hợp đồng liên quan đến hồ sơ người thuê
  const associatedContracts = selectedTenant
    ? contracts.filter(
        (c) =>
          (c.tenant &&
            selectedTenant.fullName &&
            c.tenant.toLowerCase() === selectedTenant.fullName.toLowerCase()) ||
          c.tenantId === selectedTenant.id,
      )
    : [];

  function openCreateForm() {
    setSelectedId(null);
    setSelectedSnapshot(null);
    setIsEditing(false);
    setDraft(emptyDraft);
    setFieldErrors({});
    setGeneralError("");
    setShowAddForm(true);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function openEditForm(tenant) {
    setShowAddForm(false);
    setIsEditing(true);
    setSelectedId(tenant.id);
    setSelectedSnapshot(tenant);
    setDraft({
      fullName: tenant.fullName || "",
      phoneNumber: tenant.phoneNumber || "",
      email: tenant.email || "",
      citizenId: tenant.citizenId || "",
      dateOfBirth: tenant.dateOfBirth ? String(tenant.dateOfBirth).split("T")[0] : "",
      gender: tenant.gender || "",
      permanentAddress: tenant.permanentAddress || "",
    });
    setFieldErrors({});
    setGeneralError("");
    setTimeout(() => {
      detailRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
    }, 50);
  }

  function openDetail(tenant) {
    setShowAddForm(false);
    setIsEditing(false);
    setSelectedId(tenant.id);
    setSelectedSnapshot(tenant);
    setFieldErrors({});
    setGeneralError("");
    setTimeout(() => {
      detailRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
    }, 50);
  }

  function handleDraftChange(field, value) {
    setDraft((prev) => ({ ...prev, [field]: value }));
    if (fieldErrors[field]) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next[field];
        return next;
      });
    }
    if (generalError) setGeneralError("");
  }

  async function handleSave(e) {
    e.preventDefault();
    setGeneralError("");

    // 1. Kiểm tra tính hợp lệ dữ liệu phía frontend (đồng bộ TenantProfileRules)
    const validation = validateTenantProfile(draft);
    if (!validation.isValid) {
      setFieldErrors(validation.errors);
      setGeneralError("Vui lòng kiểm tra lại các trường thông tin đánh dấu đỏ.");
      return;
    }

    const cleanCitizenId = (draft.citizenId || "").trim();

    setIsSubmitting(true);

    try {
      // Chuẩn bị payload chuẩn theo Backend API Contract (CreateTenantProfileRequest / UpdateTenantProfileRequest)
      const payload = {
        fullName: draft.fullName.trim(),
        phoneNumber: draft.phoneNumber.trim(),
        email: draft.email?.trim() || null,
        citizenId: cleanCitizenId || null,
        dateOfBirth: draft.dateOfBirth || null,
        gender: draft.gender || null,
        permanentAddress: draft.permanentAddress?.trim() || null,
      };

      if (isEditing && selectedId) {
        const response = await updateLandlordTenant(selectedId, payload);
        const updatedRecord = response?.data || {
          ...selectedTenant,
          ...payload,
          id: selectedId,
        };
        setSelectedSnapshot(updatedRecord);
        setIsEditing(false);
        reloadList();
        reloadDetail();
        notify?.(
          response?.message || `Đã cập nhật hồ sơ người ở "${payload.fullName}".`,
        );
      } else {
        const response = await createLandlordTenant(payload);
        const createdRecord = response?.data;
        setShowAddForm(false);
        setDraft(emptyDraft);

        if (createdRecord?.id) {
          setSelectedId(createdRecord.id);
          setSelectedSnapshot(createdRecord);
        }

        if (page !== 1) {
          setPage(1);
        } else {
          reloadList();
        }

        const shortCode = createdRecord?.id
          ? ` (Mã: ${String(createdRecord.id).substring(0, 8)})`
          : "";
        notify?.(
          response?.message
            ? `${response.message}${shortCode}`
            : `Đã tạo hồ sơ người ở "${payload.fullName}"${shortCode}.`,
        );
      }
    } catch (err) {
      const mapped = mapTenantApiError(err);
      setFieldErrors(mapped.fieldErrors);
      setGeneralError(mapped.generalError);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <Heading
        eyebrow="NGƯỜI THUÊ & NHÂN KHẨU"
        title="Hồ sơ người ở"
        description="Lưu thông tin nhân khẩu trước khi lập hợp đồng, kể cả khi khách thuê chưa tạo tài khoản."
        action={
          <Button
            icon="plus"
            onClick={showAddForm ? () => setShowAddForm(false) : openCreateForm}
          >
            {showAddForm ? "Đóng biểu mẫu" : "Thêm hồ sơ người ở"}
          </Button>
        }
      />

      {/* FORM TẠO MỚI HỒ SƠ */}
      {showAddForm && (
        <section className="content-card inline-form" aria-label="Biểu mẫu tạo hồ sơ">
          <div className="detail-header">
            <div>
              <span className="eyebrow">HỒ SƠ MỚI · KHÔNG CẦN TÀI KHOẢN</span>
              <h2>Nhập thông tin người ở mới</h2>
              <p>Hồ sơ này có thể dùng lại khi lập hợp đồng thuê bất cứ lúc nào.</p>
            </div>
            <button
              className="icon-button"
              type="button"
              aria-label="Đóng biểu mẫu"
              onClick={() => setShowAddForm(false)}
            >
              <Icon name="close" />
            </button>
          </div>

          {generalError && (
            <p className="form-error" role="alert" style={{ marginBottom: "16px" }}>
              <Icon name="alert" size={16} /> {generalError}
            </p>
          )}

          <form onSubmit={handleSave} className="form-grid" noValidate>
            <Field label="Họ và tên" required>
              <input
                required
                maxLength={150}
                disabled={isSubmitting}
                placeholder="Ví dụ: Nguyễn Văn A"
                value={draft.fullName}
                onChange={(e) => handleDraftChange("fullName", e.target.value)}
                aria-invalid={Boolean(fieldErrors.fullName)}
              />
              {fieldErrors.fullName && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.fullName}
                </small>
              )}
            </Field>

            <Field label="Số điện thoại" required>
              <input
                required
                type="tel"
                maxLength={20}
                disabled={isSubmitting}
                placeholder="10 số, bắt đầu bằng 0 (vd: 0912345678)"
                value={draft.phoneNumber}
                onChange={(e) => handleDraftChange("phoneNumber", e.target.value)}
                aria-invalid={Boolean(fieldErrors.phoneNumber)}
              />
              {fieldErrors.phoneNumber && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.phoneNumber}
                </small>
              )}
            </Field>

            <Field label="Số CCCD / CMND">
              <input
                maxLength={12}
                disabled={isSubmitting}
                placeholder="Đúng 12 chữ số nếu có"
                value={draft.citizenId}
                onChange={(e) => handleDraftChange("citizenId", e.target.value)}
                aria-invalid={Boolean(fieldErrors.citizenId)}
              />
              {fieldErrors.citizenId && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.citizenId}
                </small>
              )}
            </Field>

            <Field label="Email">
              <input
                type="email"
                maxLength={150}
                disabled={isSubmitting}
                placeholder="vidu@email.com (tùy chọn)"
                value={draft.email}
                onChange={(e) => handleDraftChange("email", e.target.value)}
                aria-invalid={Boolean(fieldErrors.email)}
              />
              {fieldErrors.email && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.email}
                </small>
              )}
            </Field>

            <Field label="Ngày sinh">
              <input
                type="date"
                disabled={isSubmitting}
                value={draft.dateOfBirth}
                onChange={(e) => handleDraftChange("dateOfBirth", e.target.value)}
                aria-invalid={Boolean(fieldErrors.dateOfBirth)}
              />
              {fieldErrors.dateOfBirth && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.dateOfBirth}
                </small>
              )}
            </Field>

            <Field label="Giới tính">
              <select
                disabled={isSubmitting}
                value={draft.gender}
                onChange={(e) => handleDraftChange("gender", e.target.value)}
                aria-invalid={Boolean(fieldErrors.gender)}
              >
                {GENDER_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
              {fieldErrors.gender && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.gender}
                </small>
              )}
            </Field>

            <Field label="Địa chỉ thường trú" wide>
              <textarea
                rows="2"
                maxLength={300}
                disabled={isSubmitting}
                placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành phố..."
                value={draft.permanentAddress}
                onChange={(e) => handleDraftChange("permanentAddress", e.target.value)}
                aria-invalid={Boolean(fieldErrors.permanentAddress)}
              />
              {fieldErrors.permanentAddress && (
                <small className="field-error-text" style={{ color: "#dc2626", fontSize: "11px" }}>
                  {fieldErrors.permanentAddress}
                </small>
              )}
            </Field>

            <div className="notice field--wide">
              <Icon name="alert" size={17} />
              <span>
                <strong>Lưu ý:</strong> Việc lưu hồ sơ không tạo tài khoản hệ thống (User ID trống). Khi lập hợp đồng, hồ sơ này sẽ được chọn qua danh sách người ở của hợp đồng (ContractTenants).
              </span>
            </div>

            <div className="form-action field--wide" style={{ display: "flex", justifyContent: "flex-end", gap: "10px" }}>
              <Button
                variant="secondary"
                disabled={isSubmitting}
                onClick={() => setShowAddForm(false)}
              >
                Hủy bỏ
              </Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Đang lưu..." : "Lưu hồ sơ người ở"}
              </Button>
            </div>
          </form>
        </section>
      )}

      {/* DANH SÁCH & THANH TÌM KIẾM HỒ SƠ */}
      <section className="content-card">
        <div className="card-toolbar card-toolbar--wrap">
          <div className="search-box" style={{ width: "min(340px, 100%)" }}>
            <Icon name="search" size={18} />
            <input
              aria-label="Tìm theo họ tên, số điện thoại hoặc CCCD"
              placeholder="Tìm theo họ tên, SĐT hoặc CCCD..."
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
            />
            {keyword && (
              <button
                type="button"
                className="icon-button"
                aria-label="Xóa từ khóa tìm kiếm"
                onClick={() => setKeyword("")}
                style={{ width: "26px", height: "26px" }}
              >
                <Icon name="close" size={14} />
              </button>
            )}
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
            <span className="subtle-count">
              Tổng cộng: <strong>{totalItems}</strong> hồ sơ
            </span>
            <Button
              variant="secondary"
              disabled={isListLoading}
              onClick={reloadList}
              style={{ minHeight: "36px", padding: "6px 12px", fontSize: "12px" }}
            >
              {isListLoading ? "Đang tải..." : "Làm mới"}
            </Button>
          </div>
        </div>

        {listError ? (
          <div className="empty-state" role="alert">
            <span
              className="empty-icon"
              style={{ backgroundColor: "#fef2f2", color: "#dc2626" }}
            >
              <Icon name="alert" size={26} />
            </span>
            <strong>Không thể tải danh sách hồ sơ người ở</strong>
            <p>{listError.message || "Không kết nối được với máy chủ Backend API."}</p>
            <div style={{ marginTop: "14px" }}>
              <Button variant="secondary" onClick={reloadList}>
                Thử tải lại
              </Button>
            </div>
          </div>
        ) : isListLoading && pagedTenants.length === 0 ? (
          <div className="empty-state" aria-busy="true">
            <span className="empty-icon">
              <Icon name="clock" size={26} />
            </span>
            <strong>Đang tải danh sách hồ sơ người ở...</strong>
            <p>Vui lòng chờ trong giây lát khi hệ thống đồng bộ dữ liệu từ máy chủ.</p>
          </div>
        ) : pagedTenants.length > 0 ? (
          <>
            <div className="responsive-table">
              <table>
                <thead>
                  <tr>
                    <th>Họ và tên</th>
                    <th>Số điện thoại</th>
                    <th>CCCD / CMND</th>
                    <th>Giới tính</th>
                    <th>Tài khoản</th>
                    <th>
                      <span className="sr-only">Thao tác</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {pagedTenants.map((item) => (
                    <tr
                      key={item.id}
                      style={
                        selectedId === item.id
                          ? { backgroundColor: "#f0f6ff" }
                          : undefined
                      }
                    >
                      <td>
                        <strong>{item.fullName}</strong>
                        {item.email && <small className="table-sub">{item.email}</small>}
                      </td>
                      <td>
                        <strong>{item.phoneNumber}</strong>
                      </td>
                      <td>{item.citizenId || <span className="muted">Chưa cập nhật</span>}</td>
                      <td>
                        {GENDER_LABELS[item.gender] || <span className="muted">—</span>}
                      </td>
                      <td>
                        {item.userId ? (
                          <Badge kind="approved">Đã liên kết</Badge>
                        ) : (
                          <Badge kind="vacant">Chưa có tài khoản</Badge>
                        )}
                      </td>
                      <td className="align-right nowrap">
                        <button
                          className="table-link"
                          type="button"
                          onClick={() => openDetail(item)}
                          style={{ marginRight: "12px" }}
                        >
                          Chi tiết <Icon name="chevron" size={15} />
                        </button>
                        <button
                          className="text-link"
                          type="button"
                          onClick={() => openEditForm(item)}
                        >
                          Sửa
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* PHÂN TRANG SERVER-SIDE */}
            {totalPages > 1 && (
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  marginTop: "18px",
                  paddingTop: "14px",
                  borderTop: "1px solid #edf1f6",
                  fontSize: "12px",
                }}
              >
                <span className="muted">
                  Trang {currentPage} / {totalPages} ({totalItems} hồ sơ)
                </span>
                <div style={{ display: "flex", gap: "6px" }}>
                  <Button
                    variant="secondary"
                    disabled={currentPage <= 1 || isListLoading}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    style={{ minHeight: "34px", padding: "6px 12px", fontSize: "12px" }}
                  >
                    Trang trước
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={currentPage >= totalPages || isListLoading}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    style={{ minHeight: "34px", padding: "6px 12px", fontSize: "12px" }}
                  >
                    Trang sau
                  </Button>
                </div>
              </div>
            )}
          </>
        ) : (
          <Empty
            title="Không tìm thấy hồ sơ người ở"
            description={
              debouncedKeyword
                ? `Không có hồ sơ nào khớp với từ khóa "${debouncedKeyword}".`
                : "Chưa có hồ sơ người ở nào được tạo. Hãy nhấn Thêm hồ sơ người ở."
            }
          />
        )}
      </section>

      {/* CHI TIẾT & CHỈNH SỬA HỒ SƠ */}
      {selectedId && (
        <section
          className="content-card detail-panel"
          ref={detailRef}
          aria-label={`Chi tiết hồ sơ ${selectedTenant?.fullName || ""}`}
        >
          {detailError && !selectedTenant ? (
            <div className="empty-state" role="alert">
              <span
                className="empty-icon"
                style={{ backgroundColor: "#fef2f2", color: "#dc2626" }}
              >
                <Icon name="alert" size={26} />
              </span>
              <strong>Không thể tải thông tin chi tiết hồ sơ</strong>
              <p>{detailError.message}</p>
              <div style={{ display: "flex", gap: "8px", marginTop: "12px" }}>
                <Button variant="secondary" onClick={reloadDetail}>
                  Thử lại
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => {
                    setSelectedId(null);
                    setSelectedSnapshot(null);
                    setIsEditing(false);
                  }}
                >
                  Đóng
                </Button>
              </div>
            </div>
          ) : !selectedTenant && isDetailLoading ? (
            <div className="empty-state" aria-busy="true">
              <span className="empty-icon">
                <Icon name="clock" size={26} />
              </span>
              <strong>Đang tải thông tin chi tiết...</strong>
            </div>
          ) : selectedTenant ? (
            <>
              <div className="detail-header">
                <div>
                  <span className="eyebrow">
                    MÃ HỒ SƠ: {String(selectedTenant.id).substring(0, 8)}...
                    {isDetailLoading ? " · ĐANG ĐỒNG BỘ..." : ""}
                  </span>
                  <h2>{selectedTenant.fullName}</h2>
                  <p>
                    {selectedTenant.userId ? (
                      <span style={{ color: "#16804a", fontWeight: "600" }}>
                        ✓ Tài khoản hệ thống đã liên kết ({selectedTenant.userId})
                      </span>
                    ) : (
                      <span style={{ color: "#2563eb", fontWeight: "600" }}>
                        ℹ Hồ sơ lưu độc lập (Chưa có tài khoản đăng nhập)
                      </span>
                    )}
                  </p>
                </div>
                <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                  {!isEditing && (
                    <Button variant="secondary" onClick={() => openEditForm(selectedTenant)}>
                      Sửa hồ sơ
                    </Button>
                  )}
                  <button
                    className="icon-button"
                    type="button"
                    aria-label="Đóng chi tiết hồ sơ"
                    onClick={() => {
                      setSelectedId(null);
                      setSelectedSnapshot(null);
                      setIsEditing(false);
                    }}
                  >
                    <Icon name="close" />
                  </button>
                </div>
              </div>

              {isEditing ? (
                /* BIỂU MẪU CHỈNH SỬA */
                <form onSubmit={handleSave} className="form-grid" noValidate>
                  {generalError && (
                    <p className="form-error field--wide" role="alert">
                      <Icon name="alert" size={16} /> {generalError}
                    </p>
                  )}

                  <Field label="Họ và tên" required>
                    <input
                      required
                      maxLength={150}
                      disabled={isSubmitting}
                      value={draft.fullName}
                      onChange={(e) => handleDraftChange("fullName", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.fullName)}
                    />
                    {fieldErrors.fullName && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.fullName}
                      </small>
                    )}
                  </Field>

                  <Field label="Số điện thoại" required>
                    <input
                      required
                      type="tel"
                      maxLength={20}
                      disabled={isSubmitting}
                      value={draft.phoneNumber}
                      onChange={(e) => handleDraftChange("phoneNumber", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.phoneNumber)}
                    />
                    {fieldErrors.phoneNumber && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.phoneNumber}
                      </small>
                    )}
                  </Field>

                  <Field label="Số CCCD / CMND">
                    <input
                      maxLength={12}
                      disabled={isSubmitting}
                      value={draft.citizenId}
                      onChange={(e) => handleDraftChange("citizenId", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.citizenId)}
                    />
                    {fieldErrors.citizenId && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.citizenId}
                      </small>
                    )}
                  </Field>

                  <Field label="Email">
                    <input
                      type="email"
                      maxLength={150}
                      disabled={isSubmitting}
                      value={draft.email}
                      onChange={(e) => handleDraftChange("email", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.email)}
                    />
                    {fieldErrors.email && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.email}
                      </small>
                    )}
                  </Field>

                  <Field label="Ngày sinh">
                    <input
                      type="date"
                      disabled={isSubmitting}
                      value={draft.dateOfBirth}
                      onChange={(e) => handleDraftChange("dateOfBirth", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.dateOfBirth)}
                    />
                    {fieldErrors.dateOfBirth && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.dateOfBirth}
                      </small>
                    )}
                  </Field>

                  <Field label="Giới tính">
                    <select
                      disabled={isSubmitting}
                      value={draft.gender}
                      onChange={(e) => handleDraftChange("gender", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.gender)}
                    >
                      {GENDER_OPTIONS.map((opt) => (
                        <option key={opt.value} value={opt.value}>
                          {opt.label}
                        </option>
                      ))}
                    </select>
                    {fieldErrors.gender && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.gender}
                      </small>
                    )}
                  </Field>

                  <Field label="Địa chỉ thường trú" wide>
                    <textarea
                      rows="2"
                      maxLength={300}
                      disabled={isSubmitting}
                      value={draft.permanentAddress}
                      onChange={(e) => handleDraftChange("permanentAddress", e.target.value)}
                      aria-invalid={Boolean(fieldErrors.permanentAddress)}
                    />
                    {fieldErrors.permanentAddress && (
                      <small style={{ color: "#dc2626", fontSize: "11px" }}>
                        {fieldErrors.permanentAddress}
                      </small>
                    )}
                  </Field>

                  <div className="notice field--wide">
                    <Icon name="alert" size={16} />
                    <span>
                      Lưu ý nghiệp vụ: Việc sửa hồ sơ người ở không tự ý làm thay đổi các chứng từ hợp đồng đã chốt trước đó.
                    </span>
                  </div>

                  <div
                    className="detail-actions field--wide"
                    style={{ display: "flex", justifyContent: "flex-end", gap: "9px" }}
                  >
                    <Button
                      variant="secondary"
                      disabled={isSubmitting}
                      onClick={() => {
                        setIsEditing(false);
                        setFieldErrors({});
                        setGeneralError("");
                      }}
                    >
                      Hủy chỉnh sửa
                    </Button>
                    <Button type="submit" disabled={isSubmitting}>
                      {isSubmitting ? "Đang lưu..." : "Lưu thay đổi"}
                    </Button>
                  </div>
                </form>
              ) : (
                /* XEM CHI TIẾT THÔNG TIN */
                <>
                  <div className="detail-grid">
                    <Info label="Họ và tên" value={selectedTenant.fullName} />
                    <Info label="Số điện thoại" value={selectedTenant.phoneNumber} />
                    <Info label="Số CCCD" value={selectedTenant.citizenId || "—"} />
                    <Info label="Email" value={selectedTenant.email || "—"} />
                    <Info
                      label="Giới tính"
                      value={GENDER_LABELS[selectedTenant.gender] || "Chưa xác định"}
                    />
                    <Info
                      label="Ngày sinh"
                      value={
                        selectedTenant.dateOfBirth
                          ? new Date(selectedTenant.dateOfBirth).toLocaleDateString("vi-VN")
                          : "—"
                      }
                    />
                    <Info
                      label="Địa chỉ thường trú"
                      value={selectedTenant.permanentAddress || "—"}
                      wide
                    />
                    <Info
                      label="Tình trạng tài khoản"
                      value={
                        selectedTenant.userId
                          ? `Đã liên kết (User: ${selectedTenant.userId})`
                          : "Chưa có tài khoản (Không yêu cầu đăng nhập)"
                      }
                    />
                    <Info
                      label="Ngày khởi tạo hồ sơ"
                      value={
                        selectedTenant.createdAt
                          ? new Date(selectedTenant.createdAt).toLocaleString("vi-VN")
                          : "—"
                      }
                    />
                    {selectedTenant.updatedAt && (
                      <Info
                        label="Cập nhật gần nhất"
                        value={new Date(selectedTenant.updatedAt).toLocaleString("vi-VN")}
                      />
                    )}
                  </div>

                  {/* DÙNG LẠI KHI LẬP HỢP ĐỒNG (ContractTenants Relationship) */}
                  <div style={{ marginTop: "24px", paddingTop: "20px", borderTop: "1px solid #eef1f6" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                      <h3 style={{ fontSize: "14px", margin: 0, color: "#1f2d43", fontWeight: 750 }}>
                        Hợp đồng đã liên kết ({associatedContracts.length})
                      </h3>
                      {navigate && (
                        <button
                          className="text-link"
                          type="button"
                          onClick={() => navigate("landlord/contracts")}
                        >
                          Đi tới danh sách hợp đồng <Icon name="arrow" size={15} />
                        </button>
                      )}
                    </div>

                    {associatedContracts.length > 0 ? (
                      <div className="responsive-table">
                        <table>
                          <thead>
                            <tr>
                              <th>Mã hợp đồng</th>
                              <th>Phòng</th>
                              <th>Thời hạn</th>
                              <th>Trạng thái</th>
                            </tr>
                          </thead>
                          <tbody>
                            {associatedContracts.map((c) => (
                              <tr key={c.id}>
                                <td><strong>{c.id}</strong></td>
                                <td>{c.room}</td>
                                <td>{c.start} – {c.end}</td>
                                <td>
                                  <Badge kind={c.status === "active" ? "approved" : "pending"}>
                                    {c.status === "active" ? "Hiệu lực" : "Sắp hết hạn"}
                                  </Badge>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    ) : (
                      <p style={{ fontSize: "12px", color: "#64748b", margin: 0 }}>
                        Hồ sơ này chưa được gán vào hợp đồng nào. Khi lập hợp đồng mới cho bất kỳ phòng nào, bạn có thể chọn hồ sơ này từ danh sách người ở.
                      </p>
                    )}
                  </div>
                </>
              )}
            </>
          ) : null}
        </section>
      )}
    </>
  );
}
