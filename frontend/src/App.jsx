import { useEffect, useRef, useState } from "react";
import { Bell, Check, ChevronRight, CircleAlert, FileText, House, LayoutDashboard, LogOut, Menu, MessageSquareText, ReceiptText, UserRound, X } from "lucide-react";
import { useAuth } from "./contexts/AuthContext";
import Admin from "./Admin";
import Auth from "./Auth";
import Landlord from "./Landlord";
import Tenant from "./Tenant";
import Profile from "./Profile";
import PropertyManagement from "./components/PropertyManagement";
import RoomManagementPage from "./pages/RoomManagementPage";
import RoomDetailPage from "./pages/RoomDetailPage";
import CreateRoomPage from "./pages/CreateRoomPage";
import LandlordBilling from "./components/billing/LandlordBilling";
import LandlordContracts from "./components/contracts/LandlordContracts";
import { PropertyRules } from "./components/contracts/ContractRules";
import TenantBilling from "./components/billing/TenantBilling";
import AdminServices from "./components/billing/AdminServices";
import {
  initialApprovals,
  contracts,
  invoices as initialInvoices,
  invoiceHistory,
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
  { id: "services", label: "Dịch vụ", icon: "file" },
  { id: "profile", label: "Hồ sơ", icon: "users" },
];
const landlordNav = [
  { id: "overview", label: "Tổng quan", icon: "grid" },
  { id: "properties", label: "Khu trọ", icon: "building" },
  { id: "tenants", label: "Người ở", icon: "users" },
  { id: "contracts", label: "Hợp đồng", icon: "file" },
  { id: "billing", label: "Hóa đơn", icon: "receipt" },
  { id: "requests", label: "Yêu cầu", icon: "chat" },
  { id: "profile", label: "Hồ sơ", icon: "users" },
];
const tenantNav = [
  { id: "overview", label: "Tổng quan", icon: LayoutDashboard },
  { id: "room", label: "Chỗ ở của tôi", icon: House },
  { id: "billing", label: "Hóa đơn", icon: ReceiptText },
  { id: "contracts", label: "Hợp đồng", icon: FileText },
  { id: "requests", label: "Yêu cầu", icon: MessageSquareText },
  { id: "notices", label: "Thông báo", icon: Bell },
  { id: "profile", label: "Tài khoản", icon: UserRound },
];
const titleMap = {
  overview: "Tổng quan",
  approvals: "Duyệt chủ trọ",
  users: "Tài khoản người dùng",
  services: "Danh mục dịch vụ",
  properties: "Khu trọ",
  rooms: "Danh sách phòng",
  room: "Chi tiết phòng",
  tenants: "Hồ sơ người ở",
  notices: "Thông báo quan trọng",
  contracts: "Hợp đồng",
  billing: "Hóa đơn & công nợ",
  requests: "Yêu cầu người thuê",
  profile: "Hồ sơ của tôi",
};
const fromHash = () => window.location.hash.replace(/^#\/?/, "") || "login";

export default function App() {
  const { user, isAuthenticated, signOut } = useAuth();
  const [route, setRoute] = useState(fromHash);
  const [mobileNav, setMobileNav] = useState(false);
  const [toast, setToast] = useState("");
  const [approvals, setApprovals] = useState(initialApprovals);
  const [users, setUsers] = useState(initialUsers);
  const [properties, setProperties] = useState(initialProperties);
  const [rooms, setRooms] = useState(initialRooms);
  const [requests, setRequests] = useState(initialRequests);
  const [invoices, setInvoices] = useState(initialInvoices);
  const [selectedProperty, setSelectedProperty] = useState("all");
  const toastTimer = useRef(null);
  const [routeRole, rawPage, roomId, propertySection, roomAction] =
    route.split("/");
  const protectedRoute = ["admin", "landlord", "tenant"].includes(routeRole);
  const userRoute =
    user?.role === "Admin"
      ? "admin"
      : user?.role === "Landlord"
        ? "landlord"
        : user?.role === "Tenant"
          ? "tenant"
          : null;
  const canAccess = isAuthenticated && routeRole === userRoute;
  const isPublic = !protectedRoute;
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
    const [, page, detailId, section, action] = route.split("/");
    const title =
      page === "properties" && section === "rooms"
        ? action === "new"
          ? "Tạo phòng"
          : action ? "Chi tiết phòng" : "Quản lý phòng"
        : page === "billing" && detailId && detailId !== "rooms"
          ? "Chi tiết hóa đơn"
        : page === "tenants" && detailId
          ? "Chi tiết người thuê"
        : titleMap[page] ||
          (route === "register" ? "Đăng ký chủ trọ" : route === "tenant-register" ? "Đăng ký người thuê" : "Đăng nhập");
    document.title = `${route.startsWith('tenant/room') ? 'Chỗ ở của tôi' : title} | Nhà Trọ Xanh`;
  }, [route]);
  useEffect(() => {
    if (protectedRoute && !canAccess) {
      window.location.hash = userRoute ? `/${userRoute}/overview` : "/login";
    }
  }, [protectedRoute, canAccess, userRoute]);
  useEffect(() => {
    if (
      canAccess &&
      routeRole === "landlord" &&
      (rawPage === "rooms" || rawPage === "room")
    ) {
      window.location.hash = "/landlord/properties";
    }
  }, [canAccess, routeRole, rawPage]);
  useEffect(() => () => clearTimeout(toastTimer.current), []);
  function notify(message) {
    setToast(message);
    clearTimeout(toastTimer.current);
    toastTimer.current = setTimeout(() => setToast(""), 4200);
  }
  function navigate(path) {
    const next = `/${path}`;
    if (window.location.hash === next) {
      setMobileNav(false);
      window.scrollTo(0, 0);
    } else window.location.hash = next;
  }
  function logout() {
    signOut();
    navigate("login");
    notify("Đã đăng xuất.");
  }
  if (protectedRoute && !canAccess) return null;
  if (isPublic)
    return (
      <>
        <Auth route={route} navigate={navigate} />
        {toast && (
          <div className="toast" role="status">
            {toast}
          </div>
        )}
      </>
    );
  const activeRole = routeRole;
  const page = titleMap[rawPage] ? rawPage : "overview";
  const propertyPageTitle =
    propertySection === "rooms"
      ? roomAction === "new"
        ? "Tạo phòng"
        : roomAction ? "Chi tiết phòng" : "Quản lý phòng"
      : titleMap[page];
  const items = activeRole === "admin" ? adminNav : activeRole === "tenant" ? tenantNav : landlordNav;
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
            {activeRole === "tenant" ? <X size={19} aria-hidden="true" /> : <Icon name="close" />}
          </button>
        </div>
        <div className="sidebar-section-label">KHÔNG GIAN LÀM VIỆC</div>
        <nav
          className="nav-list"
          aria-label={activeRole === "admin" ? "Menu quản trị" : activeRole === "tenant" ? "Menu người thuê" : "Menu chủ trọ"}
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
              {activeRole === "tenant" ? (
                <item.icon size={19} aria-hidden="true" />
              ) : item.id === "billing" ? (
                <ReceiptText size={19} aria-hidden="true" />
              ) : (
                <Icon name={item.icon} size={19} />
              )}
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
          {page !== "properties" && page !== "billing" && page !== "services" && page !== "contracts" && (
            <div className="sidebar-help">
              <span className="help-icon">
                {activeRole === "tenant" ? <CircleAlert size={18} aria-hidden="true" /> : <Icon name="alert" size={18} />}
              </span>
              <strong>Đang xem giao diện mẫu</strong>
              <p>Mọi thao tác chỉ thay đổi dữ liệu trong phiên này.</p>
            </div>
          )}
          <button
            className="nav-item logout-button"
            type="button"
            onClick={logout}
          >
            {activeRole === "tenant" ? <LogOut size={19} aria-hidden="true" /> : <Icon name="logout" size={19} />}
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
              {activeRole === "tenant" ? <Menu size={19} aria-hidden="true" /> : <Icon name="menu" />}
            </button>
            <span className="breadcrumb">
              Không gian làm việc {activeRole === "tenant" ? <ChevronRight size={15} aria-hidden="true" /> : <Icon name="chevron" size={15} />} {" "}
              <strong>
                {activeRole === "tenant" && page === "room" ? "Chỗ ở của tôi" : page === "properties" ? propertyPageTitle : page === "billing" && roomId && roomId !== "rooms" ? "Chi tiết hóa đơn" : titleMap[page]}
              </strong>
            </span>
          </div>
          <div className="topbar-right">
            {page !== "properties" && page !== "billing" && page !== "services" && page !== "contracts" && (
              <span className="demo-chip">
                <span className="preview-dot" /> Dữ liệu mẫu
              </span>
            )}
            <div className="topbar-profile">
              <span className="avatar">
                {activeRole === "admin" ? "AD" : activeRole === "tenant" ? "NT" : "NA"}
              </span>
              <span>
                <strong>{user?.userName || "Người dùng"}</strong>
                <small>{activeRole === "admin" ? "Admin" : activeRole === "tenant" ? "Người thuê" : `Chủ trọ · Mã ${user?.landlordCode || "đang cập nhật"}`}</small>
              </span>
            </div>
          </div>
        </header>
        <main
          id="main-content"
          tabIndex="-1"
          className={`main-content${page === "properties" && propertySection === "rooms" && roomAction === "new" ? " main-content--room-batch" : ""}`}
        >
          {activeRole === "tenant" && page === "billing" ? (
            <TenantBilling />
          ) : activeRole === "tenant" ? (
            <Tenant
              page={page}
              user={user}
              navigate={navigate}
              notify={notify}
              rooms={rooms}
              properties={properties}
              contracts={contracts}
              invoices={[]}
              invoiceHistory={[]}
              requests={requests}
              setRequests={setRequests}
            />
          ) : page === "profile" ? (
            <Profile role={activeRole} notify={notify} />
          ) : activeRole === "admin" && page === "services" ? (
            <AdminServices />
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
          ) : activeRole === "landlord" && page === "billing" ? (
            <LandlordBilling invoiceId={roomId === 'rooms' ? undefined : roomId} initialPropertyId={roomId === 'rooms' ? propertySection : undefined} initialRoomId={roomId === 'rooms' ? roomAction : undefined} navigate={navigate} />
          ) : activeRole === "landlord" && page === "contracts" ? (
            <LandlordContracts key={route} routeParts={[roomId, propertySection, roomAction]} navigate={navigate} notify={notify} />
          ) : page === "properties" ? (
            propertySection === "rules" && roomId ? (
              <PropertyRules key={roomId} propertyId={roomId} navigate={navigate} />
            ) : propertySection === "rooms" && roomId ? (
              roomAction === "new" ? (
                <CreateRoomPage
                  key={roomId}
                  propertyId={roomId}
                  navigate={navigate}
                  notify={notify}
                />
              ) : roomAction ? (
                <RoomDetailPage
                  key={`${roomId}:${roomAction}`}
                  propertyId={roomId}
                  roomId={roomAction}
                  navigate={navigate}
                  notify={notify}
                />
              ) : (
                <RoomManagementPage
                  key={roomId}
                  propertyId={roomId}
                  navigate={navigate}
                  notify={notify}
                />
              )
            ) : (
              <PropertyManagement
                propertyId={roomId}
                navigate={navigate}
                notify={notify}
              />
            )
          ) : (
            <Landlord
              page={page}
              roomId={roomId}
              contractId={page === 'contracts' ? propertySection : undefined}
              properties={properties}
              setProperties={setProperties}
              rooms={rooms}
              setRooms={setRooms}
              requests={requests}
              setRequests={setRequests}
              invoices={[]}
              setInvoices={setInvoices}
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
          {activeRole === "tenant" ? <Check size={18} aria-hidden="true" /> : <Icon name="check" size={18} />}
          {toast}
        </div>
      )}
    </div>
  );
}
