import { useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  ArrowRight,
  Bell,
  Building2,
  Check,
  CheckCheck,
  CheckCircle2,
  CircleHelp,
  ClipboardList,
  Copy,
  FileText,
  Home,
  MapPin,
  ReceiptText,
  Send,
  UserRound,
  Wrench,
} from "lucide-react";
import { initialUsers } from "./mockData";
import {
  tenantMockInvoiceItems,
  tenantMockNotices,
  tenantMockProfiles,
} from "./tenantMockData";
import { contractApi } from "./services/api/contractApi";
import { requestApi } from "./services/api/requestApi";
import { TenantRules } from "./components/contracts/ContractRules";
import "./components/contracts/contracts.css";
import "./Tenant.css";

const money = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
const balanceOf = (invoice) =>
  Math.max(0, Number(invoice.amount || 0) - Number(invoice.paid || 0));
const dateOf = (value) => {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value || "");
  return match
    ? new Date(Number(match[3]), Number(match[2]) - 1, Number(match[1]))
    : null;
};
const invoiceStatus = (invoice) => {
  if (balanceOf(invoice) === 0)
    return { label: "Đã thanh toán", tone: "success" };
  const due = dateOf(invoice.due);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  if (due && due < today) return { label: "Quá hạn", tone: "danger" };
  return Number(invoice.paid || 0) > 0
    ? { label: "Thanh toán một phần", tone: "warning" }
    : { label: "Chưa thanh toán", tone: "neutral" };
};
const requestLabels = {
  new: "Đã gửi",
  progress: "Đang xử lý",
  done: "Đã hoàn tất",
};
async function copyCode(code, notify) {
  try {
    await navigator.clipboard.writeText(code);
    notify?.(`Đã sao chép mã ${code}.`);
  } catch {
    notify?.("Không sao chép được. Vui lòng chọn và sao chép mã thủ công.");
  }
}
const pageMeta = {
  overview: [
    "TỔNG QUAN",
    "Chỗ ở của bạn",
    "Những thông tin cần biết trong một nơi.",
  ],
  room: [
    "CHỖ Ở HIỆN TẠI",
    "Phòng của tôi",
    "Thông tin phòng và khu trọ bạn đang ở.",
  ],
  billing: [
    "TÀI CHÍNH",
    "Hóa đơn của tôi",
    "Theo dõi các kỳ thanh toán và số tiền còn lại.",
  ],
  contracts: [
    "HỢP ĐỒNG",
    "Hợp đồng thuê",
    "Thông tin thỏa thuận thuê phòng hiện tại.",
  ],
  requests: [
    "HỖ TRỢ",
    "Yêu cầu của tôi",
    "Gửi vấn đề cần hỗ trợ và theo dõi tiến độ.",
  ],
  notices: [
    "CẦN CHÚ Ý",
    "Thông báo quan trọng",
    "Các việc cần chú ý liên quan đến chỗ ở.",
  ],
  profile: [
    "TÀI KHOẢN",
    "Thông tin cá nhân",
    "Kiểm tra thông tin tài khoản của bạn.",
  ],
};

function Header({ page, name }) {
  const [eyebrow, title, description] = pageMeta[page] || pageMeta.overview;
  return (
    <header className="tn-heading">
      <span>{eyebrow}</span>
      <h1>{page === "overview" && name ? `Chào ${name}` : title}</h1>
      <p>{description}</p>
    </header>
  );
}

function Empty({ icon: Icon = CircleHelp, title, description }) {
  return (
    <div className="tn-empty">
      <Icon size={25} aria-hidden="true" />
      <strong>{title}</strong>
      <p>{description}</p>
    </div>
  );
}

function StateTag({ label, tone = "neutral" }) {
  return <span className={`tn-tag tn-tag--${tone}`}>{label}</span>;
}

function LinkButton({ children, onClick, secondary = false }) {
  return (
    <button
      type="button"
      className={`tn-action${secondary ? " tn-action--secondary" : ""}`}
      onClick={onClick}
    >
      {children}
      <ArrowRight size={15} aria-hidden="true" />
    </button>
  );
}

function Field({ label, value }) {
  return (
    <div className="tn-field">
      <dt>{label}</dt>
      <dd>{value || "Chưa cập nhật"}</dd>
    </div>
  );
}

function Overview({
  name,
  room,
  property,
  currentInvoice,
  openRequests,
  notices,
  navigate,
}) {
  return (
    <>
      <Header page="overview" name={name} />
      <div className="tn-overview-grid">
        <section className="tn-card tn-home-card">
          <div className="tn-card-kicker">
            <Home size={16} aria-hidden="true" /> CHỖ Ở HIỆN TẠI
          </div>
          {room ? (
            <>
              <div className="tn-home-identity">
                <strong>Phòng {room.number}</strong>
                <StateTag label="Đang ở" tone="success" />
              </div>
              <p className="tn-home-property">
                {property?.name || "Khu trọ chưa cập nhật"}
              </p>
              <p className="tn-home-address">
                <MapPin size={15} aria-hidden="true" />
                {property?.address || "Địa chỉ chưa cập nhật"}
              </p>
              <div className="tn-home-facts">
                <span>
                  Tiền thuê <strong>{money(room.price)}/tháng</strong>
                </span>
                <span>
                  Diện tích{" "}
                  <strong>
                    {room.area ? `${room.area} m²` : "Chưa cập nhật"}
                  </strong>
                </span>
              </div>
              <LinkButton onClick={() => navigate("tenant/room")}>
                Xem chỗ ở
              </LinkButton>
            </>
          ) : (
            <Empty
              icon={Home}
              title="Chưa có thông tin chỗ ở"
              description="Tài khoản này chưa được liên kết với phòng trong dữ liệu hiện tại."
            />
          )}
        </section>
        <section className="tn-card tn-attention-card">
          <div className="tn-card-kicker">
            <Bell size={16} aria-hidden="true" /> VIỆC CẦN CHÚ Ý
          </div>
          {currentInvoice && balanceOf(currentInvoice) > 0 ? (
            <div className="tn-attention-item">
              <span>Hóa đơn kỳ {currentInvoice.month}</span>
              <strong>{money(balanceOf(currentInvoice))}</strong>
              <small>Hạn thanh toán {currentInvoice.due}</small>
            </div>
          ) : (
            <div className="tn-attention-item tn-attention-item--clear">
              {currentInvoice ? (
                <CheckCircle2 size={18} aria-hidden="true" />
              ) : (
                <ReceiptText size={18} aria-hidden="true" />
              )}
              <span>
                {currentInvoice
                  ? "Không có hóa đơn còn phải thu."
                  : "Mở mục Hóa đơn để xem số dư mới nhất."}
              </span>
            </div>
          )}
          <div className="tn-attention-bottom">
            <span>{openRequests} yêu cầu đang theo dõi</span>
            <span>{notices.length} thông tin cần chú ý</span>
          </div>
          <LinkButton secondary onClick={() => navigate("tenant/notices")}>
            Xem thông báo
          </LinkButton>
        </section>
      </div>
      <section className="tn-quick-section">
        <h2>Truy cập nhanh</h2>
        <div className="tn-quick-grid">
          <button type="button" onClick={() => navigate("tenant/billing")}>
            <ReceiptText size={19} aria-hidden="true" />
            <span>
              <strong>Hóa đơn</strong>
              <small>Xem kỳ thu và số dư</small>
            </span>
            <ArrowRight size={16} aria-hidden="true" />
          </button>
          <button type="button" onClick={() => navigate("tenant/contracts")}>
            <FileText size={19} aria-hidden="true" />
            <span>
              <strong>Hợp đồng</strong>
              <small>Thời hạn và tiền thuê</small>
            </span>
            <ArrowRight size={16} aria-hidden="true" />
          </button>
          <button type="button" onClick={() => navigate("tenant/requests")}>
            <Wrench size={19} aria-hidden="true" />
            <span>
              <strong>Gửi yêu cầu</strong>
              <small>Liên hệ khi cần hỗ trợ</small>
            </span>
            <ArrowRight size={16} aria-hidden="true" />
          </button>
        </div>
      </section>
    </>
  );
}

