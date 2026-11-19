import { useEffect, useState } from "react";
import { Bell } from "lucide-react";
import { listProperties } from "../services/api/PropertyApi";
import { requestApi } from "../services/api/requestApi";
import { Badge, Empty, Heading, Icon } from "../ui";

const statusInfo = {
  Open: { label: "Chờ xử lý", badge: "rejected" },
  InProgress: { label: "Đang xử lý", badge: "pending" },
  Resolved: { label: "Đã giải quyết", badge: "approved" },
  Closed: { label: "Đã đóng", badge: "vacant" },
};

function formatDateTime(value) {
  if (!value) return "—";
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
}

export default function LandlordRequestsPage({ notify, navigate }) {
  const [properties, setProperties] = useState([]);

  const [filterPropertyId, setFilterPropertyId] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [filterRead, setFilterRead] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [requests, setRequests] = useState([]);
  const [loadingRequests, setLoadingRequests] = useState(true);
  const [requestError, setRequestError] = useState("");
  const [selectedRequest, setSelectedRequest] = useState(null);
  const [updatingStatus, setUpdatingStatus] = useState(false);

  useEffect(() => {
    listProperties({ page: 1, pageSize: 100, isActive: true })
      .then((res) => {
        setProperties(res?.data || []);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setLoadingRequests(true);
    setRequestError("");
    requestApi
      .getLandlordRequests({
        propertyId: filterPropertyId || undefined,
        status: filterStatus || undefined,
        isRead: filterRead === "" ? undefined : filterRead === "true",
        search: searchQuery || undefined,
        page: 1,
        pageSize: 50,
        signal: controller.signal,
      })
      .then((res) => {
        const items = res?.data || [];
        setRequests(items);
        if (selectedRequest) {
          const match = items.find((x) => x.id === selectedRequest.id);
          if (match) setSelectedRequest(match);
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted) {
          setRequestError(e.message || "Không thể tải danh sách yêu cầu.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoadingRequests(false);
        }
      });
    return () => controller.abort();
  }, [filterPropertyId, filterStatus, filterRead, searchQuery]);

  async function handleSelectRequest(item) {
    setSelectedRequest(item);
    try {
      const detail = await requestApi.getLandlordRequestById(item.id);
      setSelectedRequest(detail);
      setRequests((current) =>
        current.map((r) => (r.id === detail.id ? detail : r)),
      );
    } catch (e) {
      notify?.(e.message || "Không thể tải chi tiết yêu cầu.");
    }
  }

  async function handleChangeStatus(requestId, newStatus) {
    setUpdatingStatus(true);
    try {
      const updated = await requestApi.updateLandlordRequestStatus(
        requestId,
        newStatus,
      );
      setSelectedRequest(updated);
      setRequests((current) =>
        current.map((r) => (r.id === updated.id ? updated : r)),
      );
      notify?.(
        `Đã cập nhật trạng thái xử lý: ${statusInfo[newStatus]?.label || newStatus}.`,
      );
    } catch (e) {
      notify?.(e.message || "Không thể cập nhật trạng thái.");
    } finally {
      setUpdatingStatus(false);
    }
  }

  return (
    <>
      <Heading
        eyebrow="TIẾP NHẬN & XỬ LÝ"
        title="Yêu cầu từ người thuê"
        description="Theo dõi, phản hồi và cập nhật trạng thái xử lý các yêu cầu hỗ trợ gửi từ người thuê."
        action={
          navigate ? (
            <button
              type="button"
              className="button"
              onClick={() => navigate("landlord/notifications")}
            >
              <Bell size={16} aria-hidden="true" />
              <span>Trung tâm Thông báo</span>
            </button>
          ) : null
        }
      />

      <div
        style={{
          display: "grid",
          gridTemplateColumns: selectedRequest
            ? "minmax(0, 1.35fr) minmax(320px, 1fr)"
            : "1fr",
          gap: "18px",
          alignItems: "start",
        }}
      >
        <section className="content-card">
          <div
            className="card-toolbar card-toolbar--wrap"
            style={{ marginBottom: "16px" }}
          >
            <label className="filter-select">
              <Icon name="building" size={16} />
              <select
                value={filterPropertyId}
                onChange={(e) => setFilterPropertyId(e.target.value)}
              >
                <option value="">Tất cả khu trọ</option>
                {properties.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
              <Icon name="down" size={14} />
            </label>

            <label className="filter-select">
              <select
                value={filterStatus}
                onChange={(e) => setFilterStatus(e.target.value)}
              >
                <option value="">Mọi trạng thái xử lý</option>
                <option value="Open">Chờ xử lý (Open)</option>
                <option value="InProgress">Đang xử lý (InProgress)</option>
                <option value="Resolved">Đã giải quyết (Resolved)</option>
                <option value="Closed">Đã đóng (Closed)</option>
              </select>
              <Icon name="down" size={14} />
            </label>

            <label className="filter-select">
              <select
                value={filterRead}
                onChange={(e) => setFilterRead(e.target.value)}
              >
                <option value="">Tất cả (Chưa đọc & Đã đọc)</option>
                <option value="false">Chưa xem</option>
                <option value="true">Đã xem</option>
              </select>
              <Icon name="down" size={14} />
            </label>

            <div className="search-box">
              <Icon name="search" size={17} />
              <input
                placeholder="Tìm theo tiêu đề, tên, SĐT..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
          </div>

          {loadingRequests ? (
            <Empty
              title="Đang tải danh sách yêu cầu..."
              description="Vui lòng chờ trong giây lát."
            />
          ) : requestError ? (
            <Empty title="Không thể tải yêu cầu" description={requestError} />
          ) : requests.length ? (
            <div className="request-grid">
              {requests.map((item) => {
                const st = statusInfo[item.status] || {
                  label: item.status,
                  badge: "pending",
                };
                const isSelected = selectedRequest?.id === item.id;
                return (
                  <article
                    className="request-card"
                    key={item.id}
                    onClick={() => handleSelectRequest(item)}
                    style={{
                      cursor: "pointer",
                      border: isSelected ? "2px solid #2563eb" : undefined,
                      background: !item.isRead ? "#f8fbff" : undefined,
                    }}
                  >
                    <div className="request-top">
                      <span className="request-id">
                        {item.propertyName} · Phòng {item.roomName}
                      </span>
                      <div
                        style={{
                          display: "flex",
                          gap: "6px",
                          alignItems: "center",
                        }}
                      >
                        <Badge kind={item.isRead ? "vacant" : "rejected"}>
                          {item.isRead ? "Đã xem" : "Mới / Chưa xem"}
                        </Badge>
                        <Badge kind={st.badge}>{st.label}</Badge>
                      </div>
                    </div>
                    <h2>{item.title}</h2>
                    <p style={{ margin: "6px 0", color: "#475569" }}>
                      Người gửi: <strong>{item.senderFullName}</strong>
                      {item.senderPhoneNumber
                        ? ` (${item.senderPhoneNumber})`
                        : ""}
                    </p>
                    <p
                      style={{
                        fontSize: "13px",
                        color: "#64748b",
                        display: "-webkit-box",
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: "vertical",
                        overflow: "hidden",
                      }}
                    >
                      {item.content}
                    </p>
                    <div className="request-bottom">
                      <span>Gửi lúc {formatDateTime(item.createdAt)}</span>
                      <button
                        type="button"
                        className="text-link"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleSelectRequest(item);
                        }}
                      >
                        Xem & xử lý <Icon name="chevron" size={15} />
                      </button>
                    </div>
                  </article>
                );
              })}
            </div>
          ) : (
            <Empty
              title="Không có yêu cầu nào"
              description="Hiện chưa có yêu cầu nào từ người thuê khớp với bộ lọc."
            />
          )}
        </section>

        {selectedRequest && (
          <aside
            className="content-card"
            style={{ position: "sticky", top: "20px" }}
          >
            <div className="detail-header" style={{ marginBottom: "12px" }}>
              <div>
                <span
                  style={{
                    fontSize: "11px",
                    fontWeight: 700,
                    color: "#2563eb",
                  }}
                >
                  CHI TIẾT YÊU CẦU
                </span>
                <h2 style={{ margin: "4px 0 0" }}>{selectedRequest.title}</h2>
              </div>
              <button
                type="button"
                className="icon-button"
                aria-label="Đóng chi tiết"
                onClick={() => setSelectedRequest(null)}
              >
                <Icon name="close" />
              </button>
            </div>

            <div
              style={{
                display: "flex",
                gap: "8px",
                flexWrap: "wrap",
                marginBottom: "14px",
              }}
            >
              <Badge kind={selectedRequest.isRead ? "approved" : "pending"}>
                {selectedRequest.isRead
                  ? `Đã xem lúc ${formatDateTime(selectedRequest.readAt)}`
                  : "Đang đánh dấu đã xem..."}
              </Badge>
              <Badge
                kind={statusInfo[selectedRequest.status]?.badge || "pending"}
              >
                {statusInfo[selectedRequest.status]?.label ||
                  selectedRequest.status}
              </Badge>
            </div>

            <div
              style={{
                display: "grid",
                gap: "8px",
                fontSize: "13px",
                padding: "12px",
                background: "#f8fafc",
                borderRadius: "8px",
                marginBottom: "14px",
              }}
            >
              <div>
                <strong>Khu trọ:</strong> {selectedRequest.propertyName}
              </div>
              <div>
                <strong>Phòng:</strong> {selectedRequest.roomName}
              </div>
              <div>
                <strong>Người gửi:</strong> {selectedRequest.senderFullName}{" "}
                {selectedRequest.senderPhoneNumber
                  ? `· ${selectedRequest.senderPhoneNumber}`
                  : ""}
              </div>
              <div>
                <strong>Thời gian gửi:</strong>{" "}
                {formatDateTime(selectedRequest.createdAt)}
              </div>
            </div>

            <div style={{ marginBottom: "18px" }}>
              <strong
                style={{
                  display: "block",
                  fontSize: "12px",
                  marginBottom: "6px",
                  color: "#475569",
                }}
              >
                NỘI DUNG CHI TIẾT
              </strong>
              <div
                style={{
                  padding: "14px",
                  border: "1px solid #e2e8f0",
                  borderRadius: "8px",
                  whiteSpace: "pre-wrap",
                  fontSize: "13px",
                  lineHeight: 1.6,
                }}
              >
                {selectedRequest.content}
              </div>
            </div>

            <div>
              <strong
                style={{
                  display: "block",
                  fontSize: "12px",
                  marginBottom: "8px",
                  color: "#475569",
                }}
              >
                CẬP NHẬT TRẠNG THÁI XỬ LÝ
              </strong>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(2, 1fr)",
                  gap: "8px",
                }}
              >
                {["Open", "InProgress", "Resolved", "Closed"].map((stKey) => {
                  const active = selectedRequest.status === stKey;
                  return (
                    <button
                      key={stKey}
                      type="button"
                      disabled={updatingStatus || active}
                      className={active ? "button button--primary" : "button"}
                      style={{
                        padding: "8px 10px",
                        fontSize: "12px",
                        opacity: active ? 1 : 0.9,
                      }}
                      onClick={() =>
                        handleChangeStatus(selectedRequest.id, stKey)
                      }
                    >
                      {statusInfo[stKey]?.label || stKey}
                    </button>
                  );
                })}
              </div>
            </div>
          </aside>
        )}
      </div>
    </>
  );
}
