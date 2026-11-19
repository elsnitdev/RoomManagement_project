import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertCircle,
  Check,
  CheckCircle2,
  Clock,
  Copy,
  FileCheck2,
  FileText,
  FolderOpen,
  History,
  RefreshCw,
  Search,
  ShieldAlert,
  ShieldCheck,
  UserRound,
  X,
} from "lucide-react";
import { contractApi } from "../../services/api/contractApi";
import { TenantRules } from "./ContractRules";
import ContractDocuments from "./ContractDocuments";
import "./contracts.css";
import "./tenantContracts.css";

const PAGE_SIZE = 10;

const money = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);

const formatDate = (value) =>
  value
    ? new Intl.DateTimeFormat("vi-VN").format(new Date(`${value}T12:00:00`))
    : "Không xác định thời hạn";

const formatShortDate = (value) =>
  value
    ? new Intl.DateTimeFormat("vi-VN").format(new Date(`${value}T12:00:00`))
    : "Chưa xác định";

const statusText = {
  Draft: "Bản nháp",
  PendingTenantConfirmation: "Chờ bạn xác nhận",
  PendingOperatorConfirmation: "Chờ chủ trọ đối ký",
  PendingStart: "Chờ ngày bắt đầu",
  PendingCancelSettlement: "Chờ xử lý hủy & cọc",
  Active: "Đang hiệu lực",
  PendingMoveOut: "Chờ bàn giao",
  PendingSettlement: "Chờ thanh toán và tất toán",
  Expired: "Hết hạn",
  Terminated: "Đã kết thúc",
  Cancelled: "Đã hủy",
};

const ENDED_STATUSES = [
  "PendingMoveOut",
  "PendingSettlement",
  "PendingCancelSettlement",
  "Expired",
  "Terminated",
  "Cancelled",
];

const UPCOMING_STATUSES = [
  "PendingStart",
  "PendingOperatorConfirmation",
  "Draft",
];

function getInvitationState(contract) {
  if (contract?.workflow !== "Invitation") return null;
  const ownLink = contract.tenants?.find((tenant) => !tenant.leaveDate);
  const isRepresentative = Boolean(ownLink?.isContactPerson);
  const accepted =
    ownLink?.invitationAcceptedOfferVersion === contract.offerVersion;
  const awaitingResponse = contract.status === "PendingTenantConfirmation";
  const canConfirm = isRepresentative
    ? awaitingResponse ||
      Boolean(
        contract.offerResponseTimedOutAt &&
        ["PendingStart", "Active"].includes(contract.status),
      )
    : [
        "PendingTenantConfirmation",
        "PendingOperatorConfirmation",
        "PendingStart",
        "Active",
      ].includes(contract.status);
  return { ownLink, isRepresentative, accepted, canConfirm };
}

function getDisplayStatus(contract) {
  if (
    contract?.workflow === "Invitation" &&
    contract?.status === "PendingTenantConfirmation" &&
    contract?.tenants?.some(
      (tenant) =>
        !tenant.leaveDate &&
        tenant.invitationAcceptedOfferVersion === contract.offerVersion,
    )
  ) {
    return "Chờ người đại diện";
  }
  return statusText[contract?.status] || contract?.status;
}

function needsContractConfirmation(contract) {
  if (!contract) return false;
  if (contract.workflow === "Invitation") {
    const inv = getInvitationState(contract);
    return Boolean(inv && !inv.accepted && inv.canConfirm);
  }
  return contract.status === "PendingTenantConfirmation";
}

async function copyContractCode(code, notify, onCopied) {
  try {
    await navigator.clipboard.writeText(code);
    notify?.(`Đã sao chép mã ${code}.`);
    onCopied?.();
  } catch {
    notify?.("Không sao chép được. Vui lòng chọn và sao chép mã thủ công.");
  }
}