function RoomPage({ room, property, navigate }) {
  return (
    <>
      <Header page="room" />
      {room ? (
        <div className="tn-room-grid">
          <section className="tn-card tn-room-main">
            <div className="tn-room-photo">
              <img src="/room-studio.jpg" alt="Ảnh minh họa phòng trọ" />
              <span>Ảnh minh họa</span>
            </div>
            <div className="tn-room-content">
              <div className="tn-section-head">
                <div>
                  <span>PHÒNG ĐANG Ở</span>
                  <h2>Phòng {room.number}</h2>
                </div>
                <StateTag label="Đang ở" tone="success" />
              </div>
              <p className="tn-room-address">
                <Building2 size={16} aria-hidden="true" />
                {property?.name || "Khu trọ chưa cập nhật"}
              </p>
              <p className="tn-room-address">
                <MapPin size={16} aria-hidden="true" />
                {property?.address || "Địa chỉ chưa cập nhật"}
              </p>
              <dl className="tn-facts">
                <Field
                  label="Tầng"
                  value={
                    room.floor === 0
                      ? "Tầng trệt"
                      : room.floor != null
                        ? `Tầng ${room.floor}`
                        : ""
                  }
                />
                <Field
                  label="Diện tích"
                  value={room.area ? `${room.area} m²` : ""}
                />
                <Field label="Giá thuê/tháng" value={money(room.price)} />
                <Field label="Kỳ thanh toán tiếp theo" value={room.due} />
              </dl>
              {room.description && (
                <p className="tn-room-description">{room.description}</p>
              )}
            </div>
          </section>
          <aside className="tn-card tn-room-side">
            <div className="tn-card-kicker">
              <UserRound size={16} aria-hidden="true" /> NGƯỜI ĐANG Ở
            </div>
            {room.occupants?.length ? (
              <ul className="tn-occupants">
                {room.occupants.map((person) => (
                  <li key={person}>{person}</li>
                ))}
              </ul>
            ) : (
              <p>{room.tenant || "Chưa có danh sách người ở."}</p>
            )}
            <div className="tn-room-actions">
              <LinkButton
                secondary
                onClick={() => navigate("tenant/contracts")}
              >
                Xem hợp đồng
              </LinkButton>
              <LinkButton secondary onClick={() => navigate("tenant/billing")}>
                Xem hóa đơn
              </LinkButton>
              <LinkButton onClick={() => navigate("tenant/requests")}>
                Gửi yêu cầu
              </LinkButton>
            </div>
          </aside>
        </div>
      ) : (
        <section className="tn-card">
          <Empty
            icon={Home}
            title="Chưa có phòng được liên kết"
            description="Thông tin chỗ ở sẽ xuất hiện khi tài khoản được liên kết với phòng."
          />
        </section>
      )}
    </>
  );
}

