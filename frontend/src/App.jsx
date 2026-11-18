import { useEffect, useRef, useState } from "react";
import {
  Bell,
  Building2,
  Check,
  ChevronRight,
  CircleAlert,
  FileText,
  House,
  LayoutDashboard,
  LogOut,
  Menu,
  MessageSquareText,
  ReceiptText,
  UserCog,
  UserRound,
  Users,
  X,
} from "lucide-react";
import { useAuth } from "./contexts/AuthContext";
import Admin from "./Admin";
import Auth from "./Auth";
import Landlord from "./Landlord";
import Tenant from "./Tenant";
import Profile from "./Profile";
import PropertyManagement from "./components/PropertyManagement";
import ManagerManagement from "./components/ManagerManagement";
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
import { Brand } from "./ui";
import "./App.css";

const adminNavGroups = [
  {
    label: "Quản trị hệ thống",
    items: [
      { id: "overview", label: "Tổng quan", icon: LayoutDashboard },
      { id: "approvals", label: "Duyệt chủ trọ", icon: Users },
      { id: "users", label: "Tài khoản", icon: UserCog },
      { id: "services", label: "Dịch vụ", icon: FileText },
    ],
  },
  {
    label: "Tài khoản",
    items: [{ id: "profile", label: "Hồ sơ", icon: UserRound }],
  },
];

const landlordNavGroups = [
  {
    label: "Vận hành",
    items: [
      { id: "overview", label: "Tổng quan", icon: LayoutDashboard },
      { id: "properties", label: "Khu trọ", icon: Building2 },
      { id: "tenants", label: "Người ở", icon: Users },
      { id: "requests", label: "Yêu cầu", icon: MessageSquareText },
    ],
  },
  {
    label: "Tài chính & Hợp đồng",
    items: [
      { id: "contracts", label: "Hợp đồng", icon: FileText },
      { id: "billing", label: "Hóa đơn", icon: ReceiptText },
    ],
  },
  {
    label: "Quản trị & Tài khoản",
    items: [
      { id: "managers", label: "Quản lý nhân sự", icon: UserCog },
      { id: "profile", label: "Hồ sơ", icon: UserRound },
    ],
  },
];

const managerNavGroups = landlordNavGroups
  .map((group) => ({
    ...group,
    items: group.items.filter((item) => item.id !== "managers"),
  }))
  .filter((group) => group.items.length > 0);

