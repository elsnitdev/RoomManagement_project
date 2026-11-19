import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  Building2,
  ChevronRight,
  Clock,
  CreditCard,
  DoorOpen,
  FilePlus2,
  FileText,
  MessageSquareText,
  Plus,
  ReceiptText,
  RefreshCw,
  Wallet,
} from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { listProperties } from "../services/api/PropertyApi";
import { listRooms } from "../services/api/RoomApi";
import { billingApi } from "../services/api/billingApi";
import { contractApi } from "../services/api/contractApi";
import { getLandlordRequests } from "../services/api/requestApi";
import "../styles/landlord-overview.css";

const PENDING_CONTRACT_STATUSES = new Set([
  "Draft",
  "PendingTenantConfirmation",
  "PendingOperatorConfirmation",
]);

async function fetchAllRooms(propertyId, signal) {
  const items = [];
  for (let page = 1; ; page += 1) {
    const res = await listRooms(propertyId, { page, pageSize: 100 }, { signal });
    const rows = res?.data || [];
    items.push(...rows);
    const totalPages = res?.meta?.totalPages;
    if (!rows.length || (totalPages ? page >= totalPages : rows.length < 100)) {
      return items;
    }
  }
}

function formatCurrency(value) {
  return (
    new Intl.NumberFormat("vi-VN", {
      maximumFractionDigits: 0,
    }).format(Number(value) || 0) + " ₫"
  );
}

