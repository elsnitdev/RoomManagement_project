import { useRef, useState } from "react";
import { todayLabel } from "./mockData";
import { Badge, Button, Empty, Field, Heading, Icon, Info, Stat } from "./ui";

const initials = (name) =>
  name
    .split(" ")
    .slice(-2)
    .map((word) => word[0])
    .join("");
function Person({ name, email }) {
  return (
    <div className="person-cell">
      <span className="person-avatar">{initials(name)}</span>
      <span>
        <strong>{name}</strong>
        <small>{email}</small>
      </span>
    </div>
  );
}

export default function Admin({
  page,
  approvals,
  setApprovals,
  users,
  setUsers,
  navigate,
  notify,
}) {
  const [selectedId, setSelectedId] = useState(null);
  const [filter, setFilter] = useState("pending");
  const [search, setSearch] = useState("");
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [draft, setDraft] = useState({
    userName: "",
    email: "",
    password: "",
    role: "Chủ trọ",
  });
  const detailRef = useRef(null);
  const pending = approvals.filter((item) => item.status === "pending");
  const selected = approvals.find((item) => item.id === selectedId);
  const filtered = approvals.filter(
    (item) =>
      (filter === "all" || item.status === filter) &&
      `${item.name} ${item.email}`.toLowerCase().includes(search.toLowerCase()),
  );
  function openDetail(id) {
    setSelectedId(id);
    setRejecting(false);
    setReason("");
    requestAnimationFrame(() =>
      detailRef.current?.scrollIntoView({
        block: "nearest",
        behavior: "smooth",
      }),
    );
  }
  function updateApproval(status) {
    if (status === "rejected" && !reason.trim()) return;
    setApprovals((current) =>
      current.map((item) =>
        item.id === selectedId
          ? {
              ...item,
              status,
              reason: status === "rejected" ? reason.trim() : undefined,
            }
          : item,
      ),
    );
    setSelectedId(null);
    setRejecting(false);
    setReason("");
    notify(
      status === "approved" ? "Đã duyệt hồ sơ mẫu." : "Đã từ chối hồ sơ mẫu.",
    );
  }
  function addUser(event) {
    event.preventDefault();
    setUsers((current) => [
      {
        id: `U-${1004 + current.length}`,
        name: draft.userName,
        email: draft.email,
        role: draft.role,
        status: "Đang hoạt động",
        joined: "25/10/2026",
      },
      ...current,
    ]);
    setDraft({ userName: "", email: "", password: "", role: "Chủ trọ" });
    setShowCreate(false);
    notify("Đã thêm tài khoản mẫu.");
  }
  if (page === "approvals")
    return (
      <>
        <Heading
          eyebrow="QUẢN TRỊ HỆ THỐNG"
          title="Duyệt hồ sơ chủ trọ"
          description="Xem thông tin đăng ký và xử lý từng yêu cầu."
        />
        <section className="content-card">
          <div className="card-toolbar">
            <div
              className="segmented"
              role="group"
              aria-label="Lọc trạng thái hồ sơ"
            >
              <button
                type="button"
                className={filter === "pending" ? "selected" : ""}
                onClick={() => setFilter("pending")}
              >
                Chờ duyệt <span>{pending.length}</span>
              </button>
              <button
                type="button"
                className={filter === "all" ? "selected" : ""}
                onClick={() => setFilter("all")}
              >
                Tất cả
              </button>
            </div>
            <div className="search-box">
              <Icon name="search" size={18} />
              <input
                aria-label="Tìm theo tên hoặc email"
                placeholder="Tìm tên hoặc email..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
          {filtered.length ? (
            <div className="responsive-table">
              <table>
                <thead>
                  <tr>
                    <th>Người đăng ký</th>
                    <th>Ngày gửi</th>
                    <th>Trạng thái</th>
                    <th>
                      <span className="sr-only">Thao tác</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <Person name={item.name} email={item.email} />
                      </td>
                      <td>{item.submitted}</td>
                      <td>
                        <Badge kind={item.status}>
                          {item.status === "pending"
                            ? "Chờ duyệt"
                            : item.status === "approved"
                              ? "Đã duyệt"
                              : "Từ chối"}
                        </Badge>
                      </td>
                      <td className="align-right">
                        <button
                          className="table-link"
                          type="button"
                          onClick={() => openDetail(item.id)}
                        >
                          Xem hồ sơ <Icon name="chevron" size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty
              title="Không tìm thấy hồ sơ"
              description="Thử một từ khóa khác hoặc đổi bộ lọc trạng thái."
            />
          )}
        </section>
        {selected && (
          <section
            className="content-card detail-panel"
            ref={detailRef}
            aria-label={`Hồ sơ ${selected.name}`}
          >
            <div className="detail-header">
              <div>
                <span className="eyebrow">HỒ SƠ {selected.id}</span>
                <h2>{selected.name}</h2>
                <p>Gửi ngày {selected.submitted}</p>
              </div>
              <button
                className="icon-button"
                type="button"
                aria-label="Đóng chi tiết hồ sơ"
                onClick={() => setSelectedId(null)}
              >
                <Icon name="close" />
              </button>
            </div>
            <div className="detail-grid">
              <Info label="Email" value={selected.email} />
              <Info label="Số điện thoại" value={selected.phone} />
              <Info label="Số CCCD" value={selected.citizenId} />
              <Info label="Ngày sinh" value={selected.birthDate} />
              <Info label="Địa chỉ thường trú" value={selected.address} wide />
            </div>
            {selected.status === "pending" ? (
              <>
                <div className="detail-actions">
                  <Button
                    variant="secondary"
                    onClick={() => setRejecting(!rejecting)}
                  >
                    Từ chối
                  </Button>
                  <Button
                    icon="check"
                    onClick={() => updateApproval("approved")}
                  >
                    Duyệt hồ sơ
                  </Button>
                </div>
                {rejecting && (
                  <div className="reject-box">
                    <Field label="Lý do từ chối" required>
                      <textarea
                        rows="3"
                        required
                        value={reason}
                        onChange={(e) => setReason(e.target.value)}
                        placeholder="Nêu rõ lý do để người đăng ký biết cần bổ sung gì"
                      />
                    </Field>
                    <Button
                      variant="danger"
                      disabled={!reason.trim()}
                      onClick={() => updateApproval("rejected")}
                    >
                      Xác nhận từ chối
                    </Button>
                  </div>
                )}
              </>
            ) : (
              <div className="notice">
                Hồ sơ đã được xử lý.{" "}
                {selected.reason && `Lý do từ chối: ${selected.reason}`}
              </div>
            )}
          </section>
        )}
      </>
    );
  if (page === "users")
    return (
      <>
        <Heading
          eyebrow="QUẢN TRỊ HỆ THỐNG"
          title="Tài khoản người dùng"
          description="Theo dõi người dùng và tạo tài khoản mẫu cho chủ trọ hoặc người thuê."
          action={
            <Button icon="plus" onClick={() => setShowCreate(!showCreate)}>
              Tạo tài khoản
            </Button>
          }
        />
        {showCreate && (
          <section className="content-card inline-form">
            <div className="detail-header">
              <div>
                <h2>Tạo tài khoản mẫu</h2>
                <p>Dữ liệu chỉ tồn tại trong phiên xem trước.</p>
              </div>
              <button
                className="icon-button"
                type="button"
                aria-label="Đóng biểu mẫu"
                onClick={() => setShowCreate(false)}
              >
                <Icon name="close" />
              </button>
            </div>
            <form onSubmit={addUser} className="form-grid">
              <Field label="Tên đăng nhập" required>
                <input
                  required
                  minLength="3"
                  value={draft.userName}
                  onChange={(e) =>
                    setDraft({ ...draft, userName: e.target.value })
                  }
                  placeholder="nguyenvana"
                />
              </Field>
              <Field label="Email" required>
                <input
                  required
                  type="email"
                  value={draft.email}
                  onChange={(e) =>
                    setDraft({ ...draft, email: e.target.value })
                  }
                  placeholder="email@example.com"
                />
              </Field>
              <Field label="Mật khẩu" required>
                <input
                  required
                  type="password"
                  minLength="6"
                  value={draft.password}
                  onChange={(e) =>
                    setDraft({ ...draft, password: e.target.value })
                  }
                  placeholder="Ít nhất 6 ký tự"
                />
              </Field>
              <Field label="Vai trò">
                <select
                  value={draft.role}
                  onChange={(e) => setDraft({ ...draft, role: e.target.value })}
                >
                  <option>Chủ trọ</option>
                  <option>Người thuê</option>
                </select>
              </Field>
              <div className="form-action">
                <button className="button button--primary" type="submit">
                  Thêm tài khoản mẫu
                </button>
              </div>
            </form>
          </section>
        )}
        <section className="content-card">
          <div className="card-title-row">
            <h2>Danh sách tài khoản</h2>
            <span className="subtle-count">{users.length} tài khoản mẫu</span>
          </div>
          <div className="responsive-table">
            <table>
              <thead>
                <tr>
                  <th>Người dùng</th>
                  <th>Vai trò</th>
                  <th>Ngày tạo</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>
                      <Person name={user.name} email={user.email} />
                    </td>
                    <td>{user.role}</td>
                    <td>{user.joined}</td>
                    <td>
                      <Badge kind="approved">{user.status}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </>
    );
  return (
    <>
      <Heading
        eyebrow={todayLabel}
        title="Xin chào, Quản trị viên 👋"
        description="Đây là những việc cần bạn chú ý hôm nay."
        action={
          <Button
            variant="secondary"
            onClick={() => navigate("admin/approvals")}
          >
            Xem hồ sơ chờ <Icon name="arrow" size={16} />
          </Button>
        }
      />
      <div className="stats-grid stats-grid--admin">
        <Stat
          icon="clock"
          label="Hồ sơ chờ duyệt"
          value={pending.length.toString()}
          note="Cần xem xét"
          tint="amber"
        />
        <Stat
          icon="users"
          label="Tài khoản mẫu"
          value={users.length.toString()}
          note="Chủ trọ và người thuê"
        />
        <Stat
          icon="check"
          label="Đã duyệt"
          value={approvals
            .filter((item) => item.status === "approved")
            .length.toString()}
          note="Hồ sơ chủ trọ"
          tint="green"
        />
      </div>
      <div className="dashboard-grid">
        <section className="content-card">
          <div className="card-title-row">
            <div>
              <h2>Hồ sơ mới cần duyệt</h2>
              <p>Ưu tiên xử lý các hồ sơ được gửi gần đây.</p>
            </div>
            <button
              className="text-link"
              type="button"
              onClick={() => navigate("admin/approvals")}
            >
              Xem tất cả <Icon name="arrow" size={16} />
            </button>
          </div>
          {pending.length ? (
            <div className="list-rows">
              {pending.slice(0, 3).map((item) => (
                <button
                  className="list-row"
                  type="button"
                  key={item.id}
                  onClick={() => navigate("admin/approvals")}
                >
                  <span className="person-avatar">{initials(item.name)}</span>
                  <span className="list-row-main">
                    <strong>{item.name}</strong>
                    <small>{item.email}</small>
                  </span>
                  <span className="list-row-date">{item.submitted}</span>
                  <Icon name="chevron" size={17} />
                </button>
              ))}
            </div>
          ) : (
            <Empty
              title="Đã xử lý hết hồ sơ"
              description="Các yêu cầu mới sẽ xuất hiện tại đây."
              icon="check"
            />
          )}
        </section>
        <section className="content-card quick-card">
          <span className="quick-card-icon">
            <Icon name="users" size={25} />
          </span>
          <h2>Quản lý tài khoản tập trung</h2>
          <p>
            Tạo tài khoản cho chủ trọ và người thuê, theo dõi danh sách người
            dùng ở một nơi.
          </p>
          <Button variant="secondary" onClick={() => navigate("admin/users")}>
            Đến tài khoản <Icon name="arrow" size={16} />
          </Button>
        </section>
      </div>
    </>
  );
}
