import { useMemo, useState } from "react";
import { todayLabel } from "./mockData";
import { Button, Empty, Heading, Icon, Stat } from "./ui";
import { useApiQuery } from "./hooks/useApiQuery";
import {
  createUser as createAdminUser,
  deleteUser as deleteAdminUser,
  listUsers as listAdminUsers,
  lockUser as lockAdminUser,
  unlockUser as unlockAdminUser,
  updateUser as updateAdminUser,
} from "./services/api/adminUserApi";
import {
  AlertCircle,
  Calendar,
  Check,
  CheckCircle2,
  Clock,
  Edit3,
  Home,
  Lock,
  Mail,
  Phone,
  Plus,
  RefreshCw,
  Search,
  Shield,
  Trash2,
  Unlock,
  UserCheck,
  UserPlus,
  Users,
  X,
} from "lucide-react";
import "./styles/admin-users.css";

const initials = (name) =>
  name
    .split(" ")
    .slice(-2)
    .map((word) => word[0])
    .join("");
function Person({ name, email }) {
  return (
    <div className="person-cell">
      <span className="person-avatar">{initials(name)}</span>
      <span>
        <strong>{name}</strong>
        <small>{email}</small>
      </span>
    </div>
  );
}

export default function Admin({
  page,
  reviewSummary,
  users,
  setUsers,
  navigate,
  notify,
}) {
  const [showCreate, setShowCreate] = useState(false);
  const [draft, setDraft] = useState({
    userName: "",
    email: "",
    password: "",
    role: "Landlord",
  });
  const [userRoleFilter, setUserRoleFilter] = useState("");
  const [userActiveFilter, setUserActiveFilter] = useState("");
  const [userVerifyFilter, setUserVerifyFilter] = useState("");
  const [userPage, setUserPage] = useState(1);
  const [userKeyword, setUserKeyword] = useState("");
  const [userBusy, setUserBusy] = useState(false);
  const [userFormError, setUserFormError] = useState("");
  const [editingUser, setEditingUser] = useState(null);
  const [editForm, setEditForm] = useState({
    fullName: "",
    phoneNumber: "",
    email: "",
    citizenId: "",
    dateOfBirth: "",
    address: "",
  });
  const [editError, setEditError] = useState("");
  const [editBusy, setEditBusy] = useState(false);

  const {
    data: adminUsersRes,
    error: adminUsersError,
    isLoading: adminUsersLoading,
    reload: reloadAdminUsers,
  } = useApiQuery(
    `admin:users:${userRoleFilter}:${userKeyword.trim()}:${userPage}`,
    () =>
      listAdminUsers({
        role: userRoleFilter,
        keyword: userKeyword.trim(),
        page: userPage,
        pageSize: 50,
      }),
    { enabled: page === "users" },
  );
  const apiUsers = Array.isArray(adminUsersRes?.data) ? adminUsersRes.data : [];
  const adminUsersMeta = adminUsersRes?.meta;
  const filteredUsers = useMemo(() => {
    return apiUsers.filter((u) => {
      if (userActiveFilter === "active" && !u.isActive) return false;
      if (userActiveFilter === "locked" && u.isActive) return false;
      if (userVerifyFilter) {
        if (u.role !== "Tenant") return false;
        const status = u.identityVerificationStatus || "Unverified";
        if (status !== userVerifyFilter) return false;
      }
      return true;
    });
  }, [apiUsers, userActiveFilter, userVerifyFilter]);
  const hasActiveFilters = Boolean(
    userRoleFilter || userActiveFilter || userVerifyFilter || userKeyword.trim()
  );
  function resetUserFilters() {
    setUserRoleFilter("");
    setUserActiveFilter("");
    setUserVerifyFilter("");
    setUserKeyword("");
    setUserPage(1);
  }
  async function addUser(event) {
    event.preventDefault();
    setUserFormError("");
    setUserBusy(true);
    try {
      await createAdminUser({
        userName: draft.userName.trim(),
        email: draft.email.trim(),
        password: draft.password,
        role: draft.role,
      });
      setDraft({ userName: "", email: "", password: "", role: "Landlord" });
      setShowCreate(false);
      reloadAdminUsers();
      notify("Đã tạo tài khoản người dùng mới thành công.");
    } catch (err) {
      setUserFormError(err.message || "Không thể tạo tài khoản.");
    } finally {
      setUserBusy(false);
    }
  }

  function openEditUser(u) {
    setEditingUser(u);
    setEditError("");
    setEditForm({
      fullName: u.fullName || "",
      phoneNumber: u.phoneNumber || "",
      email: u.email || "",
      citizenId: u.citizenId || "",
      dateOfBirth: u.dateOfBirth ? String(u.dateOfBirth).split("T")[0] : "",
      address: u.address || "",
    });
  }

  function closeEditUser() {
    setEditingUser(null);
    setEditError("");
    setEditBusy(false);
  }

  async function handleSaveEditUser(e) {
    e.preventDefault();
    if (!editingUser) return;
    setEditError("");
    setEditBusy(true);
    try {
      await updateAdminUser(editingUser.id, {
        fullName: editForm.fullName.trim(),
        phoneNumber: editForm.phoneNumber.trim() || null,
        email: editForm.email.trim() || null,
        citizenId: editForm.citizenId.trim() || null,
        dateOfBirth: editForm.dateOfBirth ? editForm.dateOfBirth : null,
        address: editForm.address.trim() || null,
      });
      notify(`Đã cập nhật thông tin tài khoản ${editingUser.userName}.`);
      closeEditUser();
      reloadAdminUsers();
    } catch (err) {
      setEditError(err.message || "Không thể cập nhật thông tin người dùng.");
    } finally {
      setEditBusy(false);
    }
  }

  const stats = useMemo(() => {
    return {
      total: apiUsers.length,
      landlords: apiUsers.filter((u) => u.role === "Landlord").length,
      tenants: apiUsers.filter((u) => u.role === "Tenant").length,
      managers: apiUsers.filter((u) => u.role === "Manager").length,
    };
  }, [apiUsers]);

  const formatDate = (isoString) => {
    if (!isoString) return "—";
    try {
      const d = new Date(isoString);
      return d.toLocaleDateString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
      });
    } catch {
      return isoString;
    }
  };

  async function handleToggleUserLock(u) {
    setUserBusy(true);
    try {
      if (u.isActive) {
        await lockAdminUser(u.id);
        notify(`Đã khóa tài khoản ${u.userName}.`);
      } else {
        await unlockAdminUser(u.id);
        notify(`Đã mở khóa tài khoản ${u.userName}.`);
      }
      reloadAdminUsers();
    } catch (err) {
      notify(err.message || "Không thể cập nhật trạng thái tài khoản.");
    } finally {
      setUserBusy(false);
    }
  }

  async function handleDeleteUser(u) {
    if (
      !window.confirm(
        `Xóa / vô hiệu hóa tài khoản "${u.fullName || u.userName}"? Hệ thống sẽ giữ lại toàn bộ lịch sử hợp đồng và chứng từ.`,
      )
    ) {
      return;
    }
    setUserBusy(true);
    try {
      await deleteAdminUser(u.id);
      reloadAdminUsers();
      notify(`Đã xử lý xóa tài khoản ${u.userName}.`);
    } catch (err) {
      notify(err.message || "Không thể xóa tài khoản.");
    } finally {
      setUserBusy(false);
    }
  }
  if (page === "users")
    return (
      <div className="admin-users-page">
        <Heading
          eyebrow="QUẢN TRỊ HỆ THỐNG"
          title="Tài khoản người dùng"
          description="Quản trị toàn diện vòng đời tài khoản (Chủ trọ, Người thuê, Quản lý), phân quyền, chỉnh sửa thông tin cá nhân và kiểm soát trạng thái hoạt động an toàn."
          action={
            <Button
              icon="plus"
              onClick={() => {
                setUserFormError("");
                setShowCreate(true);
              }}
            >
              Tạo tài khoản
            </Button>
          }
        />

        {/* 1. KPI Summary Stat Cards */}
        <section className="admin-stats-grid" aria-label="Thống kê tài khoản theo vai trò">
          <div
            role="button"
            tabIndex={0}
            className={`admin-stat-card ${userRoleFilter === "" ? "is-active" : ""}`}
            onClick={() => setUserRoleFilter("")}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") setUserRoleFilter("");
            }}
          >
            <div className="admin-stat-card__top">
              <span className="admin-stat-card__label">Tổng tài khoản</span>
              <div className="admin-stat-icon admin-stat-icon--total">
                <Users size={19} />
              </div>
            </div>
            <div className="admin-stat-card__value">{stats.total}</div>
            <div className="admin-stat-card__footer">Toàn bộ người dùng trong danh sách</div>
          </div>

          <div
            role="button"
            tabIndex={0}
            className={`admin-stat-card ${userRoleFilter === "Tenant" ? "is-active" : ""}`}
            onClick={() => setUserRoleFilter(userRoleFilter === "Tenant" ? "" : "Tenant")}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ")
                setUserRoleFilter(userRoleFilter === "Tenant" ? "" : "Tenant");
            }}
          >
            <div className="admin-stat-card__top">
              <span className="admin-stat-card__label">Người thuê</span>
              <div className="admin-stat-icon admin-stat-icon--tenant">
                <UserCheck size={19} />
              </div>
            </div>
            <div className="admin-stat-card__value">{stats.tenants}</div>
            <div className="admin-stat-card__footer">Khách thuê phòng & cư trú</div>
          </div>

          <div
            role="button"
            tabIndex={0}
            className={`admin-stat-card ${userRoleFilter === "Landlord" ? "is-active" : ""}`}
            onClick={() => setUserRoleFilter(userRoleFilter === "Landlord" ? "" : "Landlord")}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ")
                setUserRoleFilter(userRoleFilter === "Landlord" ? "" : "Landlord");
            }}
          >
            <div className="admin-stat-card__top">
              <span className="admin-stat-card__label">Chủ trọ</span>
              <div className="admin-stat-icon admin-stat-icon--landlord">
                <Home size={19} />
              </div>
            </div>
            <div className="admin-stat-card__value">{stats.landlords}</div>
            <div className="admin-stat-card__footer">Quản lý khu trọ & phòng cho thuê</div>
          </div>

          <div
            role="button"
            tabIndex={0}
            className={`admin-stat-card ${userRoleFilter === "Manager" ? "is-active" : ""}`}
            onClick={() => setUserRoleFilter(userRoleFilter === "Manager" ? "" : "Manager")}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ")
                setUserRoleFilter(userRoleFilter === "Manager" ? "" : "Manager");
            }}
          >
            <div className="admin-stat-card__top">
              <span className="admin-stat-card__label">Quản lý</span>
              <div className="admin-stat-icon admin-stat-icon--manager">
                <Shield size={19} />
              </div>
            </div>
            <div className="admin-stat-card__value">{stats.managers}</div>
            <div className="admin-stat-card__footer">Nhân viên hỗ trợ vận hành</div>
          </div>
        </section>

        {/* 2. Modern Filter Toolbar */}
        <section className="admin-toolbar-card">
          <div className="admin-toolbar">
            <div className="admin-role-pills" role="group" aria-label="Lọc theo vai trò người dùng">
              {[
                { val: "", label: "Tất cả", count: stats.total },
                { val: "Tenant", label: "Người thuê", count: stats.tenants },
                { val: "Landlord", label: "Chủ trọ", count: stats.landlords },
                { val: "Manager", label: "Quản lý", count: stats.managers },
              ].map(({ val, label, count }) => (
                <button
                  key={val || "all"}
                  type="button"
                  className={`admin-role-pill ${userRoleFilter === val ? "is-active" : ""}`}
                  onClick={() => { setUserRoleFilter(val); setUserPage(1); }}
                >
                  <span>{label}</span>
                  <span className="admin-pill-badge">{count}</span>
                </button>
              ))}
            </div>

            <div className="admin-toolbar__controls">
              <div className="admin-search-wrap">
                <Search className="admin-search-icon" size={16} />
                <input
                  className="admin-search-input"
                  aria-label="Tìm tài khoản người dùng"
                  placeholder="Tìm tên, username, email, CCCD, SĐT..."
                  value={userKeyword}
                  onChange={(e) => { setUserKeyword(e.target.value); setUserPage(1); }}
                />
                {userKeyword && (
                  <button
                    type="button"
                    className="admin-search-clear"
                    aria-label="Xóa từ khóa tìm kiếm"
                    onClick={() => setUserKeyword("")}
                  >
                    <X size={13} />
                  </button>
                )}
              </div>

              <button
                type="button"
                className="admin-btn-action-refresh"
                title="Làm mới danh sách"
                aria-label="Làm mới danh sách người dùng"
                onClick={() => reloadAdminUsers()}
                disabled={adminUsersLoading}
              >
                <RefreshCw size={16} className={adminUsersLoading ? "spin" : ""} />
              </button>
            </div>
          </div>
          <div className="admin-toolbar-filters">
            <div className="admin-filter-group">
              <label htmlFor="admin-user-active-filter">Trạng thái tài khoản</label>
              <select
                id="admin-user-active-filter"
                className="admin-filter-select"
                value={userActiveFilter}
                onChange={(e) => setUserActiveFilter(e.target.value)}
              >
                <option value="">Tất cả</option>
                <option value="active">Đang hoạt động</option>
                <option value="locked">Đã khóa</option>
              </select>
            </div>
            <div className="admin-filter-group">
              <label htmlFor="admin-user-verify-filter">Xác minh danh tính</label>
              <select
                id="admin-user-verify-filter"
                className="admin-filter-select"
                value={userVerifyFilter}
                onChange={(e) => setUserVerifyFilter(e.target.value)}
              >
                <option value="">Tất cả</option>
                <option value="Verified">Đã xác minh</option>
                <option value="Pending">Chờ duyệt</option>
                <option value="Unverified">Chưa xác minh</option>
              </select>
            </div>
            {hasActiveFilters && (
              <button
                type="button"
                className="admin-filter-clear"
                onClick={resetUserFilters}
              >
                <X size={14} />
                <span>Xóa bộ lọc</span>
              </button>
            )}
          </div>
        </section>

        {/* 3. Redesigned User Data Table */}
        <section className="admin-table-card">
          {adminUsersLoading ? (
            <div style={{ padding: "48px 24px", textAlign: "center" }}>
              <p className="muted-copy">Đang tải danh sách tài khoản người dùng...</p>
            </div>
          ) : adminUsersError ? (
            <div style={{ padding: "24px" }}>
              <p className="form-error">
                {adminUsersError.message || "Không thể tải danh sách tài khoản."}
              </p>
            </div>
          ) : filteredUsers.length ? (
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Người dùng</th>
                    <th>Liên hệ</th>
                    <th>Vai trò</th>
                    <th>Xác minh</th>
                    <th>Trạng thái</th>
                    <th>Ngày tham gia</th>
                    <th style={{ textAlign: "right" }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredUsers.map((u) => (
                    <tr key={u.id}>
                      <td>
                        <div className="admin-user-cell">
                          <span className="admin-avatar">
                            {initials(u.fullName || u.userName || "U")}
                          </span>
                          <div className="admin-user-info">
                            <span className="admin-user-name">
                              {u.fullName || u.userName || "Người dùng"}
                            </span>
                            <span className="admin-user-handle">@{u.userName}</span>
                            {u.citizenId && (
                              <span
                                className="admin-cccd-pill"
                                title="Số Căn cước công dân / CMND"
                              >
                                CCCD: {u.citizenId}
                              </span>
                            )}
                          </div>
                        </div>
                      </td>

                      <td>
                        <div className="admin-contact-cell">
                          {u.email ? (
                            <a
                              href={`mailto:${u.email}`}
                              className="admin-contact-link"
                              title={`Gửi email đến ${u.email}`}
                            >
                              <Mail size={12} />
                              <span>{u.email}</span>
                            </a>
                          ) : null}
                          {u.phoneNumber ? (
                            <a
                              href={`tel:${u.phoneNumber}`}
                              className="admin-contact-link"
                              title={`Gọi số ${u.phoneNumber}`}
                            >
                              <Phone size={12} />
                              <span>{u.phoneNumber}</span>
                            </a>
                          ) : null}
                          {!u.email && !u.phoneNumber && (
                            <span className="muted-copy" style={{ fontSize: 12 }}>
                              Chưa có thông tin
                            </span>
                          )}
                        </div>
                      </td>

                      <td>
                        {u.role === "Landlord" && (
                          <span className="admin-badge admin-badge--role-landlord">
                            <Home size={12} /> Chủ trọ
                          </span>
                        )}
                        {u.role === "Tenant" && (
                          <span className="admin-badge admin-badge--role-tenant">
                            <UserCheck size={12} /> Người thuê
                          </span>
                        )}
                        {u.role === "Manager" && (
                          <span className="admin-badge admin-badge--role-manager">
                            <Shield size={12} /> Quản lý
                          </span>
                        )}
                        {u.role === "Admin" && (
                          <span className="admin-badge admin-badge--role-admin">
                            <Shield size={12} /> Quản trị
                          </span>
                        )}
                        {!["Landlord", "Tenant", "Manager", "Admin"].includes(u.role) && (
                          <span className="admin-badge admin-badge--unverified">{u.role}</span>
                        )}
                      </td>

                      <td>
                        {u.role === "Tenant" ? (
                          u.identityVerificationStatus === "Verified" ? (
                            <span
                              className="admin-badge admin-badge--verified"
                              title="Căn cước công dân đã được xác thực"
                            >
                              <CheckCircle2 size={12} /> Đã xác minh
                            </span>
                          ) : u.identityVerificationStatus === "Pending" ? (
                            <span
                              className="admin-badge admin-badge--pending"
                              title="Đang chờ xác minh danh tính"
                            >
                              <Clock size={12} /> Chờ duyệt
                            </span>
                          ) : (
                            <span
                              className="admin-badge admin-badge--unverified"
                              title="Chưa nộp thông tin định danh"
                            >
                              <AlertCircle size={12} /> Chưa xác minh
                            </span>
                          )
                        ) : (
                          <span className="muted-copy" style={{ fontSize: 12 }}>
                            —
                          </span>
                        )}
                      </td>

                      <td>
                        {u.isActive ? (
                          <span className="admin-badge admin-badge--active">
                            <Check size={12} /> Hoạt động
                          </span>
                        ) : (
                          <span className="admin-badge admin-badge--locked">
                            <Lock size={12} /> Đã khóa
                          </span>
                        )}
                      </td>

                      <td
                        style={{
                          color: "#64748b",
                          fontSize: "12px",
                          fontVariantNumeric: "tabular-nums",
                        }}
                      >
                        {formatDate(u.createdAtUtc || u.createdAt)}
                      </td>

                      <td>
                        {u.role !== "Admin" ? (
                          <div className="admin-actions-group">
                            <button
                              type="button"
                              className="admin-btn admin-btn--edit"
                              title="Chỉnh sửa thông tin người dùng"
                              onClick={() => openEditUser(u)}
                              disabled={userBusy}
                            >
                              <Edit3 size={13} />
                              <span>Sửa</span>
                            </button>

                            <button
                              type="button"
                              className={`admin-btn ${
                                u.isActive ? "admin-btn--lock" : "admin-btn--unlock"
                              }`}
                              title={u.isActive ? "Khóa tài khoản" : "Mở khóa tài khoản"}
                              onClick={() => handleToggleUserLock(u)}
                              disabled={userBusy}
                            >
                              {u.isActive ? <Lock size={13} /> : <Unlock size={13} />}
                              <span>{u.isActive ? "Khóa" : "Mở"}</span>
                            </button>

                            <button
                              type="button"
                              className="admin-btn admin-btn--delete"
                              title="Xóa / vô hiệu hóa tài khoản"
                              onClick={() => handleDeleteUser(u)}
                              disabled={userBusy}
                            >
                              <Trash2 size={13} />
                            </button>
                          </div>
                        ) : (
                          <span className="muted-copy" style={{ fontSize: 11.5 }}>
                            Tài khoản Admin
                          </span>
                        )}
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
                title="Không có tài khoản phù hợp"
                description="Thử thay đổi bộ lọc vai trò hoặc tìm kiếm với từ khóa khác."
              />
            </div>
          )}
          {adminUsersMeta && adminUsersMeta.totalPages > 1 && (
            <div className="admin-pagination">
              <span>
                Trang {userPage} / {adminUsersMeta.totalPages} · {adminUsersMeta.totalItems ?? filteredUsers.length} tài khoản
              </span>
              <div className="admin-pagination__controls">
                <button
                  type="button"
                  className="admin-pagination__btn"
                  disabled={adminUsersLoading || userPage <= 1}
                  onClick={() => setUserPage((p) => Math.max(1, p - 1))}
                >
                  Trang trước
                </button>
                <button
                  type="button"
                  className="admin-pagination__btn"
                  disabled={adminUsersLoading || userPage >= adminUsersMeta.totalPages}
                  onClick={() => setUserPage((p) => p + 1)}
                >
                  Trang sau
                </button>
              </div>
            </div>
          )}
        </section>

        {/* 4. Edit User Modal */}
        {editingUser && (
          <div
            className="admin-modal-backdrop"
            onClick={(e) => {
              if (e.target === e.currentTarget && !editBusy) closeEditUser();
            }}
          >
            <div
              className="admin-modal"
              role="dialog"
              aria-modal="true"
              aria-labelledby="modal-edit-title"
            >
              <div className="admin-modal__header">
                <div className="admin-modal__title-wrap">
                  <div className="admin-modal__icon">
                    <Edit3 size={20} />
                  </div>
                  <div>
                    <h2 id="modal-edit-title">Chỉnh sửa thông tin người dùng</h2>
                    <p>
                      Tài khoản: <strong>@{editingUser.userName}</strong> • Vai trò:{" "}
                      <strong>{editingUser.role}</strong>
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  className="admin-modal__close"
                  aria-label="Đóng cửa sổ"
                  onClick={closeEditUser}
                  disabled={editBusy}
                >
                  <X size={18} />
                </button>
              </div>

              <form onSubmit={handleSaveEditUser} style={{ display: "contents" }}>
                <div className="admin-modal__body">
                  <div className="admin-form-grid">
                    <div className="admin-field">
                      <label htmlFor="edit-fullname">
                        Họ và tên <span style={{ color: "#e11d48" }}>*</span>
                      </label>
                      <input
                        id="edit-fullname"
                        required
                        value={editForm.fullName}
                        onChange={(e) => setEditForm({ ...editForm, fullName: e.target.value })}
                        placeholder="Nguyễn Văn A"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="edit-username">Tên đăng nhập (Chỉ xem)</label>
                      <input id="edit-username" disabled value={editingUser.userName} />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="edit-phone">Số điện thoại</label>
                      <input
                        id="edit-phone"
                        type="tel"
                        value={editForm.phoneNumber}
                        onChange={(e) =>
                          setEditForm({ ...editForm, phoneNumber: e.target.value })
                        }
                        placeholder="0912345678"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="edit-email">Email liên hệ</label>
                      <input
                        id="edit-email"
                        type="email"
                        value={editForm.email}
                        onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
                        placeholder="user@example.com"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="edit-citizen">Số CCCD / CMND</label>
                      <input
                        id="edit-citizen"
                        maxLength={12}
                        value={editForm.citizenId}
                        onChange={(e) => setEditForm({ ...editForm, citizenId: e.target.value })}
                        placeholder="12 chữ số CCCD"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="edit-dob">Ngày sinh</label>
                      <input
                        id="edit-dob"
                        type="date"
                        value={editForm.dateOfBirth}
                        onChange={(e) =>
                          setEditForm({ ...editForm, dateOfBirth: e.target.value })
                        }
                      />
                    </div>

                    <div className="admin-field admin-field--wide">
                      <label htmlFor="edit-address">Địa chỉ thường trú / liên hệ</label>
                      <input
                        id="edit-address"
                        value={editForm.address}
                        onChange={(e) => setEditForm({ ...editForm, address: e.target.value })}
                        placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành phố"
                      />
                    </div>

                    {["Tenant", "Landlord"].includes(editingUser.role) && (
                      <Button type="button" onClick={() => navigate("admin/identity/" + editingUser.id)}>
                        Mở xác minh danh tính
                      </Button>
                    )}
                  </div>

                  <div className="admin-modal-note">
                    <CheckCircle2 size={16} style={{ flexShrink: 0, marginTop: 2 }} />
                    <span>
                      Dữ liệu người dùng sẽ được tự động đồng bộ sang hồ sơ người thuê (Tenant
                      Profile), thông tin hợp đồng và các chứng từ liên quan trong toàn hệ thống.
                    </span>
                  </div>

                  {editError && (
                    <div className="admin-modal-error" role="alert">
                      <AlertCircle size={16} style={{ flexShrink: 0 }} />
                      <span>{editError}</span>
                    </div>
                  )}
                </div>

                <div className="admin-modal__footer">
                  <button
                    type="button"
                    className="button button--secondary"
                    onClick={closeEditUser}
                    disabled={editBusy}
                  >
                    Hủy bỏ
                  </button>
                  <button
                    type="submit"
                    className="button button--primary"
                    disabled={editBusy}
                    style={{ minWidth: 120 }}
                  >
                    {editBusy ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* 5. Create User Modal */}
        {showCreate && (
          <div
            className="admin-modal-backdrop"
            onClick={(e) => {
              if (e.target === e.currentTarget && !userBusy) setShowCreate(false);
            }}
          >
            <div
              className="admin-modal"
              role="dialog"
              aria-modal="true"
              aria-labelledby="modal-create-title"
            >
              <div className="admin-modal__header">
                <div className="admin-modal__title-wrap">
                  <div className="admin-modal__icon">
                    <UserPlus size={20} />
                  </div>
                  <div>
                    <h2 id="modal-create-title">Tạo tài khoản người dùng mới</h2>
                    <p>Khởi tạo tài khoản trực tiếp cho Chủ trọ, Người thuê hoặc Quản lý</p>
                  </div>
                </div>
                <button
                  type="button"
                  className="admin-modal__close"
                  aria-label="Đóng cửa sổ"
                  onClick={() => setShowCreate(false)}
                  disabled={userBusy}
                >
                  <X size={18} />
                </button>
              </div>

              <form onSubmit={addUser} style={{ display: "contents" }}>
                <div className="admin-modal__body">
                  <div className="admin-form-grid">
                    <div className="admin-field">
                      <label htmlFor="create-username">
                        Tên đăng nhập <span style={{ color: "#e11d48" }}>*</span>
                      </label>
                      <input
                        id="create-username"
                        required
                        minLength={3}
                        value={draft.userName}
                        onChange={(e) => setDraft({ ...draft, userName: e.target.value })}
                        placeholder="nguyenvana"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="create-email">
                        Email <span style={{ color: "#e11d48" }}>*</span>
                      </label>
                      <input
                        id="create-email"
                        required
                        type="email"
                        value={draft.email}
                        onChange={(e) => setDraft({ ...draft, email: e.target.value })}
                        placeholder="email@example.com"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="create-pass">
                        Mật khẩu ban đầu <span style={{ color: "#e11d48" }}>*</span>
                      </label>
                      <input
                        id="create-pass"
                        required
                        type="password"
                        minLength={6}
                        value={draft.password}
                        onChange={(e) => setDraft({ ...draft, password: e.target.value })}
                        placeholder="Tối thiểu 6 ký tự"
                      />
                    </div>

                    <div className="admin-field">
                      <label htmlFor="create-role">
                        Vai trò tài khoản <span style={{ color: "#e11d48" }}>*</span>
                      </label>
                      <select
                        id="create-role"
                        value={draft.role}
                        onChange={(e) => setDraft({ ...draft, role: e.target.value })}
                      >
                        <option value="Landlord">Chủ trọ (Landlord)</option>
                        <option value="Tenant">Người thuê (Tenant)</option>
                        <option value="Manager">Quản lý (Manager)</option>
                      </select>
                    </div>
                  </div>

                  {userFormError && (
                    <div className="admin-modal-error" role="alert">
                      <AlertCircle size={16} style={{ flexShrink: 0 }} />
                      <span>{userFormError}</span>
                    </div>
                  )}
                </div>

                <div className="admin-modal__footer">
                  <button
                    type="button"
                    className="button button--secondary"
                    onClick={() => setShowCreate(false)}
                    disabled={userBusy}
                  >
                    Hủy bỏ
                  </button>
                  <button
                    type="submit"
                    className="button button--primary"
                    disabled={userBusy}
                    style={{ minWidth: 120 }}
                  >
                    {userBusy ? "Đang tạo..." : "Tạo tài khoản"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    );
  return (
    <>
      <Heading
        eyebrow={todayLabel}
        title="Xin chào, Quản trị viên 👋"
        description="Đây là những việc cần bạn chú ý hôm nay."
        action={
          <Button
            variant="secondary"
            onClick={() => navigate("admin/approvals")}
          >
            Xem hồ sơ chờ <Icon name="arrow" size={16} />
          </Button>
        }
      />
      <div className="stats-grid stats-grid--admin">
        <Stat
          icon="clock"
          label="Hồ sơ chờ duyệt"
          value={reviewSummary ? String(reviewSummary.pending) : "—"}
          note="Cần xem xét"
          tint="amber"
        />
        <Stat
          icon="users"
          label="Tài khoản mẫu"
          value={users.length.toString()}
          note="Chủ trọ và người thuê"
        />
        <Stat
          icon="check"
          label="Đã duyệt"
          value={reviewSummary ? String(reviewSummary.approved) : "—"}
          note="Hồ sơ chủ trọ"
          tint="green"
        />
      </div>
      <div className="dashboard-grid">
        <section className="content-card">
          <div className="card-title-row">
            <div>
              <h2>Hồ sơ mới cần duyệt</h2>
              <p>Ưu tiên xử lý các hồ sơ được gửi gần đây.</p>
            </div>
            <button
              className="text-link"
              type="button"
              onClick={() => navigate("admin/approvals")}
            >
              Xem tất cả <Icon name="arrow" size={16} />
            </button>
          </div>
          <p>Xem danh sách để xử lý hồ sơ đang chờ.</p>
        </section>
        <section className="content-card quick-card">
          <span className="quick-card-icon">
            <Icon name="users" size={25} />
          </span>
          <h2>Quản lý tài khoản tập trung</h2>
          <p>
            Tạo tài khoản cho chủ trọ và người thuê, theo dõi danh sách người
            dùng ở một nơi.
          </p>
          <Button variant="secondary" onClick={() => navigate("admin/users")}>
            Đến tài khoản <Icon name="arrow" size={16} />
          </Button>
        </section>
      </div>
    </>
  );
}
