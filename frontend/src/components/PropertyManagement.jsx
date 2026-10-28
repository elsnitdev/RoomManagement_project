import { useState } from "react";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Building2,
  CalendarDays,
  MapPin,
  Pencil,
  Plus,
  RotateCcw,
  Save,
  Search,
  Trash2,
  X,
} from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { useApiQuery } from "../hooks/useApiQuery";
import { ConfirmationToast, Feedback, IconButton, Pagination, Status, dateText, messageOf } from "./property/ManagementUi";
import {
  createProperty,
  deleteProperty,
  getProperty,
  listProperties,
  updateProperty,
} from "../services/api";
import "../styles/property-management.css";

const propertyDraft = { name: "", address: "", description: "", isActive: true };
function PropertyForm({ initial, onClose, onSaved }) {
  const [draft, setDraft] = useState(initial ? {
    name: initial.name,
    address: initial.address,
    description: initial.description || "",
    isActive: initial.isActive,
  } : propertyDraft);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const editing = Boolean(initial);

  async function submit(event) {
    event.preventDefault();
    const name = draft.name.trim();
    const address = draft.address.trim();
    if (!name || !address) {
      setError("Vui lòng nhập tên và địa chỉ khu trọ.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const payload = { name, address, description: draft.description.trim() || null };
      const result = editing
        ? await updateProperty(initial.id, { ...payload, isActive: draft.isActive, rowVersion: initial.rowVersion })
        : await createProperty(payload);
      onSaved(result);
    } catch (cause) {
      setError(messageOf(cause));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="pm-form-card content-card" aria-labelledby="property-form-title">
      <div className="pm-section-heading"><div><span className="pm-eyebrow">THÔNG TIN KHU TRỌ</span><h2 id="property-form-title">{editing ? "Chỉnh sửa khu trọ" : "Tạo khu trọ mới"}</h2></div><IconButton label="Đóng biểu mẫu khu trọ" icon={X} onClick={onClose} disabled={busy} /></div>
      <form onSubmit={submit} className="pm-form-grid">
        <label className="field"><span>Tên khu trọ *</span><input autoFocus required maxLength={150} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} placeholder="Ví dụ: Khu trọ An Bình" /></label>
        <label className="field"><span>Địa chỉ *</span><input required maxLength={300} value={draft.address} onChange={(event) => setDraft({ ...draft, address: event.target.value })} placeholder="Số nhà, đường, phường/xã..." /></label>
        <label className="field pm-span"><span>Mô tả</span><textarea rows="3" maxLength={1000} value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} placeholder="Thông tin thêm về khu trọ" /></label>
        {editing && <label className="pm-check pm-span"><input type="checkbox" checked={draft.isActive} onChange={(event) => setDraft({ ...draft, isActive: event.target.checked })} /><span>Khu trọ đang hoạt động</span></label>}
        {error && <p className="form-error pm-span" role="alert">{error}</p>}
        <div className="pm-form-actions pm-span"><IconButton label="Hủy chỉnh sửa khu trọ" icon={X} onClick={onClose} disabled={busy} /><IconButton type="submit" label={busy ? "Đang lưu khu trọ" : editing ? "Lưu thay đổi khu trọ" : "Tạo khu trọ"} icon={Save} tone="primary" disabled={busy} /></div>
      </form>
    </section>
  );
}

