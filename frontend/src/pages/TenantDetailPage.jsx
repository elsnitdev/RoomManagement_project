import { useEffect, useState } from "react";
import {
  ArrowLeft,
  CalendarDays,
  FileText,
  House,
  Image,
  UserRound,
} from "lucide-react";
import { getLandlordTenantById } from "../services/api/landlordTenantApi";
import { billingApi } from "../services/api/billingApi";
import { contractApi } from "../services/api/contractApi";
import { listRoomRows } from "../services/api/RoomRowApi";
import { GENDER_LABELS } from "../constants/tenants";
import "./TenantDetailPage.css";

const today = () =>
  new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
const date = (value) =>
  value ? String(value).slice(0, 10).split("-").reverse().join("/") : "—";
const money = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
const errorMessage = (error) =>
  error?.message || "Không thể tải dữ liệu. Vui lòng thử lại.";
const statusLabels = {
  Draft: "Bản nháp",
  Active: "Đang hiệu lực",
  PendingMoveOut: "Chờ bàn giao",
  PendingSettlement: "Chờ thanh toán và tất toán",
  Expired: "Hết hạn",
  Terminated: "Đã kết thúc",
  Cancelled: "Đã hủy",
};

function Fact({ label, children }) {
  return (
    <div className="tenant-profile-fact">
      <dt>{label}</dt>
      <dd>{children || "Chưa cập nhật"}</dd>
    </div>
  );
}

function Placeholder({ label, note, className = "" }) {
  return (
    <div
      className={`tenant-profile-placeholder ${className}`}
      aria-label={label}
    >
      <Image size={25} aria-hidden="true" />
      <strong>{label}</strong>
      <small>{note}</small>
    </div>
  );
}

