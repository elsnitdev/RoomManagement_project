import { useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  Building2,
  Check,
  Clock,
  Edit3,
  Lock,
  Mail,
  Phone,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Trash2,
  Unlock,
  UserCheck,
  UserPlus,
  Users,
  X,
} from "lucide-react";
import { useApiQuery } from "../hooks/useApiQuery";
import {
  assignManagerProperties,
  createManager,
  deleteManager,
  listManagers,
  listProperties,
  lockManager,
  revokeManagerProperty,
  unlockManager,
  updateManager,
} from "../services/api";
import { Button, Empty, Heading } from "../ui";
import "../styles/admin-users.css";

const emptyDraft = {
  userName: "",
  password: "",
  fullName: "",
  email: "",
  phoneNumber: "",
  propertyIds: [],
};

const emptyEditForm = {
  fullName: "",
  email: "",
  phoneNumber: "",
};

function getInitials(name = "") {
  return (
    name
      .trim()
      .split(/\s+/)
      .slice(-2)
      .map((word) => word[0] || "")
      .join("")
      .toUpperCase() || "QL"
  );
}

export default function ManagerManagement({ notify }) {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);

  const [showCreate, setShowCreate] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const [createError, setCreateError] = useState("");

  const [editingManager, setEditingManager] = useState(null);
  const [editForm, setEditForm] = useState(emptyEditForm);
  const [editError, setEditError] = useState("");

  const [assigningFor, setAssigningFor] = useState(null);
  const [selectedPropertyIds, setSelectedPropertyIds] = useState([]);
  const [assignError, setAssignError] = useState("");

  const [busy, setBusy] = useState(false);

  const isActiveParam =
    statusFilter === "active"
      ? true
      : statusFilter === "locked"
        ? false
        : undefined;

  const {
    data: managersRes,
    error: managersErr,
    isLoading: managersLoading,
    reload: reloadManagers,
  } = useApiQuery(
    `landlord:managers:${statusFilter}:${search.trim()}:${page}`,
    () =>
      listManagers({
        page,
        pageSize: 50,
        search: search.trim(),
        isActive: isActiveParam,
      }),
  );

  const { data: propertiesRes } = useApiQuery(
    "landlord:properties:all-for-manager",
    () => listProperties({ page: 1, pageSize: 100 }),
  );

  const managers = Array.isArray(managersRes?.data) ? managersRes.data : [];
  const meta = managersRes?.meta;
  const properties = Array.isArray(propertiesRes?.data)
    ? propertiesRes.data
    : [];

  const stats = useMemo(() => {
    const active = managers.filter((m) => m.isActive).length;
    const locked = managers.filter((m) => !m.isActive).length;
    return {
      total: meta?.totalItems ?? managers.length,
      active,
      locked,
    };
  }, [managers, meta]);

  const anyModalOpen = Boolean(showCreate || editingManager || assigningFor);
  useEffect(() => {
    if (!anyModalOpen) return;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKeyDown = (event) => {
      if (event.key !== "Escape" || busy) return;
      if (editingManager) closeEditModal();
      else if (assigningFor) closeAssignModal();
      else if (showCreate) closeCreateModal();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = prevOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [anyModalOpen, busy, editingManager, assigningFor, showCreate]);

  function openCreateModal() {
    setDraft(emptyDraft);
    setCreateError("");
    setShowCreate(true);
  }

  function closeCreateModal() {
    setShowCreate(false);
    setCreateError("");
  }

  function openEditModal(manager) {
    setEditingManager(manager);
    setEditForm({
      fullName: manager.fullName || "",
      email: manager.email || "",
      phoneNumber: manager.phoneNumber || "",
    });
    setEditError("");
  }

  function closeEditModal() {
    setEditingManager(null);
    setEditForm(emptyEditForm);
    setEditError("");
  }

  function openAssignModal(manager) {
    setAssigningFor(manager);
    setSelectedPropertyIds([]);
    setAssignError("");
  }

  function closeAssignModal() {
    setAssigningFor(null);
    setSelectedPropertyIds([]);
    setAssignError("");
  }

  function toggleDraftProperty(id) {
    setDraft((current) => ({
      ...current,
      propertyIds: current.propertyIds.includes(id)
        ? current.propertyIds.filter((item) => item !== id)
        : [...current.propertyIds, id],
    }));
  }

  async function handleCreate(event) {
    event.preventDefault();
    setCreateError("");
    setBusy(true);
    try {
      await createManager({
        userName: draft.userName.trim(),
        password: draft.password,
        fullName: draft.fullName.trim() || null,
        email: draft.email.trim() || null,
        phoneNumber: draft.phoneNumber.trim() || null,
        propertyIds: draft.propertyIds,
      });
      closeCreateModal();
      reloadManagers();
      notify?.(
        "Đã tạo tài khoản Quản lý khu trọ. Hãy bàn giao mật khẩu tạm cho nhân sự.",
      );
    } catch (err) {
      setCreateError(err.message || "Không thể tạo tài khoản quản lý.");
    } finally {
      setBusy(false);
    }
  }

  async function handleSaveEdit(event) {
    event.preventDefault();
    if (!editingManager) return;
    setEditError("");
    setBusy(true);
    try {
      await updateManager(editingManager.id, {
        fullName: editForm.fullName.trim() || null,
        email: editForm.email.trim() || null,
        phoneNumber: editForm.phoneNumber.trim() || null,
      });
      const label = editingManager.userName;
      closeEditModal();
      reloadManagers();
      notify?.(`Đã cập nhật thông tin tài khoản ${label}.`);
    } catch (err) {
      setEditError(
        err.message || "Không thể cập nhật thông tin tài khoản quản lý.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function handleToggleLock(manager) {
    setBusy(true);
    try {
      if (manager.isActive) {
        await lockManager(manager.id);
        notify?.(`Đã khóa tài khoản ${manager.userName}.`);
      } else {
        await unlockManager(manager.id);
        notify?.(`Đã mở khóa tài khoản ${manager.userName}.`);
      }
      reloadManagers();
    } catch (err) {
      notify?.(err.message || "Không thể cập nhật trạng thái tài khoản.");
    } finally {
      setBusy(false);
    }
  }

  async function handleRevokeProperty(managerId, propertyId, propertyName) {
    setBusy(true);
    try {
      await revokeManagerProperty(managerId, propertyId);
      reloadManagers();
      notify?.(`Đã thu hồi quyền quản lý khu trọ "${propertyName}".`);
    } catch (err) {
      notify?.(err.message || "Không thể thu hồi khu trọ.");
    } finally {
      setBusy(false);
    }
  }

  async function handleAssignProperties(event) {
    event.preventDefault();
    if (!assigningFor || !selectedPropertyIds.length) return;
    setAssignError("");
    setBusy(true);
    try {
      await assignManagerProperties(assigningFor.id, selectedPropertyIds);
      closeAssignModal();
      reloadManagers();
      notify?.("Đã giao thêm khu trọ cho quản lý.");
    } catch (err) {
      setAssignError(err.message || "Không thể phân công khu trọ.");
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete(manager) {
    if (
      !window.confirm(
        `Xóa/vô hiệu hóa tài khoản quản lý "${manager.fullName || manager.userName}"? Mọi lịch sử hợp đồng và chứng từ vẫn được giữ nguyên.`,
      )
    ) {
      return;
    }
    setBusy(true);
    try {
      await deleteManager(manager.id);
      reloadManagers();
      notify?.(`Đã xóa tài khoản ${manager.userName} khỏi danh sách quản lý.`);
    } catch (err) {
      notify?.(err.message || "Không thể xóa tài khoản quản lý.");
    } finally {
      setBusy(false);
    }
  }

  const unassignedProperties = assigningFor
    ? properties.filter(
        (p) =>
          !(assigningFor.assignedProperties || []).some(
            (ap) => ap.propertyId === p.id,
          ),
      )
    : [];

  return (
    <div className="admin-users-page">
      <Heading
        eyebrow="PHÂN QUYỀN KHU TRỌ"
        title="Quản lý nhân sự (Manager)"
        description="Tạo tài khoản Quản lý khu trọ, chỉnh sửa thông tin liên hệ, giao hoặc thu hồi phạm vi khu trọ phụ trách."
        action={
          <Button onClick={openCreateModal}>
            <UserPlus size={16} aria-hidden="true" />
            <span>Thêm Quản lý</span>
          </Button>
        }
      />

      {/* 1. KPI Summary Cards */}
      <section className="mgr-stats-grid" aria-label="Thống kê nhân sự quản lý">
        <div
          role="button"
          tabIndex={0}
          className={`admin-stat-card ${statusFilter === "all" ? "is-active" : ""}`}
          onClick={() => {
            setStatusFilter("all");
            setPage(1);
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              setStatusFilter("all");
              setPage(1);
            }
          }}
        >
          <div className="admin-stat-card__top">
            <span className="admin-stat-card__label">Tổng nhân sự</span>
            <div className="admin-stat-icon admin-stat-icon--total">
              <Users size={19} />
            </div>
          </div>
          <div className="admin-stat-card__value">{stats.total}</div>
          <div className="admin-stat-card__footer">
            Tài khoản quản lý thuộc quyền bạn
          </div>
        </div>

        <div
          role="button"
          tabIndex={0}
          className={`admin-stat-card ${statusFilter === "active" ? "is-active" : ""}`}
          onClick={() => {
            setStatusFilter(statusFilter === "active" ? "all" : "active");
            setPage(1);
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              setStatusFilter(statusFilter === "active" ? "all" : "active");
              setPage(1);
            }
          }}
        >
          <div className="admin-stat-card__top">
            <span className="admin-stat-card__label">Đang hoạt động</span>
            <div className="admin-stat-icon admin-stat-icon--landlord">
              <UserCheck size={19} />
            </div>
          </div>
          <div className="admin-stat-card__value">{stats.active}</div>
          <div className="admin-stat-card__footer">
            Được phép đăng nhập &amp; thao tác
          </div>
        </div>

        <div
          role="button"
          tabIndex={0}
          className={`admin-stat-card ${statusFilter === "locked" ? "is-active" : ""}`}
          onClick={() => {
            setStatusFilter(statusFilter === "locked" ? "all" : "locked");
            setPage(1);
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              setStatusFilter(statusFilter === "locked" ? "all" : "locked");
              setPage(1);
            }
          }}
        >
          <div className="admin-stat-card__top">
            <span className="admin-stat-card__label">Đã khóa</span>
            <div className="admin-stat-icon admin-stat-icon--manager">
              <Lock size={19} />
            </div>
          </div>
          <div className="admin-stat-card__value">{stats.locked}</div>
          <div className="admin-stat-card__footer">
            Tạm ngừng quyền truy cập hệ thống
          </div>
        </div>
      </section>

      {/* 2. Toolbar */}
      <section className="admin-toolbar-card">
        <div className="admin-toolbar">
          <div
            className="admin-role-pills"
            role="tablist"
            aria-label="Lọc trạng thái quản lý"
          >
            {[
              { val: "all", label: "Tất cả" },
              { val: "active", label: "Đang hoạt động" },
              { val: "locked", label: "Đã khóa" },
            ].map(({ val, label }) => (
              <button
                key={val}
                type="button"
                role="tab"
                aria-selected={statusFilter === val}
                className={`admin-role-pill ${statusFilter === val ? "is-active" : ""}`}
                onClick={() => {
                  setStatusFilter(val);
                  setPage(1);
                }}
              >
                <span>{label}</span>
              </button>
            ))}
          </div>

          <div className="admin-toolbar__controls">
            <div className="admin-search-wrap">
              <Search className="admin-search-icon" size={16} />
              <input
                className="admin-search-input"
                aria-label="Tìm quản lý"
                placeholder="Tìm tên, username, email hoặc SĐT..."
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
              {search && (
                <button
                  type="button"
                  className="admin-search-clear"
                  aria-label="Xóa từ khóa tìm kiếm"
                  onClick={() => {
                    setSearch("");
                    setPage(1);
                  }}
                >
                  <X size={13} />
                </button>
              )}
            </div>

            <button
              type="button"
              className="admin-btn-action-refresh"
              title="Làm mới danh sách"
              aria-label="Làm mới danh sách quản lý"
              onClick={reloadManagers}
              disabled={managersLoading}
            >
              <RefreshCw
                size={16}
                className={managersLoading ? "spin" : ""}
              />
            </button>
          </div>
        </div>
      </section>

      {/* 3. Manager Table */}
      <section className="admin-table-card">
        {managersLoading ? (
          <div style={{ padding: "48px 24px", textAlign: "center" }}>
            <p className="muted-copy" role="status">
              Đang tải danh sách quản lý...
            </p>
          </div>
        ) : managersErr ? (
          <div style={{ padding: "24px" }}>
            <p className="form-error" role="alert">
              {managersErr.message || "Không thể tải danh sách quản lý."}
            </p>
          </div>
        ) : managers.length ? (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Nhân sự</th>
                  <th>Liên hệ</th>
                  <th>Trạng thái</th>
                  <th>Khu trọ được giao</th>
                  <th style={{ textAlign: "right" }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {managers.map((m) => (
                  <tr key={m.id}>
                    <td>
                      <div className="admin-user-cell">
                        <span className="admin-avatar">
                          {getInitials(m.fullName || m.userName)}
                        </span>
                        <div className="admin-user-info">
                          <span className="admin-user-name">
                            {m.fullName || m.userName}
                          </span>
                          <span className="admin-user-handle">
                            @{m.userName}
                          </span>
                        </div>
                      </div>
                    </td>

                    <td>
                      <div className="admin-contact-cell">
                        {m.email ? (
                          <a
                            href={`mailto:${m.email}`}
                            className="admin-contact-link"
                          >
                            <Mail size={12} />
                            <span>{m.email}</span>
                          </a>
                        ) : null}
                        {m.phoneNumber ? (
                          <a
                            href={`tel:${m.phoneNumber}`}
                            className="admin-contact-link"
                          >
                            <Phone size={12} />
                            <span>{m.phoneNumber}</span>
                          </a>
                        ) : null}
                        {!m.email && !m.phoneNumber && (
                          <span className="muted-copy" style={{ fontSize: 12 }}>
                            Chưa có thông tin
                          </span>
                        )}
                      </div>
                    </td>

                    <td>
                      <div
                        style={{
                          display: "flex",
                          flexDirection: "column",
                          gap: 5,
                          alignItems: "flex-start",
                        }}
                      >
                        {m.isActive ? (
                          <span className="admin-badge admin-badge--active">
                            <Check size={12} /> Hoạt động
                          </span>
                        ) : (
                          <span className="admin-badge admin-badge--locked">
                            <Lock size={12} /> Đã khóa
                          </span>
                        )}
                        {m.mustChangePassword && (
                          <span className="admin-badge admin-badge--pending">
                            <Clock size={12} /> Chờ đổi MK tạm
                          </span>
                        )}
                      </div>
                    </td>

                    <td>
                      <div className="manager-assigned-list">
                        {(m.assignedProperties || []).length ? (
                          m.assignedProperties.map((ap) => (
                            <span
                              key={ap.propertyId}
                              className="manager-assigned-chip"
                            >
                              <ShieldCheck size={13} aria-hidden="true" />
                              <span>{ap.propertyName}</span>
                              <button
                                type="button"
                                title="Thu hồi khu trọ này"
                                aria-label={`Thu hồi khu trọ ${ap.propertyName}`}
                                onClick={() =>
                                  handleRevokeProperty(
                                    m.id,
                                    ap.propertyId,
                                    ap.propertyName,
                                  )
                                }
                                disabled={busy}
                              >
                                <X size={13} />
                              </button>
                            </span>
                          ))
                        ) : (
                          <span className="muted-copy" style={{ fontSize: 12 }}>
                            Chưa được giao khu trọ
                          </span>
                        )}
                        <button
                          type="button"
                          className="admin-btn admin-btn--edit"
                          style={{ minHeight: 28, padding: "4px 9px", fontSize: 11.5 }}
                          onClick={() => openAssignModal(m)}
                          disabled={busy}
                        >
                          <Plus size={12} />
                          <span>Giao khu trọ</span>
                        </button>
                      </div>
                    </td>

                    <td>
                      <div className="admin-actions-group">
                        <button
                          type="button"
                          className="admin-btn admin-btn--edit"
                          title="Chỉnh sửa thông tin tài khoản"
                          onClick={() => openEditModal(m)}
                          disabled={busy}
                        >
                          <Edit3 size={13} />
                          <span>Sửa</span>
                        </button>
                        <button
                          type="button"
                          className={`admin-btn ${
                            m.isActive ? "admin-btn--lock" : "admin-btn--unlock"
                          }`}
                          title={
                            m.isActive ? "Khóa tài khoản" : "Mở khóa tài khoản"
                          }
                          onClick={() => handleToggleLock(m)}
                          disabled={busy}
                        >
                          {m.isActive ? (
                            <Lock size={13} />
                          ) : (
                            <Unlock size={13} />
                          )}
                          <span>{m.isActive ? "Khóa" : "Mở"}</span>
                        </button>
                        <button
                          type="button"
                          className="admin-btn admin-btn--delete"
                          title="Xóa / vô hiệu hóa tài khoản"
                          aria-label={`Xóa tài khoản ${m.userName}`}
                          onClick={() => handleDelete(m)}
                          disabled={busy}
                        >
                          <Trash2 size={13} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div style={{ padding: "48px 20px" }}>
            <Empty
              icon="users"
              title="Chưa có nhân sự quản lý phù hợp"
              description={
                search || statusFilter !== "all"
                  ? "Thử xóa bộ lọc hoặc tìm kiếm với từ khóa khác."
                  : "Nhấn 'Thêm Quản lý' để tạo tài khoản nhân sự và giao khu trọ phụ trách."
              }
            />
          </div>
        )}

        {meta && meta.totalPages > 1 && (
          <div className="admin-pagination">
            <span>
              Trang {page} / {meta.totalPages} · {meta.totalItems} nhân sự
            </span>
            <div className="admin-pagination__controls">
              <button
                type="button"
                className="admin-pagination__btn"
                disabled={managersLoading || page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Trang trước
              </button>
              <button
                type="button"
                className="admin-pagination__btn"
                disabled={managersLoading || page >= meta.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Trang sau
              </button>
            </div>
          </div>
        )}
      </section>

      {/* Modal 1: Create Manager */}
      {showCreate && (
        <div
          className="admin-modal-backdrop"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget && !busy) closeCreateModal();
          }}
        >
          <div
            className="admin-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="mgr-create-title"
          >
            <header className="admin-modal__header">
              <div>
                <h2 id="mgr-create-title">Tạo tài khoản Quản lý mới</h2>
                <p>
                  Mật khẩu tạm dùng để bàn giao lần đầu; nhân sự bắt buộc đổi
                  mật khẩu khi đăng nhập.
                </p>
              </div>
              <button
                type="button"
                className="admin-modal__close"
                aria-label="Đóng cửa sổ"
                disabled={busy}
                onClick={closeCreateModal}
              >
                <X size={18} />
              </button>
            </header>

            <form onSubmit={handleCreate}>
              <div className="admin-modal__body">
                <div className="admin-form-grid">
                  <div className="admin-field">
                    <label htmlFor="mgr-create-username">
                      Tên đăng nhập *
                    </label>
                    <input
                      id="mgr-create-username"
                      required
                      minLength={3}
                      value={draft.userName}
                      onChange={(e) =>
                        setDraft({ ...draft, userName: e.target.value })
                      }
                      placeholder="Ví dụ: quanly_khu_a"
                    />
                  </div>
                  <div className="admin-field">
                    <label htmlFor="mgr-create-password">Mật khẩu tạm *</label>
                    <input
                      id="mgr-create-password"
                      required
                      type="password"
                      minLength={6}
                      value={draft.password}
                      onChange={(e) =>
                        setDraft({ ...draft, password: e.target.value })
                      }
                      placeholder="Ít nhất 6 ký tự"
                    />
                  </div>
                  <div className="admin-field">
                    <label htmlFor="mgr-create-fullname">Họ và tên</label>
                    <input
                      id="mgr-create-fullname"
                      maxLength={150}
                      value={draft.fullName}
                      onChange={(e) =>
                        setDraft({ ...draft, fullName: e.target.value })
                      }
                      placeholder="Trần Văn Quản Lý"
                    />
                  </div>
                  <div className="admin-field">
                    <label htmlFor="mgr-create-phone">Số điện thoại</label>
                    <input
                      id="mgr-create-phone"
                      inputMode="tel"
                      maxLength={20}
                      value={draft.phoneNumber}
                      onChange={(e) =>
                        setDraft({ ...draft, phoneNumber: e.target.value })
                      }
                      placeholder="09xxxxxxxx"
                    />
                  </div>
                  <div className="admin-field admin-field--wide">
                    <label htmlFor="mgr-create-email">Email</label>
                    <input
                      id="mgr-create-email"
                      type="email"
                      maxLength={256}
                      value={draft.email}
                      onChange={(e) =>
                        setDraft({ ...draft, email: e.target.value })
                      }
                      placeholder="quanly@email.com (không bắt buộc)"
                    />
                  </div>
                  <div className="admin-field admin-field--wide">
                    <label>Giao khu trọ phụ trách ban đầu</label>
                    {properties.length ? (
                      <div className="manager-property-checks">
                        {properties.map((prop) => (
                          <label key={prop.id} className="manager-check-pill">
                            <input
                              type="checkbox"
                              checked={draft.propertyIds.includes(prop.id)}
                              onChange={() => toggleDraftProperty(prop.id)}
                            />
                            <span>{prop.name}</span>
                          </label>
                        ))}
                      </div>
                    ) : (
                      <span className="muted-copy" style={{ fontSize: 12 }}>
                        Bạn chưa có khu trọ nào. Có thể tạo tài khoản trước và
                        giao khu trọ sau.
                      </span>
                    )}
                  </div>
                </div>

                {createError && (
                  <div className="admin-modal-error" role="alert">
                    <AlertCircle size={16} />
                    <span>{createError}</span>
                  </div>
                )}
              </div>

              <footer className="admin-modal__footer">
                <button
                  type="button"
                  className="admin-btn admin-btn--edit"
                  disabled={busy}
                  onClick={closeCreateModal}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="button button--primary"
                  disabled={busy}
                >
                  {busy ? "Đang tạo..." : "Tạo tài khoản"}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}

      {/* Modal 2: Edit Manager Info */}
      {editingManager && (
        <div
          className="admin-modal-backdrop"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget && !busy) closeEditModal();
          }}
        >
          <div
            className="admin-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="mgr-edit-title"
          >
            <header className="admin-modal__header">
              <div>
                <h2 id="mgr-edit-title">Chỉnh sửa thông tin tài khoản</h2>
                <p>
                  Cập nhật thông tin liên hệ cho nhân sự{" "}
                  <strong>@{editingManager.userName}</strong>.
                </p>
              </div>
              <button
                type="button"
                className="admin-modal__close"
                aria-label="Đóng cửa sổ"
                disabled={busy}
                onClick={closeEditModal}
              >
                <X size={18} />
              </button>
            </header>

            <form onSubmit={handleSaveEdit}>
              <div className="admin-modal__body">
                <div className="admin-form-grid">
                  <div className="admin-field">
                    <label htmlFor="mgr-edit-username">Tên đăng nhập</label>
                    <input
                      id="mgr-edit-username"
                      value={`@${editingManager.userName}`}
                      disabled
                    />
                  </div>
                  <div className="admin-field">
                    <label htmlFor="mgr-edit-fullname">Họ và tên</label>
                    <input
                      id="mgr-edit-fullname"
                      maxLength={150}
                      value={editForm.fullName}
                      onChange={(e) =>
                        setEditForm({ ...editForm, fullName: e.target.value })
                      }
                      placeholder="Nhập họ và tên nhân sự"
                    />
                  </div>
                  <div className="admin-field">
                    <label htmlFor="mgr-edit-phone">Số điện thoại</label>
                    <input
                      id="mgr-edit-phone"
                      inputMode="tel"
                      maxLength={20}
                      value={editForm.phoneNumber}
                      onChange={(e) =>
                        setEditForm({
                          ...editForm,
                          phoneNumber: e.target.value,
                        })
                      }
                      placeholder="09xxxxxxxx"
                    />
                  </div>
                  <div className="admin-field">
                    <label htmlFor="mgr-edit-email">Email liên hệ</label>
                    <input
                      id="mgr-edit-email"
                      type="email"
                      maxLength={256}
                      value={editForm.email}
                      onChange={(e) =>
                        setEditForm({ ...editForm, email: e.target.value })
                      }
                      placeholder="quanly@email.com"
                    />
                  </div>
                </div>

                {editError && (
                  <div className="admin-modal-error" role="alert">
                    <AlertCircle size={16} />
                    <span>{editError}</span>
                  </div>
                )}
              </div>

              <footer className="admin-modal__footer">
                <button
                  type="button"
                  className="admin-btn admin-btn--edit"
                  disabled={busy}
                  onClick={closeEditModal}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="button button--primary"
                  disabled={busy}
                >
                  {busy ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}

      {/* Modal 3: Assign Properties */}
      {assigningFor && (
        <div
          className="admin-modal-backdrop"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget && !busy) closeAssignModal();
          }}
        >
          <div
            className="admin-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="mgr-assign-title"
          >
            <header className="admin-modal__header">
              <div>
                <h2 id="mgr-assign-title">Giao thêm khu trọ phụ trách</h2>
                <p>
                  Chọn các khu trọ giao cho{" "}
                  <strong>
                    {assigningFor.fullName || assigningFor.userName}
                  </strong>
                  .
                </p>
              </div>
              <button
                type="button"
                className="admin-modal__close"
                aria-label="Đóng cửa sổ"
                disabled={busy}
                onClick={closeAssignModal}
              >
                <X size={18} />
              </button>
            </header>

            <form onSubmit={handleAssignProperties}>
              <div className="admin-modal__body">
                {unassignedProperties.length ? (
                  <div className="manager-property-checks">
                    {unassignedProperties.map((prop) => (
                      <label key={prop.id} className="manager-check-pill">
                        <input
                          type="checkbox"
                          checked={selectedPropertyIds.includes(prop.id)}
                          onChange={() =>
                            setSelectedPropertyIds((cur) =>
                              cur.includes(prop.id)
                                ? cur.filter((x) => x !== prop.id)
                                : [...cur, prop.id],
                            )
                          }
                        />
                        <Building2 size={14} aria-hidden="true" />
                        <span>{prop.name}</span>
                      </label>
                    ))}
                  </div>
                ) : (
                  <p className="muted-copy">
                    Nhân sự này đã được giao toàn bộ khu trọ hiện có của bạn.
                  </p>
                )}

                {assignError && (
                  <div className="admin-modal-error" role="alert">
                    <AlertCircle size={16} />
                    <span>{assignError}</span>
                  </div>
                )}
              </div>

              <footer className="admin-modal__footer">
                <button
                  type="button"
                  className="admin-btn admin-btn--edit"
                  disabled={busy}
                  onClick={closeAssignModal}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="button button--primary"
                  disabled={busy || !selectedPropertyIds.length}
                >
                  {busy ? "Đang lưu..." : "Lưu phân công"}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
