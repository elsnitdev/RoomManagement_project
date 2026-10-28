import { useEffect, useRef, useState } from "react";
import Admin from "./Admin";
import Auth from "./Auth";
import Landlord from "./Landlord";
import Profile from "./Profile";
import {
  initialApprovals,
  initialProperties,
  initialRequests,
  initialRooms,
  initialUsers,
} from "./mockData";
import { Brand, Icon } from "./ui";
import "./App.css";

const adminNav = [
  { id: "overview", label: "Tổng quan", icon: "grid" },
  { id: "approvals", label: "Duyệt chủ trọ", icon: "users" },
  { id: "users", label: "Tài khoản", icon: "file" },
  { id: "profile", label: "Hồ sơ", icon: "users" },
];
const landlordNav = [
  { id: "overview", label: "Tổng quan", icon: "grid" },
  { id: "properties", label: "Khu trọ", icon: "building" },
  { id: "rooms", label: "Phòng", icon: "door" },
  { id: "tenants", label: "Người ở", icon: "users" },
  { id: "contracts", label: "Hợp đồng", icon: "file" },
  { id: "billing", label: "Hóa đơn", icon: "receipt" },
  { id: "requests", label: "Yêu cầu", icon: "chat" },
  { id: "profile", label: "Hồ sơ", icon: "users" },
];
const titleMap = {
  overview: "Tổng quan",
  approvals: "Duyệt chủ trọ",
  users: "Tài khoản người dùng",
  properties: "Khu trọ",
  rooms: "Danh sách phòng",
  room: "Chi tiết phòng",
  tenants: "Hồ sơ người ở",
  contracts: "Hợp đồng",
  billing: "Hóa đơn & công nợ",
  requests: "Yêu cầu người thuê",
  profile: "Hồ sơ của tôi",
};
const fromHash = () => window.location.hash.replace(/^#\/?/, "") || "login";

export default function App() {
  const [route, setRoute] = useState(fromHash);
  const [mobileNav, setMobileNav] = useState(false);
  const [toast, setToast] = useState("");
  const [approvals, setApprovals] = useState(initialApprovals);
  const [users, setUsers] = useState(initialUsers);
  const [properties, setProperties] = useState(initialProperties);
  const [rooms, setRooms] = useState(initialRooms);
  const [requests, setRequests] = useState(initialRequests);
  const [selectedProperty, setSelectedProperty] = useState("all");
  const toastTimer = useRef(null);
  useEffect(() => {
    const onHash = () => {
      setRoute(fromHash());
      setMobileNav(false);
      window.scrollTo(0, 0);
    };
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);
  useEffect(() => {
    const page = route.split("/")[1];
    document.title = `${titleMap[page] || (route === "register" ? "Đăng ký chủ trọ" : "Đăng nhập")} | Nhà Trọ Xanh`;
  }, [route]);
  useEffect(() => () => clearTimeout(toastTimer.current), []);
  function notify(message) {
    setToast(message);
    clearTimeout(toastTimer.current);
    toastTimer.current = setTimeout(() => setToast(""), 4200);
  }
  function navigate(path) {
    const next = `#/${path}`;
    if (window.location.hash === next) {
      setMobileNav(false);
      window.scrollTo(0, 0);
    } else window.location.hash = next;
  }
  function enterDemo(nextRole) {
    navigate(`${nextRole}/overview`);
  }
  function logout() {
    navigate("login");
    notify("Đã thoát bản xem trước.");
  }
  const [routeRole, rawPage, roomId] = route.split("/");
  const isPublic = !["admin", "landlord"].includes(routeRole);
  if (isPublic)
    return (
      <>
        <div className="preview-ribbon">
          <span className="preview-dot" />
          BẢN XEM TRƯỚC · DỮ LIỆU MẪU
        </div>
        <Auth
          route={route}
          navigate={navigate}
          enterDemo={enterDemo}
          notify={notify}
        />
        {toast && (
          <div className="toast" role="status">
            {toast}
          </div>
        )}
      </>
    );
  const activeRole = routeRole;
  const page = titleMap[rawPage] ? rawPage : "overview";
  const items = activeRole === "admin" ? adminNav : landlordNav;
  return (
    <div className="app-shell">
      <button
        className="skip-link"
        type="button"
        onClick={() => document.getElementById("main-content")?.focus()}
      >
        Bỏ qua menu
      </button>
      {mobileNav && (
        <button
          className="sidebar-backdrop"
          type="button"
          aria-label="Đóng menu"
          onClick={() => setMobileNav(false)}
        />
      )}
      <aside
        className={`sidebar ${mobileNav ? "sidebar--open" : ""}`}
        aria-label="Điều hướng chính"
      >
        <div className="sidebar-top">
          <Brand />
          <button
            className="icon-button sidebar-close"
            type="button"
            onClick={() => setMobileNav(false)}
            aria-label="Đóng menu"
          >
            <Icon name="close" />
          </button>
        </div>
        <div className="sidebar-section-label">KHÔNG GIAN LÀM VIỆC</div>
        <nav
          className="nav-list"
          aria-label={activeRole === "admin" ? "Menu quản trị" : "Menu chủ trọ"}
        >
          {items.map((item) => (
            <button
              key={item.id}
              className={`nav-item ${page === item.id || (page === "room" && item.id === "rooms") ? "nav-item--active" : ""}`}
              type="button"
              aria-current={
                page === item.id || (page === "room" && item.id === "rooms")
                  ? "page"
                  : undefined
              }
              onClick={() => navigate(`${activeRole}/${item.id}`)}
            >
              <Icon name={item.icon} size={19} />
              <span>{item.label}</span>
              {item.id === "approvals" &&
                approvals.filter((entry) => entry.status === "pending").length >
                  0 && (
                  <span className="nav-count">
                    {
                      approvals.filter((entry) => entry.status === "pending")
                        .length
                    }
                  </span>
                )}
            </button>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="sidebar-help">
            <span className="help-icon">
              <Icon name="alert" size={18} />
            </span>
            <strong>Đang xem giao diện mẫu</strong>
            <p>Mọi thao tác chỉ thay đổi dữ liệu trong phiên này.</p>
          </div>
          <button
            className="nav-item logout-button"
            type="button"
            onClick={logout}
          >
            <Icon name="logout" size={19} />
            <span>Đăng xuất</span>
          </button>
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <div className="topbar-left">
            <button
              className="icon-button mobile-menu"
              type="button"
              aria-label="Mở menu"
              aria-expanded={mobileNav}
              onClick={() => setMobileNav(true)}
            >
              <Icon name="menu" />
            </button>
            <span className="breadcrumb">
              Không gian làm việc <Icon name="chevron" size={15} />{" "}
              <strong>{titleMap[page]}</strong>
            </span>
          </div>
          <div className="topbar-right">
            <span className="demo-chip">
              <span className="preview-dot" /> Dữ liệu mẫu
            </span>
            <div className="topbar-profile">
              <span className="avatar">
                {activeRole === "admin" ? "AD" : "NA"}
              </span>
              <span>
                <strong>
                  {activeRole === "admin" ? "Quản trị viên" : "Nguyễn An"}
                </strong>
                <small>{activeRole === "admin" ? "Admin" : "Chủ trọ"}</small>
              </span>
            </div>
          </div>
        </header>
        <main id="main-content" tabIndex="-1" className="main-content">
          {page === "profile" ? (
            <Profile role={activeRole} notify={notify} />
          ) : activeRole === "admin" ? (
            <Admin
              page={page}
              approvals={approvals}
              setApprovals={setApprovals}
              users={users}
              setUsers={setUsers}
              navigate={navigate}
              notify={notify}
            />
          ) : (
            <Landlord
              page={page}
              roomId={roomId}
              properties={properties}
              setProperties={setProperties}
              rooms={rooms}
              setRooms={setRooms}
              requests={requests}
              setRequests={setRequests}
              selectedProperty={selectedProperty}
              setSelectedProperty={setSelectedProperty}
              navigate={navigate}
              notify={notify}
            />
          )}
        </main>
      </div>
      {toast && (
        <div className="toast" role="status">
          <Icon name="check" size={18} />
          {toast}
        </div>
      )}
    </div>
  );
}
