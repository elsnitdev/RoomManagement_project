import { useState } from "react";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Building2,
  CalendarDays,
  ChevronDown,
  DoorOpen,
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
import {
  ConfirmationToast,
  Feedback,
  IconButton,
  Pagination,
  Status,
  dateText,
  messageOf,
  moneyText,
} from "./property/ManagementUi";
import { billingApi } from "../services/api/billingApi";
import { PropertyRules } from "./contracts/ContractRules";
import {
  createProperty,
  deleteProperty,
  getProperty,
  listProperties,
  updateProperty,
  listRooms,
  listRoomRows,
} from "../services/api";
import "../styles/property-management.css";

const propertyDraft = {
  name: "",
  address: "",
  description: "",
  isActive: true,
};
function PropertyForm({ initial, onClose, onSaved }) {
  const [draft, setDraft] = useState(
    initial
      ? {
          name: initial.name,
          address: initial.address,
          description: initial.description || "",
          isActive: initial.isActive,
        }
      : propertyDraft,
  );
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
      const payload = {
        name,
        address,
        description: draft.description.trim() || null,
      };
      const result = editing
        ? await updateProperty(initial.id, {
            ...payload,
            isActive: draft.isActive,
            rowVersion: initial.rowVersion,
          })
        : await createProperty(payload);
      onSaved(result);
    } catch (cause) {
      setError(messageOf(cause));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section
      className="pm-form-card content-card"
      aria-labelledby="property-form-title"
    >
      <div className="pm-section-heading">
        <div>
          <span className="pm-eyebrow">THÔNG TIN KHU TRỌ</span>
          <h2 id="property-form-title">
            {editing ? "Chỉnh sửa khu trọ" : "Tạo khu trọ mới"}
          </h2>
        </div>
        <IconButton
          label="Đóng biểu mẫu khu trọ"
          icon={X}
          onClick={onClose}
          disabled={busy}
        />
      </div>
      <form onSubmit={submit} className="pm-form-grid">
        <label className="field">
          <span>Tên khu trọ *</span>
          <input
            autoFocus
            required
            maxLength={150}
            value={draft.name}
            onChange={(event) =>
              setDraft({ ...draft, name: event.target.value })
            }
            placeholder="Ví dụ: Khu trọ An Bình"
          />
        </label>
        <label className="field">
          <span>Địa chỉ *</span>
          <input
            required
            maxLength={300}
            value={draft.address}
            onChange={(event) =>
              setDraft({ ...draft, address: event.target.value })
            }
            placeholder="Số nhà, đường, phường/xã..."
          />
        </label>
        <label className="field pm-span">
          <span>Mô tả</span>
          <textarea
            rows="3"
            maxLength={1000}
            value={draft.description}
            onChange={(event) =>
              setDraft({ ...draft, description: event.target.value })
            }
            placeholder="Thông tin thêm về khu trọ"
          />
        </label>
        {editing && (
          <label className="pm-check pm-span">
            <input
              type="checkbox"
              checked={draft.isActive}
              onChange={(event) =>
                setDraft({ ...draft, isActive: event.target.checked })
              }
            />
            <span>Khu trọ đang hoạt động</span>
          </label>
        )}
        {error && (
          <p className="form-error pm-span" role="alert">
            {error}
          </p>
        )}
        <div className="pm-form-actions pm-span">
          <IconButton
            label="Hủy chỉnh sửa khu trọ"
            icon={X}
            onClick={onClose}
            disabled={busy}
          />
          <IconButton
            type="submit"
            label={
              busy
                ? "Đang lưu khu trọ"
                : editing
                  ? "Lưu thay đổi khu trọ"
                  : "Tạo khu trọ"
            }
            icon={Save}
            tone="primary"
            disabled={busy}
          />
        </div>
      </form>
    </section>
  );
}

