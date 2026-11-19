import { useEffect, useRef, useState } from "react";
import {
  Bell,
  CheckCheck,
  ChevronRight,
  Clock,
  FileText,
  Inbox,
  MessageSquareText,
  ReceiptText,
} from "lucide-react";
import { notificationApi } from "../services/api/notificationApi";

function formatShortTime(value) {
  if (!value) return "";
  try {
    return new Intl.DateTimeFormat("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    }).format(new Date(value));
  } catch {
    return "";
  }
}

function getItemMeta(item) {
  if (item.invoiceId) return { label: "Hóa đơn", Icon: ReceiptText };
  if (item.contractId) return { label: "Hợp đồng", Icon: FileText };
  if (item.requestId) return { label: "Yêu cầu", Icon: MessageSquareText };
  return { label: "Thông báo", Icon: Bell };
}

export default function NotificationBellDropdown({
  unreadCount = 0,
  activeRole,
  onNavigate,
  onReadChange,
}) {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [markingAll, setMarkingAll] = useState(false);
  const containerRef = useRef(null);

  async function fetchRecent() {
    setLoading(true);
    setError("");
    try {
      const response = await notificationApi.list(1, false, 5);
      setItems((response.data || []).slice(0, 5));
    } catch (e) {
      setError(e.message || "Không thể tải thông báo.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!open) return;
    fetchRecent();
  }, [open]);

  useEffect(() => {
    if (!open) return;

    function handleMouseDown(event) {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target)
      ) {
        setOpen(false);
      }
    }

    function handleKeyDown(event) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }

    document.addEventListener("mousedown", handleMouseDown);
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleMouseDown);
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  async function handleSelectItem(item) {
    setOpen(false);
    if (!item.isRead) {
      try {
        await notificationApi.read(item.id);
        setItems((current) =>
          current.map((entry) =>
            entry.id === item.id ? { ...entry, isRead: true } : entry,
          ),
        );
        onReadChange?.();
      } catch {
        // Detail page will also mark as read if needed.
      }
    }
    onNavigate(`${activeRole}/notifications/${item.id}`);
  }

  async function handleMarkAllRead() {
    if (markingAll || unreadCount === 0) return;
    setMarkingAll(true);
    try {
      await notificationApi.readAll();
      setItems((current) =>
        current.map((entry) => ({ ...entry, isRead: true })),
      );
      onReadChange?.();
    } catch (e) {
      setError(e.message || "Không thể đánh dấu tất cả đã đọc.");
    } finally {
      setMarkingAll(false);
    }
  }

  function handleViewAll() {
    setOpen(false);
    onNavigate(`${activeRole}/notifications`);
  }

  return (
    <div className="notif-bell-wrap" ref={containerRef}>
      <button
        type="button"
        className={`icon-button notif-bell-btn${open ? " is-open" : ""}`}
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={`Thông báo, ${unreadCount} chưa đọc`}
        title="Thông báo"
      >
        <Bell size={20} aria-hidden="true" />
        {unreadCount > 0 && (
          <span className="notif-bell-badge" aria-hidden="true">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div
          className="notif-dropdown"
          role="dialog"
          aria-label="Thông báo mới nhất"
        >
          <div className="notif-dropdown__head">
            <div>
              <strong>Thông báo</strong>
              {unreadCount > 0 && (
                <span className="notif-dropdown__count">
                  {unreadCount} chưa đọc
                </span>
              )}
            </div>
            {unreadCount > 0 && (
              <button
                type="button"
                className="notif-dropdown__mark-all"
                disabled={markingAll}
                onClick={handleMarkAllRead}
              >
                <CheckCheck size={14} aria-hidden="true" />
                <span>{markingAll ? "Đang xử lý…" : "Đọc tất cả"}</span>
              </button>
            )}
          </div>

          <div className="notif-dropdown__body">
            {loading ? (
              <div className="notif-dropdown__state" role="status">
                Đang tải thông báo…
              </div>
            ) : error ? (
              <div className="notif-dropdown__state notif-dropdown__state--error">
                <p>{error}</p>
                <button type="button" onClick={fetchRecent}>
                  Thử lại
                </button>
              </div>
            ) : items.length === 0 ? (
              <div className="notif-dropdown__state">
                <Inbox size={22} aria-hidden="true" />
                <span>Bạn chưa có thông báo nào.</span>
              </div>
            ) : (
              <ul className="notif-dropdown__list">
                {items.map((item) => {
                  const { Icon } = getItemMeta(item);
                  return (
                    <li key={item.id}>
                      <button
                        type="button"
                        className={`notif-dropdown__item${
                          !item.isRead ? " is-unread" : ""
                        }`}
                        onClick={() => handleSelectItem(item)}
                      >
                        <span className="notif-dropdown__icon">
                          <Icon size={16} aria-hidden="true" />
                        </span>
                        <span className="notif-dropdown__content">
                          <span className="notif-dropdown__title-row">
                            <strong>{item.title}</strong>
                            {!item.isRead && (
                              <span
                                className="notif-dropdown__dot"
                                aria-label="Chưa đọc"
                              />
                            )}
                          </span>
                          <span className="notif-dropdown__preview">
                            {item.message}
                          </span>
                          <span className="notif-dropdown__time">
                            <Clock size={11} aria-hidden="true" />
                            {formatShortTime(item.createdAt)}
                          </span>
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          <div className="notif-dropdown__foot">
            <button
              type="button"
              className="notif-dropdown__view-all"
              onClick={handleViewAll}
            >
              <span>Xem tất cả thông báo</span>
              <ChevronRight size={15} aria-hidden="true" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