function BillingPage({ invoices, notify }) {
  const [selectedId, setSelectedId] = useState(null);
  const [filter, setFilter] = useState("all");
  const filtered = invoices.filter(
    (invoice) =>
      filter === "all" ||
      (filter === "open" ? balanceOf(invoice) > 0 : balanceOf(invoice) === 0),
  );
  const selected =
    filtered.find((item) => item.id === selectedId) || filtered[0];
  const items = selected?.items?.length
    ? selected.items
    : tenantMockInvoiceItems[selected?.id] || [];
  const filters = [
    { id: "all", label: "Tất cả", count: invoices.length },
    {
      id: "open",
      label: "Còn phải thu",
      count: invoices.filter((item) => balanceOf(item) > 0).length,
    },
    {
      id: "paid",
      label: "Đã thanh toán",
      count: invoices.filter((item) => balanceOf(item) === 0).length,
    },
  ];
  return (
    <>
      <Header page="billing" />
      <div className="tn-billing-layout">
        <section className="tn-card tn-billing-list">
          <div className="tn-section-head">
            <div>
              <span>CÁC KỲ HÓA ĐƠN</span>
              <h2>Danh sách hóa đơn</h2>
            </div>
            <small>{invoices.length} kỳ</small>
          </div>
          <div className="tn-filter-bar" role="group" aria-label="Lọc hóa đơn">
            {filters.map((item) => (
              <button
                key={item.id}
                type="button"
                className={filter === item.id ? "is-active" : ""}
                onClick={() => {
                  setFilter(item.id);
                  setSelectedId(null);
                }}
                aria-pressed={filter === item.id}
              >
                {item.label}
                <span>{item.count}</span>
              </button>
            ))}
          </div>
          {filtered.length ? (
            <>
              <div className="tn-bill-head" aria-hidden="true">
                <span>Kỳ / Mã</span>
                <span>Hạn thu</span>
                <span>Tổng tiền</span>
                <span>Còn thu</span>
              </div>
              <ul className="tn-bills">
                {filtered.map((invoice) => {
                  const status = invoiceStatus(invoice);
                  return (
                    <li key={invoice.id}>
                      <button
                        type="button"
                        className={
                          selected?.id === invoice.id ? "is-selected" : ""
                        }
                        onClick={() => setSelectedId(invoice.id)}
                        aria-pressed={selected?.id === invoice.id}
                      >
                        <span>
                          <strong>Kỳ {invoice.month}</strong>
                          <small>{invoice.id}</small>
                        </span>
                        <span>{invoice.due}</span>
                        <span>{money(invoice.amount)}</span>
                        <span>
                          <strong>{money(balanceOf(invoice))}</strong>
                          <StateTag {...status} />
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ul>
            </>
          ) : (
            <Empty
              icon={ReceiptText}
              title="Không có hóa đơn phù hợp"
              description="Chọn trạng thái khác để xem các kỳ hóa đơn."
            />
          )}
        </section>
        <aside className="tn-card tn-bill-detail">
          <div className="tn-section-head">
            <div>
              <span>CHI TIẾT HÓA ĐƠN</span>
              <h2>{selected?.id || "Chưa có hóa đơn"}</h2>
            </div>
            {selected && <StateTag {...invoiceStatus(selected)} />}
          </div>
          {selected ? (
            <>
              <div className="tn-bill-period">
                Phòng {selected.room} · Kỳ {selected.month}
              </div>
              <dl className="tn-bill-totals">
                <Field label="Tổng hóa đơn" value={money(selected.amount)} />
                <Field label="Đã ghi nhận" value={money(selected.paid)} />
                <Field
                  label="Còn phải thu"
                  value={money(balanceOf(selected))}
                />
                <Field label="Hạn thanh toán" value={selected.due} />
              </dl>
              {items.length > 0 && (
                <div className="tn-bill-items">
                  <h3>Các khoản thu</h3>
                  {items.map((item) => (
                    <div key={item.id}>
                      <span>{item.description}</span>
                      <strong>{money(item.amount)}</strong>
                    </div>
                  ))}
                </div>
              )}
              {selected.adjustments?.length > 0 && (
                <div className="tn-bill-adjustments">
                  <h3>Điều chỉnh</h3>
                  {selected.adjustments.map((item) => (
                    <p key={item.id}>
                      {item.reason} · {item.direction === "add" ? "+" : "−"}
                      {money(item.amount)}
                    </p>
                  ))}
                </div>
              )}
              <button
                type="button"
                className="tn-inline-action"
                onClick={() => copyCode(selected.id, notify)}
              >
                <Copy size={14} aria-hidden="true" /> Sao chép mã hóa đơn
              </button>
              <p className="tn-small-note">
                Số liệu đang dùng dữ liệu mẫu, chưa kết nối lịch sử giao dịch.
              </p>
            </>
          ) : (
            <p className="tn-small-note">Chọn một hóa đơn để xem thông tin.</p>
          )}
        </aside>
      </div>
    </>
  );
}

function ContractsPage({ user, notify }) {
  const [contracts, setContracts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [confirmBusyId, setConfirmBusyId] = useState(null);
  const [agreedMap, setAgreedMap] = useState({});
  const [offerError, setOfferError] = useState("");
  const formatDate = (value) =>
    value
      ? new Intl.DateTimeFormat("vi-VN").format(new Date(`${value}T12:00:00`))
      : "Không xác định thời hạn";
  const statusText = {
    PendingTenantConfirmation: "Chờ bạn xác nhận",
    PendingOperatorConfirmation: "Chờ chủ trọ đối ký",
    Active: "Đang hiệu lực",
    PendingMoveOut: "Chờ bàn giao",
    PendingSettlement: "Chờ thanh toán và tất toán",
    Expired: "Hết hạn",
    Terminated: "Đã kết thúc",
    Cancelled: "Đã hủy",
  };
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    contractApi
      .mine(controller.signal)
      .then((response) => setContracts(response.data || []))
      .catch((e) => {
        if (!controller.signal.aborted)
          setError(e.message || "Không thể tải hợp đồng.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [revision]);
  return (
    <>
      <Header page="contracts" />
      <TenantRules />
      {loading ? (
        <section className="tn-card tn-contract-state" role="status">
          <FileText size={28} aria-hidden="true" />
          <strong>Đang tải hợp đồng…</strong>
        </section>
      ) : error ? (
        <section className="tn-card tn-contract-state" role="alert">
          <AlertCircle size={28} aria-hidden="true" />
          <strong>Không thể tải hợp đồng</strong>
          <p>{error}</p>
          <button
            type="button"
            className="tn-action tn-action--secondary"
            onClick={() => setRevision((value) => value + 1)}
          >
            Thử lại
          </button>
        </section>
      ) : contracts.length ? (
        <div className="tn-contract-layout">
          <div className="tn-contract-intro">
            <div>
              <span>THÔNG TIN THUÊ PHÒNG</span>
              <h2>Hợp đồng của bạn</h2>
              <p>
                Kiểm tra thời hạn, tiền thuê và những người cùng ở trong phòng.
              </p>
            </div>
            <strong>{contracts.length} hợp đồng</strong>
          </div>
          <div className="tn-contract-cards">
            {contracts.map((contract) => (
              <article className="tn-card tn-contract-card" key={contract.id}>
                <div className="tn-contract-card-head">
                  <div>
                    <span className="tn-contract-kicker">
                      MÃ HỢP ĐỒNG · {contract.contractCode}
                    </span>
                    <h3>Phòng {contract.roomNumber}</h3>
                  </div>
                  <span
                    className={`contract-status contract-status--${contract.status?.toLowerCase()}`}
                  >
                    {statusText[contract.status] || contract.status}
                  </span>
                </div>
                <div className="tn-contract-amounts">
                  <div>
                    <small>TIỀN THUÊ HÀNG THÁNG</small>
                    <strong>{money(contract.monthlyRent)}</strong>
                  </div>
                  <div>
                    <small>TIỀN CỌC</small>
                    <strong>{money(contract.depositAmount)}</strong>
                  </div>
                </div>
                <dl className="tn-contract-facts">
                  <div>
                    <dt>Ngày bắt đầu</dt>
                    <dd>{formatDate(contract.startDate)}</dd>
                  </div>
                  <div>
                    <dt>Ngày kết thúc dự kiến</dt>
                    <dd>{formatDate(contract.endDate)}</dd>
                  </div>
                  {contract.closedOn && (
                    <div>
                      <dt>Kết thúc thực tế</dt>
                      <dd>{formatDate(contract.closedOn)}</dd>
                    </div>
                  )}
                  <div>
                    <dt>Hạn thanh toán</dt>
                    <dd>
                      {contract.paymentDueDay
                        ? `Ngày ${contract.paymentDueDay} hằng tháng`
                        : "Chưa thiết lập"}
                    </dd>
                  </div>
                </dl>
                <div className="tn-contract-people">
                  <h4>
                    <UserRound size={17} aria-hidden="true" /> Thành viên hợp
                    đồng{" "}
                    <span>
                      {(contract.members ?? contract.tenants ?? []).length}
                    </span>
                  </h4>
                  {(contract.members ?? contract.tenants ?? []).length ? (
                    <ul>
                      {(contract.members ?? contract.tenants).map((tenant) => (
                        <li key={tenant.id}>
                          <span
                            className="tn-contract-avatar"
                            aria-hidden="true"
                          >
                            {tenant.fullName
                              ?.trim()
                              .slice(0, 1)
                              .toLocaleUpperCase("vi-VN") || "N"}
                          </span>
                          <div>
                            <strong>{tenant.fullName}</strong>
                            <small>
                              {tenant.role === "AuthorizedOccupant"
                                ? "Người cùng ở"
                                : "Người đứng tên"}
                              {tenant.isContactPerson ? " · Người liên hệ" : ""}
                            </small>
                            <small>
                              Vào ở {formatDate(tenant.joinDate)}
                              {tenant.leaveDate
                                ? ` · Đã rời ${formatDate(tenant.leaveDate)}`
                                : ""}
                            </small>
                          </div>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p>Chưa có người thuê được ghi nhận.</p>
                  )}
                </div>
                {contract.note && (
                  <div className="tn-contract-note">
                    <strong>Ghi chú từ chủ trọ</strong>
                    <p>{contract.note}</p>
                  </div>
                )}
                {contract.closureReason && (
                  <div className="tn-contract-note">
                    <strong>Lý do kết thúc</strong>
                    <p>{contract.closureReason}</p>
                  </div>
                )}
                {contract.status === "PendingTenantConfirmation" &&
                  (() => {
                    const isRepresentative = contract.tenants?.some(
                      (t) =>
                        t.isContactPerson &&
                        (!t.userId || t.userId === user?.id),
                    );
                    const agreed = Boolean(agreedMap[contract.id]);
                    const isBusy = confirmBusyId === contract.id;
                    return (
                      <div className="tn-offer-box">
                        <strong>
                          Đề nghị hợp đồng mới (Phiên bản{" "}
                          {contract.offerVersion || 1})
                        </strong>
                        {isRepresentative ? (
                          <>
                            <p>
                              Vui lòng kiểm tra kỹ thông tin tiền thuê, tiền
                              cọc, thời hạn và nội quy khu trọ ở trên trước khi
                              xác nhận.
                            </p>
                            <label className="tn-offer-agree">
                              <input
                                type="checkbox"
                                checked={agreed}
                                disabled={isBusy}
                                onChange={(e) =>
                                  setAgreedMap((cur) => ({
                                    ...cur,
                                    [contract.id]: e.target.checked,
                                  }))
                                }
                              />
                              <span>
                                Tôi đồng ý với toàn bộ nội dung hợp đồng thuê
                                phòng và nội quy khu trọ áp dụng.
                              </span>
                            </label>
                            {offerError && (
                              <p className="form-error" role="alert">
                                {offerError}
                              </p>
                            )}
                            <div className="tn-offer-actions">
                              <button
                                type="button"
                                className="tn-submit"
                                disabled={!agreed || isBusy}
                                onClick={async () => {
                                  setConfirmBusyId(contract.id);
                                  setOfferError("");
                                  try {
                                    await contractApi.confirmOffer(
                                      contract.id,
                                      {
                                        offerVersion:
                                          contract.offerVersion || 1,
                                        termsHash:
                                          contract.offerTermsHash || "",
                                        rulesVersionId:
                                          contract.offerRulesVersionId,
                                        acceptRules: true,
                                      },
                                    );
                                    notify?.(
                                      "Đã xác nhận đồng ý hợp đồng và nội quy. Chờ chủ trọ đối ký.",
                                    );
                                    setRevision((v) => v + 1);
                                  } catch (err) {
                                    setOfferError(
                                      err.message ||
                                        "Không thể xác nhận đề nghị hợp đồng.",
                                    );
                                  } finally {
                                    setConfirmBusyId(null);
                                  }
                                }}
                              >
                                {isBusy
                                  ? "Đang xác nhận..."
                                  : "Xác nhận đồng ý hợp đồng & nội quy"}
                              </button>
                              <button
                                type="button"
                                className="tn-action tn-action--secondary"
                                disabled={isBusy}
                                onClick={async () => {
                                  const reason =
                                    window.prompt(
                                      "Lý do từ chối đề nghị hợp đồng (không bắt buộc):",
                                    ) ?? "";
                                  setConfirmBusyId(contract.id);
                                  setOfferError("");
                                  try {
                                    await contractApi.rejectOffer(
                                      contract.id,
                                      reason,
                                    );
                                    notify?.("Đã từ chối đề nghị hợp đồng.");
                                    setRevision((v) => v + 1);
                                  } catch (err) {
                                    setOfferError(
                                      err.message ||
                                        "Không thể từ chối đề nghị.",
                                    );
                                  } finally {
                                    setConfirmBusyId(null);
                                  }
                                }}
                              >
                                Từ chối đề nghị
                              </button>
                            </div>
                          </>
                        ) : (
                          <p>
                            Đề nghị này đang chờ người thuê đại diện xác nhận
                            đồng ý hợp đồng và nội quy.
                          </p>
                        )}
                      </div>
                    );
                  })()}
                <div className="tn-contract-card-footer">
                  <button
                    type="button"
                    className="tn-inline-action"
                    onClick={() => copyCode(contract.contractCode, notify)}
                  >
                    <Copy size={15} aria-hidden="true" /> Sao chép mã hợp đồng
                  </button>
                </div>
              </article>
            ))}
          </div>
        </div>
      ) : (
        <section className="tn-card tn-contract-state">
          <FileText size={29} aria-hidden="true" />
          <strong>Chưa có hợp đồng</strong>
          <p>
            Khi chủ trọ kích hoạt hợp đồng gắn với tài khoản của bạn, thông tin
            sẽ xuất hiện tại đây.
          </p>
        </section>
      )}
    </>
  );
}

const tenantStatusMap = {
  Open: { label: "Chờ xử lý", tone: "neutral" },
  InProgress: { label: "Đang xử lý", tone: "warning" },
  Resolved: { label: "Đã giải quyết", tone: "success" },
  Closed: { label: "Đã đóng", tone: "danger" },
};

const formatDateTime = (value) => {
  if (!value) return "";
  try {
    return new Intl.DateTimeFormat("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(new Date(value));
  } catch {
    return String(value);
  }
};

function RequestsPage({ notify }) {
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [selectedPropertyId, setSelectedPropertyId] = useState("");
  const [activeStays, setActiveStays] = useState([]);
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [listError, setListError] = useState("");
  const [filter, setFilter] = useState("all");
  const [selectedRequest, setSelectedRequest] = useState(null);

  const loadData = async (signal) => {
    setLoading(true);
    setListError("");
    try {
      const [stays, requestsRes] = await Promise.all([
        requestApi.getMyRequestProperties({ signal }).catch(() => []),
        requestApi.getMyRequests({ page: 1, pageSize: 50, signal }),
      ]);
      setActiveStays(stays);
      if (stays.length === 1) {
        setSelectedPropertyId(stays[0].propertyId);
      }
      const items = requestsRes?.data || [];
      setRequests(items);
    } catch (e) {
      if (!signal?.aborted) {
        setListError(e.message || "Không thể tải danh sách yêu cầu.");
      }
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    const controller = new AbortController();
    loadData(controller.signal);
    return () => controller.abort();
  }, []);

  const filtered = requests.filter((req) => {
    if (filter === "all") return true;
    if (filter === "open")
      return req.status === "Open" || req.status === "InProgress";
    return req.status === "Resolved" || req.status === "Closed";
  });

  async function submit(event) {
    event.preventDefault();
    const cleanTitle = title.trim();
    const cleanContent = content.trim();
    if (activeStays.length > 1 && !selectedPropertyId) {
      setError("Vui lòng chọn khu trọ bạn muốn gửi yêu cầu.");
      return;
    }
    if (!cleanTitle) {
      setError("Tiêu đề không được để trống.");
      return;
    }
    if (!cleanContent) {
      setError("Nội dung không được để trống.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      const created = await requestApi.createTenantRequest({
        propertyId: selectedPropertyId || undefined,
        title: cleanTitle,
        content: cleanContent,
      });
      setRequests((current) => [created, ...current]);
      setTitle("");
      setContent("");
      setSelectedRequest(created);
      notify?.("Đã gửi yêu cầu tới chủ trọ thành công.");
    } catch (e) {
      setError(e.message || "Không thể gửi yêu cầu. Vui lòng thử lại.");
    } finally {
      setSubmitting(false);
    }
  }

  async function inspectRequest(req) {
    setSelectedRequest(req);
    try {
      const fresh = await requestApi.getMyRequestById(req.id);
      setSelectedRequest(fresh);
      setRequests((current) =>
        current.map((item) => (item.id === fresh.id ? fresh : item)),
      );
    } catch {
      // ignore background refresh error
    }
  }

  return (
    <>
      <Header page="requests" />
      <div className="tn-request-layout">
        <section className="tn-card tn-request-form">
          <div className="tn-section-head">
            <div>
              <span>GỬI TỚI CHỦ TRỌ</span>
              <h2>Tạo yêu cầu mới</h2>
            </div>
            <Wrench size={19} aria-hidden="true" />
          </div>
          <p className="tn-small-note">
            Nhập tiêu đề và nội dung chi tiết. Bạn có thể theo dõi chủ trọ đã
            xem yêu cầu hay chưa và tiến độ xử lý trực tiếp tại đây.
          </p>
          <form onSubmit={submit} noValidate>
            {activeStays.length > 1 && (
              <label>
                Khu trọ áp dụng
                <select
                  value={selectedPropertyId}
                  onChange={(e) => setSelectedPropertyId(e.target.value)}
                  required
                >
                  <option value="">-- Chọn khu trọ --</option>
                  {activeStays.map((stay) => (
                    <option key={stay.propertyId} value={stay.propertyId}>
                      {stay.propertyName} (Phòng {stay.roomNumber})
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label>
              Tiêu đề yêu cầu
              <input
                value={title}
                onChange={(event) => {
                  setTitle(event.target.value);
                  setError("");
                }}
                maxLength={250}
                placeholder="Ví dụ: Đèn hành lang hoặc máy lạnh cần kiểm tra"
                required
              />
            </label>
            <label>
              Nội dung chi tiết
              <textarea
                value={content}
                onChange={(event) => {
                  setContent(event.target.value);
                  setError("");
                }}
                maxLength={4000}
                rows={5}
                placeholder="Mô tả rõ tình trạng, vị trí phòng và thời gian thuận tiện để chủ trọ hỗ trợ..."
                required
              />
            </label>
            {error && (
              <p className="tn-form-error" role="alert">
                <AlertCircle size={15} aria-hidden="true" />
                {error}
              </p>
            )}
            <button className="tn-submit" type="submit" disabled={submitting}>
              <Send size={16} aria-hidden="true" />
              {submitting ? "Đang gửi..." : "Gửi yêu cầu"}
            </button>
          </form>
        </section>

        <section className="tn-card tn-request-history">
          <div className="tn-section-head">
            <div>
              <span>THEO DÕI TRẠNG THÁI</span>
              <h2>Yêu cầu đã gửi</h2>
            </div>
            <small>{requests.length} yêu cầu</small>
          </div>
          <div className="tn-filter-bar" role="group" aria-label="Lọc yêu cầu">
            <button
              type="button"
              className={filter === "all" ? "is-active" : ""}
              onClick={() => setFilter("all")}
              aria-pressed={filter === "all"}
            >
              Tất cả <span>{requests.length}</span>
            </button>
            <button
              type="button"
              className={filter === "open" ? "is-active" : ""}
              onClick={() => setFilter("open")}
              aria-pressed={filter === "open"}
            >
              Đang xử lý{" "}
              <span>
                {
                  requests.filter(
                    (i) => i.status === "Open" || i.status === "InProgress",
                  ).length
                }
              </span>
            </button>
            <button
              type="button"
              className={filter === "done" ? "is-active" : ""}
              onClick={() => setFilter("done")}
              aria-pressed={filter === "done"}
            >
              Đã kết thúc{" "}
              <span>
                {
                  requests.filter(
                    (i) => i.status === "Resolved" || i.status === "Closed",
                  ).length
                }
              </span>
            </button>
          </div>

          {loading ? (
            <Empty
              icon={ClipboardList}
              title="Đang tải yêu cầu..."
              description="Vui lòng đợi giây lát."
            />
          ) : listError ? (
            <Empty
              icon={AlertCircle}
              title="Không thể tải yêu cầu"
              description={listError}
            />
          ) : filtered.length ? (
            <ul>
              {filtered.map((request) => {
                const st = tenantStatusMap[request.status] || {
                  label: request.status,
                  tone: "neutral",
                };
                const isSelected = selectedRequest?.id === request.id;
                return (
                  <li
                    key={request.id}
                    style={{
                      cursor: "pointer",
                      background: isSelected ? "#f4f8ff" : undefined,
                      paddingLeft: isSelected ? "10px" : undefined,
                      paddingRight: isSelected ? "10px" : undefined,
                      borderRadius: "8px",
                    }}
                    onClick={() => inspectRequest(request)}
                  >
                    <div style={{ flex: 1 }}>
                      <strong>{request.title}</strong>
                      <small>
                        {request.propertyName} · Phòng {request.roomNumber} ·
                        Gửi lúc {formatDateTime(request.createdAt)}
                      </small>
                      <p>{request.content}</p>
                      <div
                        style={{
                          display: "flex",
                          gap: "8px",
                          flexWrap: "wrap",
                          marginTop: "8px",
                        }}
                      >
                        <StateTag
                          label={
                            request.landlordIsRead
                              ? `Chủ trọ đã xem${request.landlordReadAt ? ` lúc ${formatDateTime(request.landlordReadAt)}` : ""}`
                              : "Chủ trọ chưa xem"
                          }
                          tone={request.landlordIsRead ? "success" : "neutral"}
                        />
                        <StateTag label={st.label} tone={st.tone} />
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          ) : (
            <Empty
              icon={ClipboardList}
              title="Chưa có yêu cầu nào"
              description="Khi bạn gửi yêu cầu hỗ trợ tới chủ trọ, danh sách và trạng thái xem sẽ hiển thị tại đây."
            />
          )}
        </section>
      </div>
    </>
  );
}

function NoticesPage({ notify }) {
  const [announcements, setAnnouncements] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("all");
  const [selectedNotice, setSelectedNotice] = useState(null);

  const loadAnnouncements = async (signal) => {
    setLoading(true);
    setError("");
    try {
      const response = await requestApi.getMyAnnouncements({
        page: 1,
        pageSize: 50,
        signal,
      });
      setAnnouncements(response?.data || []);
    } catch (e) {
      if (!signal?.aborted) {
        setError(e.message || "Không thể tải thông báo từ chủ trọ.");
      }
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    const controller = new AbortController();
    loadAnnouncements(controller.signal);
    return () => controller.abort();
  }, []);

  const unread = announcements.filter((item) => !item.isRead);
  const visible = filter === "unread" ? unread : announcements;

  async function handleRead(notice) {
    setSelectedNotice(notice);
    if (notice.isRead) return;
    try {
      const updated = await requestApi.markAnnouncementAsRead(notice.id);
      setAnnouncements((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      );
      setSelectedNotice(updated);
    } catch (e) {
      notify?.(e.message || "Không thể đánh dấu đã đọc.");
    }
  }

  async function handleReadAll() {
    for (const item of unread) {
      try {
        const updated = await requestApi.markAnnouncementAsRead(item.id);
        setAnnouncements((current) =>
          current.map((x) => (x.id === updated.id ? updated : x)),
        );
      } catch {
        // ignore
      }
    }
    notify?.("Đã đánh dấu đọc toàn bộ thông báo.");
  }

  return (
    <>
      <Header page="notices" />
      <section className="tn-card tn-notices">
        <div className="tn-section-head">
          <div>
            <span>THÔNG BÁO TỪ CHỦ TRỌ</span>
            <h2>Bảng tin khu trọ</h2>
          </div>
          <small>{unread.length} chưa đọc</small>
        </div>
        <div className="tn-notice-toolbar">
          <div
            className="tn-filter-bar"
            role="group"
            aria-label="Lọc thông báo"
          >
            <button
              type="button"
              className={filter === "all" ? "is-active" : ""}
              onClick={() => setFilter("all")}
              aria-pressed={filter === "all"}
            >
              Tất cả <span>{announcements.length}</span>
            </button>
            <button
              type="button"
              className={filter === "unread" ? "is-active" : ""}
              onClick={() => setFilter("unread")}
              aria-pressed={filter === "unread"}
            >
              Chưa đọc <span>{unread.length}</span>
            </button>
          </div>
          <button
            type="button"
            className="tn-inline-action"
            onClick={handleReadAll}
            disabled={!unread.length}
          >
            <CheckCheck size={15} aria-hidden="true" /> Đánh dấu tất cả đã đọc
          </button>
        </div>

        {loading ? (
          <Empty
            icon={Bell}
            title="Đang tải thông báo..."
            description="Vui lòng đợi giây lát."
          />
        ) : error ? (
          <Empty
            icon={AlertCircle}
            title="Không thể tải thông báo"
            description={error}
          />
        ) : visible.length ? (
          <ul>
            {visible.map((notice) => {
              const isRead = notice.isRead;
              const isSelected = selectedNotice?.id === notice.id;
              return (
                <li
                  key={notice.id}
                  className={isRead ? "is-read" : "is-unread"}
                  style={{
                    cursor: "pointer",
                    background: isSelected ? "#f4f8ff" : undefined,
                    paddingLeft: "10px",
                    paddingRight: "10px",
                    borderRadius: "8px",
                  }}
                  onClick={() => handleRead(notice)}
                >
                  <span
                    className={`tn-notice-icon tn-notice-icon--${isRead ? "neutral" : "warning"}`}
                  >
                    <Bell size={18} aria-hidden="true" />
                  </span>
                  <div>
                    <strong>{notice.title}</strong>
                    <p style={{ whiteSpace: "pre-wrap" }}>{notice.content}</p>
                    <small>
                      {notice.propertyName} · Phòng {notice.roomNumber} · Người
                      gửi: {notice.senderFullName} ·{" "}
                      {formatDateTime(notice.createdAt)}
                      {isRead && notice.readAt
                        ? ` · Đã xem lúc ${formatDateTime(notice.readAt)}`
                        : ""}
                    </small>
                    {!isRead && (
                      <div className="tn-notice-actions">
                        <button
                          type="button"
                          className="tn-inline-action"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleRead(notice);
                          }}
                        >
                          <Check size={14} aria-hidden="true" /> Đánh dấu đã đọc
                        </button>
                      </div>
                    )}
                  </div>
                  <StateTag
                    label={isRead ? "Đã đọc" : "Mới"}
                    tone={isRead ? "neutral" : "warning"}
                  />
                </li>
              );
            })}
          </ul>
        ) : (
          <Empty
            icon={Bell}
            title={
              filter === "unread"
                ? "Đã đọc hết thông báo"
                : "Chưa có thông báo từ chủ trọ"
            }
            description="Các thông báo mới từ chủ trọ sẽ xuất hiện tại đây."
          />
        )}
      </section>
    </>
  );
}

function ProfilePage({ user, profile, onSave, updateUser, notify }) {
  const [draft, setDraft] = useState(profile);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [profileLoading, setProfileLoading] = useState(Boolean(user?.id));
  const [profileLoadError, setProfileLoadError] = useState("");
  const [profileReload, setProfileReload] = useState(0);

  useEffect(() => {
    setDraft(profile);
    setEditing(false);
    setErrorMessage("");
  }, [profile, user?.id]);

  useEffect(() => {
    let active = true;
    if (user?.id) {
      setProfileLoading(true);
      setProfileLoadError("");
      getMyProfile()
        .then((res) => {
          if (!active) return;
          const data = res?.data || res;
          if (!data?.id) throw new Error("Máy chủ chưa trả về hồ sơ hợp lệ.");
          const next = {
            fullName: data.fullName ?? "",
            email: data.email ?? "",
            phoneNumber: data.phoneNumber ?? "",
            citizenId: data.citizenId ?? "",
            dateOfBirth: data.dateOfBirth
              ? String(data.dateOfBirth).split("T")[0]
              : "",
            address: data.address ?? "",
          };
          setDraft(next);
          onSave?.(next);
          updateUser?.(data);
        })
        .catch((err) => {
          if (active)
            setProfileLoadError(err.message || "Không tải được hồ sơ cá nhân.");
        })
        .finally(() => {
          if (active) setProfileLoading(false);
        });
    }
    return () => {
      active = false;
    };
  }, [user?.id, profileReload, onSave, updateUser]);

  async function save(event) {
    event.preventDefault();
    if (!editing || profileLoading || profileLoadError) return;
    setErrorMessage("");

    const trimmedName = (draft.fullName || "").trim();
    if (!trimmedName) {
      setErrorMessage("Vui lòng nhập họ và tên.");
      return;
    }
    const trimmedPhone = (draft.phoneNumber || "").trim();
    if (trimmedPhone && !/^0\d{9}$/.test(trimmedPhone)) {
      setErrorMessage(
        "Số điện thoại không hợp lệ (phải gồm 10 chữ số và bắt đầu bằng số 0).",
      );
      return;
    }
    const trimmedCitizen = (draft.citizenId || "").trim();
    if (trimmedCitizen && !/^\d{12}$/.test(trimmedCitizen)) {
      setErrorMessage(
        "Số Căn cước công dân (CCCD) không hợp lệ (phải gồm đúng 12 chữ số).",
      );
      return;
    }
    if (draft.dateOfBirth) {
      const dob = new Date(draft.dateOfBirth);
      const today = new Date();
      if (dob > today) {
        setErrorMessage("Ngày sinh không được nằm ở tương lai.");
        return;
      }
      let age = today.getFullYear() - dob.getFullYear();
      const m = today.getMonth() - dob.getMonth();
      if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) {
        age--;
      }
      if (age < 18) {
        setErrorMessage("Người dùng phải từ đủ 18 tuổi trở lên.");
        return;
      }
    }

    setSaving(true);
    try {
      const payload = {
        fullName: trimmedName,
        phoneNumber: trimmedPhone || null,
        citizenId: trimmedCitizen || null,
        dateOfBirth: draft.dateOfBirth || null,
        address: (draft.address || "").trim() || null,
      };

      const res = await updateMyProfile(payload);
      if (!res?.data) throw new Error("Máy chủ chưa xác nhận cập nhật hồ sơ.");
      const updatedData = {
        fullName: res.data.fullName ?? "",
        phoneNumber: res.data.phoneNumber ?? "",
        citizenId: res.data.citizenId ?? "",
        dateOfBirth: res.data.dateOfBirth
          ? String(res.data.dateOfBirth).split("T")[0]
          : "",
        address: res.data.address ?? "",
        email: res.data.email ?? "",
      };

      updateUser?.(res.data);
      onSave?.(updatedData);
      setDraft(updatedData);
      setEditing(false);
      notify?.("Cập nhật thông tin hồ sơ thành công.");
    } catch (err) {
      setErrorMessage(err.message || "Không thể cập nhật hồ sơ cá nhân.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <Header page="profile" />
      <section className="tn-card tn-profile">
        <div className="tn-section-head">
          <div>
            <span>TÀI KHOẢN CỦA BẠN</span>
            <h2>{profile.fullName || user?.userName || "Người thuê"}</h2>
          </div>
          <div style={{ display: "flex", gap: "0.4rem", flexWrap: "wrap" }}>
            <StateTag label="Người thuê" tone="neutral" />
            <StateTag
              label={
                user?.identityVerificationStatus === "Verified"
                  ? "Đã xác minh danh tính"
                  : "Chưa xác minh (Unverified)"
              }
              tone={
                user?.identityVerificationStatus === "Verified"
                  ? "success"
                  : "warning"
              }
            />
          </div>
        </div>
        <p className="tn-small-note">
          Thông tin SĐT/CCCD tự khai ở trạng thái chưa xác minh vẫn dùng để nhận
          và xác nhận hợp đồng; không được tự đổi danh tính trên hợp đồng đã xác
          nhận.
        </p>
        {profileLoading && (
          <p role="status" className="tn-small-note">
            Đang tải hồ sơ cá nhân…
          </p>
        )}
        {profileLoadError && (
          <p role="alert" className="tn-form-error">
            <AlertCircle size={15} aria-hidden="true" />
            {profileLoadError}
            <button
              type="button"
              className="tn-action tn-action--secondary"
              onClick={() => setProfileReload((value) => value + 1)}
            >
              Tải lại
            </button>
          </p>
        )}
        <form onSubmit={save}>
          <label>
            Họ và tên
            <input
              value={draft.fullName}
              disabled={!editing || saving}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  fullName: event.target.value,
                }))
              }
              required
            />
          </label>
          <label>
            Email (Tài khoản)
            <input value={draft.email} disabled readOnly />
          </label>
          <label>
            Số điện thoại
            <input
              type="tel"
              value={draft.phoneNumber}
              disabled={!editing || saving}
              maxLength={10}
              placeholder="0912345678"
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  phoneNumber: event.target.value,
                }))
              }
            />
          </label>
          <label>
            Số CCCD / CMND
            <input
              type="text"
              value={draft.citizenId}
              disabled={!editing || saving}
              maxLength={12}
              placeholder="12 chữ số CCCD"
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  citizenId: event.target.value,
                }))
              }
            />
            <small>CCCD hiện tại: {profile.citizenId || "Chưa cập nhật"}</small>
          </label>
          <label>
            Ngày tháng năm sinh
            <input
              type="date"
              value={draft.dateOfBirth}
              disabled={!editing || saving}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  dateOfBirth: event.target.value,
                }))
              }
            />
          </label>
          <label style={{ gridColumn: "1 / -1" }}>
            Địa chỉ thường trú / liên hệ
            <input
              value={draft.address}
              disabled={!editing || saving}
              placeholder="Số nhà, đường, phường/xã, tỉnh/thành phố"
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  address: event.target.value,
                }))
              }
            />
          </label>

          {errorMessage && (
            <div
              className="tn-form-error"
              role="alert"
              style={{
                gridColumn: "1 / -1",
                padding: "9px 13px",
                background: "#fef2f2",
                border: "1px solid #fecaca",
                borderRadius: "7px",
                color: "#991b1b",
                fontSize: "12px",
                display: "flex",
                alignItems: "center",
                gap: "7px",
              }}
            >
              <AlertCircle size={15} style={{ flexShrink: 0 }} />
              <span>{errorMessage}</span>
            </div>
          )}

          <div className="tn-profile-actions">
            {editing ? (
              <>
                <button
                  type="button"
                  className="tn-action tn-action--secondary"
                  disabled={saving}
                  onClick={() => {
                    setDraft(profile);
                    setErrorMessage("");
                    setEditing(false);
                  }}
                >
                  Hủy
                </button>
                <button type="submit" className="tn-submit" disabled={saving}>
                  {saving ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </>
            ) : (
              <button
                type="button"
                className="tn-action tn-action--secondary"
                disabled={profileLoading || !!profileLoadError}
                onClick={() => {
                  setErrorMessage("");
                  setEditing(true);
                }}
              >
                Chỉnh sửa thông tin <ArrowRight size={15} aria-hidden="true" />
              </button>
            )}
          </div>
        </form>
      </section>
    </>
  );
}

export default function Tenant({
  page = "overview",
  user,
  updateUser,
  navigate,
  notify,
  rooms = [],
  properties = [],
  contracts = [],
  invoices = [],
  invoiceHistory = [],
  requests = [],
  setRequests,
}) {
  const [readIds, setReadIds] = useState([]);
  useEffect(() => setReadIds([]), [user?.id]);
  const mockTenant = initialUsers.find(
    (item) =>
      item.role === "Người thuê" &&
      item.email?.toLocaleLowerCase("vi-VN") ===
        user?.email?.toLocaleLowerCase("vi-VN"),
  );
  const tenantName = mockTenant?.name || "";
  const displayName = (
    user?.fullName ||
    user?.name ||
    mockTenant?.name ||
    user?.userName ||
    ""
  ).trim();
  const [profile, setProfile] = useState(() => ({
    fullName: displayName,
    email: user?.email || "",
    phoneNumber: user?.phoneNumber || "",
    citizenId: user?.citizenId || "",
    dateOfBirth: user?.dateOfBirth
      ? String(user.dateOfBirth).split("T")[0]
      : "",
    address: user?.address || "",
  }));
  useEffect(
    () =>
      setProfile({
        fullName: displayName,
        email: user?.email || "",
        phoneNumber: user?.phoneNumber || "",
        citizenId: user?.citizenId || "",
        dateOfBirth: user?.dateOfBirth
          ? String(user.dateOfBirth).split("T")[0]
          : "",
        address: user?.address || "",
      }),
    [
      user?.id,
      user?.email,
      user?.phoneNumber,
      user?.citizenId,
      user?.dateOfBirth,
      user?.address,
      displayName,
    ],
  );
  const mine = (item) =>
    tenantName &&
    item?.tenant?.trim().toLocaleLowerCase("vi-VN") ===
      tenantName.toLocaleLowerCase("vi-VN");
  const myRooms = rooms.filter(
    (room) =>
      mine(room) ||
      (tenantName &&
        room.occupants?.some(
          (name) =>
            name.trim().toLocaleLowerCase("vi-VN") ===
            tenantName.toLocaleLowerCase("vi-VN"),
        )),
  );
  const room = myRooms[0];
  const property = properties.find((item) => item.id === room?.propertyId);
  const myContracts = contracts.filter(mine);
  const myInvoices = [...invoices, ...invoiceHistory]
    .filter(mine)
    .sort(
      (a, b) =>
        (dateOf(b.due)?.getTime() || 0) - (dateOf(a.due)?.getTime() || 0),
    );
  const myRequests = requests.filter(mine);
  const currentInvoice = myInvoices[0];
  const notices = useMemo(() => {
    const result = tenantMockNotices
      .filter(
        (notice) =>
          mockTenant &&
          notice.audienceEmail.toLowerCase() === mockTenant.email.toLowerCase(),
      )
      .map((notice) => ({
        ...notice,
        icon: Bell,
        meta: `${notice.category} · ${notice.date}`,
        label: notice.important ? "Quan trọng" : "Thông tin",
        tone: notice.important ? "warning" : "neutral",
      }));
    myInvoices
      .filter((invoice) => balanceOf(invoice) > 0)
      .forEach((invoice) =>
        result.push({
          id: invoice.id,
          icon: ReceiptText,
          title: `Hóa đơn kỳ ${invoice.month} còn phải thu`,
          description: `${money(balanceOf(invoice))} · hạn thanh toán ${invoice.due}.`,
          meta: `Phòng ${invoice.room} · ${invoice.id}`,
          label: invoiceStatus(invoice).label,
          tone: invoiceStatus(invoice).tone,
          target: "billing",
        }),
      );
    myRequests
      .filter((request) => request.status === "progress")
      .forEach((request) =>
        result.push({
          id: request.id,
          icon: Wrench,
          title: `Yêu cầu đang được xử lý: ${request.title}`,
          description: "Theo dõi tiến độ xử lý trong mục Yêu cầu.",
          meta: `${request.id} · ${request.date}`,
          label: "Đang xử lý",
          tone: "warning",
          target: "requests",
        }),
      );
    return result;
  }, [mockTenant, myInvoices, myRequests]);
  const unreadNotices = notices.filter(
    (notice) => !readIds.includes(notice.id),
  );
  const markRead = (id) =>
    setReadIds((current) =>
      current.includes(id) ? current : [...current, id],
    );

  if (page === "room")
    return (
      <div className="tn-page">
        <RoomPage room={room} property={property} navigate={navigate} />
      </div>
    );
  if (page === "billing")
    return (
      <div className="tn-page">
        <BillingPage invoices={myInvoices} notify={notify} />
      </div>
    );
  if (page === "contracts")
    return (
      <div className="tn-page">
        <ContractsPage
          contracts={myContracts}
          navigate={navigate}
          notify={notify}
        />
      </div>
    );
  if (page === "requests")
    return (
      <div className="tn-page">
        <RequestsPage notify={notify} />
      </div>
    );
  if (page === "notices" || page === "notifications")
    return (
      <div className="tn-page">
        <NoticesPage notify={notify} />
      </div>
    );
  if (page === "profile")
    return (
      <div className="tn-page">
        <ProfilePage
          user={user}
          profile={profile}
          onSave={setProfile}
          notify={notify}
        />
      </div>
    );
  return (
    <div className="tn-page">
      <Overview
        name={profile.fullName || displayName}
        room={room}
        property={property}
        currentInvoice={currentInvoice}
        openRequests={
          myRequests.filter((request) => request.status !== "done").length
        }
        notices={unreadNotices}
        navigate={navigate}
      />
    </div>
  );
}
