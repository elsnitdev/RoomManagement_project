import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  Bell,
  Building2,
  CalendarClock,
  CheckCircle2,
  ChevronRight,
  FileText,
  Home,
  MessageSquareText,
  ReceiptText,
  RefreshCw,
  ShieldAlert,
  Wrench,
} from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { billingApi } from "../services/api/billingApi";
import { contractApi } from "../services/api/contractApi";
import { notificationApi } from "../services/api/notificationApi";
import { requestApi } from "../services/api/requestApi";
import { vietnamToday } from "./billing/paymentDate";
import {
  buildTenantOverviewModel,
  formatCurrency,
  formatShortDate,
} from "./tenantOverviewModel";
import "../styles/tenant-overview.css";

function getTodayLabel() {
  return new Intl.DateTimeFormat("vi-VN", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(new Date());
}

const INITIAL_RAW = {
  contracts: [],
  stays: [],
  invoices: [],
  requests: [],
  unreadNotifications: 0,
  rules: [],
};

export default function TenantOverview({ name, navigate }) {
  const { user } = useAuth();
  const [raw, setRaw] = useState(INITIAL_RAW);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [fatalError, setFatalError] = useState("");
  const [partialError, setPartialError] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);

  const displayName = useMemo(() => {
    const resolved = (
      name ||
      user?.fullName ||
      user?.name ||
      user?.userName ||
      ""
    ).trim();
    return resolved || "bạn";
  }, [name, user]);

  const reload = useCallback(() => {
    setReloadTick((tick) => tick + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;

    async function loadOverview() {
      const isInitial = reloadTick === 0;
      if (isInitial) setLoading(true);
      else setRefreshing(true);
      setFatalError("");
      setPartialError(false);

      const results = await Promise.allSettled([
        contractApi.mine(signal),
        requestApi.getMyRequestProperties({ signal }),
        billingApi.allMyInvoices(signal),
        requestApi.getMyRequests({ page: 1, pageSize: 50, signal }),
        notificationApi.unreadCount(),
      ]);

      if (signal.aborted) return;

      const failedCount = results.filter((r) => r.status === "rejected").length;
      if (failedCount === results.length) {
        setFatalError(
          results[0].reason?.message ||
            "Không thể tải dữ liệu tổng quan. Vui lòng thử lại.",
        );
        setLoading(false);
        setRefreshing(false);
        return;
      }

      const contracts =
        results[0].status === "fulfilled" ? results[0].value?.data || [] : [];
      const stays =
        results[1].status === "fulfilled" ? results[1].value || [] : [];
      const invoices =
        results[2].status === "fulfilled" ? results[2].value?.data || [] : [];
      const requests =
        results[3].status === "fulfilled" ? results[3].value?.data || [] : [];
      const unreadNotifications =
        results[4].status === "fulfilled"
          ? Number(results[4].value?.data?.count || 0)
          : 0;

      let rules = [];
      let rulesFailed = false;
      const hasLegacyContract = contracts.some(
        (contract) => contract.workflow !== "Invitation",
      );
      if (hasLegacyContract) {
        try {
          rules = (await contractApi.myRules(signal)) || [];
        } catch {
          if (!signal.aborted) rulesFailed = true;
        }
      }

      if (signal.aborted) return;

      setRaw({
        contracts,
        stays,
        invoices,
        requests,
        unreadNotifications,
        rules,
      });
      setPartialError(failedCount > 0 || rulesFailed);
      setLoading(false);
      setRefreshing(false);
    }

    loadOverview();
    return () => controller.abort();
  }, [reloadTick]);

  const model = useMemo(
    () =>
      buildTenantOverviewModel({
        ...raw,
        todayIso: vietnamToday(),
      }),
    [raw],
  );

  const { home, billing, activity, attentionItems } = model;

  return (
    <div className="to-page" aria-busy={loading || refreshing}>
      <header className="to-header">
        <div className="to-header__intro">
          <div className="to-header__meta">
            <span className="to-eyebrow">TỔNG QUAN</span>
            <span className="to-date">{getTodayLabel()}</span>
          </div>
          <h1>Chào {displayName}</h1>
          <p>Những thông tin nổi bật về chỗ ở, hóa đơn và hoạt động của bạn.</p>
        </div>

        <button
          type="button"
          className="to-refresh-btn"
          onClick={reload}
          disabled={loading || refreshing}
          aria-label="Làm mới dữ liệu tổng quan"
        >
          <RefreshCw
            size={15}
            className={loading || refreshing ? "to-spin" : ""}
            aria-hidden="true"
          />
          <span>{refreshing ? "Đang cập nhật…" : "Làm mới"}</span>
        </button>
      </header>

      {partialError && !fatalError && (
        <div className="to-banner to-banner--warn" role="status">
          <AlertCircle size={17} aria-hidden="true" />
          <span>
            Một số thông tin chưa thể cập nhật đầy đủ. Dữ liệu còn lại vẫn hiển
            thị bình thường.
          </span>
          <button type="button" className="to-banner__btn" onClick={reload}>
            Thử lại
          </button>
        </div>
      )}

      {fatalError ? (
        <section className="to-card to-error-state" role="alert">
          <AlertCircle size={28} aria-hidden="true" />
          <strong>Không thể tải trang tổng quan</strong>
          <p>{fatalError}</p>
          <button
            type="button"
            className="to-btn to-btn--primary"
            onClick={reload}
          >
            Thử lại
          </button>
        </section>
      ) : loading ? (
        <section
          className="to-highlight-grid"
          aria-label="Đang tải dữ liệu tổng quan"
        >
          {[0, 1, 2].map((idx) => (
            <div key={idx} className="to-card to-skeleton-card">
              <div className="to-skeleton to-skeleton--sm" />
              <div className="to-skeleton to-skeleton--lg" />
              <div className="to-skeleton to-skeleton--md" />
              <div className="to-skeleton to-skeleton--btn" />
            </div>
          ))}
        </section>
      ) : (
        <>
          <section
            className="to-highlight-grid"
            aria-label="Thông tin nổi bật"
          >
            {/* Thẻ 1: Chỗ ở hiện tại */}
            <article className="to-card to-card--home">
              <div className="to-card__head">
                <span className="to-kicker">
                  <Home size={15} aria-hidden="true" />
                  CHỖ Ở HIỆN TẠI
                </span>
                <span
                  className={`to-pill to-pill--${home ? home.statusTone : "neutral"}`}
                >
                  {home ? home.statusLabel : "Chưa có phòng"}
                </span>
              </div>

              {home ? (
                <div className="to-home-body">
                  <div className="to-home-title-row">
                    <strong className="to-room-name">
                      Phòng {home.roomNumber || "—"}
                    </strong>
                    {home.extraRoomsCount > 0 && (
                      <span className="to-extra-chip">
                        +{home.extraRoomsCount} phòng khác
                      </span>
                    )}
                  </div>
                  <p className="to-property-name">
                    <Building2 size={15} aria-hidden="true" />
                    <span>{home.propertyName}</span>
                  </p>

                  <dl className="to-facts">
                    <div>
                      <dt>Tiền thuê / tháng</dt>
                      <dd className="to-tabular">
                        {home.monthlyRent != null
                          ? formatCurrency(home.monthlyRent)
                          : "Theo hợp đồng"}
                      </dd>
                    </div>
                    <div>
                      <dt>
                        {home.paymentDueDay
                          ? "Hạn thanh toán"
                          : home.endDate
                            ? "Thời hạn đến"
                            : "Ngày bắt đầu"}
                      </dt>
                      <dd className="to-tabular">
                        {home.paymentDueDay
                          ? `Ngày ${home.paymentDueDay} hằng tháng`
                          : home.endDate
                            ? formatShortDate(home.endDate)
                            : formatShortDate(home.startDate)}
                      </dd>
                    </div>
                  </dl>
                </div>
              ) : (
                <div className="to-empty-inline">
                  <strong>Chưa có phòng đang thuê</strong>
                  <p>
                    Thông tin phòng sẽ xuất hiện khi bạn có hợp đồng đang hiệu
                    lực hoặc chờ xác nhận.
                  </p>
                </div>
              )}

              <div className="to-card__foot">
                <button
                  type="button"
                  className="to-btn to-btn--primary"
                  onClick={() => navigate("tenant/contracts")}
                >
                  <span>Xem hợp đồng</span>
                  <ArrowRight size={15} aria-hidden="true" />
                </button>
              </div>
            </article>

            {/* Thẻ 2: Hóa đơn & Số dư */}
            <article className="to-card to-card--billing">
              <div className="to-card__head">
                <span className="to-kicker">
                  <ReceiptText size={15} aria-hidden="true" />
                  HÓA ĐƠN & SỐ DƯ
                </span>
                <span className={`to-pill to-pill--${billing.tone}`}>
                  {billing.overdueCount > 0
                    ? `${billing.overdueCount} quá hạn`
                    : billing.totalAmountDue > 0
                      ? `${billing.unpaidCount} chưa xong`
                      : "Đã thanh toán đủ"}
                </span>
              </div>

              {billing.totalAmountDue > 0 ? (
                <div className="to-billing-body">
                  <small className="to-billing-caption">
                    Tổng cần thanh toán
                  </small>
                  <strong
                    className={`to-balance-value to-tabular${billing.overdueCount > 0 ? " is-danger" : ""}`}
                  >
                    {formatCurrency(billing.totalAmountDue)}
                  </strong>
                  <p className="to-billing-meta">
                    <CalendarClock size={14} aria-hidden="true" />
                    <span>
                      {billing.unpaidCount} hóa đơn chưa thanh toán
                      {billing.nearestDueDate
                        ? ` · Hạn gần nhất ${formatShortDate(billing.nearestDueDate)}`
                        : ""}
                    </span>
                  </p>
                  {billing.totalCredit > 0 && (
                    <p className="to-credit-note">
                      Có {formatCurrency(billing.totalCredit)} tiền dư chờ đối
                      soát.
                    </p>
                  )}
                </div>
              ) : (
                <div className="to-billing-clear">
                  <div className="to-clear-badge">
                    <CheckCircle2 size={20} aria-hidden="true" />
                    <strong>Đã thanh toán đủ</strong>
                  </div>
                  <p>
                    {billing.totalInvoices > 0
                      ? "Hiện bạn không còn khoản hóa đơn nào cần thanh toán."
                      : "Chưa có hóa đơn phát sinh cần thanh toán."}
                  </p>
                  {billing.totalCredit > 0 && (
                    <p className="to-credit-note">
                      Số dư tiền thừa: {formatCurrency(billing.totalCredit)}.
                    </p>
                  )}
                </div>
              )}

              <div className="to-card__foot">
                <button
                  type="button"
                  className={`to-btn ${billing.totalAmountDue > 0 ? "to-btn--primary" : "to-btn--secondary"}`}
                  onClick={() => navigate("tenant/billing")}
                >
                  <span>
                    {billing.totalAmountDue > 0
                      ? "Thanh toán / Xem hóa đơn"
                      : "Xem hóa đơn"}
                  </span>
                  <ArrowRight size={15} aria-hidden="true" />
                </button>
              </div>
            </article>

            {/* Thẻ 3: Tóm tắt hoạt động */}
            <article className="to-card to-card--activity">
              <div className="to-card__head">
                <span className="to-kicker">
                  <Bell size={15} aria-hidden="true" />
                  HOẠT ĐỘNG NỔI BẬT
                </span>
              </div>

              <div className="to-activity-list">
                <button
                  type="button"
                  className="to-activity-row"
                  onClick={() => navigate("tenant/contracts")}
                >
                  <span className="to-activity-icon to-activity-icon--blue">
                    <FileText size={16} aria-hidden="true" />
                  </span>
                  <span className="to-activity-text">
                    <strong>Hợp đồng thuê</strong>
                    <small>
                      {activity.pendingConfirmCount > 0
                        ? `${activity.pendingConfirmCount} hợp đồng chờ xác nhận`
                        : `${activity.activeContractsCount} đang hiệu lực`}
                    </small>
                  </span>
                  <span
                    className={`to-count-pill ${activity.pendingConfirmCount > 0 ? "is-warn" : ""}`}
                  >
                    {activity.pendingConfirmCount > 0
                      ? activity.pendingConfirmCount
                      : activity.activeContractsCount}
                  </span>
                  <ChevronRight size={15} aria-hidden="true" />
                </button>

                <button
                  type="button"
                  className="to-activity-row"
                  onClick={() => navigate("tenant/requests")}
                >
                  <span className="to-activity-icon to-activity-icon--teal">
                    <Wrench size={16} aria-hidden="true" />
                  </span>
                  <span className="to-activity-text">
                    <strong>Yêu cầu hỗ trợ</strong>
                    <small>
                      {activity.openRequestsCount > 0
                        ? "Đang chờ phản hồi / xử lý"
                        : "Không có yêu cầu đang mở"}
                    </small>
                  </span>
                  <span
                    className={`to-count-pill ${activity.openRequestsCount > 0 ? "is-info" : ""}`}
                  >
                    {activity.openRequestsCount}
                  </span>
                  <ChevronRight size={15} aria-hidden="true" />
                </button>

                <button
                  type="button"
                  className="to-activity-row"
                  onClick={() => navigate("tenant/notices")}
                >
                  <span className="to-activity-icon to-activity-icon--amber">
                    <Bell size={16} aria-hidden="true" />
                  </span>
                  <span className="to-activity-text">
                    <strong>Thông báo</strong>
                    <small>
                      {activity.unreadNotificationsCount > 0
                        ? "Thông báo chưa đọc"
                        : "Bạn đã đọc tất cả"}
                    </small>
                  </span>
                  <span
                    className={`to-count-pill ${activity.unreadNotificationsCount > 0 ? "is-warn" : ""}`}
                  >
                    {activity.unreadNotificationsCount}
                  </span>
                  <ChevronRight size={15} aria-hidden="true" />
                </button>
              </div>
            </article>
          </section>

          {/* Khối Cần chú ý: chỉ hiển thị khi có việc thực sự cần làm */}
          {attentionItems.length > 0 && (
            <section
              className="to-attention"
              aria-label="Việc cần chú ý"
            >
              <div className="to-section-head">
                <h2>Cần chú ý</h2>
                <span>{attentionItems.length} mục ưu tiên</span>
              </div>
              <div className="to-attention-list">
                {attentionItems.map((item) => (
                  <div
                    key={item.id}
                    className={`to-attention-item to-attention-item--${item.tone}`}
                  >
                    <span className="to-attention-icon" aria-hidden="true">
                      {item.tone === "danger" ? (
                        <AlertTriangle size={18} />
                      ) : (
                        <ShieldAlert size={18} />
                      )}
                    </span>
                    <div className="to-attention-content">
                      <strong>{item.title}</strong>
                      <p>{item.detail}</p>
                    </div>
                    <button
                      type="button"
                      className="to-attention-btn"
                      onClick={() => navigate(item.target)}
                    >
                      {item.actionLabel}
                      <ArrowRight size={14} aria-hidden="true" />
                    </button>
                  </div>
                ))}
              </div>
            </section>
          )}

          {/* Truy cập nhanh */}
          <section className="to-quick-section" aria-label="Truy cập nhanh">
            <h2>Truy cập nhanh</h2>
            <div className="to-quick-grid">
              <button
                type="button"
                className="to-quick-card"
                onClick={() => navigate("tenant/billing")}
              >
                <span className="to-quick-icon">
                  <ReceiptText size={18} aria-hidden="true" />
                </span>
                <span className="to-quick-text">
                  <strong>Hóa đơn</strong>
                  <small>Số dư & thanh toán</small>
                </span>
                <ChevronRight size={16} aria-hidden="true" />
              </button>

              <button
                type="button"
                className="to-quick-card"
                onClick={() => navigate("tenant/contracts")}
              >
                <span className="to-quick-icon">
                  <FileText size={18} aria-hidden="true" />
                </span>
                <span className="to-quick-text">
                  <strong>Hợp đồng</strong>
                  <small>Thời hạn & nội quy</small>
                </span>
                <ChevronRight size={16} aria-hidden="true" />
              </button>

              <button
                type="button"
                className="to-quick-card"
                onClick={() => navigate("tenant/requests")}
              >
                <span className="to-quick-icon">
                  <MessageSquareText size={18} aria-hidden="true" />
                </span>
                <span className="to-quick-text">
                  <strong>Yêu cầu hỗ trợ</strong>
                  <small>Báo sự cố, sửa chữa</small>
                </span>
                <ChevronRight size={16} aria-hidden="true" />
              </button>

              <button
                type="button"
                className="to-quick-card"
                onClick={() => navigate("tenant/notices")}
              >
                <span className="to-quick-icon">
                  <Bell size={18} aria-hidden="true" />
                </span>
                <span className="to-quick-text">
                  <strong>Thông báo</strong>
                  <small>Tin tức từ chủ trọ</small>
                </span>
                <ChevronRight size={16} aria-hidden="true" />
              </button>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