function formatDate(value) {
  if (!value) return "—";
  if (/^\d{4}-\d{2}-\d{2}/.test(value)) {
    const [y, m, d] = value.slice(0, 10).split("-");
    return `${d}/${m}/${y}`;
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return new Intl.DateTimeFormat("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

function getTodayLabel() {
  return new Intl.DateTimeFormat("vi-VN", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(new Date());
}

function getGreetingPrefix() {
  const hour = new Date().getHours();
  if (hour < 12) return "Chào buổi sáng";
  if (hour < 18) return "Chào buổi chiều";
  return "Chào buổi tối";
}

function getTodayIsoInVietnam() {
  return new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
}

function daysUntil(dateStr, todayIso) {
  if (!dateStr) return null;
  const now = new Date(`${todayIso}T00:00:00`);
  const target = new Date(`${String(dateStr).slice(0, 10)}T00:00:00`);
  if (Number.isNaN(target.getTime())) return null;
  return Math.round((target - now) / 86400000);
}

function isContractExpiringSoon(contract, todayIso) {
  if (contract?.status !== "Active" || !contract.endDate) return false;
  const diff = daysUntil(contract.endDate, todayIso);
  return diff !== null && diff >= 0 && diff <= 30;
}

const INITIAL_DATA = {
  properties: [],
  rooms: [],
  pendingPayments: [],
  unpaidInvoices: [],
  contracts: [],
  openRequestsCount: 0,
  partialError: false,
};

export default function LandlordOverview({
  selectedProperty,
  setSelectedProperty,
  navigate,
}) {
  const { user } = useAuth();
  const [data, setData] = useState(INITIAL_DATA);
  const [loading, setLoading] = useState(true);
  const [fatalError, setFatalError] = useState("");
  const [reloadTick, setReloadTick] = useState(0);

  const reload = useCallback(() => {
    setReloadTick((tick) => tick + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;

    async function loadOverview() {
      setLoading(true);
      setFatalError("");

      try {
        const propRes = await listProperties(
          { page: 1, pageSize: 100 },
          { signal },
        );
        if (signal.aborted) return;

        const allProperties = propRes?.data || [];
        if (allProperties.length === 0) {
          setData({ ...INITIAL_DATA, properties: [] });
          setLoading(false);
          return;
        }

        const hasSelected =
          selectedProperty &&
          selectedProperty !== "all" &&
          allProperties.some((p) => p.id === selectedProperty);

        const targetProperties = hasSelected
          ? allProperties.filter((p) => p.id === selectedProperty)
          : allProperties;

        const requestPropertyId = hasSelected ? selectedProperty : undefined;

        const [propertyBundles, requestsResult] = await Promise.all([
          Promise.allSettled(
            targetProperties.map((prop) =>
              Promise.allSettled([
                fetchAllRooms(prop.id, signal),
                billingApi.invoiceAttention(prop.id, signal),
                contractApi.list(prop.id, signal),
              ]),
            ),
          ),
          getLandlordRequests({
            propertyId: requestPropertyId,
            page: 1,
            pageSize: 100,
            signal,
          })
            .then((res) => ({ status: "fulfilled", value: res }))
            .catch((err) => ({ status: "rejected", reason: err })),
        ]);

        if (signal.aborted) return;

        const rooms = [];
        const pendingPayments = [];
        const unpaidInvoices = [];
        const contracts = [];
        let partialError = false;

        propertyBundles.forEach((bundleResult, idx) => {
          const prop = targetProperties[idx];
          if (bundleResult.status !== "fulfilled") {
            partialError = true;
            return;
          }
          const [roomsRes, attentionRes, contractsRes] =
            bundleResult.value;

          if (roomsRes.status === "fulfilled") {
            const propRooms = Array.isArray(roomsRes.value) ? roomsRes.value : (roomsRes.value?.data || []);
            propRooms.forEach((room) =>
              rooms.push({
                ...room,
                propertyId: room.propertyId || prop.id,
                propertyName: room.propertyName || prop.name,
              }),
            );
          } else if (roomsRes.reason?.name !== "AbortError") {
            partialError = true;
          }

          if (attentionRes.status === "fulfilled") {
            const att = attentionRes.value || {};
            if (Array.isArray(att.pendingPayments)) {
              pendingPayments.push(...att.pendingPayments);
            }
            if (Array.isArray(att.unpaid)) {
              unpaidInvoices.push(...att.unpaid);
            }
          } else if (attentionRes.reason?.name !== "AbortError") {
            partialError = true;
          }

          if (contractsRes.status === "fulfilled") {
            contracts.push(...(contractsRes.value?.data || []));
          } else if (contractsRes.reason?.name !== "AbortError") {
            partialError = true;
          }
        });

        let openRequestsCount = 0;
        if (requestsResult.status === "fulfilled") {
          const reqItems = requestsResult.value?.data || [];
          openRequestsCount = reqItems.filter(
            (item) => item.status !== "Resolved" && item.status !== "Closed",
          ).length;
        } else if (requestsResult.reason?.name !== "AbortError") {
          partialError = true;
        }

        setData({
          properties: allProperties,
          rooms,
          pendingPayments,
          unpaidInvoices,
          contracts,
          openRequestsCount,
          partialError,
        });
      } catch (err) {
        if (err?.name === "AbortError" || signal.aborted) return;
        setFatalError(
          err?.message || "Không thể tải dữ liệu tổng quan. Vui lòng thử lại.",
        );
      } finally {
        if (!signal.aborted) {
          setLoading(false);
        }
      }
    }

    loadOverview();
    return () => controller.abort();
  }, [selectedProperty, reloadTick]);

  const metrics = useMemo(() => {
    const todayIso = getTodayIsoInVietnam();
    const totalRooms = data.rooms.length;
    const occupied = data.rooms.filter((r) => r.status === "Occupied").length;
    const vacantRooms = data.rooms.filter((r) => r.status === "Available");
    const vacant = vacantRooms.length;
    const maintenance = data.rooms.filter(
      (r) => r.status === "Maintenance",
    ).length;



    const totalAmountDue = data.unpaidInvoices.reduce(
      (sum, inv) => sum + (Number(inv.amountDue) || 0),
      0,
    );

    const overdueInvoices = data.unpaidInvoices.filter(
      (inv) => inv.displayStatus === "Overdue",
    );
    const overdueCount = overdueInvoices.length;
    const firstOverdueId = overdueInvoices[0]?.id || null;

    const pendingPaymentCount = data.pendingPayments.length;

    const pendingContracts = data.contracts.filter((c) =>
      PENDING_CONTRACT_STATUSES.has(c.status),
    ).length;
    const expiringContracts = data.contracts.filter((c) =>
      isContractExpiringSoon(c, todayIso),
    ).length;
    const pendingSettlement = data.contracts.filter(
      (c) => c.status === "PendingSettlement",
    ).length;

    const attentionContractsAndRequests =
      data.openRequestsCount + pendingContracts + expiringContracts;

    const roomsById = new Map();
    const roomCountsByProperty = new Map();
    data.rooms.forEach((r) => {
      roomsById.set(r.id, r);
      const entry = roomCountsByProperty.get(r.propertyId) || { total: 0, occupied: 0, available: 0 };
      entry.total += 1;
      if (r.status === "Occupied") entry.occupied += 1;
      if (r.status === "Available") entry.available += 1;
      roomCountsByProperty.set(r.propertyId, entry);
    });

    return {
      totalRooms,
      occupied,
      vacant,
      maintenance,
      vacantRooms: vacantRooms.slice(0, 4),
      totalAmountDue,
      overdueCount,
      firstOverdueId,
      roomCountsByProperty,
      pendingPaymentCount,
      pendingContracts,
      expiringContracts,
      pendingSettlement,
      contractsActionCount: expiringContracts + pendingSettlement,
      attentionContractsAndRequests,
      roomsById,
    };
  }, [data]);

  const displayName = user?.fullName || user?.userName || "bạn";
  const isManager = user?.role === "Manager";
  const isAllProperties = !selectedProperty || selectedProperty === "all";
  const todayLabel = useMemo(() => getTodayLabel(), []);
  const greetingPrefix = useMemo(() => getGreetingPrefix(), []);

  if (loading && data.properties.length === 0 && !fatalError) {
    return (
      <div className="lo-page" aria-busy="true" aria-live="polite">
        <div className="lo-header-skeleton">
          <div className="lo-skel lo-skel--title" />
          <div className="lo-skel lo-skel--bar" />
        </div>
        <div className="lo-kpi-grid">
          {[1, 2, 3, 4].map((item) => (
            <div className="lo-kpi-card lo-kpi-card--skel" key={item}>
              <div className="lo-skel lo-skel--line" />
              <div className="lo-skel lo-skel--num" />
              <div className="lo-skel lo-skel--sub" />
            </div>
          ))}
        </div>
        <div className="lo-bento-grid">
          <div className="lo-card lo-card--skel" />
          <div className="lo-card lo-card--skel" />
        </div>
      </div>
    );
  }

  if (fatalError && data.properties.length === 0) {
    return (
      <div className="lo-page">
        <section className="lo-error-card" role="alert">
          <AlertCircle size={24} aria-hidden="true" />
          <div>
            <h2>Không tải được dữ liệu tổng quan</h2>
            <p>{fatalError}</p>
          </div>
          <button
            type="button"
            className="lo-btn lo-btn--primary"
            onClick={reload}
          >
            <RefreshCw size={16} aria-hidden="true" />
            Thử lại
          </button>
        </section>
      </div>
    );
  }

  // Empty state for new landlords without any property
  if (!loading && data.properties.length === 0) {
    return (
      <div className="lo-page">
        <header className="lo-header">
          <div className="lo-header__intro">
            <span className="lo-date">{todayLabel}</span>
            <h1>
              {greetingPrefix}, {displayName}
            </h1>
            <p>
              {isManager
                ? "Bạn chưa được phân quyền quản lý khu trọ nào."
                : "Hãy hoàn tất 3 bước khởi tạo để bắt đầu quản lý nhà trọ trên hệ thống."}
            </p>
          </div>
        </header>

        <section className="lo-onboarding-card">
          <div className="lo-onboarding-head">
            <div className="lo-onboarding-icon">
              <Building2 size={24} aria-hidden="true" />
            </div>
            <div>
              <h2>
                {isManager
                  ? "Chưa có khu trọ được giao"
                  : "Bắt đầu thiết lập khu trọ đầu tiên"}
              </h2>
              <p>
                {isManager
                  ? "Vui lòng liên hệ chủ trọ để được phân quyền vào khu trọ cần quản lý."
                  : "Chỉ mất vài phút để đưa khu trọ của bạn vào vận hành."}
              </p>
            </div>
          </div>

          {!isManager && (
            <>
              <ol className="lo-onboarding-steps">
                <li>
                  <span className="lo-step-num">1</span>
                  <div>
                    <strong>Tạo khu trọ</strong>
                    <small>Khai báo tên, địa chỉ và thông tin nhận thanh toán.</small>
                  </div>
                </li>
                <li>
                  <span className="lo-step-num">2</span>
                  <div>
                    <strong>Thêm phòng</strong>
                    <small>Tạo từng phòng hoặc tạo hàng loạt theo tầng / dãy.</small>
                  </div>
                </li>
                <li>
                  <span className="lo-step-num">3</span>
                  <div>
                    <strong>Tạo hợp đồng</strong>
                    <small>Gắn người thuê, tiền cọc và bắt đầu tính hóa đơn tự động.</small>
                  </div>
                </li>
              </ol>

              <div className="lo-onboarding-actions">
                <button
                  type="button"
                  className="lo-btn lo-btn--primary"
                  onClick={() => navigate("landlord/properties")}
                >
                  <Plus size={17} aria-hidden="true" />
                  Tạo khu trọ đầu tiên
                </button>
              </div>
            </>
          )}
        </section>
      </div>
    );
  }

  return (
    <div className="lo-page">
      {/* Tầng 1 — Header & Thanh Thao Tác Nhanh */}
      <header className="lo-header">
        <div className="lo-header__intro">
          <div className="lo-header__meta">
            <span className="lo-date">{todayLabel}</span>
            {isManager && <span className="lo-role-pill">Quản lý khu trọ</span>}
          </div>
          <h1>
            {greetingPrefix}, {displayName}
          </h1>
          <p>
            Theo dõi nhanh công suất phòng, dòng tiền cần thu và các đầu việc ưu
            tiên hôm nay.
          </p>
        </div>

        <div className="lo-header__controls">
          <label className="lo-property-select">
            <Building2 size={17} aria-hidden="true" />
            <span className="sr-only">Chọn khu trọ</span>
            <select
              value={
                selectedProperty !== "all" &&
                data.properties.some((p) => p.id === selectedProperty)
                  ? selectedProperty
                  : "all"
              }
              onChange={(e) => setSelectedProperty(e.target.value)}
            >
              <option value="all">
                Tất cả khu trọ ({data.properties.length})
              </option>
              {data.properties.map((prop) => (
                <option key={prop.id} value={prop.id}>
                  {prop.name}
                </option>
              ))}
            </select>
          </label>

          <div className="lo-quick-actions">
            <button
              type="button"
              className="lo-btn lo-btn--primary"
              onClick={() => navigate(`landlord/contracts/new/${room.propertyId}/${room.id}`)}
            >
              <Plus size={16} aria-hidden="true" />
              Tạo hợp đồng
            </button>
            <button
              type="button"
              className="lo-btn lo-btn--secondary"
              onClick={() => navigate("landlord/billing")}
            >
              <ReceiptText size={16} aria-hidden="true" />
              Ghi chỉ số / Thu tiền
            </button>
            <button
              type="button"
              className="lo-btn lo-btn--ghost"
              onClick={() => navigate("landlord/properties")}
            >
              <Building2 size={16} aria-hidden="true" />
              Quản lý khu trọ
            </button>
          </div>
        </div>
      </header>

      {data.partialError && (
        <div className="lo-partial-banner" role="status">
          <AlertCircle size={17} aria-hidden="true" />
          <span>
            Một số dữ liệu chưa thể đồng bộ đầy đủ. Số liệu hiển thị có thể chưa
            cập nhật hết.
          </span>
          <button type="button" className="lo-link-btn" onClick={reload}>
            Tải lại
          </button>
        </div>
      )}

      {/* Tầng 2 — 4 Thẻ KPI Cốt Lõi */}
      <section className="lo-kpi-grid" aria-label="Chỉ số vận hành cốt lõi">
        <button
          type="button"
          className="lo-kpi-card"
          onClick={() => navigate("landlord/properties")}
        >
          <div className="lo-kpi-card__top">
            <span className="lo-kpi-icon lo-kpi-icon--blue">
              <DoorOpen size={19} aria-hidden="true" />
            </span>
            <span className="lo-kpi-pill lo-kpi-pill--blue">
              {metrics.vacant} trống
            </span>
          </div>
          <div className="lo-kpi-card__body">
            <span className="lo-kpi-label">Tổng số phòng</span>
            <strong className="lo-kpi-value">
              {metrics.occupied}/{metrics.totalRooms} <span>phòng</span>
            </strong>
          </div>
          <div className="lo-kpi-card__foot">
            <span>{metrics.vacant} phòng còn trống</span>
            <ChevronRight size={15} aria-hidden="true" />
          </div>
        </button>

        <button
          type="button"
          className="lo-kpi-card"
          onClick={() =>
            navigate(
              selectedProperty && selectedProperty !== "all"
                ? `landlord/billing/status/Unpaid/${selectedProperty}`
                : "landlord/billing/status/Unpaid",
            )
          }
        >
          <div className="lo-kpi-card__top">
            <span className="lo-kpi-icon lo-kpi-icon--amber">
              <Wallet size={19} aria-hidden="true" />
            </span>
            {metrics.overdueCount > 0 ? (
              <span className="lo-kpi-pill lo-kpi-pill--danger">
                {metrics.overdueCount} quá hạn
              </span>
            ) : (
              <span className="lo-kpi-pill lo-kpi-pill--neutral">
                {data.unpaidInvoices.length} hóa đơn
              </span>
            )}
          </div>
          <div className="lo-kpi-card__body">
            <span className="lo-kpi-label">Tổng tiền còn phải thu</span>
            <strong className="lo-kpi-value">
              {formatCurrency(metrics.totalAmountDue)}
            </strong>
          </div>
          <div className="lo-kpi-card__foot">
            <span>
              {data.unpaidInvoices.length > 0
                ? `${data.unpaidInvoices.length} hóa đơn chưa thu đủ`
                : "Không có công nợ"}
            </span>
            <ChevronRight size={15} aria-hidden="true" />
          </div>
        </button>

        <button
          type="button"
          className="lo-kpi-card"
          onClick={() => navigate("landlord/billing")}
        >
          <div className="lo-kpi-card__top">
            <span className="lo-kpi-icon lo-kpi-icon--green">
              <CreditCard size={19} aria-hidden="true" />
            </span>
            <span
              className={`lo-kpi-pill ${
                metrics.pendingPaymentCount > 0
                  ? "lo-kpi-pill--warning"
                  : "lo-kpi-pill--success"
              }`}
            >
              {metrics.pendingPaymentCount > 0 ? "Cần duyệt" : "Đã xử lý"}
            </span>
          </div>
          <div className="lo-kpi-card__body">
            <span className="lo-kpi-label">Thanh toán chờ xác nhận</span>
            <strong className="lo-kpi-value">
              {metrics.pendingPaymentCount} <span>chứng từ</span>
            </strong>
          </div>
          <div className="lo-kpi-card__foot">
            <span>
              {metrics.pendingPaymentCount > 0
                ? "Người thuê đã gửi minh chứng"
                : "Không có chứng từ chờ"}
            </span>
            <ChevronRight size={15} aria-hidden="true" />
          </div>
        </button>

        <button
          type="button"
          className="lo-kpi-card"
          onClick={() =>
            navigate(
              data.openRequestsCount > 0
                ? "landlord/requests"
                : "landlord/contracts",
            )
          }
        >
          <div className="lo-kpi-card__top">
            <span className="lo-kpi-icon lo-kpi-icon--red">
              <MessageSquareText size={19} aria-hidden="true" />
            </span>
            <span
              className={`lo-kpi-pill ${
                metrics.attentionContractsAndRequests > 0
                  ? "lo-kpi-pill--warning"
                  : "lo-kpi-pill--success"
              }`}
            >
              {metrics.attentionContractsAndRequests > 0
                ? "Cần chú ý"
                : "Ổn định"}
            </span>
          </div>
          <div className="lo-kpi-card__body">
            <span className="lo-kpi-label">Yêu cầu & Hợp đồng</span>
            <strong className="lo-kpi-value">
              {metrics.attentionContractsAndRequests} <span>mục</span>
            </strong>
          </div>
          <div className="lo-kpi-card__foot">
            <span>
              {data.openRequestsCount} yêu cầu ·{" "}
              {metrics.pendingContracts + metrics.expiringContracts} hợp đồng
            </span>
            <ChevronRight size={15} aria-hidden="true" />
          </div>
        </button>
      </section>

      {/* Tầng 3 — Khu vực Theo dõi & Hành động (Bento 2 cột) */}
      <section className="lo-bento-grid">
        {/* Cột trái: Tình trạng phòng & Phòng trống nổi bật */}
        <article className="lo-card">
          <div className="lo-card__head">
            <div>
              <h2>Tình trạng phòng</h2>
              <p>Phân bổ phòng hiện tại và phòng trống sẵn sàng cho thuê.</p>
            </div>
            <button
              type="button"
              className="lo-text-link"
              onClick={() => navigate("landlord/properties")}
            >
              Xem khu trọ
              <ChevronRight size={15} aria-hidden="true" />
            </button>
          </div>

          <div className="lo-room-stats-strip">
            <div className="lo-room-stat">
              <span>Đã thuê</span>
              <strong>{metrics.occupied}</strong>
            </div>
            <div className="lo-room-stat lo-room-stat--vacant">
              <span>Còn trống</span>
              <strong>{metrics.vacant}</strong>
            </div>
            <div className="lo-room-stat lo-room-stat--maint">
              <span>Bảo trì</span>
              <strong>{metrics.maintenance}</strong>
            </div>
          </div>

          {!isAllProperties ? (
            <div className="lo-vacant-list">
              <div className="lo-subhead">
                <span>Phòng trống sẵn sàng</span>
                <small>Tối đa 4 phòng</small>
              </div>
              {metrics.vacantRooms.length > 0 ? (
                metrics.vacantRooms.map((room) => (
                  <div className="lo-vacant-item" key={room.id}>
                    <div className="lo-vacant-item__info">
                      <span className="lo-vacant-icon">
                        <DoorOpen size={17} aria-hidden="true" />
                      </span>
                      <div>
                        <strong>Phòng {room.roomNumber}</strong>
                        <small>
                          {room.propertyName || "—"} ·{" "}
                          {room.basePrice != null
                            ? `${formatCurrency(room.basePrice)}/tháng`
                            : "Chưa đặt giá"}
                        </small>
                      </div>
                    </div>
                    <button
                      type="button"
                      className="lo-mini-btn"
                      onClick={() => navigate(`landlord/contracts/new/${room.propertyId}/${room.id}`)}
                    >
                      <FilePlus2 size={14} aria-hidden="true" />
                      Tạo HĐ
                    </button>
                  </div>
                ))
              ) : (
                <p className="lo-empty-inline">
                  Hiện không có phòng trống nào sẵn sàng.
                </p>
              )}
            </div>
          ) : (
            <p className="lo-empty-inline">
              Đang xem số liệu chung của tất cả khu trọ. Chọn một khu trọ cụ thể để xem danh sách phòng trống chi tiết.
            </p>
          )}
        </article>

        {/* Cột phải: Việc cần xử lý ngay (Action Inbox) */}
        <article className="lo-card">
          <div className="lo-card__head">
            <div>
              <h2>Việc cần xử lý ngay</h2>
              <p>Các đầu việc ưu tiên ảnh hưởng đến thu chi và vận hành.</p>
            </div>
          </div>

          <div className="lo-inbox-list">
            <button
              type="button"
              className="lo-inbox-row"
              onClick={() => {
                if (metrics.pendingPaymentCount === 1 && data.pendingPayments[0]?.invoiceId) {
                  navigate(`landlord/billing/${data.pendingPayments[0].invoiceId}`);
                } else if (selectedProperty && selectedProperty !== "all") {
                  navigate(`landlord/billing/rooms/${selectedProperty}`);
                } else {
                  navigate("landlord/billing");
                }
              }}
            >
              <span className="lo-inbox-icon lo-inbox-icon--blue">
                <CreditCard size={18} aria-hidden="true" />
              </span>
              <span className="lo-inbox-text">
                <strong>Chứng từ thanh toán chờ duyệt</strong>
                <small>Kiểm tra và xác nhận khoản tiền người thuê đã gửi</small>
              </span>
              <span
                className={`lo-count-badge ${
                  metrics.pendingPaymentCount > 0 ? "is-warning" : ""
                }`}
              >
                {metrics.pendingPaymentCount}
              </span>
              <ChevronRight size={16} aria-hidden="true" />
            </button>

            <button
              type="button"
              className="lo-inbox-row"
              onClick={() => {
                if (metrics.overdueCount === 1 && metrics.firstOverdueId) {
                  navigate(`landlord/billing/${metrics.firstOverdueId}`);
                } else if (selectedProperty && selectedProperty !== "all") {
                  navigate(`landlord/billing/status/Overdue/${selectedProperty}`);
                } else {
                  navigate("landlord/billing/status/Overdue");
                }
              }}
            >
              <span className="lo-inbox-icon lo-inbox-icon--danger">
                <Clock size={18} aria-hidden="true" />
              </span>
              <span className="lo-inbox-text">
                <strong>Hóa đơn quá hạn thu</strong>
                <small>Nhắc thanh toán hoặc ghi nhận thu tiền mặt/chuyển khoản</small>
              </span>
              <span
                className={`lo-count-badge ${
                  metrics.overdueCount > 0 ? "is-danger" : ""
                }`}
              >
                {metrics.overdueCount}
              </span>
              <ChevronRight size={16} aria-hidden="true" />
            </button>

            <button
              type="button"
              className="lo-inbox-row"
              onClick={() => {
                if (metrics.pendingSettlement > 0 && metrics.expiringContracts === 0) {
                  navigate("landlord/contracts/settlement");
                } else if (metrics.expiringContracts > 0) {
                  navigate("landlord/contracts/expiring");
                } else {
                  navigate("landlord/contracts");
                }
              }}
            >
              <span className="lo-inbox-icon lo-inbox-icon--amber">
                <FileText size={18} aria-hidden="true" />
              </span>
              <span className="lo-inbox-text">
                <strong>Hợp đồng sắp hết hạn / chờ tất toán</strong>
                <small>
                  {metrics.expiringContracts} sắp hết hạn (≤ 30 ngày) ·{" "}
                  {metrics.pendingSettlement} chờ tất toán
                </small>
              </span>
              <span
                className={`lo-count-badge ${
                  metrics.contractsActionCount > 0 ? "is-warning" : ""
                }`}
              >
                {metrics.contractsActionCount}
              </span>
              <ChevronRight size={16} aria-hidden="true" />
            </button>

            <button
              type="button"
              className="lo-inbox-row"
              onClick={() => navigate("landlord/requests")}
            >
              <span className="lo-inbox-icon lo-inbox-icon--green">
                <MessageSquareText size={18} aria-hidden="true" />
              </span>
              <span className="lo-inbox-text">
                <strong>Yêu cầu người thuê chưa phản hồi</strong>
                <small>Phản hồi sự cố, sửa chữa và đề xuất từ phòng trọ</small>
              </span>
              <span
                className={`lo-count-badge ${
                  data.openRequestsCount > 0 ? "is-info" : ""
                }`}
              >
                {data.openRequestsCount}
              </span>
              <ChevronRight size={16} aria-hidden="true" />
            </button>
          </div>
        </article>
      </section>

      {/* Tầng 4 — Danh sách Khu trọ (dành cho Chủ trọ) */}
      {!isManager && data.properties.length > 0 && (
        <section className="lo-card" aria-label="Khu trọ của bạn">
          <div className="lo-card__head">
            <div>
              <h2>Khu trọ của bạn</h2>
              <p>Chọn khu trọ để xem chi tiết phòng, hợp đồng và cấu hình thanh toán.</p>
            </div>
            <button
              type="button"
              className="lo-text-link"
              onClick={() => navigate("landlord/properties")}
            >
              Quản lý khu trọ
              <ChevronRight size={15} aria-hidden="true" />
            </button>
          </div>
          <div className="lo-properties-grid">
            {data.properties.slice(0, 6).map((prop) => {
              const counts = metrics.roomCountsByProperty.get(prop.id);
              return (
                <button
                  type="button"
                  key={prop.id}
                  className="lo-property-item"
                  onClick={() => navigate(`landlord/properties/${prop.id}`)}
                >
                  <div className="lo-property-item__top">
                    <span className="lo-property-icon">
                      <Building2 size={17} aria-hidden="true" />
                    </span>
                    <span className={`lo-status ${prop.isActive ? "lo-status--success" : "lo-status--neutral"}`}>
                      {prop.isActive ? "Hoạt động" : "Tạm ngừng"}
                    </span>
                  </div>
                  <strong>{prop.name}</strong>
                  <small className="lo-property-item__addr">{prop.address || "Chưa cập nhật địa chỉ"}</small>
                  <div className="lo-property-item__foot">
                    <span>
                      {counts
                        ? `${counts.occupied}/${counts.total} phòng đã thuê · ${counts.available} trống`
                        : "Xem chi tiết khu trọ"}
                    </span>
                    <ChevronRight size={14} aria-hidden="true" />
                  </div>
                </button>
              );
            })}
          </div>
        </section>
      )}
    </div>
  );
}