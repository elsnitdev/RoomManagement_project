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
import { billingApi } from "./services/api/billingApi";
import { contractApi } from "./services/api/contractApi";
import { requestApi } from "./services/api/requestApi";
import { getMyProfile, updateMyProfile } from "./services/api/userApi";
import { TenantRules } from "./components/contracts/ContractRules";
import { formatVnd } from "./ui";
import "./components/contracts/contracts.css";
import "./Tenant.css";

const money = (value) => formatVnd(value);
const formatDateOnly = (value) => {
  if (!value) return "Chưa cập nhật";
  const str = String(value).split("T")[0];
  if (/^\d{4}-\d{2}-\d{2}$/.test(str)) {
    const [y, m, d] = str.split("-");
    return `${d}/${m}/${y}`;
  }
  return str;
};
const contractStatusMeta = (status) => {
  switch (status) {
    case "Active":
      return { label: "Đang ở", tone: "success" };
    case "PendingMoveOut":
      return { label: "Chờ bàn giao", tone: "warning" };
    case "PendingSettlement":
      return { label: "Chờ tất toán", tone: "warning" };
    case "PendingTenantConfirmation":
      return { label: "Chờ bạn xác nhận", tone: "warning" };
    case "PendingOperatorConfirmation":
      return { label: "Chờ chủ trọ đối ký", tone: "neutral" };
    case "Expired":
      return { label: "Đã hết hạn", tone: "neutral" };
    case "Terminated":
      return { label: "Đã kết thúc", tone: "danger" };
    default:
      return { label: status || "Chưa rõ", tone: "neutral" };
  }
};
const liveInvoiceStatus = (invoice) => {
  const remaining = Number(invoice?.remainingAmount ?? invoice?.amountDue ?? 0);
  if (invoice?.status === "Paid" || remaining <= 0) {
    return { label: "Đã thanh toán", tone: "success" };
  }
  const dueStr = invoice?.dueDate ? String(invoice.dueDate).split("T")[0] : "";
  if (dueStr) {
    const due = new Date(`${dueStr}T23:59:59`);
    if (due < new Date()) {
      return { label: "Quá hạn", tone: "danger" };
    }
  }
  return Number(invoice?.totalPaid || 0) > 0
    ? { label: "Thanh toán một phần", tone: "warning" }
    : { label: "Chưa thanh toán", tone: "warning" };
};
const invoicePeriodLabel = (invoice) => {
  if (invoice?.type === "Deposit") return "Tiền đặt cọc";
  if (invoice?.billingMonth && invoice?.billingYear) {
    return `Kỳ ${String(invoice.billingMonth).padStart(2, "0")}/${invoice.billingYear}`;
  }
  return "Hóa đơn phát sinh";
};
const serviceTypeLabel = (type) => {
  switch (type) {
    case "Electricity":
      return "Điện";
    case "Water":
      return "Nước";
    case "Internet":
      return "Internet";
    case "Trash":
      return "Rác";
    case "Parking":
      return "Gửi xe";
    default:
      return "Dịch vụ khác";
  }
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
  contracts = [],
  unpaidInvoices = [],
  loading,
  error,
  onRetry,
  openRequestsCount = 0,
  unreadNoticesCount = 0,
  navigate,
}) {
  return (
    <>
      <Header page="overview" name={name} />
      <div className="tn-overview-grid">
        <section className="tn-card tn-home-card">
          <div className="tn-card-kicker">
            <Home size={16} aria-hidden="true" /> CHỖ Ở HIỆN TẠI
            {contracts.length > 0 && (
              <StateTag label={`${contracts.length} hợp đồng`} tone="neutral" />
            )}
          </div>
          {loading ? (
            <div className="tn-loading">
              <div className="tn-skeleton tn-skeleton--title" />
              <div className="tn-skeleton tn-skeleton--line" />
              <div className="tn-skeleton tn-skeleton--line" />
            </div>
          ) : error ? (
            <div className="tn-error-box" role="alert">
              <AlertCircle size={18} aria-hidden="true" />
              <div>
                <strong>Không thể tải thông tin chỗ ở</strong>
                <p>{error}</p>
              </div>
              <button
                type="button"
                className="tn-action tn-action--secondary"
                onClick={onRetry}
              >
                Thử lại
              </button>
            </div>
          ) : contracts.length > 0 ? (
            <>
              <div className="tn-home-contract-list">
                {contracts.map((c) => {
                  const status = contractStatusMeta(c.status);
                  const roomLabel = c.roomNumber
                    ? `Phòng ${c.roomNumber}`
                    : c.roomName || "Phòng chưa cập nhật";
                  return (
                    <article key={c.id} className="tn-home-contract-item">
                      <div className="tn-home-identity">
                        <div>
                          <strong>{roomLabel}</strong>
                          {c.roomName &&
                            c.roomNumber &&
                            c.roomName !== c.roomNumber && (
                              <span className="tn-home-room-sub">
                                {" "}
                                · {c.roomName}
                              </span>
                            )}
                        </div>
                        <StateTag label={status.label} tone={status.tone} />
                      </div>
                      <p className="tn-home-property">
                        <Building2 size={14} aria-hidden="true" />{" "}
                        {c.propertyName || "Khu trọ chưa cập nhật"}
                      </p>
                      <p className="tn-home-address">
                        <MapPin size={15} aria-hidden="true" />
                        {c.propertyAddress || "Địa chỉ chưa cập nhật"}
                      </p>
                      <div className="tn-home-facts">
                        <span>
                          Mã HĐ <strong>{c.contractCode}</strong>
                        </span>
                        <span>
                          Tiền thuê{" "}
                          <strong>{money(c.monthlyRent)}/tháng</strong>
                        </span>
                        <span>
                          Tiền cọc <strong>{money(c.depositAmount)}</strong>
                        </span>
                        <span>
                          Diện tích{" "}
                          <strong>
                            {c.area ? `${c.area} m²` : "Chưa cập nhật"}
                          </strong>
                        </span>
                        <span>
                          Tầng{" "}
                          <strong>
                            {c.floor === 0
                              ? "Tầng trệt"
                              : c.floor != null
                                ? `Tầng ${c.floor}`
                                : "Chưa cập nhật"}
                          </strong>
                        </span>
                        <span>
                          Thời hạn{" "}
                          <strong>
                            {formatDateOnly(c.startDate)} –{" "}
                            {c.endDate
                              ? formatDateOnly(c.endDate)
                              : "Không kỳ hạn"}
                          </strong>
                        </span>
                      </div>
                    </article>
                  );
                })}
              </div>
              <div className="tn-home-actions">
                <LinkButton onClick={() => navigate("tenant/room")}>
                  Xem chi tiết chỗ ở
                </LinkButton>
                <LinkButton
                  secondary
                  onClick={() => navigate("tenant/contracts")}
                >
                  Xem hợp đồng
                </LinkButton>
              </div>
            </>
          ) : (
            <Empty
              icon={Home}
              title="Chưa có thông tin chỗ ở"
              description="Tài khoản này chưa được liên kết với hợp đồng phòng đang hiệu lực."
            />
          )}
        </section>

        <section className="tn-card tn-attention-card">
          <div className="tn-card-kicker">
            <Bell size={16} aria-hidden="true" /> VIỆC CẦN CHÚ Ý
            {unpaidInvoices.length > 0 && (
              <StateTag
                label={`${unpaidInvoices.length} chưa thanh toán`}
                tone="warning"
              />
            )}
          </div>
          {loading ? (
            <div className="tn-loading">
              <div className="tn-skeleton tn-skeleton--title" />
              <div className="tn-skeleton tn-skeleton--line" />
            </div>
          ) : error ? (
            <div className="tn-error-box" role="alert">
              <AlertCircle size={18} aria-hidden="true" />
              <div>
                <strong>Không thể tải danh sách hóa đơn</strong>
                <p>{error}</p>
              </div>
            </div>
          ) : unpaidInvoices.length > 0 ? (
            <div className="tn-unpaid-list">
              {unpaidInvoices.map((inv) => {
                const status = liveInvoiceStatus(inv);
                const remaining = Number(
                  inv.remainingAmount ?? inv.amountDue ?? 0,
                );
                const effective = Number(
                  inv.effectiveAmount ?? inv.effectiveTotal ?? 0,
                );
                const paid = Number(inv.totalPaid ?? 0);
                return (
                  <div
                    key={inv.id}
                    className="tn-unpaid-item"
                    role="button"
                    tabIndex={0}
                    onClick={() => navigate(`tenant/billing/${inv.id}`)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        navigate(`tenant/billing/${inv.id}`);
                      }
                    }}
                  >
                    <div className="tn-unpaid-head">
                      <div>
                        <span className="tn-unpaid-period">
                          {invoicePeriodLabel(inv)} · <b>{inv.code}</b>
                        </span>
                        <small className="tn-unpaid-room">
                          {inv.roomNumber
                            ? `Phòng ${inv.roomNumber}`
                            : "Phòng thuê"}
                          {inv.propertyName ? ` · ${inv.propertyName}` : ""}
                        </small>
                      </div>
                      <StateTag label={status.label} tone={status.tone} />
                    </div>
                    <div className="tn-unpaid-amounts">
                      <div>
                        <span>Còn phải thu</span>
                        <strong className="tn-unpaid-remaining">
                          {money(remaining)}
                        </strong>
                      </div>
                      <div className="tn-unpaid-meta">
                        <span>Tổng HĐ: {money(effective)}</span>
                        {paid > 0 && <span>Đã trả: {money(paid)}</span>}
                        <span>Hạn TT: {formatDateOnly(inv.dueDate)}</span>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="tn-attention-item tn-attention-item--clear">
              <CheckCircle2 size={18} aria-hidden="true" />
              <span>Không có hóa đơn còn phải thu.</span>
            </div>
          )}
          <div className="tn-attention-bottom">
            <span>{openRequestsCount} yêu cầu đang theo dõi</span>
            <span>{unreadNoticesCount} thông báo chưa đọc</span>
          </div>
          <div className="tn-home-actions">
            {unpaidInvoices.length > 0 && (
              <LinkButton onClick={() => navigate("tenant/billing")}>
                Thanh toán hóa đơn
              </LinkButton>
            )}
            <LinkButton secondary onClick={() => navigate("tenant/notices")}>
              Xem thông báo
            </LinkButton>
          </div>
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

function RoomPage({
  contracts = [],
  unpaidInvoices = [],
  loading,
  error,
  onRetry,
  navigate,
}) {
  return (
    <>
      <Header page="room" />
      {loading ? (
        <section className="tn-card">
          <div className="tn-loading">
            <div className="tn-skeleton tn-skeleton--title" />
            <div className="tn-skeleton tn-skeleton--line" />
            <div className="tn-skeleton tn-skeleton--line" />
          </div>
        </section>
      ) : error ? (
        <section className="tn-card">
          <div className="tn-error-box" role="alert">
            <AlertCircle size={18} aria-hidden="true" />
            <div>
              <strong>Không thể tải danh sách phòng</strong>
              <p>{error}</p>
            </div>
            <button
              type="button"
              className="tn-action tn-action--secondary"
              onClick={onRetry}
            >
              Thử lại
            </button>
          </div>
        </section>
      ) : contracts.length > 0 ? (
        <div className="tn-room-list">
          {contracts.map((c) => {
            const status = contractStatusMeta(c.status);
            const members = c.members?.length ? c.members : c.tenants || [];
            const contractInvoices = unpaidInvoices.filter(
              (inv) =>
                inv.contractId === c.id ||
                (inv.roomId && c.roomId && inv.roomId === c.roomId),
            );
            return (
              <div key={c.id} className="tn-room-grid">
                <section className="tn-card tn-room-main">
                  <div className="tn-room-photo">
                    <img src="/room-studio.jpg" alt="Ảnh minh họa phòng trọ" />
                    <span>Hợp đồng {c.contractCode}</span>
                  </div>
                  <div className="tn-room-content">
                    <div className="tn-section-head">
                      <div>
                        <span>CHỖ Ở THEO HỢP ĐỒNG · {c.contractCode}</span>
                        <h2>
                          Phòng {c.roomNumber || c.roomName || "Chưa cập nhật"}
                          {c.roomName &&
                            c.roomNumber &&
                            c.roomName !== c.roomNumber &&
                            ` (${c.roomName})`}
                        </h2>
                      </div>
                      <StateTag label={status.label} tone={status.tone} />
                    </div>
                    <p className="tn-room-address">
                      <Building2 size={16} aria-hidden="true" />
                      {c.propertyName || "Khu trọ chưa cập nhật"}
                    </p>
                    <p className="tn-room-address">
                      <MapPin size={16} aria-hidden="true" />
                      {c.propertyAddress || "Địa chỉ chưa cập nhật"}
                    </p>
                    <dl className="tn-facts">
                      <Field label="Mã hợp đồng" value={c.contractCode} />
                      <Field label="Trạng thái hợp đồng" value={status.label} />
                      <Field
                        label="Tầng"
                        value={
                          c.floor === 0
                            ? "Tầng trệt"
                            : c.floor != null
                              ? `Tầng ${c.floor}`
                              : ""
                        }
                      />
                      <Field
                        label="Diện tích"
                        value={c.area ? `${c.area} m²` : ""}
                      />
                      <Field
                        label="Giá thuê/tháng"
                        value={money(c.monthlyRent)}
                      />
                      <Field
                        label="Tiền đặt cọc"
                        value={money(c.depositAmount)}
                      />
                      <Field
                        label="Thời hạn hợp đồng"
                        value={`${formatDateOnly(c.startDate)} – ${
                          c.endDate
                            ? formatDateOnly(c.endDate)
                            : "Không thời hạn"
                        }`}
                      />
                      <Field
                        label="Ngày đến hạn TT hàng tháng"
                        value={
                          c.paymentDueDay
                            ? `Ngày ${c.paymentDueDay} hàng tháng`
                            : ""
                        }
                      />
                      <Field
                        label="Số người ở"
                        value={
                          c.maxOccupants
                            ? `${members.length}/${c.maxOccupants} người`
                            : `${members.length} người`
                        }
                      />
                    </dl>

                    {c.roomServices?.length > 0 && (
                      <div className="tn-room-services">
                        <h3>Dịch vụ áp dụng tại phòng</h3>
                        <div className="tn-room-service-grid">
                          {c.roomServices.map((srv) => (
                            <div
                              key={srv.id || srv.serviceId}
                              className="tn-room-service-chip"
                            >
                              <div>
                                <strong>{srv.name || srv.serviceName}</strong>
                                <small>
                                  {srv.quantity != null
                                    ? `Số lượng: ${srv.quantity}`
                                    : serviceTypeLabel(srv.serviceType)}
                                  {srv.startDate
                                    ? ` · Từ ${formatDateOnly(srv.startDate)}`
                                    : ""}
                                </small>
                              </div>
                              <span>
                                {money(srv.unitPrice)}
                                {srv.unit ? `/${srv.unit}` : ""}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}

                    {c.roomDescription && (
                      <p className="tn-room-description">{c.roomDescription}</p>
                    )}
                    {c.note && (
                      <p className="tn-room-description">
                        <strong>Ghi chú hợp đồng:</strong> {c.note}
                      </p>
                    )}
                  </div>
                </section>
                <aside className="tn-card tn-room-side">
                  <div className="tn-card-kicker">
                    <UserRound size={16} aria-hidden="true" /> NGƯỜI ĐANG Ở (
                    {members.length})
                  </div>
                  {members.length > 0 ? (
                    <ul className="tn-occupants">
                      {members.map((m) => {
                        const isContractParty =
                          m.role === "ContractParty" || m.role === "Primary";
                        const joinDate = m.joinDate || m.joinedAt;
                        const leaveDate = m.leaveDate || m.leftAt;
                        return (
                          <li
                            key={m.id || m.userId || m.fullName}
                            className="tn-occupant-item"
                          >
                            <div className="tn-occupant-top">
                              <strong>
                                {m.fullName || m.email || m.userId}
                              </strong>
                              <StateTag
                                label={
                                  isContractParty
                                    ? "Đứng tên HĐ"
                                    : "Người ở cùng"
                                }
                                tone={isContractParty ? "success" : "neutral"}
                              />
                            </div>
                            <small>
                              {m.isContactPerson
                                ? "Người liên hệ chính · "
                                : ""}
                              Từ {formatDateOnly(joinDate)}
                              {leaveDate
                                ? ` đến ${formatDateOnly(leaveDate)}`
                                : ""}
                            </small>
                          </li>
                        );
                      })}
                    </ul>
                  ) : (
                    <p>Chưa có danh sách người ở.</p>
                  )}

                  {contractInvoices.length > 0 && (
                    <div className="tn-room-unpaid">
                      <div className="tn-card-kicker">
                        <ReceiptText size={15} aria-hidden="true" /> HÓA ĐƠN CẦN
                        THANH TOÁN ({contractInvoices.length})
                      </div>
                      <ul className="tn-room-unpaid-list">
                        {contractInvoices.map((inv) => {
                          const remaining = Number(
                            inv.remainingAmount ?? inv.amountDue ?? 0,
                          );
                          return (
                            <li key={inv.id}>
                              <div>
                                <strong>
                                  {invoicePeriodLabel(inv)} ({inv.code})
                                </strong>
                                <small>
                                  Còn nợ {money(remaining)} · Hạn{" "}
                                  {formatDateOnly(inv.dueDate)}
                                </small>
                              </div>
                              <button
                                type="button"
                                className="tn-inline-action"
                                onClick={() =>
                                  navigate(`tenant/billing/${inv.id}`)
                                }
                              >
                                Thanh toán
                              </button>
                            </li>
                          );
                        })}
                      </ul>
                    </div>
                  )}

                  <div className="tn-room-actions">
                    <LinkButton
                      secondary
                      onClick={() => navigate("tenant/contracts")}
                    >
                      Xem hợp đồng
                    </LinkButton>
                    <LinkButton
                      secondary
                      onClick={() => navigate("tenant/billing")}
                    >
                      Xem hóa đơn
                    </LinkButton>
                    <LinkButton onClick={() => navigate("tenant/requests")}>
                      Gửi yêu cầu
                    </LinkButton>
                  </div>
                </aside>
              </div>
            );
          })}
        </div>
      ) : (
        <section className="tn-card">
          <Empty
            icon={Home}
            title="Chưa có phòng được liên kết"
            description="Thông tin chỗ ở sẽ xuất hiện khi tài khoản được liên kết với hợp đồng phòng."
          />
        </section>
      )}
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
}) {
  const displayName = (
    user?.fullName ||
    user?.name ||
    user?.userName ||
    user?.email ||
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

  const [contracts, setContracts] = useState([]);
  const [invoices, setInvoices] = useState([]);
  const [requests, setRequests] = useState([]);
  const [announcements, setAnnouncements] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    if (page !== "overview" && page !== "room") return undefined;
    const controller = new AbortController();
    setLoading(true);
    setError("");

    Promise.all([
      contractApi.mine(controller.signal),
      billingApi.allMyInvoices(controller.signal).catch(() => []),
      requestApi
        .getMyRequests({ page: 1, pageSize: 50, signal: controller.signal })
        .catch(() => ({ items: [] })),
      requestApi
        .getMyAnnouncements({
          page: 1,
          pageSize: 50,
          signal: controller.signal,
        })
        .catch(() => ({ items: [] })),
    ])
      .then(([contractsRes, invoicesRes, requestsRes, announcementsRes]) => {
        setContracts(Array.isArray(contractsRes) ? contractsRes : []);
        setInvoices(Array.isArray(invoicesRes) ? invoicesRes : []);
        setRequests(
          Array.isArray(requestsRes?.items)
            ? requestsRes.items
            : Array.isArray(requestsRes)
              ? requestsRes
              : [],
        );
        setAnnouncements(
          Array.isArray(announcementsRes?.items)
            ? announcementsRes.items
            : Array.isArray(announcementsRes)
              ? announcementsRes
              : [],
        );
        setLoading(false);
      })
      .catch((err) => {
        if (err?.name !== "AbortError") {
          setError(err?.message || "Không tải được dữ liệu chỗ ở từ máy chủ.");
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [page, user?.id, revision]);

  const activeStatuses = useMemo(
    () =>
      new Set([
        "Active",
        "PendingMoveOut",
        "PendingSettlement",
        "PendingTenantConfirmation",
        "PendingOperatorConfirmation",
      ]),
    [],
  );

  const displayContracts = useMemo(() => {
    const active = contracts.filter((c) => activeStatuses.has(c.status));
    return active.length > 0 ? active : contracts;
  }, [contracts, activeStatuses]);

  const contractById = useMemo(() => {
    const map = new Map();
    contracts.forEach((c) => map.set(c.id, c));
    return map;
  }, [contracts]);

  const unpaidInvoices = useMemo(() => {
    return invoices
      .filter((inv) => {
        const remaining = Number(inv?.remainingAmount ?? inv?.amountDue ?? 0);
        return inv?.status === "Issued" && remaining > 0;
      })
      .map((inv) => {
        const matched = contractById.get(inv.contractId);
        return {
          ...inv,
          roomNumber: inv.roomNumber || matched?.roomNumber || "",
          propertyName: inv.propertyName || matched?.propertyName || "",
          contractCode: inv.contractCode || matched?.contractCode || "",
        };
      })
      .sort((a, b) => {
        const da = a.dueDate ? new Date(`${a.dueDate}T00:00:00`).getTime() : 0;
        const db = b.dueDate ? new Date(`${b.dueDate}T00:00:00`).getTime() : 0;
        return da - db;
      });
  }, [invoices, contractById]);

  const openRequestsCount = useMemo(
    () =>
      requests.filter(
        (r) => r.status === "Open" || r.status === "InProgress",
      ).length,
    [requests],
  );

  const unreadNoticesCount = useMemo(
    () => announcements.filter((a) => !a.isRead).length,
    [announcements],
  );

  const handleRetry = () => setRevision((v) => v + 1);

  if (page === "room")
    return (
      <div className="tn-page">
        <RoomPage
          contracts={displayContracts}
          unpaidInvoices={unpaidInvoices}
          loading={loading}
          error={error}
          onRetry={handleRetry}
          navigate={navigate}
        />
      </div>
    );
  if (page === "contracts")
    return (
      <div className="tn-page">
        <ContractsPage user={user} navigate={navigate} notify={notify} />
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
          updateUser={updateUser}
          notify={notify}
        />
      </div>
    );
  return (
    <div className="tn-page">
      <Overview
        name={profile.fullName || displayName}
        contracts={displayContracts}
        unpaidInvoices={unpaidInvoices}
        loading={loading}
        error={error}
        onRetry={handleRetry}
        openRequestsCount={openRequestsCount}
        unreadNoticesCount={unreadNoticesCount}
        navigate={navigate}
      />
    </div>
  );
}