function PaymentAccountForm({ property, onSaved }) {
  const initial = {
    bankName: property.bankName || "",
    accountNumber: property.bankAccountNumber || "",
    accountHolder: property.bankAccountHolder || "",
  };
  const [draft, setDraft] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const configured = Boolean(
    initial.bankName && initial.accountNumber && initial.accountHolder,
  );
  const dirty = Object.keys(initial).some((key) => draft[key] !== initial[key]);
  async function submit(event) {
    event.preventDefault();
    const values = {
      bankName: draft.bankName.trim(),
      accountNumber: draft.accountNumber.trim(),
      accountHolder: draft.accountHolder.trim(),
    };
    const filled = Object.values(values).filter(Boolean).length;
    if (filled > 0 && filled < 3) {
      setError(
        "Hãy nhập đủ ngân hàng, số tài khoản và tên chủ tài khoản; hoặc để trống cả ba ô để xóa.",
      );
      return;
    }
    setBusy(true);
    setError("");
    try {
      await billingApi.updatePaymentAccount(property.id, {
        ...values,
        rowVersion: property.rowVersion,
      });
      onSaved();
    } catch (cause) {
      setError(messageOf(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="pm-form-card pm-payment-account content-card"
      aria-labelledby="payment-account-title"
    >
      <div className="pm-section-heading">
        <div>
          <span className="pm-eyebrow">THANH TOÁN / KHU TRỌ</span>
          <h2 id="payment-account-title">Tài khoản nhận chuyển khoản</h2>
        </div>
        <span
          className="pm-payment-account-status"
          data-configured={configured ? "true" : "false"}
        >
          {configured ? "Đã cấu hình" : "Chưa cấu hình"}
        </span>
      </div>
      <p>
        Người thuê thấy tài khoản này trên hóa đơn của khu trọ. Nếu chưa cấu
        hình, họ sẽ được hướng dẫn liên hệ chủ trọ.
      </p>
      <form className="pm-form-grid pm-payment-account-form" onSubmit={submit}>
        <label className="field">
          <span>Ngân hàng</span>
          <input
            maxLength={150}
            value={draft.bankName}
            onChange={(event) =>
              setDraft({ ...draft, bankName: event.target.value })
            }
            placeholder="Tên ngân hàng"
          />
        </label>
        <label className="field">
          <span>Số tài khoản</span>
          <input
            maxLength={50}
            inputMode="numeric"
            value={draft.accountNumber}
            onChange={(event) =>
              setDraft({ ...draft, accountNumber: event.target.value })
            }
            placeholder="Số tài khoản nhận tiền"
          />
        </label>
        <label className="field pm-span">
          <span>Tên chủ tài khoản</span>
          <input
            maxLength={150}
            value={draft.accountHolder}
            onChange={(event) =>
              setDraft({ ...draft, accountHolder: event.target.value })
            }
            placeholder="Tên theo tài khoản ngân hàng"
          />
        </label>
        {error && (
          <p className="form-error pm-span" role="alert">
            {error}
          </p>
        )}
        <div className="pm-form-actions pm-span">
          <button
            type="button"
            disabled={busy || !dirty}
            onClick={() => {
              setDraft(initial);
              setError("");
            }}
          >
            Khôi phục
          </button>
          <button type="submit" disabled={busy || !dirty}>
            {busy ? "Đang lưu…" : "Lưu tài khoản nhận tiền"}
          </button>
        </div>
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
  const [expandedPropertyId, setExpandedPropertyId] = useState(null);
  const [rulesPropertyId, setRulesPropertyId] = useState("");
  const query = useApiQuery(
    `properties:${user?.id}:list:${page}:${search}:${status}`,
    () =>
      listProperties({
        page,
        pageSize: 12,
        search: search || undefined,
        isActive: status === "all" ? undefined : status === "active",
      }),
    { enabled: Boolean(user) },
  );
  const properties = query.data?.data || [];
  const meta = query.data?.meta;
  const rulesProperties = useApiQuery(
    `properties:${user?.id}:rules-selector`,
    async () => {
      const items = [];
      for (let currentPage = 1; ; currentPage += 1) {
        const result = await listProperties({
          page: currentPage,
          pageSize: 100,
        });
        const rows = result.data || [];
        items.push(...rows);
        if (
          !rows.length ||
          (result.meta?.totalPages != null
            ? currentPage >= result.meta.totalPages
            : rows.length < 100)
        )
          return items;
      }
    },
    { enabled: Boolean(user) },
  );

  function applySearch(event) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <div className="pm-page">
      <header className="pm-hero">
        <div>
          <span className="pm-eyebrow">QUẢN LÝ BẤT ĐỘNG SẢN</span>
          <h1>Khu trọ của bạn</h1>
          <p>Quản lý thông tin từng khu trọ và các phòng bên trong.</p>
        </div>
        <IconButton
          label="Thêm khu trọ"
          icon={Plus}
          tone="primary"
          onClick={() => setCreating(true)}
        />
      </header>
      {creating && (
        <PropertyForm
          onClose={() => setCreating(false)}
          onSaved={(created) => {
            notify("Đã tạo khu trọ.");
            navigate(`landlord/properties/${created.id}`);
          }}
        />
      )}
      <section
        className="pm-list-panel content-card"
        aria-label="Danh sách khu trọ"
      >
        <div className="pm-section-heading">
          <div>
            <h2>Danh sách khu trọ</h2>
            <p>
              {meta
                ? `${meta.totalItems} khu trọ trong hệ thống`
                : "Các khu trọ thuộc tài khoản của bạn"}
            </p>
          </div>
        </div>
        <div className="pm-toolbar">
          <form className="pm-search" onSubmit={applySearch} role="search">
            <Search size={18} aria-hidden="true" />
            <input
              aria-label="Tìm khu trọ"
              placeholder="Tìm theo tên hoặc địa chỉ"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
            />
            <IconButton
              type="submit"
              label="Tìm khu trọ"
              icon={Search}
              tone="soft"
            />
          </form>
          <label className="pm-filter">
            <span>Trạng thái</span>
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              <option value="all">Tất cả</option>
              <option value="active">Đang hoạt động</option>
              <option value="inactive">Tạm ngừng</option>
            </select>
          </label>
        </div>
        {query.isLoading ? (
          <div className="pm-loading" role="status">
            Đang tải danh sách khu trọ...
          </div>
        ) : query.error ? (
          <Feedback error={query.error} onRetry={query.reload} />
        ) : properties.length === 0 ? (
          <div className="pm-empty">
            <span className="pm-empty-icon">
              <Building2 size={28} aria-hidden="true" />
            </span>
            <strong>
              {search || status !== "all"
                ? "Không tìm thấy khu trọ phù hợp"
                : "Bạn chưa có khu trọ nào"}
            </strong>
            <p>
              {search || status !== "all"
                ? "Thử thay đổi từ khóa hoặc bộ lọc."
                : "Bắt đầu bằng cách tạo khu trọ đầu tiên."}
            </p>
            {!search && status === "all" && (
              <IconButton
                label="Tạo khu trọ"
                icon={Plus}
                tone="primary"
                onClick={() => setCreating(true)}
              />
            )}
          </div>
        ) : (
          <div className="pm-property-grid">
            {properties.map((property) => {
              const expanded = expandedPropertyId === property.id;
              return (
                <article className="pm-property-card" key={property.id}>
                  <span className="pm-card-icon">
                    <Building2 size={23} aria-hidden="true" />
                  </span>
                  <div className="pm-property-card-main">
                    <h3>{property.name}</h3>
                    <p className="pm-address">
                      <MapPin size={14} aria-hidden="true" />
                      {property.address}
                    </p>
                  </div>
                  <div className="pm-property-card-meta">
                    <Status active={property.isActive} />
                    <span>Tạo ngày {dateText(property.createdAt)}</span>
                  </div>
                  <button
                    type="button"
                    className="pm-text-action"
                    aria-expanded={expanded}
                    aria-controls={`pm-property-preview-${property.id}`}
                    onClick={() =>
                      setExpandedPropertyId(expanded ? null : property.id)
                    }
                  >
                    {expanded ? "Ẩn chi tiết" : "Xem chi tiết"}{" "}
                    <ChevronDown size={17} aria-hidden="true" />
                  </button>
                  <div
                    id={`pm-property-preview-${property.id}`}
                    className={`pm-property-card-reveal${expanded ? " is-open" : ""}`}
                    aria-hidden={!expanded}
                    inert={!expanded}
                  >
                    <div className="pm-property-card-reveal-inner">
                      <div>
                        <span>Mô tả khu trọ</span>
                        <p>
                          {property.description ||
                            "Chưa có mô tả cho khu trọ này."}
                        </p>
                      </div>
                      <button
                        type="button"
                        className="pm-secondary-action"
                        onClick={() =>
                          navigate(`landlord/properties/${property.id}`)
                        }
                      >
                        Quản lý khu trọ, hóa đơn và phòng{" "}
                        <ArrowRight size={16} aria-hidden="true" />
                      </button>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        )}
        <Pagination page={page} meta={meta} onChange={setPage} />
      </section>
      <section
        className="pm-list-panel content-card"
        aria-labelledby="property-rules-select-title"
      >
        <div className="pm-section-heading">
          <div>
            <h2 id="property-rules-select-title">Điều khoản khu trọ</h2>
            <p>
              Chọn khu trọ để xem, tạo hoặc cập nhật điều khoản đang áp dụng.
            </p>
          </div>
        </div>
        {rulesProperties.isLoading ? (
          <p role="status">Đang tải khu trọ…</p>
        ) : rulesProperties.error ? (
          <Feedback
            error={rulesProperties.error}
            onRetry={rulesProperties.reload}
          />
        ) : (
          <label className="pm-filter">
            <span>Khu trọ</span>
            <select
              value={rulesPropertyId}
              onChange={(event) => setRulesPropertyId(event.target.value)}
            >
              <option value="">Chọn khu trọ</option>
              {(rulesProperties.data || []).map((property) => (
                <option key={property.id} value={property.id}>
                  {property.name}
                  {property.isActive ? "" : " (tạm ngừng)"}
                </option>
              ))}
            </select>
          </label>
        )}
        {rulesPropertyId && (
          <PropertyRules
            key={rulesPropertyId}
            propertyId={rulesPropertyId}
            navigate={navigate}
            embedded
          />
        )}
      </section>
    </div>
  );
}

function PropertyBillingSummary({ propertyId, navigate }) {
  const { user } = useAuth();
  const ratesQuery = useApiQuery(
    `property-billing-rates:${user?.id}:${propertyId}`,
    () => billingApi.rates(propertyId),
    { enabled: Boolean(user) },
  );
  const invoicesQuery = useApiQuery(
    `property-billing-invoices:${user?.id}:${propertyId}`,
    () => billingApi.invoices(propertyId, 1),
    { enabled: Boolean(user) },
  );
  const today = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
  const rates = ratesQuery.data || [];
  const currentRate = (type) =>
    rates
      .filter(
        (rate) =>
          rate.type === type &&
          rate.effectiveFrom <= today &&
          (!rate.effectiveTo || rate.effectiveTo >= today),
      )
      .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0];
  const electricity = currentRate("Electricity");
  const water = currentRate("Water");
  const billingPath = `landlord/billing/rooms/${propertyId}`;

  return (
    <section
      className="pm-billing-panel content-card"
      aria-labelledby="pm-billing-title"
    >
      <div className="pm-section-heading">
        <div>
          <span className="pm-eyebrow">DỮ LIỆU LẬP HÓA ĐƠN</span>
          <h2 id="pm-billing-title">Thông tin tính tiền</h2>
          <p>
            Đơn giá đang áp dụng tại khu trọ và dữ liệu cần nhập cho từng phòng.
          </p>
        </div>
        <button
          type="button"
          className="pm-text-action"
          onClick={() => navigate(billingPath)}
        >
          Quản lý hóa đơn <ArrowRight size={17} aria-hidden="true" />
        </button>
      </div>
      <div className="pm-billing-grid">
        <div className="pm-billing-stat">
          <span>Giá điện hiện tại</span>
          <strong>
            {ratesQuery.isLoading
              ? "Đang tải..."
              : ratesQuery.error
                ? "Không tải được"
                : electricity
                  ? `${moneyText(electricity.unitPrice)} / ${electricity.unit}`
                  : "Chưa thiết lập"}
          </strong>
        </div>
        <div className="pm-billing-stat">
          <span>Giá nước hiện tại</span>
          <strong>
            {ratesQuery.isLoading
              ? "Đang tải..."
              : ratesQuery.error
                ? "Không tải được"
                : water
                  ? `${moneyText(water.unitPrice)} / ${water.unit}`
                  : "Chưa thiết lập"}
          </strong>
        </div>
        <div className="pm-billing-stat">
          <span>Hóa đơn đã tạo</span>
          <strong>
            {invoicesQuery.isLoading
              ? "Đang tải..."
              : invoicesQuery.error
                ? "Không tải được"
                : (invoicesQuery.data?.meta?.totalItems ?? "—")}
          </strong>
        </div>
      </div>
      {(ratesQuery.error || invoicesQuery.error) && (
        <div className="pm-inline-error" role="alert">
          Không tải được một phần dữ liệu hóa đơn.{" "}
          <button
            type="button"
            onClick={() => {
              ratesQuery.reload();
              invoicesQuery.reload();
            }}
          >
            Thử lại
          </button>
        </div>
      )}
      <div className="pm-billing-bottom">
        <p>
          Nhập chỉ số điện nước, gán dịch vụ cho phòng rồi lập hóa đơn theo kỳ.
        </p>
        <button
          type="button"
          className="pm-secondary-action"
          onClick={() => navigate(billingPath)}
        >
          Nhập dữ liệu hóa đơn <ArrowRight size={16} aria-hidden="true" />
        </button>
      </div>
    </section>
  );
}

function PropertyRoomList({ propertyId, property, navigate }) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [rowId, setRowId] = useState("");
  const [floor, setFloor] = useState("");
  const roomsQuery = useApiQuery(
    `property-rooms:${user?.id}:${propertyId}:${page}:${search}:${status}:${rowId}:${floor}`,
    () =>
      listRooms(propertyId, {
        page,
        pageSize: 10,
        search: search || undefined,
        status: status === "all" ? undefined : status,
        rowId: rowId || undefined,
        floor: floor || undefined,
      }),
    { enabled: Boolean(user) },
  );
  const rowsQuery = useApiQuery(
    `property-room-rows:${user?.id}:${propertyId}`,
    () => listRoomRows(propertyId),
    { enabled: Boolean(user) },
  );
  const rooms = roomsQuery.data?.data || [];
  const meta = roomsQuery.data?.meta;
  const roomRows = rowsQuery.data || [];
  const rowNames = new Map(
    roomRows.map((row) => [row.id, row.code || row.name]),
  );
  const filtering = Boolean(search || status !== "all" || rowId || floor);

  function applySearch(event) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <section
      className="pm-room-panel content-card"
      aria-labelledby="pm-rooms-title"
    >
      <div className="pm-section-heading pm-room-heading">
        <div>
          <span className="pm-eyebrow">PHÒNG THUỘC KHU TRỌ</span>
          <h2 id="pm-rooms-title">Danh sách phòng</h2>
          <p>
            {meta
              ? `${meta.totalItems} phòng${filtering ? " phù hợp" : ""}`
              : "Tìm kiếm và lọc phòng trong khu trọ."}
          </p>
        </div>
        <div className="pm-heading-actions">
          <button
            type="button"
            className="pm-secondary-action"
            onClick={() => navigate(`landlord/properties/${propertyId}/rooms`)}
          >
            Quản lý phòng <ArrowRight size={16} aria-hidden="true" />
          </button>
          <button
            type="button"
            className="pm-primary-action"
            onClick={() =>
              navigate(`landlord/properties/${propertyId}/rooms/new`)
            }
            disabled={!property.isActive}
          >
            <Plus size={16} aria-hidden="true" /> Tạo phòng
          </button>
        </div>
      </div>
      <div className="pm-toolbar">
        <form className="pm-search" onSubmit={applySearch} role="search">
          <Search size={18} aria-hidden="true" />
          <input
            aria-label="Tìm phòng"
            placeholder="Tìm theo mã hoặc tên phòng"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
          />
          <IconButton
            type="submit"
            label="Tìm phòng"
            icon={Search}
            tone="soft"
          />
        </form>
        <label className="pm-filter">
          <span>Trạng thái</span>
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
            }}
          >
            <option value="all">Tất cả</option>
            <option value="Available">Còn trống</option>
            <option value="Occupied">Đã thuê</option>
            <option value="Maintenance">Bảo trì</option>
            <option value="Inactive">Ngừng sử dụng</option>
          </select>
        </label>
        <label className="pm-filter">
          <span>Dãy</span>
          <select
            value={rowId}
            onChange={(event) => {
              setRowId(event.target.value);
              setPage(1);
            }}
          >
            <option value="">Tất cả dãy</option>
            {roomRows.map((row) => (
              <option key={row.id} value={row.id}>
                {row.code} · {row.name}
              </option>
            ))}
          </select>
        </label>
        <label className="pm-filter">
          <span>Tầng</span>
          <input
            type="number"
            min="1"
            step="1"
            placeholder="Tất cả"
            value={floor}
            onChange={(event) => {
              setFloor(event.target.value);
              setPage(1);
            }}
          />
        </label>
      </div>
      {rowsQuery.error && (
        <div className="pm-inline-error" role="alert">
          Không tải được danh sách dãy.{" "}
          <button type="button" onClick={rowsQuery.reload}>
            Thử lại
          </button>
        </div>
      )}
      {roomsQuery.isLoading ? (
        <div className="pm-loading" role="status">
          Đang tải danh sách phòng...
        </div>
      ) : roomsQuery.error ? (
        <Feedback error={roomsQuery.error} onRetry={roomsQuery.reload} />
      ) : rooms.length === 0 ? (
        <div className="pm-empty">
          <span className="pm-empty-icon">
            <DoorOpen size={28} aria-hidden="true" />
          </span>
          <strong>
            {filtering
              ? "Không tìm thấy phòng phù hợp"
              : "Khu trọ chưa có phòng"}
          </strong>
          <p>
            {filtering
              ? "Thử thay đổi từ khóa hoặc bộ lọc."
              : "Tạo phòng đầu tiên để bắt đầu quản lý."}
          </p>
        </div>
      ) : (
        <div className="pm-room-table-wrap">
          <table className="pm-room-table">
            <thead>
              <tr>
                <th scope="col">Phòng</th>
                <th scope="col">Dãy · Tầng</th>
                <th scope="col">Diện tích</th>
                <th scope="col">Giá/tháng</th>
                <th scope="col">Trạng thái</th>
                <th scope="col">
                  <span className="sr-only">Thao tác</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {rooms.map((room) => (
                <tr key={room.id}>
                  <td>
                    <strong>{room.roomNumber}</strong>
                    <small>{room.roomName || "Chưa đặt tên"}</small>
                  </td>
                  <td>
                    {rowNames.get(room.rowId) || "—"} ·{" "}
                    {room.floor == null ? "—" : `Tầng ${room.floor}`}
                  </td>
                  <td>{room.area == null ? "—" : `${room.area} m²`}</td>
                  <td>{moneyText(room.basePrice)}</td>
                  <td>
                    <Status roomStatus={room.status} />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="pm-table-link"
                      onClick={() =>
                        navigate(
                          `landlord/properties/${propertyId}/rooms/${room.id}`,
                        )
                      }
                    >
                      Xem phòng <ArrowRight size={14} aria-hidden="true" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Pagination page={page} meta={meta} onChange={setPage} />
    </section>
  );
}

function PropertyDetail({ propertyId, navigate, notify }) {
  const { user } = useAuth();
  const [showDetails, setShowDetails] = useState(false);
  const [editingProperty, setEditingProperty] = useState(false);
  const [actionError, setActionError] = useState("");
  const [busy, setBusy] = useState(false);
  const [pendingDelete, setPendingDelete] = useState(null);
  const propertyQuery = useApiQuery(
    `properties:${user?.id}:detail:${propertyId}`,
    () => getProperty(propertyId),
    { enabled: Boolean(user) },
  );
  const property = propertyQuery.data?.data;

  function askDeleteProperty(event) {
    setActionError("");
    setPendingDelete({
      kind: "property",
      propertyName: property.name,
      trigger: event.currentTarget,
      error: "",
    });
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
        <IconButton
          label="Quay về danh sách khu trọ"
          icon={ArrowLeft}
          onClick={() => navigate("landlord/properties")}
        />
        <span>Danh sách khu trọ / Chi tiết</span>
      </div>
      {propertyQuery.isLoading ? (
        <div className="pm-loading" role="status">
          Đang tải khu trọ...
        </div>
      ) : propertyQuery.error ? (
        <Feedback error={propertyQuery.error} onRetry={propertyQuery.reload} />
      ) : (
        property && (
          <>
            <header className="pm-detail-hero">
              <div className="pm-detail-hero-main">
                <span className="pm-detail-icon">
                  <Building2 size={30} aria-hidden="true" />
                </span>
                <div className="pm-detail-title">
                  <span className="pm-eyebrow">
                    KHU TRỌ / THÔNG TIN CHI TIẾT
                  </span>
                  <h1>{property.name}</h1>
                  <p>
                    <MapPin size={16} aria-hidden="true" />
                    {property.address}
                  </p>
                </div>
              </div>
              <div className="pm-detail-hero-actions">
                <Status active={property.isActive} />
                <div className="pm-heading-actions">
                  <IconButton
                    label="Chỉnh sửa khu trọ"
                    icon={Pencil}
                    onClick={() => {
                      setEditingProperty(true);
                      setActionError("");
                    }}
                    disabled={busy}
                  />
                  <IconButton
                    label="Xóa khu trọ"
                    icon={Trash2}
                    tone="danger"
                    onClick={askDeleteProperty}
                    disabled={busy}
                  />
                </div>
              </div>
            </header>

            {actionError && (
              <p className="form-error pm-action-error" role="alert">
                {actionError}
              </p>
            )}
            {editingProperty && (
              <PropertyForm
                initial={property}
                onClose={() => setEditingProperty(false)}
                onSaved={() => {
                  setEditingProperty(false);
                  propertyQuery.reload();
                  notify("Đã cập nhật khu trọ.");
                }}
              />
            )}

            <button
              type="button"
              className="pm-detail-toggle"
              aria-expanded={showDetails}
              aria-controls="pm-property-details"
              onClick={() => setShowDetails((value) => !value)}
            >
              <span>
                {showDetails ? "Ẩn chi tiết khu trọ" : "Xem chi tiết khu trọ"}
              </span>
              <ChevronDown size={18} aria-hidden="true" />
            </button>
            <div
              id="pm-property-details"
              className={`pm-details-reveal${showDetails ? " is-open" : ""}`}
              aria-hidden={!showDetails}
              inert={!showDetails}
            >
              <div className="pm-details-reveal-inner">
                <section
                  className="pm-detail-card content-card"
                  aria-labelledby="pm-info-title"
                >
                  <div className="pm-section-heading">
                    <div>
                      <span className="pm-eyebrow">TỔNG QUAN</span>
                      <h2 id="pm-info-title">Thông tin khu trọ</h2>
                    </div>
                  </div>
                  <div className="pm-detail-field">
                    <span className="pm-detail-field-icon">
                      <MapPin size={18} aria-hidden="true" />
                    </span>
                    <div>
                      <span>Địa chỉ</span>
                      <strong>{property.address}</strong>
                    </div>
                  </div>
                  <div className="pm-detail-field pm-detail-field--description">
                    <span className="pm-detail-field-icon">
                      <Building2 size={18} aria-hidden="true" />
                    </span>
                    <div>
                      <span>Mô tả</span>
                      <p>
                        {property.description ||
                          "Chưa có mô tả cho khu trọ này."}
                      </p>
                    </div>
                  </div>
                </section>
              </div>
            </div>

            <section className="pm-billing-card content-card">
              <div className="pm-section-heading">
                <div>
                  <span className="pm-eyebrow">QUY ĐỊNH CHUNG</span>
                  <h2>Nội quy & điều khoản khu trọ</h2>
                  <p>
                    Thiết lập nội dung áp dụng cho các hợp đồng trong khu trọ.
                  </p>
                </div>
                <button
                  type="button"
                  className="pm-secondary-action"
                  onClick={() =>
                    navigate(`landlord/properties/${propertyId}/rules`)
                  }
                >
                  Quản lý nội quy <ArrowRight size={16} aria-hidden="true" />
                </button>
              </div>
            </section>
            <PropertyBillingSummary
              propertyId={propertyId}
              navigate={navigate}
            />
            <PaymentAccountForm
              key={property.rowVersion}
              property={property}
              onSaved={() => {
                propertyQuery.reload();
                notify("Đã cập nhật tài khoản nhận chuyển khoản.");
              }}
            />
            <section
              className="pm-management-panel content-card"
              aria-labelledby="pm-management-title"
            >
              <div className="pm-section-heading">
                <div>
                  <span className="pm-eyebrow">QUẢN LÝ CHUNG</span>
                  <h2 id="pm-management-title">Vận hành khu trọ</h2>
                </div>
              </div>
              <div className="pm-management-grid">
                <div>
                  <span>Trạng thái</span>
                  <Status active={property.isActive} />
                </div>
                <div>
                  <span>Ngày tạo</span>
                  <strong>
                    <CalendarDays size={16} aria-hidden="true" />
                    {dateText(property.createdAt)}
                  </strong>
                </div>
                <div>
                  <span>Cập nhật gần nhất</span>
                  <strong>
                    <RotateCcw size={16} aria-hidden="true" />
                    {dateText(property.updatedAt || property.createdAt)}
                  </strong>
                </div>
              </div>
              {!property.isActive && (
                <p className="pm-inactive-note">
                  <AlertTriangle size={17} aria-hidden="true" /> Khu trọ đang
                  tạm ngừng. Hãy kích hoạt khu trọ trước khi thêm phòng.
                </p>
              )}
            </section>
            <PropertyRoomList
              propertyId={propertyId}
              property={property}
              navigate={navigate}
            />
            <ConfirmationToast
              request={pendingDelete}
              busy={busy}
              onCancel={cancelDelete}
              onConfirm={confirmDelete}
            />
          </>
        )
      )}
    </div>
  );
}

export default function PropertyManagement({ propertyId, navigate, notify }) {
  return propertyId ? (
    <PropertyDetail
      key={propertyId}
      propertyId={propertyId}
      navigate={navigate}
      notify={notify}
    />
  ) : (
    <PropertyList navigate={navigate} notify={notify} />
  );
}