function PropertyList({ navigate, notify }) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [creating, setCreating] = useState(false);
  const query = useApiQuery(
    `properties:${user?.id}:list:${page}:${search}:${status}`,
    () => listProperties({ page, pageSize: 12, search: search || undefined, isActive: status === "all" ? undefined : status === "active" }),
    { enabled: Boolean(user) },
  );
  const properties = query.data?.data || [];
  const meta = query.data?.meta;

  function applySearch(event) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <div className="pm-page">
      <header className="pm-hero"><div><span className="pm-eyebrow">QUẢN LÝ BẤT ĐỘNG SẢN</span><h1>Khu trọ của bạn</h1><p>Quản lý thông tin từng khu trọ và các phòng bên trong.</p></div><IconButton label="Thêm khu trọ" icon={Plus} tone="primary" onClick={() => setCreating(true)} /></header>
      {creating && <PropertyForm onClose={() => setCreating(false)} onSaved={(created) => { notify("Đã tạo khu trọ."); navigate(`landlord/properties/${created.id}`); }} />}
      <section className="pm-list-panel content-card" aria-label="Danh sách khu trọ">
        <div className="pm-section-heading"><div><h2>Danh sách khu trọ</h2><p>{meta ? `${meta.totalItems} khu trọ trong hệ thống` : "Các khu trọ thuộc tài khoản của bạn"}</p></div></div>
        <div className="pm-toolbar"><form className="pm-search" onSubmit={applySearch} role="search"><Search size={18} aria-hidden="true" /><input aria-label="Tìm khu trọ" placeholder="Tìm theo tên hoặc địa chỉ" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} /><IconButton type="submit" label="Tìm khu trọ" icon={Search} tone="soft" /></form><label className="pm-filter"><span>Trạng thái</span><select value={status} onChange={(event) => { setStatus(event.target.value); setPage(1); }}><option value="all">Tất cả</option><option value="active">Đang hoạt động</option><option value="inactive">Tạm ngừng</option></select></label></div>
        {query.isLoading ? <div className="pm-loading" role="status">Đang tải danh sách khu trọ...</div> : query.error ? <Feedback error={query.error} onRetry={query.reload} /> : properties.length === 0 ? <div className="pm-empty"><span className="pm-empty-icon"><Building2 size={28} aria-hidden="true" /></span><strong>{search || status !== "all" ? "Không tìm thấy khu trọ phù hợp" : "Bạn chưa có khu trọ nào"}</strong><p>{search || status !== "all" ? "Thử thay đổi từ khóa hoặc bộ lọc." : "Bắt đầu bằng cách tạo khu trọ đầu tiên."}</p>{!search && status === "all" && <IconButton label="Tạo khu trọ" icon={Plus} tone="primary" onClick={() => setCreating(true)} />}</div> : <div className="pm-property-grid">{properties.map((property) => <article className="pm-property-card" key={property.id}><div className="pm-property-card-top"><span className="pm-card-icon"><Building2 size={23} aria-hidden="true" /></span><Status active={property.isActive} /></div><h3>{property.name}</h3><p className="pm-address">{property.address}</p><p className="pm-description">{property.description || "Chưa có mô tả."}</p><div className="pm-card-footer"><span>Tạo ngày {dateText(property.createdAt)}</span><IconButton label={`Xem chi tiết ${property.name}`} icon={ArrowRight} tone="soft" onClick={() => navigate(`landlord/properties/${property.id}`)} /></div></article>)}</div>}
        <Pagination page={page} meta={meta} onChange={setPage} />
      </section>
    </div>
  );
}

