import { useEffect, useRef, useState } from "react";
import { Badge, Button, Empty, Field, Heading, Icon, Info } from "./ui";
import {
  GENDER_OPTIONS,
  GENDER_LABELS,
  TENANT_GENDERS,
} from "./constants/tenants";
import { validateTenantProfile } from "./utils/tenantValidators";
// Khi kết nối Backend API thực tế, import:
// import { createLandlordTenant, updateLandlordTenant, getLandlordTenants } from "./services/api";

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
  tenants,
  setTenants,
  notify,
  contracts = [],
  navigate,
}) {
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState(null);
  const [isEditing, setIsEditing] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const [fieldErrors, setFieldErrors] = useState({});
  const [generalError, setGeneralError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const detailRef = useRef(null);

  // Lọc tìm kiếm theo họ tên hoặc số điện thoại (client-side khi dùng dữ liệu mẫu)
  const normalizedKeyword = keyword.trim().toLowerCase();
  const filteredTenants = tenants.filter((item) => {
    if (!normalizedKeyword) return true;
    return (
      item.fullName.toLowerCase().includes(normalizedKeyword) ||
      item.phoneNumber.includes(normalizedKeyword) ||
      (item.citizenId && item.citizenId.includes(normalizedKeyword))
    );
  });

  // Phân trang
  const totalItems = filteredTenants.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const pagedTenants = filteredTenants.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE,
  );

  const selectedTenant = tenants.find((item) => item.id === selectedId) || null;

  // Lấy các hợp đồng liên quan đến hồ sơ người thuê (thông qua ContractTenants logic)
  const associatedContracts = selectedTenant
    ? contracts.filter(
        (c) =>
          c.tenant?.toLowerCase() === selectedTenant.fullName.toLowerCase() ||
          c.tenantId === selectedTenant.id,
      )
    : [];

  useEffect(() => {
    setPage(1);
  }, [keyword]);

  function openCreateForm() {
    setSelectedId(null);
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
    setDraft({
      fullName: tenant.fullName || "",
      phoneNumber: tenant.phoneNumber || "",
      email: tenant.email || "",
      citizenId: tenant.citizenId || "",
      dateOfBirth: tenant.dateOfBirth ? tenant.dateOfBirth.split("T")[0] : "",
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

    // 2. Kiểm tra trùng CCCD trong danh sách hiện tại (mô phỏng Conflict 409)
    const cleanCitizenId = (draft.citizenId || "").trim();
    if (cleanCitizenId) {
      const duplicate = tenants.find(
        (t) =>
          t.citizenId === cleanCitizenId &&
          (!isEditing || t.id !== selectedId),
      );
      if (duplicate) {
        setFieldErrors((prev) => ({
          ...prev,
          citizenId: `Số CCCD ${cleanCitizenId} đã tồn tại ở hồ sơ của ${duplicate.fullName}.`,
        }));
        setGeneralError("Số Căn cước công dân đã được sử dụng bởi một hồ sơ khác.");
        return;
      }
    }

    setIsSubmitting(true);

    try {
      // Chuẩn bị payload chuẩn theo Backend API Contract
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
        // Cập nhật hồ sơ trên mock data
        setTenants((current) =>
          current.map((t) =>
            t.id === selectedId
              ? {
                  ...t,
                  ...payload,
                  updatedAt: new Date().toISOString(),
                }
              : t,
          ),
        );
        notify(`Đã cập nhật hồ sơ người ở "${payload.fullName}".`);
        setIsEditing(false);
      } else {
        // Tạo mới hồ sơ trên mock data
        const newId = `tp-${Date.now().toString(36)}-${Math.random().toString(36).substring(2, 7)}`;
        const createdRecord = {
          ...payload,
          id: newId,
          userId: null, // Tiêu chí: Không tạo tài khoản, UserId = null
          createdByUserId: "U-1001",
          createdAt: new Date().toISOString(),
          updatedAt: null,
        };

        setTenants((current) => [createdRecord, ...current]);
        notify(`Đã lưu hồ sơ người ở "${payload.fullName}". Cấp mã: ${createdRecord.id.substring(0, 8)}`);
        setShowAddForm(false);
        setSelectedId(createdRecord.id);
      }
    } catch (err) {
      setGeneralError(err.message || "Đã có lỗi xảy ra khi lưu hồ sơ.");
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

          <form onSubmit={handleSave} className="form-grid">
            <Field label="Họ và tên" required>
              <input
                required
                maxLength={150}
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
                value={draft.gender}
                onChange={(e) => handleDraftChange("gender", e.target.value)}
              >
                {GENDER_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </Field>

            <Field label="Địa chỉ thường trú" wide>
              <textarea
                rows="2"
                maxLength={300}
                placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành phố..."
                value={draft.permanentAddress}
                onChange={(e) => handleDraftChange("permanentAddress", e.target.value)}
              />
            </Field>

            <div className="notice field--wide">
              <Icon name="alert" size={17} />
              <span>
                <strong>Lưu ý:</strong> Việc lưu hồ sơ không tạo tài khoản hệ thống (User ID trống). Khi lập hợp đồng, hồ sơ này sẽ được chọn qua danh sách người ở của hợp đồng (ContractTenants).
              </span>
            </div>

            <div className="form-action field--wide" style={{ display: "flex", justifyContent: "flex-end", gap: "10px" }}>
              <Button variant="secondary" onClick={() => setShowAddForm(false)}>
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
          </div>
          <div className="subtle-count">
            Tổng cộng: <strong>{totalItems}</strong> hồ sơ
          </div>
        </div>

        {pagedTenants.length > 0 ? (
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

            {/* PHÂN TRANG */}
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
                    disabled={currentPage <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    style={{ minHeight: "34px", padding: "6px 12px", fontSize: "12px" }}
                  >
                    Trang trước
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={currentPage >= totalPages}
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
              keyword
                ? `Không có hồ sơ nào khớp với từ khóa "${keyword}".`
                : "Chưa có hồ sơ người ở nào được tạo. Hãy nhấn Thêm hồ sơ người ở."
            }
          />
        )}
      </section>

      {/* CHI TIẾT & CHỈNH SỬA HỒ SƠ */}
      {selectedTenant && (
        <section
          className="content-card detail-panel"
          ref={detailRef}
          aria-label={`Chi tiết hồ sơ ${selectedTenant.fullName}`}
        >
          <div className="detail-header">
            <div>
              <span className="eyebrow">
                MÃ HỒ SƠ: {selectedTenant.id.substring(0, 8)}...
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
                  setIsEditing(false);
                }}
              >
                <Icon name="close" />
              </button>
            </div>
          </div>

          {isEditing ? (
            /* BIỂU MẪU CHỈNH SỬA */
            <form onSubmit={handleSave} className="form-grid">
              {generalError && (
                <p className="form-error field--wide" role="alert">
                  <Icon name="alert" size={16} /> {generalError}
                </p>
              )}

              <Field label="Họ và tên" required>
                <input
                  required
                  maxLength={150}
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
                  value={draft.dateOfBirth}
                  onChange={(e) => handleDraftChange("dateOfBirth", e.target.value)}
                />
              </Field>

              <Field label="Giới tính">
                <select
                  value={draft.gender}
                  onChange={(e) => handleDraftChange("gender", e.target.value)}
                >
                  {GENDER_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </Field>

              <Field label="Địa chỉ thường trú" wide>
                <textarea
                  rows="2"
                  maxLength={300}
                  value={draft.permanentAddress}
                  onChange={(e) => handleDraftChange("permanentAddress", e.target.value)}
                />
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
                <Button variant="secondary" onClick={() => setIsEditing(false)}>
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
        </section>
      )}
    </>
  );
}