const tenantNavGroups = [
  {
    label: "Chỗ ở & Giao dịch",
    items: [
      { id: "overview", label: "Tổng quan", icon: LayoutDashboard },
      { id: "room", label: "Chỗ ở của tôi", icon: House },
      { id: "billing", label: "Hóa đơn", icon: ReceiptText },
      { id: "contracts", label: "Hợp đồng", icon: FileText },
    ],
  },
  {
    label: "Hỗ trợ & Tài khoản",
    items: [
      { id: "requests", label: "Yêu cầu", icon: MessageSquareText },
      { id: "notices", label: "Thông báo", icon: Bell },
      { id: "profile", label: "Tài khoản", icon: UserRound },
    ],
  },
];
const titleMap = {
  overview: "Tổng quan",
  approvals: "Duyệt chủ trọ",
  users: "Tài khoản người dùng",
  services: "Danh mục dịch vụ",
  properties: "Khu trọ",
  managers: "Quản lý nhân sự",
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
  const { user, isAuthenticated, signOut, updateUser } = useAuth();
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
      : user?.role === "Landlord" || user?.role === "Manager"
        ? "landlord"
        : user?.role === "Tenant"
          ? "tenant"
          : null;
  const mustChangePassword = Boolean(
    isAuthenticated && user?.mustChangePassword,
  );
  const canAccess =
    isAuthenticated && !mustChangePassword && routeRole === userRoute;
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
          : action
            ? "Chi tiết phòng"
            : "Quản lý phòng"
        : page === "billing" && detailId && detailId !== "rooms"
          ? "Chi tiết hóa đơn"
          : page === "tenants" && detailId
            ? "Chi tiết người thuê"
            : titleMap[page] ||
              (route === "register"
                ? "Đăng ký chủ trọ"
                : route === "tenant-register"
                  ? "Đăng ký người thuê"
                  : route === "change-password"
                    ? "Đổi mật khẩu tạm"
                    : "Đăng nhập");
    document.title = `${route.startsWith("tenant/room") ? "Chỗ ở của tôi" : title} | Nhà Trọ Xanh`;
  }, [route]);
  useEffect(() => {
    if (mustChangePassword && route !== "change-password") {
      window.location.hash = "/change-password";
      return;
    }
    if (protectedRoute && !canAccess) {
      window.location.hash = userRoute ? `/${userRoute}/overview` : "/login";
    }
  }, [mustChangePassword, route, protectedRoute, canAccess, userRoute]);
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
        : roomAction
          ? "Chi tiết phòng"
          : "Quản lý phòng"
      : titleMap[page];
  const navGroups =
    activeRole === "admin"
      ? adminNavGroups
      : activeRole === "tenant"
        ? tenantNavGroups
        : user?.role === "Manager"
          ? managerNavGroups
          : landlordNavGroups;
  const pendingApprovalCount = approvals.filter(
    (entry) => entry.status === "pending",
  ).length;
  return (
    <div className="app-shell">
      {/* <button
        className="skip-link"
        type="button"
        onClick={() => document.getElementById("main-content")?.focus()}
      >
        Bỏ qua menu
      </button> */}
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
            <X size={19} strokeWidth={1.85} aria-hidden="true" />
          </button>
        </div>
        <nav
          className="sidebar-nav"
          aria-label={
            activeRole === "admin"
              ? "Menu quản trị"
              : activeRole === "tenant"
                ? "Menu người thuê"
                : "Menu chủ trọ"
          }
        >
          {navGroups.map((group) => (
            <div key={group.label} className="sidebar-group">
              <div className="sidebar-section-label">{group.label}</div>
              <div className="nav-list">
                {group.items.map((item) => {
                  const NavIcon = item.icon;
                  const isActive =
                    page === item.id ||
                    (page === "room" && item.id === "rooms");
                  return (
                    <button
                      key={item.id}
                      className={`nav-item ${isActive ? "nav-item--active" : ""}`}
                      type="button"
                      aria-current={isActive ? "page" : undefined}
                      onClick={() => navigate(`${activeRole}/${item.id}`)}
                    >
                      <NavIcon
                        size={18}
                        strokeWidth={1.85}
                        aria-hidden="true"
                      />
                      <span>{item.label}</span>
                      {item.id === "approvals" && pendingApprovalCount > 0 && (
                        <span className="nav-count">
                          {pendingApprovalCount}
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </nav>
        <div className="sidebar-bottom">
          {page !== "properties" &&
            page !== "billing" &&
            page !== "services" &&
            page !== "contracts" &&
            page !== "managers" &&
            page !== "users"}
          <button
            className="nav-item logout-button"
            type="button"
            onClick={logout}
          >
            <LogOut size={18} strokeWidth={1.85} aria-hidden="true" />
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
              <Menu size={20} strokeWidth={1.85} aria-hidden="true" />
            </button>
            <span className="breadcrumb">
              <span>Không gian làm việc</span>
              <ChevronRight size={15} strokeWidth={1.85} aria-hidden="true" />
              <strong>
                {activeRole === "tenant" && page === "room"
                  ? "Chỗ ở của tôi"
                  : page === "properties"
                    ? propertyPageTitle
                    : page === "billing" && roomId && roomId !== "rooms"
                      ? "Chi tiết hóa đơn"
                      : titleMap[page]}
              </strong>
            </span>
          </div>
          <div className="topbar-right">
            {user?.role === "Landlord" && user?.landlordCode && (
              <span className="topbar-code-chip">
                Mã chủ trọ: <strong>{user.landlordCode}</strong>
              </span>
            )}
            {page !== "properties" &&
              page !== "billing" &&
              page !== "services" &&
              page !== "contracts" &&
              page !== "managers" &&
              page !== "users"}
            <button
              type="button"
              className="topbar-profile"
              onClick={() => navigate(`${activeRole}/profile`)}
              aria-label="Mở hồ sơ tài khoản"
            >
              <span className="avatar">
                {activeRole === "admin"
                  ? "AD"
                  : activeRole === "tenant"
                    ? "NT"
                    : user?.role === "Manager"
                      ? "QL"
                      : "NA"}
              </span>
              <span className="topbar-profile__meta">
                <strong>{user?.userName || "Người dùng"}</strong>
                <small>
                  {activeRole === "admin"
                    ? "Admin"
                    : activeRole === "tenant"
                      ? "Người thuê"
                      : user?.role === "Manager"
                        ? "Quản lý khu trọ"
                        : "Chủ trọ"}
                </small>
              </span>
            </button>
          </div>
        </header>
        <main
          id="main-content"
          tabIndex="-1"
          className={`main-content${page === "properties" && propertySection === "rooms" && roomAction === "new" ? " main-content--room-batch" : ""}`}
        >
          {activeRole === "tenant" && page === "billing" ? (
            <TenantBilling initialInvoiceId={roomId} />
          ) : activeRole === "tenant" ? (
            <Tenant
              page={page}
              user={user}
              updateUser={updateUser}
              navigate={navigate}
              notify={notify}
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
          ) : activeRole === "landlord" &&
            page === "managers" &&
            user?.role === "Landlord" ? (
            <ManagerManagement notify={notify} />
          ) : activeRole === "landlord" && page === "billing" ? (
            <LandlordBilling
              invoiceId={roomId === "rooms" ? undefined : roomId}
              initialPropertyId={
                roomId === "rooms" ? propertySection : undefined
              }
              initialRoomId={roomId === "rooms" ? roomAction : undefined}
              navigate={navigate}
            />
          ) : activeRole === "landlord" && page === "contracts" ? (
            <LandlordContracts
              key={route}
              routeParts={[roomId, propertySection, roomAction]}
              navigate={navigate}
              notify={notify}
            />
          ) : page === "properties" ? (
            propertySection === "rules" && roomId ? (
              <PropertyRules
                key={roomId}
                propertyId={roomId}
                navigate={navigate}
              />
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
              contractId={page === "contracts" ? propertySection : undefined}
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
          <Check size={18} strokeWidth={1.85} aria-hidden="true" />
          {toast}
        </div>
      )}
    </div>
  );
}