function PropertyDetail({ propertyId, navigate, notify }) {
  const { user } = useAuth();
  const [editingProperty, setEditingProperty] = useState(false);
  const [actionError, setActionError] = useState("");
  const [busy, setBusy] = useState(false);
  const [pendingDelete, setPendingDelete] = useState(null);
  const propertyQuery = useApiQuery(`properties:${user?.id}:detail:${propertyId}`, () => getProperty(propertyId), { enabled: Boolean(user) });
  const property = propertyQuery.data?.data;

  function askDeleteProperty(event) {
    setActionError("");
    setPendingDelete({ kind: "property", propertyName: property.name, trigger: event.currentTarget, error: "" });
  }

  function cancelDelete() {
    if (busy) return;
    const trigger = pendingDelete?.trigger;
    setPendingDelete(null);
    trigger?.focus();
  }

  async function confirmDelete() {
    if (!pendingDelete || busy) return;
    const request = pendingDelete;
    setBusy(true);
    setPendingDelete({ ...request, error: "" });
    try {
      await deleteProperty(propertyId);
      setPendingDelete(null);
      notify("Đã xóa khu trọ.");
      navigate("landlord/properties");
    } catch (cause) {
      setPendingDelete({ ...request, error: messageOf(cause) });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="pm-page pm-page--detail">
      <div className="pm-detail-topline">
        <IconButton label="Quay về danh sách khu trọ" icon={ArrowLeft} onClick={() => navigate("landlord/properties")} />
        <span>Danh sách khu trọ / Chi tiết</span>
      </div>
      {propertyQuery.isLoading ? <div className="pm-loading" role="status">Đang tải khu trọ...</div> : propertyQuery.error ? <Feedback error={propertyQuery.error} onRetry={propertyQuery.reload} /> : property && <>
        <header className="pm-detail-hero">
          <div className="pm-detail-hero-main">
            <span className="pm-detail-icon"><Building2 size={30} aria-hidden="true" /></span>
            <div className="pm-detail-title">
              <span className="pm-eyebrow">KHU TRỌ / THÔNG TIN CHI TIẾT</span>
              <h1>{property.name}</h1>
              <p><MapPin size={16} aria-hidden="true" />{property.address}</p>
            </div>
          </div>
          <div className="pm-detail-hero-actions">
            <Status active={property.isActive} />
            <div className="pm-heading-actions">
              <IconButton label="Chỉnh sửa khu trọ" icon={Pencil} onClick={() => { setEditingProperty(true); setActionError(""); }} disabled={busy} />
              <IconButton label="Xóa khu trọ" icon={Trash2} tone="danger" onClick={askDeleteProperty} disabled={busy} />
            </div>
          </div>
        </header>

        {actionError && <p className="form-error pm-action-error" role="alert">{actionError}</p>}
        {editingProperty && <PropertyForm initial={property} onClose={() => setEditingProperty(false)} onSaved={() => { setEditingProperty(false); propertyQuery.reload(); notify("Đã cập nhật khu trọ."); }} />}

        <div className="pm-detail-overview">
          <section className="pm-detail-card content-card" aria-labelledby="pm-info-title">
            <div className="pm-section-heading"><div><span className="pm-eyebrow">TỔNG QUAN</span><h2 id="pm-info-title">Thông tin khu trọ</h2></div></div>
            <div className="pm-detail-field">
              <span className="pm-detail-field-icon"><MapPin size={18} aria-hidden="true" /></span>
              <div><span>Địa chỉ</span><strong>{property.address}</strong></div>
            </div>
            <div className="pm-detail-field pm-detail-field--description">
              <span className="pm-detail-field-icon"><Building2 size={18} aria-hidden="true" /></span>
              <div><span>Mô tả</span><p>{property.description || "Chưa có mô tả cho khu trọ này."}</p></div>
            </div>
          </section>
          <aside className="pm-detail-side content-card" aria-label="Thông tin vận hành">
            <span className="pm-eyebrow">VẬN HÀNH</span>
            <h2>Tình trạng khu trọ</h2>
            <div className="pm-detail-side-status"><Status active={property.isActive} /><p>{property.isActive ? "Có thể thêm phòng và quản lý thông tin phòng." : "Cần kích hoạt khu trọ trước khi thêm phòng."}</p></div>
            <div className="pm-detail-date"><CalendarDays size={18} aria-hidden="true" /><div><span>Ngày tạo</span><strong>{dateText(property.createdAt)}</strong></div></div>
            <div className="pm-detail-date"><RotateCcw size={18} aria-hidden="true" /><div><span>Cập nhật gần nhất</span><strong>{dateText(property.updatedAt || property.createdAt)}</strong></div></div>
          </aside>
        </div>

        <section className="pm-room-panel content-card" aria-labelledby="pm-rooms-title">
          <div className="pm-section-heading pm-room-heading">
            <div><span className="pm-eyebrow">PHÒNG THUỘC KHU TRỌ</span><h2 id="pm-rooms-title">Phòng trọ</h2><p>Danh sách và thao tác quản lý phòng nằm ở trang riêng.</p></div>
            <div className="pm-heading-actions">
              <IconButton label="Xem danh sách phòng" icon={ArrowRight} onClick={() => navigate(`landlord/properties/${propertyId}/rooms`)} />
              <IconButton label="Tạo phòng mới" icon={Plus} tone="primary" onClick={() => navigate(`landlord/properties/${propertyId}/rooms/new`)} disabled={!property.isActive} />
            </div>
          </div>
          {!property.isActive && <p className="pm-inactive-note"><AlertTriangle size={17} aria-hidden="true" /> Khu trọ đang tạm ngừng. Hãy kích hoạt khu trọ trong phần chỉnh sửa trước khi thêm phòng.</p>}
        </section>
        <ConfirmationToast request={pendingDelete} busy={busy} onCancel={cancelDelete} onConfirm={confirmDelete} />
      </>}
    </div>
  );
}

export default function PropertyManagement({ propertyId, navigate, notify }) {
  return propertyId
    ? <PropertyDetail key={propertyId} propertyId={propertyId} navigate={navigate} notify={notify} />
    : <PropertyList navigate={navigate} notify={notify} />;
}