export default function TenantDetailPage({ tenantId, navigate }) {
  const [profile, setProfile] = useState(null);
  const [memberships, setMemberships] = useState([]);
  const [currentRoom, setCurrentRoom] = useState(null);
  const [currentRow, setCurrentRow] = useState(null);
  const [loading, setLoading] = useState(true);
  const [profileError, setProfileError] = useState("");
  const [contractsError, setContractsError] = useState("");
  const [roomError, setRoomError] = useState("");
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setProfile(null);
    setMemberships([]);
    setCurrentRoom(null);
    setCurrentRow(null);
    setProfileError("");
    setContractsError("");
    setRoomError("");

    async function load() {
      try {
        const response = await getLandlordTenantById(tenantId, {
          signal: controller.signal,
        });
        if (controller.signal.aborted) return;
        setProfile(response.data);
      } catch (error) {
        if (!controller.signal.aborted) {
          setProfileError(errorMessage(error));
          setLoading(false);
        }
        return;
      }

      try {
        const propertyResponse = await billingApi.properties(controller.signal);
        const properties = propertyResponse.data || [];
        const results = await Promise.allSettled(
          properties.map(async (property) => {
            const response = await contractApi.list(
              property.id,
              controller.signal,
            );
            return (response.data || []).flatMap((contract) =>
              (contract.tenants || [])
                .filter((tenant) => tenant.tenantId === tenantId)
                .map((tenant) => ({ contract, tenant, property })),
            );
          }),
        );
        if (controller.signal.aborted) return;
        const entries = results
          .flatMap((result) =>
            result.status === "fulfilled" ? result.value : [],
          )
          .sort(
            (a, b) =>
              b.tenant.joinDate.localeCompare(a.tenant.joinDate) ||
              b.contract.startDate.localeCompare(a.contract.startDate),
          );
        setMemberships(entries);
        if (results.some((result) => result.status === "rejected")) {
          setContractsError(
            "Chưa tải được hợp đồng ở một số khu trọ. Danh sách dưới đây có thể chưa đầy đủ.",
          );
        }
        const current = entries.find(
          ({ contract, tenant }) =>
            contract.status === "Active" &&
            contract.startDate <= today() &&
            (!contract.endDate || contract.endDate >= today()) &&
            tenant.joinDate <= today() &&
            (!tenant.leaveDate || tenant.leaveDate >= today()),
        );
        if (current) {
          try {
            const rooms = await billingApi.rooms(
              current.property.id,
              controller.signal,
            );
            if (!controller.signal.aborted) {
              const room =
                (rooms.data || []).find(
                  (item) => item.id === current.contract.roomId,
                ) || null;
              setCurrentRoom(room);
              if (room?.rowId) {
                try {
                  const rows = await listRoomRows(current.property.id);
                  if (!controller.signal.aborted)
                    setCurrentRow(
                      (rows || []).find((row) => row.id === room.rowId) || null,
                    );
                } catch {
                  /* Dãy là thông tin bổ sung; vẫn hiển thị phòng và hợp đồng. */
                }
              }
            }
          } catch {
            if (!controller.signal.aborted)
              setRoomError("Chưa tải được thông tin chi tiết của phòng.");
          }
        }
      } catch (error) {
        if (!controller.signal.aborted) setContractsError(errorMessage(error));
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    load();
    return () => controller.abort();
  }, [tenantId, revision]);

  const now = today();
  const current = memberships.find(
    ({ contract, tenant }) =>
      contract.status === "Active" &&
      contract.startDate <= now &&
      (!contract.endDate || contract.endDate >= now) &&
      tenant.joinDate <= now &&
      (!tenant.leaveDate || tenant.leaveDate >= now),
  );
  const past = memberships.filter(
    ({ contract, tenant }) =>
      contract.status !== "Draft" &&
      contract.status !== "Cancelled" &&
      ((tenant.leaveDate && tenant.leaveDate < now) ||
        contract.status === "Expired" ||
        contract.status === "Terminated" ||
        (contract.endDate && contract.endDate < now)),
  );

  return (
    <main className="tenant-profile-page">
      <button
        type="button"
        className="tenant-profile-back"
        onClick={() => navigate("landlord/tenants")}
      >
        <ArrowLeft size={17} aria-hidden="true" /> Danh sách người thuê
      </button>
      {profileError ? (
        <section
          className="tenant-profile-card tenant-profile-message"
          role="alert"
        >
          <h1>Không tải được hồ sơ người thuê</h1>
          <p>{profileError}</p>
          <button
            type="button"
            onClick={() => setRevision((value) => value + 1)}
          >
            Thử lại
          </button>
        </section>
      ) : !profile ? (
        <section
          className="tenant-profile-card tenant-profile-message"
          aria-busy="true"
          role="status"
        >
          Đang tải hồ sơ người thuê…
        </section>
      ) : (
        <>
          <header className="tenant-profile-header">
            <div className="tenant-profile-avatar" aria-hidden="true">
              {profile.fullName
                ?.trim()
                .slice(0, 1)
                .toLocaleUpperCase("vi-VN") || "N"}
            </div>
            <div>
              <span className="tenant-profile-eyebrow">HỒ SƠ NGƯỜI THUÊ</span>
              <h1>{profile.fullName}</h1>
              <p>
                {profile.userId
                  ? "Đã liên kết tài khoản hệ thống"
                  : "Hồ sơ chưa liên kết tài khoản"}
              </p>
            </div>
            <span
              className={`tenant-profile-state${current ? " is-current" : ""}`}
            >
              {loading
                ? "Đang kiểm tra chỗ ở"
                : current
                  ? "Đang ở"
                  : "Chưa có phòng đang ở"}
            </span>
          </header>

          <div className="tenant-profile-layout">
            <div className="tenant-profile-main">
              <section
                className="tenant-profile-card"
                aria-labelledby="tenant-current-title"
              >
                <div className="tenant-profile-section-title">
                  <House size={19} aria-hidden="true" />
                  <h2 id="tenant-current-title">Phòng đang ở</h2>
                </div>
                {loading ? (
                  <p className="tenant-profile-muted" role="status">
                    Đang kiểm tra hợp đồng và phòng…
                  </p>
                ) : current ? (
                  <>
                    {roomError && (
                      <p className="tenant-profile-warning" role="alert">
                        {roomError}{" "}
                        <button
                          type="button"
                          onClick={() => setRevision((value) => value + 1)}
                        >
                          Thử lại
                        </button>
                      </p>
                    )}
                    <div className="tenant-profile-room-heading">
                      <div>
                        <strong>
                          Phòng{" "}
                          {current.contract.roomNumber ||
                            currentRoom?.roomNumber ||
                            "—"}
                        </strong>
                        <span>{current.property.name}</span>
                      </div>
                      <button
                        type="button"
                        onClick={() =>
                          navigate(
                            `landlord/contracts/${current.property.id}/${current.contract.id}`,
                          )
                        }
                      >
                        Xem hợp đồng
                      </button>
                    </div>
                    <dl className="tenant-profile-facts">
                      <Fact label="Địa chỉ khu trọ">
                        {current.property.address}
                      </Fact>
                      <Fact label="Dãy">
                        {currentRow
                          ? `${currentRow.code} · ${currentRow.name}`
                          : "Chưa cập nhật"}
                      </Fact>
                      <Fact label="Tầng">
                        {currentRoom?.floor == null
                          ? "Chưa cập nhật"
                          : currentRoom.floor === 0
                            ? "Tầng trệt"
                            : `Tầng ${currentRoom.floor}`}
                      </Fact>
                      <Fact label="Diện tích">
                        {currentRoom?.area
                          ? `${currentRoom.area} m²`
                          : "Chưa cập nhật"}
                      </Fact>
                      <Fact label="Ngày vào ở">
                        {date(current.tenant.joinDate)}
                      </Fact>
                      <Fact label="Hợp đồng">
                        {current.contract.contractCode}
                      </Fact>
                      <Fact label="Thời hạn">
                        {date(current.contract.startDate)} –{" "}
                        {date(current.contract.endDate)}
                      </Fact>
                      <Fact label="Tiền thuê/tháng">
                        {money(current.contract.monthlyRent)}
                      </Fact>
                    </dl>
                  </>
                ) : (
                  <div className="tenant-profile-empty">
                    <House size={24} aria-hidden="true" />
                    <strong>
                      {contractsError
                        ? "Chưa xác định được phòng đang ở"
                        : "Chưa có phòng đang ở"}
                    </strong>
                    <p>
                      {contractsError
                        ? "Một phần dữ liệu hợp đồng chưa tải được. Vui lòng thử lại."
                        : "Chưa tìm thấy hợp đồng đang hiệu lực gắn với hồ sơ này."}
                    </p>
                  </div>
                )}
              </section>

              <section
                className="tenant-profile-card"
                aria-labelledby="tenant-history-title"
              >
                <div className="tenant-profile-section-title">
                  <FileText size={19} aria-hidden="true" />
                  <h2 id="tenant-history-title">Hợp đồng trước đó</h2>
                  <span>{past.length}</span>
                </div>
                {contractsError && (
                  <p className="tenant-profile-warning" role="alert">
                    {contractsError}{" "}
                    <button
                      type="button"
                      onClick={() => setRevision((value) => value + 1)}
                    >
                      Thử lại
                    </button>
                  </p>
                )}
                {loading ? (
                  <p className="tenant-profile-muted" role="status">
                    Đang tải lịch sử hợp đồng…
                  </p>
                ) : past.length ? (
                  <ul className="tenant-profile-history">
                    {past.map(({ contract, tenant, property }) => (
                      <li key={`${contract.id}-${tenant.id}`}>
                        <div className="tenant-profile-history-icon">
                          <FileText size={18} aria-hidden="true" />
                        </div>
                        <div>
                          <strong>{contract.contractCode}</strong>
                          <p>
                            Phòng {contract.roomNumber || "—"} · {property.name}
                          </p>
                          <small>
                            <CalendarDays size={13} aria-hidden="true" /> Ở từ{" "}
                            {date(tenant.joinDate)} đến{" "}
                            {date(
                              tenant.leaveDate ||
                                contract.closedOn ||
                                contract.endDate,
                            )}
                          </small>
                        </div>
                        <div className="tenant-profile-history-side">
                          <span>
                            {tenant.leaveDate
                              ? "Đã rời phòng"
                              : statusLabels[contract.status] ||
                                contract.status}
                          </span>
                          <button
                            type="button"
                            onClick={() =>
                              navigate(
                                `landlord/contracts/${property.id}/${contract.id}`,
                              )
                            }
                          >
                            Xem chi tiết
                          </button>
                        </div>
                      </li>
                    ))}
                  </ul>
                ) : (
                  !contractsError && (
                    <div className="tenant-profile-empty">
                      <FileText size={24} aria-hidden="true" />
                      <strong>Chưa có hợp đồng trước đó</strong>
                      <p>Lịch sử thuê sẽ hiển thị ở đây khi có dữ liệu.</p>
                    </div>
                  )
                )}
              </section>
            </div>

            <aside
              className="tenant-profile-aside"
              aria-label="Thông tin liên hệ và vị trí hình ảnh"
            >
              <section
                className="tenant-profile-card"
                aria-labelledby="tenant-info-title"
              >
                <div className="tenant-profile-section-title">
                  <UserRound size={19} aria-hidden="true" />
                  <h2 id="tenant-info-title">Thông tin cá nhân</h2>
                </div>
                <dl className="tenant-profile-facts tenant-profile-facts--single">
                  <Fact label="Họ và tên">{profile.fullName}</Fact>
                  <Fact label="Số điện thoại">
                    {profile.phoneNumber ? (
                      <a href={`tel:${profile.phoneNumber}`}>
                        {profile.phoneNumber}
                      </a>
                    ) : null}
                  </Fact>
                  <Fact label="Email">
                    {profile.email ? (
                      <a href={`mailto:${profile.email}`}>{profile.email}</a>
                    ) : null}
                  </Fact>
                  <Fact label="Ngày sinh">
                    {profile.dateOfBirth ? date(profile.dateOfBirth) : null}
                  </Fact>
                  <Fact label="Giới tính">
                    {GENDER_LABELS[profile.gender] || "Chưa cập nhật"}
                  </Fact>
                  <Fact label="CCCD / CMND">{profile.citizenId}</Fact>
                  <Fact label="Địa chỉ thường trú">
                    {profile.permanentAddress}
                  </Fact>
                  <Fact label="Tài khoản">
                    {profile.userId ? "Đã liên kết" : "Chưa liên kết"}
                  </Fact>
                </dl>
              </section>
              <section className="tenant-profile-card">
                <h2>Hình ảnh</h2>
                <p className="tenant-profile-muted">
                  Khu vực ảnh sẽ được cập nhật khi chức năng quản lý hình ảnh
                  sẵn sàng.
                </p>
                <Placeholder
                  label="Ảnh người thuê"
                  note="Chưa có ảnh"
                  className="tenant-profile-placeholder--portrait"
                />
                <Placeholder label="Ảnh phòng" note="Chưa có ảnh" />
              </section>
            </aside>
          </div>
        </>
      )}
    </main>
  );
}
