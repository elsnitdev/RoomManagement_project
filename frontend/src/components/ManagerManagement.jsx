import { useState } from "react";
import { Lock, Plus, ShieldCheck, Trash2, Unlock, UserPlus, X } from "lucide-react";
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
} from "../services/api";
import { Badge, Button, Empty, Field, Heading, Icon } from "../ui";

const emptyDraft = {
  userName: "",
  password: "",
  fullName: "",
  email: "",
  phoneNumber: "",
  propertyIds: [],
};

export default function ManagerManagement({ notify }) {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [showCreate, setShowCreate] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [assigningFor, setAssigningFor] = useState(null);
  const [selectedPropertyIds, setSelectedPropertyIds] = useState([]);

  const isActiveParam =
    statusFilter === "active" ? true : statusFilter === "locked" ? false : undefined;

  const {
    data: managersRes,
    error: managersErr,
    isLoading: managersLoading,
    reload: reloadManagers,
  } = useApiQuery(
    `landlord:managers:${statusFilter}:${search.trim()}`,
    () =>
      listManagers({
        page: 1,
        pageSize: 50,
        search: search.trim(),
        isActive: isActiveParam,
      }),
  );

  const { data: propertiesRes } = useApiQuery("landlord:properties:all-for-manager", () =>
    listProperties({ page: 1, pageSize: 100 }),
  );

  const managers = Array.isArray(managersRes?.data) ? managersRes.data : [];
  const properties = Array.isArray(propertiesRes?.data) ? propertiesRes.data : [];

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
    setError("");
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
      setDraft(emptyDraft);
      setShowCreate(false);
      reloadManagers();
      notify?.("Đã tạo tài khoản Quản lý khu trọ. Hãy bàn giao mật khẩu tạm cho nhân sự.");
    } catch (err) {
      setError(err.message || "Không thể tạo tài khoản quản lý.");
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
    setBusy(true);
    try {
      await assignManagerProperties(assigningFor.id, selectedPropertyIds);
      setAssigningFor(null);
      setSelectedPropertyIds([]);
      reloadManagers();
      notify?.("Đã giao thêm khu trọ cho quản lý.");
    } catch (err) {
      notify?.(err.message || "Không thể phân công khu trọ.");
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

  return (
    <>
      <Heading
        eyebrow="PHÂN QUYỀN KHU TRỌ"
        title="Quản lý nhân sự (Manager)"
        description="Tạo tài khoản Quản lý khu trọ, giao hoặc thu hồi phạm vi Property, khóa hoặc xóa quyền truy cập."
        action={
          <Button
            onClick={() => {
              setError("");
              setShowCreate((v) => !v);
            }}
          >
            <UserPlus size={16} aria-hidden="true" />
            {showCreate ? "Đóng biểu mẫu" : "Thêm Quản lý"}
          </Button>
        }
      />

      {showCreate && (
        <section className="content-card" style={{ marginBottom: "1rem" }}>
          <div className="card-header">
            <div>
              <h2>Tạo tài khoản Quản lý mới</h2>
              <p className="muted-copy">
                Mật khẩu tạm chỉ dùng để bàn giao lần đầu; Manager bắt buộc phải đổi mật khẩu trước khi thao tác nghiệp vụ.
              </p>
            </div>
            <button
              type="button"
              className="icon-button"
              onClick={() => setShowCreate(false)}
              aria-label="Đóng"
            >
              <X size={18} />
            </button>
          </div>
          <form onSubmit={handleCreate} className="form-grid">
            <Field label="Tên đăng nhập" required>
              <input
                required
                minLength={3}
                value={draft.userName}
                onChange={(e) => setDraft({ ...draft, userName: e.target.value })}
                placeholder="Ví dụ: quanly_khu_a"
              />
            </Field>
            <Field label="Mật khẩu tạm" required>
              <input
                required
                type="password"
                minLength={6}
                value={draft.password}
                onChange={(e) => setDraft({ ...draft, password: e.target.value })}
                placeholder="Ít nhất 6 ký tự"
              />
            </Field>
            <Field label="Họ và tên">
              <input
                value={draft.fullName}
                onChange={(e) => setDraft({ ...draft, fullName: e.target.value })}
                placeholder="Trần Văn Quản Lý"
              />
            </Field>
            <Field label="Số điện thoại">
              <input
                inputMode="tel"
                value={draft.phoneNumber}
                onChange={(e) => setDraft({ ...draft, phoneNumber: e.target.value })}
                placeholder="09xxxxxxxx"
              />
            </Field>
            <Field label="Email" wide>
              <input
                type="email"
                value={draft.email}
                onChange={(e) => setDraft({ ...draft, email: e.target.value })}
                placeholder="quanly@email.com (không bắt buộc)"
              />
            </Field>
            <div className="field field--wide">
              <span>Giao khu trọ (Property) ban đầu</span>
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
                <small>Bạn chưa có khu trọ nào. Có thể tạo tài khoản trước và giao khu trọ sau.</small>
              )}
            </div>
            {error && (
              <p className="form-error field--wide" role="alert">
                {error}
              </p>
            )}
            <div className="profile-actions field--wide">
              <Button variant="secondary" onClick={() => setShowCreate(false)}>
                Hủy
              </Button>
              <button className="button button--primary" type="submit" disabled={busy}>
                {busy ? "Đang tạo..." : "Tạo tài khoản Manager"}
              </button>
            </div>
          </form>
        </section>
      )}

      {assigningFor && (
        <section className="content-card" style={{ marginBottom: "1rem" }}>
          <div className="card-header">
            <div>
              <h2>Giao thêm khu trọ cho {assigningFor.fullName || assigningFor.userName}</h2>
            </div>
            <button
              type="button"
              className="icon-button"
              onClick={() => {
                setAssigningFor(null);
                setSelectedPropertyIds([]);
              }}
              aria-label="Đóng"
            >
              <X size={18} />
            </button>
          </div>
          <form onSubmit={handleAssignProperties}>
            <div className="manager-property-checks" style={{ marginBottom: "1rem" }}>
              {properties
                .filter(
                  (p) =>
                    !(assigningFor.assignedProperties || []).some(
                      (ap) => ap.propertyId === p.id,
                    ),
                )
                .map((prop) => (
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
                    <span>{prop.name}</span>
                  </label>
                ))}
            </div>
            <div className="profile-actions">
              <Button
                variant="secondary"
                onClick={() => {
                  setAssigningFor(null);
                  setSelectedPropertyIds([]);
                }}
              >
                Hủy
              </Button>
              <button
                className="button button--primary"
                type="submit"
                disabled={busy || !selectedPropertyIds.length}
              >
                Lưu phân công
              </button>
            </div>
          </form>
        </section>
      )}

      <section className="content-card">
        <div className="card-toolbar">
          <div className="segmented" role="group" aria-label="Lọc trạng thái quản lý">
            <button
              type="button"
              className={statusFilter === "all" ? "selected" : ""}
              onClick={() => setStatusFilter("all")}
            >
              Tất cả
            </button>
            <button
              type="button"
              className={statusFilter === "active" ? "selected" : ""}
              onClick={() => setStatusFilter("active")}
            >
              Đang hoạt động
            </button>
            <button
              type="button"
              className={statusFilter === "locked" ? "selected" : ""}
              onClick={() => setStatusFilter("locked")}
            >
              Đã khóa
            </button>
          </div>
          <div className="search-box">
            <Icon name="search" size={18} />
            <input
              aria-label="Tìm quản lý"
              placeholder="Tìm tên, username hoặc SĐT..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>

        {managersLoading ? (
          <p className="muted-copy">Đang tải danh sách quản lý...</p>
        ) : managersErr ? (
          <p className="form-error">{managersErr.message || "Không thể tải danh sách quản lý."}</p>
        ) : managers.length ? (
          <div className="responsive-table">
            <table>
              <thead>
                <tr>
                  <th>Quản lý</th>
                  <th>Trạng thái</th>
                  <th>Khu trọ được giao</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {managers.map((m) => (
                  <tr key={m.id}>
                    <td>
                      <strong>{m.fullName || m.userName}</strong>
                      <div className="muted-copy">
                        @{m.userName}
                        {m.phoneNumber ? ` · ${m.phoneNumber}` : ""}
                      </div>
                    </td>
                    <td>
                      <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem", alignItems: "flex-start" }}>
                        <Badge tone={m.isActive ? "success" : "danger"}>
                          {m.isActive ? "Hoạt động" : "Đã khóa"}
                        </Badge>
                        {m.mustChangePassword && (
                          <Badge tone="warning">Chờ đổi mật khẩu tạm</Badge>
                        )}
                      </div>
                    </td>
                    <td>
                      <div className="manager-assigned-list">
                        {(m.assignedProperties || []).length ? (
                          m.assignedProperties.map((ap) => (
                            <span key={ap.propertyId} className="manager-assigned-chip">
                              <ShieldCheck size={14} aria-hidden="true" />
                              {ap.propertyName}
                              <button
                                type="button"
                                title="Thu hồi khu trọ này"
                                onClick={() =>
                                  handleRevokeProperty(m.id, ap.propertyId, ap.propertyName)
                                }
                                disabled={busy}
                              >
                                <X size={13} />
                              </button>
                            </span>
                          ))
                        ) : (
                          <span className="muted-copy">Chưa được giao khu trọ</span>
                        )}
                        <button
                          type="button"
                          className="button button--secondary button--sm"
                          onClick={() => {
                            setAssigningFor(m);
                            setSelectedPropertyIds([]);
                          }}
                          disabled={busy}
                        >
                          <Plus size={14} /> Giao khu trọ
                        </button>
                      </div>
                    </td>
                    <td>
                      <div className="action-row">
                        <button
                          type="button"
                          className="button button--secondary button--sm"
                          onClick={() => handleToggleLock(m)}
                          disabled={busy}
                          title={m.isActive ? "Khóa tài khoản" : "Mở khóa tài khoản"}
                        >
                          {m.isActive ? <Lock size={14} /> : <Unlock size={14} />}
                          {m.isActive ? "Khóa" : "Mở khóa"}
                        </button>
                        <button
                          type="button"
                          className="button button--secondary button--sm"
                          onClick={() => handleDelete(m)}
                          disabled={busy}
                          title="Xóa quản lý"
                        >
                          <Trash2 size={14} /> Xóa
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Empty
            icon="users"
            title="Chưa có tài khoản quản lý nào"
            description="Tạo tài khoản Manager để phân công quản lý từng khu trọ."
          />
        )}
      </section>
    </>
  );
}