export default function TenantContracts({ notify }) {
  const [contracts, setContracts] = useState([]);
  const [rulesList, setRulesList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [rulesRevision, setRulesRevision] = useState(0);

  const [selectedId, setSelectedId] = useState("");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [detailTab, setDetailTab] = useState("overview");
  const [rulesModalOpen, setRulesModalOpen] = useState(false);

  const [confirmBusyId, setConfirmBusyId] = useState(null);
  const [agreedMap, setAgreedMap] = useState({});
  const [rejectingId, setRejectingId] = useState("");
  const [rejectReason, setRejectReason] = useState("");
  const [offerError, setOfferError] = useState("");
  const [copiedCode, setCopiedCode] = useState("");

  const detailRef = useRef(null);
  const copyTimerRef = useRef(null);

  useEffect(() => () => clearTimeout(copyTimerRef.current), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    contractApi
      .mine(controller.signal)
      .then((response) => {
        if (controller.signal.aborted) return;
        const rows = response.data || [];
        setContracts(rows);
        setSelectedId((current) => {
          if (current && rows.some((item) => item.id === current))
            return current;
          const prioritized =
            rows.find(needsContractConfirmation) ||
            rows.find((item) => item.status === "Active") ||
            rows[0];
          return prioritized?.id || "";
        });
      })
      .catch((e) => {
        if (!controller.signal.aborted) {
          setError(e.message || "Không thể tải hợp đồng.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [revision]);

  useEffect(() => {
    const hasLegacy = contracts.some(
      (contract) => contract.workflow !== "Invitation",
    );
    if (!hasLegacy) {
      setRulesList([]);
      return undefined;
    }
    const controller = new AbortController();
    contractApi
      .myRules(controller.signal)
      .then((items) => {
        if (!controller.signal.aborted) setRulesList(items || []);
      })
      .catch(() => {
        if (!controller.signal.aborted) setRulesList([]);
      });
    return () => controller.abort();
  }, [contracts, revision, rulesRevision]);

  useEffect(() => {
    if (!rulesModalOpen) return undefined;
    const onKeyDown = (event) => {
      if (event.key === "Escape") setRulesModalOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [rulesModalOpen]);

  const unacceptedRuleContractIds = useMemo(() => {
    const set = new Set();
    for (const rule of rulesList) {
      if (rule.versionId && !rule.acceptedCurrentVersion) {
        set.add(rule.contractId);
      }
    }
    return set;
  }, [rulesList]);

  const unacceptedRulesCount = useMemo(
    () =>
      rulesList.filter((rule) => rule.versionId && !rule.acceptedCurrentVersion)
        .length,
    [rulesList],
  );

  const hasLegacyContracts = useMemo(
    () => contracts.some((contract) => contract.workflow !== "Invitation"),
    [contracts],
  );

  const stats = useMemo(() => {
    let activeCount = 0;
    let actionCount = 0;
    let upcomingCount = 0;
    let endedCount = 0;

    for (const contract of contracts) {
      if (
        needsContractConfirmation(contract) ||
        unacceptedRuleContractIds.has(contract.id)
      ) {
        actionCount += 1;
      }
      if (contract.status === "Active") activeCount += 1;
      else if (UPCOMING_STATUSES.includes(contract.status)) upcomingCount += 1;
      else if (ENDED_STATUSES.includes(contract.status)) endedCount += 1;
    }

    return {
      total: contracts.length,
      activeCount,
      actionCount,
      upcomingCount,
      endedCount,
    };
  }, [contracts, unacceptedRuleContractIds]);

  const filteredContracts = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return contracts.filter((contract) => {
      if (
        statusFilter === "action" &&
        !needsContractConfirmation(contract) &&
        !unacceptedRuleContractIds.has(contract.id)
      ) {
        return false;
      }
      if (statusFilter === "active" && contract.status !== "Active") {
        return false;
      }
      if (
        statusFilter === "pending" &&
        !UPCOMING_STATUSES.includes(contract.status) &&
        contract.status !== "PendingTenantConfirmation"
      ) {
        return false;
      }
      if (
        statusFilter === "ended" &&
        !ENDED_STATUSES.includes(contract.status)
      ) {
        return false;
      }

      if (!keyword) return true;
      const memberNames = (contract.members ?? contract.tenants ?? [])
        .map((m) => m.fullName)
        .join(" ");
      const haystack = [
        contract.contractCode,
        `Phòng ${contract.roomNumber}`,
        contract.roomNumber,
        getDisplayStatus(contract),
        memberNames,
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(keyword);
    });
  }, [contracts, search, statusFilter, unacceptedRuleContractIds]);

  const totalPages = Math.max(
    1,
    Math.ceil(filteredContracts.length / PAGE_SIZE),
  );
  const safePage = Math.min(page, totalPages);
  const pagedContracts = useMemo(() => {
    const start = (safePage - 1) * PAGE_SIZE;
    return filteredContracts.slice(start, start + PAGE_SIZE);
  }, [filteredContracts, safePage]);

  useEffect(() => {
    setPage(1);
  }, [search, statusFilter]);

  useEffect(() => {
    if (!filteredContracts.length) return;
    if (!filteredContracts.some((item) => item.id === selectedId)) {
      setSelectedId(filteredContracts[0].id);
    }
  }, [filteredContracts, selectedId]);

  const selected =
    filteredContracts.find((item) => item.id === selectedId) ||
    contracts.find((item) => item.id === selectedId) ||
    null;

  function chooseContract(id) {
    setSelectedId(id);
    setDetailTab("overview");
    setOfferError("");
    setRejectingId("");
    setRejectReason("");
    if (window.innerWidth <= 960 && detailRef.current) {
      detailRef.current.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }

  function clearFilters() {
    setSearch("");
    setStatusFilter("all");
    setPage(1);
  }

  function handleCopy(code) {
    copyContractCode(code, notify, () => {
      setCopiedCode(code);
      clearTimeout(copyTimerRef.current);
      copyTimerRef.current = setTimeout(() => setCopiedCode(""), 2200);
    });
  }

  async function acceptInvitation(contract, isRepresentative) {
    setConfirmBusyId(contract.id);
    setOfferError("");
    try {
      const body = {
        offerVersion: contract.offerVersion,
        termsHash: contract.offerTermsHash,
      };
      const result = isRepresentative
        ? await contractApi.confirmOffer(contract.id, body)
        : await contractApi.joinInvitation(contract.id, body);
      notify?.(
        isRepresentative
          ? result.status === "PendingOperatorConfirmation"
            ? "Đã xác nhận lời mời. Hợp đồng đang chờ chủ trọ duyệt."
            : result.status === "PendingStart"
              ? `Đã xác nhận lời mời. Hợp đồng có hiệu lực từ ${formatDate(contract.startDate)}.`
              : "Đã xác nhận lời mời. Hợp đồng đã có hiệu lực."
          : "Đã xác nhận tham gia. Bạn sẽ được tính là thành viên phòng khi hợp đồng có hiệu lực.",
      );
      setRevision((value) => value + 1);
    } catch (cause) {
      setOfferError(cause.message || "Không thể xác nhận lời mời.");
    } finally {
      setConfirmBusyId(null);
    }
  }

  async function confirmLegacyOffer(contract) {
    setConfirmBusyId(contract.id);
    setOfferError("");
    try {
      await contractApi.confirmOffer(contract.id, {
        offerVersion: contract.offerVersion || 1,
        termsHash: contract.offerTermsHash || "",
        rulesVersionId: contract.offerRulesVersionId,
        acceptRules: true,
      });
      notify?.("Đã xác nhận đồng ý hợp đồng và nội quy. Chờ chủ trọ đối ký.");
      setRevision((v) => v + 1);
    } catch (err) {
      setOfferError(err.message || "Không thể xác nhận đề nghị hợp đồng.");
    } finally {
      setConfirmBusyId(null);
    }
  }

  async function rejectOffer(contract) {
    setConfirmBusyId(contract.id);
    setOfferError("");
    try {
      await contractApi.rejectOffer(
        contract.id,
        contract.offerVersion,
        rejectReason.trim(),
      );
      notify?.("Đã từ chối đề nghị hợp đồng.");
      setRejectingId("");
      setRejectReason("");
      setRevision((v) => v + 1);
    } catch (err) {
      setOfferError(err.message || "Không thể từ chối đề nghị.");
    } finally {
      setConfirmBusyId(null);
    }
  }

  function renderOfferRejection(contract) {
    const busy = confirmBusyId === contract.id;
    if (rejectingId !== contract.id) {
      return (
        <button
          type="button"
          className="tn-action tn-action--secondary"
          disabled={busy}
          onClick={() => {
            setRejectingId(contract.id);
            setRejectReason("");
            setOfferError("");
          }}
        >
          Từ chối đề nghị
        </button>
      );
    }
    return (
      <div className="tc-reject-box">
        <label>
          Lý do từ chối (không bắt buộc)
          <textarea
            rows={2}
            maxLength={1000}
            value={rejectReason}
            disabled={busy}
            onChange={(event) => setRejectReason(event.target.value)}
          />
        </label>
        <div className="tc-reject-actions">
          <button
            type="button"
            className="contract-button contract-button--danger"
            disabled={busy}
            onClick={() => rejectOffer(contract)}
          >
            {busy ? "Đang gửi…" : "Xác nhận từ chối"}
          </button>
          <button
            type="button"
            className="contract-button contract-button--ghost"
            disabled={busy}
            onClick={() => setRejectingId("")}
          >
            Hủy
          </button>
        </div>
      </div>
    );
  }

  const filterChips = [
    { id: "all", label: "Tất cả", count: contracts.length },
    { id: "action", label: "Cần thao tác", count: stats.actionCount },
    { id: "active", label: "Đang hiệu lực", count: stats.activeCount },
    { id: "pending", label: "Chờ hiệu lực", count: stats.upcomingCount },
    { id: "ended", label: "Đã kết thúc", count: stats.endedCount },
  ];

  const hasActiveFilters = Boolean(search.trim()) || statusFilter !== "all";
  const selectedMembers = selected
    ? (selected.members ?? selected.tenants ?? [])
    : [];
  const selectedHasDocs = Boolean(
    selected &&
    selected.workflow === "Invitation" &&
    selected.status !== "Cancelled" &&
    selected.status !== "Draft",
  );
  const selectedHasRules = Boolean(
    selected && selected.workflow !== "Invitation",
  );
  const activeDetailTab =
    (detailTab === "documents" && !selectedHasDocs) ||
    (detailTab === "rules" && !selectedHasRules)
      ? "overview"
      : detailTab;

  return (
    <div className="tc-page">
      <header className="tn-heading tc-header">
        <div>
          <span>HỢP ĐỒNG</span>
          <h1>Hợp đồng thuê</h1>
          <p>
            Quản lý thỏa thuận thuê phòng, xác nhận đề nghị hợp đồng và theo dõi
            nội quy khu trọ.
          </p>
        </div>
        <div className="tc-header-actions">
          {hasLegacyContracts && (
            <button
              type="button"
              className={`tn-action tn-action--secondary tc-rules-btn ${
                unacceptedRulesCount > 0 ? "is-alert" : ""
              }`}
              onClick={() => setRulesModalOpen(true)}
            >
              <ShieldCheck size={16} aria-hidden="true" />
              <span>Nội quy khu trọ</span>
              {unacceptedRulesCount > 0 && (
                <em className="tc-rules-pill">
                  {unacceptedRulesCount} cần duyệt
                </em>
              )}
            </button>
          )}
          <button
            type="button"
            className="tn-action tn-action--secondary tc-refresh-btn"
            disabled={loading}
            onClick={() => setRevision((value) => value + 1)}
          >
            <RefreshCw size={15} aria-hidden="true" />
            Làm mới
          </button>
        </div>
      </header>

      {!loading && contracts.length > 0 && (
        <section className="tc-kpis" aria-label="Tổng quan hợp đồng">
          <button
            type="button"
            className={`tc-kpi-card ${statusFilter === "all" ? "is-active" : ""}`}
            onClick={() => setStatusFilter("all")}
          >
            <span className="tc-kpi-icon" aria-hidden="true">
              <FileText size={19} />
            </span>
            <div>
              <small>Tổng hợp đồng</small>
              <strong>{stats.total}</strong>
            </div>
          </button>

          <button
            type="button"
            className={`tc-kpi-card ${statusFilter === "active" ? "is-active" : ""}`}
            onClick={() =>
              setStatusFilter((cur) => (cur === "active" ? "all" : "active"))
            }
          >
            <span className="tc-kpi-icon tc-kpi-icon--ok" aria-hidden="true">
              <CheckCircle2 size={19} />
            </span>
            <div>
              <small>Đang hiệu lực</small>
              <strong>{stats.activeCount}</strong>
            </div>
          </button>

          <button
            type="button"
            className={`tc-kpi-card ${statusFilter === "action" ? "is-active" : ""}`}
            onClick={() =>
              setStatusFilter((cur) => (cur === "action" ? "all" : "action"))
            }
          >
            <span
              className={`tc-kpi-icon ${stats.actionCount > 0 ? "tc-kpi-icon--warn" : "tc-kpi-icon--muted"}`}
              aria-hidden="true"
            >
              <Clock size={19} />
            </span>
            <div>
              <small>Cần xác nhận / duyệt</small>
              <strong>{stats.actionCount}</strong>
            </div>
          </button>

          <button
            type="button"
            className={`tc-kpi-card ${statusFilter === "ended" ? "is-active" : ""}`}
            onClick={() =>
              setStatusFilter((cur) => (cur === "ended" ? "all" : "ended"))
            }
          >
            <span className="tc-kpi-icon tc-kpi-icon--muted" aria-hidden="true">
              <History size={19} />
            </span>
            <div>
              <small>Đã kết thúc / Hủy</small>
              <strong>{stats.endedCount}</strong>
            </div>
          </button>
        </section>
      )}

      {!loading &&
        contracts.length > 0 &&
        (stats.actionCount > 0 || unacceptedRulesCount > 0) && (
          <div className="tc-alert-strip" role="status">
            <div className="tc-alert-strip__text">
              <ShieldAlert size={18} aria-hidden="true" />
              <span>
                {stats.actionCount > 0
                  ? `Bạn có ${stats.actionCount} hợp đồng cần xác nhận hoặc chấp thuận nội quy.`
                  : `Bạn có ${unacceptedRulesCount} nội quy khu trọ đang chờ chấp thuận.`}
              </span>
            </div>
            <div className="tc-alert-strip__actions">
              {stats.actionCount > 0 && statusFilter !== "action" && (
                <button
                  type="button"
                  className="tn-inline-action"
                  onClick={() => setStatusFilter("action")}
                >
                  Lọc hợp đồng cần thao tác
                </button>
              )}
              {unacceptedRulesCount > 0 && (
                <button
                  type="button"
                  className="tn-inline-action"
                  onClick={() => setRulesModalOpen(true)}
                >
                  Mở nội quy khu trọ
                </button>
              )}
            </div>
          </div>
        )}

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
        <div className="tc-workspace">
          <section
            className="tn-card tc-list-panel"
            aria-label="Danh sách hợp đồng"
          >
            <div className="tc-list-head">
              <div>
                <h2>Danh sách hợp đồng</h2>
                <small>
                  Hiển thị {filteredContracts.length} / {contracts.length} hợp
                  đồng
                </small>
              </div>
            </div>

            <div className="tc-search-box">
              <Search size={16} aria-hidden="true" />
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Tìm mã hợp đồng, số phòng…"
                aria-label="Tìm kiếm hợp đồng"
              />
              {search && (
                <button
                  type="button"
                  className="tc-search-clear"
                  onClick={() => setSearch("")}
                  aria-label="Xóa từ khóa tìm kiếm"
                >
                  <X size={14} aria-hidden="true" />
                </button>
              )}
            </div>

            <div
              className="tc-filter-chips"
              role="group"
              aria-label="Lọc trạng thái hợp đồng"
            >
              {filterChips.map((chip) => (
                <button
                  key={chip.id}
                  type="button"
                  className={statusFilter === chip.id ? "is-active" : ""}
                  onClick={() => setStatusFilter(chip.id)}
                  aria-pressed={statusFilter === chip.id}
                >
                  {chip.label}
                  <span>{chip.count}</span>
                </button>
              ))}
            </div>

            {pagedContracts.length ? (
              <>
                <ul className="tc-list">
                  {pagedContracts.map((contract) => {
                    const active = selected?.id === contract.id;
                    const needsConfirm = needsContractConfirmation(contract);
                    const needsRule = unacceptedRuleContractIds.has(
                      contract.id,
                    );
                    return (
                      <li key={contract.id}>
                        <button
                          type="button"
                          className={`tc-list-item ${active ? "is-active" : ""}`}
                          onClick={() => chooseContract(contract.id)}
                          aria-pressed={active}
                        >
                          <div className="tc-list-item__top">
                            <strong>Phòng {contract.roomNumber}</strong>
                            <span
                              className={`contract-status contract-status--${contract.status?.toLowerCase()}`}
                            >
                              {getDisplayStatus(contract)}
                            </span>
                          </div>
                          <div className="tc-list-item__code">
                            <span>Mã: {contract.contractCode}</span>
                            {(needsConfirm || needsRule) && (
                              <em className="tc-urgent-badge">
                                {needsConfirm
                                  ? "Cần xác nhận"
                                  : "Chưa duyệt nội quy"}
                              </em>
                            )}
                          </div>
                          <div className="tc-list-item__bottom">
                            <small>
                              {formatShortDate(contract.startDate)} →{" "}
                              {contract.endDate
                                ? formatShortDate(contract.endDate)
                                : "Không thời hạn"}
                            </small>
                            <strong>{money(contract.monthlyRent)}/tháng</strong>
                          </div>
                        </button>
                      </li>
                    );
                  })}
                </ul>

                {totalPages > 1 && (
                  <div className="bill-pagination tc-pagination">
                    <button
                      type="button"
                      disabled={safePage <= 1}
                      onClick={() => setPage((value) => value - 1)}
                    >
                      Trang trước
                    </button>
                    <span>
                      Trang {safePage} / {totalPages}
                    </span>
                    <button
                      type="button"
                      disabled={safePage >= totalPages}
                      onClick={() => setPage((value) => value + 1)}
                    >
                      Trang sau
                    </button>
                  </div>
                )}
              </>
            ) : (
              <div className="tc-filter-empty">
                <FileText size={24} aria-hidden="true" />
                <strong>Không có hợp đồng phù hợp</strong>
                <p>Thử đổi từ khóa tìm kiếm hoặc chọn bộ lọc khác.</p>
                {hasActiveFilters && (
                  <button
                    type="button"
                    className="tn-action tn-action--secondary"
                    onClick={clearFilters}
                  >
                    Xóa bộ lọc
                  </button>
                )}
              </div>
            )}
          </section>

          <div className="tc-detail-column" ref={detailRef}>
            {selected ? (
              <article className="tn-card tc-detail-card">
                <div className="tc-detail-head">
                  <div>
                    <div className="tc-detail-kicker-row">
                      <span className="tn-contract-kicker">
                        MÃ HỢP ĐỒNG · {selected.contractCode}
                      </span>
                      <button
                        type="button"
                        className="tc-copy-chip"
                        onClick={() => handleCopy(selected.contractCode)}
                      >
                        {copiedCode === selected.contractCode ? (
                          <>
                            <Check size={13} aria-hidden="true" /> Đã chép
                          </>
                        ) : (
                          <>
                            <Copy size={13} aria-hidden="true" /> Sao chép mã
                          </>
                        )}
                      </button>
                    </div>
                    <h2>Phòng {selected.roomNumber}</h2>
                    <p className="tc-detail-sub">
                      {formatDate(selected.startDate)} →{" "}
                      {formatDate(selected.endDate)} ·{" "}
                      {selected.workflow === "Invitation"
                        ? "Hợp đồng qua lời mời"
                        : "Hợp đồng truyền thống"}
                    </p>
                  </div>
                  <span
                    className={`contract-status contract-status--${selected.status?.toLowerCase()}`}
                  >
                    {getDisplayStatus(selected)}
                  </span>
                </div>

                {selected.offerResponseDeadline &&
                  selected.status === "PendingTenantConfirmation" && (
                    <p className="tc-info-box" role="status">
                      Người thuê đại diện có thể đồng ý hoặc từ chối trước{" "}
                      {new Date(selected.offerResponseDeadline).toLocaleString(
                        "vi-VN",
                        { timeZone: "Asia/Ho_Chi_Minh" },
                      )}{" "}
                      (giờ Việt Nam). Nếu hết hạn mà chưa phản hồi, đề nghị
                      chuyển sang chờ chủ trọ duyệt.
                    </p>
                  )}
                {selected.offerResponseTimedOutAt &&
                  selected.status === "PendingOperatorConfirmation" && (
                    <p className="tc-info-box" role="status">
                      Đề nghị đã hết 72 giờ phản hồi và đang chờ chủ trọ duyệt.
                    </p>
                  )}
                {selected.workflow === "Invitation" &&
                  (() => {
                    const inv = getInvitationState(selected);
                    if (!inv) return null;
                    if (inv.accepted || inv.canConfirm) {
                      return (
                        <div className="tn-offer-box tc-priority-box">
                          <strong>
                            Lời mời tham gia hợp đồng · Phiên bản{" "}
                            {selected.offerVersion}
                          </strong>
                          {inv.accepted ? (
                            <p role="status">
                              Bạn đã xác nhận tham gia
                              {selected.status === "PendingStart"
                                ? `. Hợp đồng có hiệu lực từ ${formatDate(selected.startDate)}.`
                                : selected.status ===
                                    "PendingTenantConfirmation"
                                  ? ". Đang chờ người đại diện xác nhận."
                                  : "."}
                            </p>
                          ) : (
                            <>
                              <p>
                                {inv.isRepresentative
                                  ? selected.offerResponseDeadline &&
                                    selected.status ===
                                      "PendingTenantConfirmation"
                                    ? "Bạn là người đại diện. Sau khi bạn xác nhận, đề nghị sẽ chuyển sang chờ chủ trọ duyệt."
                                    : selected.status ===
                                        "PendingTenantConfirmation"
                                      ? `Bạn là người đại diện. Sau khi bạn xác nhận, hợp đồng sẽ có hiệu lực từ ${formatDate(selected.startDate)}.`
                                      : "Bạn là người đại diện của hợp đồng."
                                  : "Xác nhận lời mời để được tính là thành viên phòng khi hợp đồng có hiệu lực."}
                              </p>
                              {offerError && (
                                <p className="form-error" role="alert">
                                  {offerError}
                                </p>
                              )}
                              <button
                                type="button"
                                className="tn-submit"
                                disabled={confirmBusyId === selected.id}
                                onClick={() =>
                                  acceptInvitation(
                                    selected,
                                    inv.isRepresentative &&
                                      selected.status ===
                                        "PendingTenantConfirmation",
                                  )
                                }
                              >
                                {confirmBusyId === selected.id
                                  ? "Đang xác nhận…"
                                  : "Xác nhận tham gia hợp đồng"}
                              </button>
                              {inv.isRepresentative &&
                                selected.status ===
                                  "PendingTenantConfirmation" &&
                                renderOfferRejection(selected)}
                            </>
                          )}
                        </div>
                      );
                    }
                    return (
                      <div className="tc-info-box">
                        <strong>
                          Lời mời tham gia hợp đồng · Phiên bản{" "}
                          {selected.offerVersion}
                        </strong>
                        <p>
                          {selected.status === "PendingOperatorConfirmation"
                            ? "Lời mời đang chờ chủ trọ duyệt."
                            : "Lời mời này không còn ở trạng thái chờ xác nhận."}
                        </p>
                      </div>
                    );
                  })()}

                {selected.workflow !== "Invitation" &&
                  selected.status === "PendingTenantConfirmation" &&
                  (() => {
                    const isRepresentative = selected.tenants?.some(
                      (t) =>
                        t.isContactPerson &&
                        t.role === "ContractParty" &&
                        !t.leaveDate,
                    );
                    const agreed = Boolean(agreedMap[selected.id]);
                    const isBusy = confirmBusyId === selected.id;
                    return (
                      <div className="tn-offer-box tc-priority-box">
                        <strong>
                          Đề nghị hợp đồng mới (Phiên bản{" "}
                          {selected.offerVersion || 1})
                        </strong>
                        {isRepresentative ? (
                          <>
                            <p>
                              Vui lòng kiểm tra kỹ thông tin tiền thuê, tiền
                              cọc, thời hạn và nội quy khu trọ trước khi xác
                              nhận.
                            </p>
                            <label className="tn-offer-agree">
                              <input
                                type="checkbox"
                                checked={agreed}
                                disabled={isBusy}
                                onChange={(e) =>
                                  setAgreedMap((cur) => ({
                                    ...cur,
                                    [selected.id]: e.target.checked,
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
                                onClick={() => confirmLegacyOffer(selected)}
                              >
                                {isBusy
                                  ? "Đang xác nhận..."
                                  : "Xác nhận đồng ý hợp đồng & nội quy"}
                              </button>
                            </div>
                            {renderOfferRejection(selected)}
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

                <div
                  className="tc-detail-tabs"
                  role="tablist"
                  aria-label="Thông tin chi tiết hợp đồng"
                >
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeDetailTab === "overview"}
                    className={
                      activeDetailTab === "overview" ? "is-active" : ""
                    }
                    onClick={() => setDetailTab("overview")}
                  >
                    <FileCheck2 size={15} aria-hidden="true" />
                    Tổng quan & Điều khoản
                  </button>
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeDetailTab === "members"}
                    className={activeDetailTab === "members" ? "is-active" : ""}
                    onClick={() => setDetailTab("members")}
                  >
                    <UserRound size={15} aria-hidden="true" />
                    Thành viên ({selectedMembers.length})
                  </button>
                  {selectedHasDocs && (
                    <button
                      type="button"
                      role="tab"
                      aria-selected={activeDetailTab === "documents"}
                      className={
                        activeDetailTab === "documents" ? "is-active" : ""
                      }
                      onClick={() => setDetailTab("documents")}
                    >
                      <FolderOpen size={15} aria-hidden="true" />
                      Tệp hợp đồng
                    </button>
                  )}
                  {selectedHasRules && (
                    <button
                      type="button"
                      role="tab"
                      aria-selected={activeDetailTab === "rules"}
                      className={activeDetailTab === "rules" ? "is-active" : ""}
                      onClick={() => setDetailTab("rules")}
                    >
                      <ShieldCheck size={15} aria-hidden="true" />
                      Nội quy khu trọ
                      {unacceptedRuleContractIds.has(selected.id) && (
                        <span className="tc-tab-badge">Chưa duyệt</span>
                      )}
                    </button>
                  )}
                </div>

                {activeDetailTab === "overview" && (
                  <div className="tc-tab-panel" role="tabpanel">
                    <div className="tn-contract-amounts tc-amounts">
                      <div>
                        <small>TIỀN THUÊ HÀNG THÁNG</small>
                        <strong>{money(selected.monthlyRent)}</strong>
                      </div>
                      <div>
                        <small>TIỀN CỌC</small>
                        <strong>{money(selected.depositAmount)}</strong>
                      </div>
                    </div>

                    {(Number(selected.outstandingAmount) > 0 ||
                      Number(selected.depositHeld) > 0) && (
                      <div className="tc-balance-row">
                        {Number(selected.outstandingAmount) > 0 && (
                          <div className="tc-balance-chip tc-balance-chip--due">
                            <span>Công nợ hiện tại</span>
                            <strong>{money(selected.outstandingAmount)}</strong>
                          </div>
                        )}
                        {Number(selected.depositHeld) > 0 && (
                          <div className="tc-balance-chip tc-balance-chip--deposit">
                            <span>Tiền cọc đang giữ</span>
                            <strong>{money(selected.depositHeld)}</strong>
                          </div>
                        )}
                      </div>
                    )}

                    <dl className="tn-contract-facts tc-facts">
                      <div>
                        <dt>Ngày bắt đầu</dt>
                        <dd>{formatDate(selected.startDate)}</dd>
                      </div>
                      <div>
                        <dt>Ngày kết thúc dự kiến</dt>
                        <dd>{formatDate(selected.endDate)}</dd>
                      </div>
                      {selected.closedOn && (
                        <div>
                          <dt>Kết thúc thực tế</dt>
                          <dd>{formatDate(selected.closedOn)}</dd>
                        </div>
                      )}
                      {selected.handoverOn && (
                        <div>
                          <dt>Ngày bàn giao</dt>
                          <dd>{formatDate(selected.handoverOn)}</dd>
                        </div>
                      )}
                      <div>
                        <dt>Hạn thanh toán</dt>
                        <dd>
                          {selected.paymentDueDay
                            ? `Ngày ${selected.paymentDueDay} hằng tháng`
                            : "Chưa thiết lập"}
                        </dd>
                      </div>
                      <div>
                        <dt>Hình thức thuê</dt>
                        <dd>
                          {selected.tenancyMode === "CoTenancy"
                            ? "Đồng thuê"
                            : selected.tenancyMode === "Single"
                              ? "Thuê đơn"
                              : selected.tenancyMode || "Theo hợp đồng"}
                        </dd>
                      </div>
                    </dl>

                    {selected.note && (
                      <div className="tn-contract-note">
                        <strong>Ghi chú từ chủ trọ</strong>
                        <p>{selected.note}</p>
                      </div>
                    )}
                    {selected.closureReason && (
                      <div className="tn-contract-note tc-note--closure">
                        <strong>Lý do kết thúc</strong>
                        <p>{selected.closureReason}</p>
                      </div>
                    )}
                  </div>
                )}

                {activeDetailTab === "members" && (
                  <div className="tc-tab-panel" role="tabpanel">
                    <div className="tn-contract-people tc-people-section">
                      <h4>
                        <UserRound size={17} aria-hidden="true" />{" "}
                        {selected.workflow === "Invitation"
                          ? "Người được mời"
                          : "Thành viên hợp đồng"}{" "}
                        <span>{selectedMembers.length}</span>
                      </h4>
                      {selectedMembers.length ? (
                        <ul>
                          {selectedMembers.map((tenant) => (
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
                              <div className="tc-member-body">
                                <div className="tc-member-top">
                                  <strong>{tenant.fullName}</strong>
                                  {tenant.isContactPerson && (
                                    <span className="tc-member-pill">
                                      Người liên hệ
                                    </span>
                                  )}
                                </div>
                                <small>
                                  {tenant.role === "AuthorizedOccupant"
                                    ? "Người nhà"
                                    : "Người đồng đứng tên hợp đồng"}
                                  {" · "}
                                  {selected.workflow === "Invitation"
                                    ? "Dự kiến vào ở"
                                    : "Vào ở"}{" "}
                                  {formatDate(tenant.joinDate)}
                                  {tenant.leaveDate
                                    ? ` · Đã rời ${formatDate(tenant.leaveDate)}`
                                    : ""}
                                </small>
                                {selected.workflow === "Invitation" && (
                                  <small
                                    className={
                                      tenant.invitationAcceptedOfferVersion ===
                                      selected.offerVersion
                                        ? "tc-invite-ok"
                                        : "tc-invite-pending"
                                    }
                                  >
                                    {tenant.invitationAcceptedOfferVersion ===
                                    selected.offerVersion
                                      ? "Đã xác nhận tham gia"
                                      : "Chưa xác nhận tham gia"}
                                  </small>
                                )}
                              </div>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <p>Chưa có người thuê được ghi nhận.</p>
                      )}
                      {selected.workflow === "Invitation" && (
                        <p className="tn-contract-member-count">
                          Thành viên phòng hiện tại: {selected.memberCount ?? 0}
                        </p>
                      )}
                    </div>
                  </div>
                )}

                {activeDetailTab === "documents" && selectedHasDocs && (
                  <div className="tc-tab-panel" role="tabpanel">
                    <ContractDocuments
                      contractId={selected.id}
                      offerVersion={selected.offerVersion}
                    />
                  </div>
                )}

                {activeDetailTab === "rules" && selectedHasRules && (
                  <div className="tc-tab-panel" role="tabpanel">
                    <TenantRules
                      filterContractId={selected.id}
                      embedded
                      onAccepted={() => setRulesRevision((v) => v + 1)}
                    />
                  </div>
                )}
              </article>
            ) : (
              <section className="tn-card tn-contract-state">
                <FileText size={28} aria-hidden="true" />
                <strong>Chọn một hợp đồng để xem chi tiết</strong>
                <p>
                  {hasActiveFilters && !filteredContracts.length
                    ? "Không có hợp đồng khớp với bộ lọc hiện tại."
                    : "Nhấn vào hợp đồng ở danh sách bên trái để xem điều khoản, thành viên và tài liệu."}
                </p>
              </section>
            )}
          </div>
        </div>
      ) : (
        <section className="tn-card tn-contract-state">
          <FileText size={29} aria-hidden="true" />
          <strong>Chưa có hợp đồng</strong>
          <p>
            Khi chủ trọ mời bạn tham gia hợp đồng, thông tin sẽ xuất hiện tại
            đây.
          </p>
        </section>
      )}

      {rulesModalOpen && (
        <div
          className="tc-modal-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setRulesModalOpen(false);
          }}
        >
          <section
            className="tc-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="tc-rules-modal-title"
          >
            <div className="tc-modal-head">
              <div>
                <span className="rules-kicker">THỎA THUẬN CỦA BẠN</span>
                <h2 id="tc-rules-modal-title">Nội quy & điều khoản khu trọ</h2>
              </div>
              <button
                type="button"
                className="tc-modal-close"
                onClick={() => setRulesModalOpen(false)}
                aria-label="Đóng bảng nội quy"
              >
                <X size={18} aria-hidden="true" />
              </button>
            </div>
            <div className="tc-modal-body">
              <TenantRules
                embedded
                onAccepted={() => setRulesRevision((v) => v + 1)}
              />
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
