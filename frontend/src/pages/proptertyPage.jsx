import { useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { useApiQuery } from "../hooks/useApiQuery";
import {
  listProperties,
  getProperty,
  createProperty,
  updateProperty,
  deleteProperty,
} from "../services/api";

const emptyDraft = {
  name: "",
  address: "",
  description: "",
  isActive: true,
};

export default function PropertyPage({ notify }) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState(emptyDraft);
  const [editing, setEditing] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState("");

  const { data, error, isLoading, reload } = useApiQuery(
    user?.id ? `properties:${user.id}:${page}` : null,
    () => listProperties({ page, pageSize: 20 }),
    { enabled: Boolean(user?.id) },
  );

  const properties = data?.data ?? [];
  const meta = data?.meta;

  function openCreate() {
    setEditing(null);
    setDraft(emptyDraft);
    setActionError("");
    setShowForm(true);
  }

  async function openEdit(id) {
    setActionError("");
    try {
      const response = await getProperty(id);
      const property = response.data;
      setEditing(property);
      setDraft({
        name: property.name,
        address: property.address,
        description: property.description ?? "",
        isActive: property.isActive,
      });
      setShowForm(true);
    } catch (err) {
      setActionError(err.message);
    }
  }

  async function save(event) {
    event.preventDefault();

    const payload = {
      name: draft.name.trim(),
      address: draft.address.trim(),
      description: draft.description.trim() || null,
    };
    if (!payload.name || !payload.address) {
      setActionError("Tên và địa chỉ không được để trống.");
      return;
    }

    setBusy(true);
    setActionError("");
    try {
      if (editing) {
        await updateProperty(editing.id, {
          ...payload,
          isActive: draft.isActive,
          rowVersion: editing.rowVersion,
        });
        notify("Đã cập nhật khu trọ.");
      } else {
        await createProperty(payload);
        notify("Đã tạo khu trọ.");
      }

      setShowForm(false);
      setEditing(null);
      setDraft(emptyDraft);
      if (!editing && page !== 1) setPage(1);
      else reload();
    } catch (err) {
      setActionError(
        err.status === 409 && err.details?.code === "ROW_VERSION_CONFLICT"
          ? "Khu trọ đã thay đổi. Đóng biểu mẫu và mở lại để lấy dữ liệu mới."
          : err.message,
      );
    } finally {
      setBusy(false);
    }
  }

  async function remove(id) {
    setBusy(true);
    setActionError("");
    try {
      await deleteProperty(id);
      notify("Đã xóa khu trọ.");
      if (properties.length === 1 && page > 1) setPage(page - 1);
      else reload();
    } catch (err) {
      setActionError(err.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>Khu trọ của bạn</h1>
          <p>Danh sách khu trọ được lưu trong hệ thống.</p>
        </div>
        <button className="button button--primary" onClick={openCreate}>
          Thêm khu trọ
        </button>
      </div>

      {actionError && (
        <p className="form-error" role="alert">
          {actionError}
        </p>
      )}

      {showForm && (
        <form className="content-card form-grid" onSubmit={save}>
          <label className="field">
            <span>Tên khu trọ</span>
            <input
              required
              maxLength={150}
              value={draft.name}
              onChange={(e) => setDraft({ ...draft, name: e.target.value })}
            />
          </label>

          <label className="field">
            <span>Địa chỉ</span>
            <input
              required
              maxLength={300}
              value={draft.address}
              onChange={(e) => setDraft({ ...draft, address: e.target.value })}
            />
          </label>

          <label className="field field--wide">
            <span>Mô tả</span>
            <textarea
              maxLength={1000}
              value={draft.description}
              onChange={(e) =>
                setDraft({ ...draft, description: e.target.value })
              }
            />
          </label>

          {editing && (
            <label>
              <input
                type="checkbox"
                checked={draft.isActive}
                onChange={(e) =>
                  setDraft({ ...draft, isActive: e.target.checked })
                }
              />
              Đang hoạt động
            </label>
          )}

          <div>
            <button className="button button--primary" disabled={busy}>
              {busy ? "Đang lưu..." : "Lưu"}
            </button>
            <button
              type="button"
              className="button button--secondary"
              onClick={() => setShowForm(false)}
            >
              Hủy
            </button>
          </div>
        </form>
      )}

      {isLoading ? (
        <p>Đang tải khu trọ...</p>
      ) : error ? (
        <p className="form-error" role="alert">
          {error.message}
        </p>
      ) : properties.length === 0 ? (
        <p>Chưa có khu trọ nào.</p>
      ) : (
        <div className="property-grid">
          {properties.map((property) => (
            <article className="property-card" key={property.id}>
              <div className="property-content">
                <h2>{property.name}</h2>
                <p>{property.address}</p>
                <p>{property.isActive ? "Đang hoạt động" : "Tạm ngừng"}</p>
                <button disabled={busy} onClick={() => openEdit(property.id)}>
                  Sửa
                </button>
                <button disabled={busy} onClick={() => remove(property.id)}>
                  Xóa
                </button>
              </div>
            </article>
          ))}
        </div>
      )}

      {meta && meta.totalPages > 1 && (
        <div>
          <button disabled={page <= 1} onClick={() => setPage(page - 1)}>
            Trang trước
          </button>
          <span>
            {" "}
            Trang {page}/{meta.totalPages}{" "}
          </span>
          <button
            disabled={page >= meta.totalPages}
            onClick={() => setPage(page + 1)}
          >
            Trang sau
          </button>
        </div>
      )}
    </>
  );
}
