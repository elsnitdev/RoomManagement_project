import {
  AlertTriangle,
  ArrowLeft,
  Building2,
  Droplets,
  FileText,
  ReceiptText,
  Users,
  Zap,
} from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { useApiQuery } from "../hooks/useApiQuery";
import { getProperty, getRoom, listRoomRows } from "../services/api";
import { billingApi } from "../services/api/billingApi";
import RoomImageGallery from "../components/rooms/RoomImageGallery";
import {
  Feedback,
  Status,
  moneyText,
} from "../components/property/ManagementUi";
import "../styles/property-management.css";
import "./RoomDetailPage.css";

const floorText = (floor) =>
  floor == null ? "Chưa cập nhật" : floor === 0 ? "Tầng trệt" : `Tầng ${floor}`;

const previousMonthPeriod = () => {
  const now = new Date(Date.now() + 7 * 3600_000);
  const year = now.getUTCFullYear();
  const month = now.getUTCMonth() + 1;
  const prev = new Date(Date.UTC(year, month - 2, 1));
  const py = prev.getUTCFullYear();
  const pm = prev.getUTCMonth() + 1;
  return {
    year: py,
    month: pm,
    periodStr: `${py}-${String(pm).padStart(2, "0")}`,
  };
};

function Fact({ label, children }) {
  return (
    <div className="rd-fact">
      <span>{label}</span>
      <strong>{children}</strong>
    </div>
  );
}

export default function RoomDetailPage({
  propertyId,
  roomId,
  navigate,
  notify,
}) {
  const { user } = useAuth();
  const roomQuery = useApiQuery(
    `rooms:${propertyId}:${user?.id}:detail:${roomId}`,
    () => getRoom(propertyId, roomId),
    { enabled: Boolean(user && propertyId && roomId) },
  );
  const propertyQuery = useApiQuery(
    `properties:${user?.id}:detail:${propertyId}`,
    () => getProperty(propertyId),
    { enabled: Boolean(user && propertyId) },
  );
  const rowsQuery = useApiQuery(
    `room-rows:${propertyId}:${user?.id}`,
    () => listRoomRows(propertyId),
    { enabled: Boolean(user && propertyId) },
  );
  const readingsQuery = useApiQuery(
    `room-readings:${propertyId}:${user?.id}:${roomId}`,
    ({ signal } = {}) => billingApi.readings(propertyId, roomId, signal),
    { enabled: Boolean(user && propertyId && roomId) },
  );

  const room = roomQuery.data?.data;
  const property = propertyQuery.data?.data;
  const row = (rowsQuery.data || []).find((item) => item.id === room?.rowId);
  const occupants = room?.occupants || [];
  const readings = Array.isArray(readingsQuery.data) ? readingsQuery.data : [];
  const sortedReadings = [...readings].sort(
    (a, b) =>
      b.year - a.year ||
      b.month - a.month ||
      String(a.type).localeCompare(String(b.type)),
  );
  const prevPeriod = previousMonthPeriod();
  const hasPrevElectricity = readings.some(
    (r) =>
      r.type === "Electricity" &&
      Number(r.year) === prevPeriod.year &&
      Number(r.month) === prevPeriod.month,
  );
  const hasPrevWater = readings.some(
    (r) =>
      r.type === "Water" &&
      Number(r.year) === prevPeriod.year &&
      Number(r.month) === prevPeriod.month,
  );
  const missingPrevTypes = [
    !hasPrevElectricity ? "Điện" : null,
    !hasPrevWater ? "Nước" : null,
  ].filter(Boolean);
  const missingPrevMeter = missingPrevTypes.length > 0;

  const back = () => navigate(`landlord/properties/${propertyId}/rooms`);
  const goToMeterInput = () =>
    navigate(`landlord/utilities/${propertyId}/meters/${prevPeriod.periodStr}`);

  return (
    <div className="pm-page rd-page">
      <nav className="rd-breadcrumb" aria-label="Đường dẫn">
        <button type="button" onClick={back}>
          <ArrowLeft size={17} aria-hidden="true" /> Danh sách phòng
        </button>
        <span aria-hidden="true">/</span>
        <span>Chi tiết phòng</span>
      </nav>

      {roomQuery.isLoading || propertyQuery.isLoading ? (
        <div className="pm-loading" role="status">
          Đang tải chi tiết phòng...
        </div>
      ) : roomQuery.error || propertyQuery.error ? (
        <Feedback
          error={roomQuery.error || propertyQuery.error}
          onRetry={() => {
            roomQuery.reload();
            propertyQuery.reload();
          }}
        />
      ) : room && property ? (
        <>
          <header className="rd-hero">
            <div className="rd-hero__identity">
              <span className="pm-eyebrow">
                CHI TIẾT PHÒNG · {property.name}
              </span>
              <h1>Phòng {room.roomNumber}</h1>
              <p>
                {room.roomName || "Chưa đặt tên phòng"} ·{" "}
                {property.address || "Chưa cập nhật địa chỉ"}
              </p>
            </div>
            <div className="rd-hero__status">
              <Status roomStatus={room.status} />
            </div>
          </header>

          <div className="rd-metrics" aria-label="Thông số chính">
            <div className="rd-metric">
              <span>Giá thuê hàng tháng</span>
              <strong>{moneyText(room.basePrice)}</strong>
            </div>
            <div className="rd-metric">
              <span>Diện tích</span>
              <strong>
                {room.area == null ? "Chưa cập nhật" : `${room.area} m²`}
              </strong>
            </div>
            <div className="rd-metric">
              <span>Sức chứa tối đa</span>
              <strong>{room.maxOccupants} người</strong>
            </div>
          </div>

          <div className="rd-layout">
            <div className="rd-main">
              <div className="content-card rd-gallery-card">
                <RoomImageGallery
                  propertyId={propertyId}
                  roomId={roomId}
                  notify={notify}
                />
              </div>
              <section
                className="content-card rd-card"
                aria-labelledby="rd-info-title"
              >
                <div className="rd-card__heading">
                  <Building2 size={20} aria-hidden="true" />
                  <div>
                    <h2 id="rd-info-title">Thông tin phòng</h2>
                    <p>Vị trí và thông tin vận hành</p>
                  </div>
                </div>
                <div className="rd-facts">
                  <Fact label="Mã phòng">{room.roomNumber}</Fact>
                  <Fact label="Tên phòng">
                    {room.roomName || "Chưa cập nhật"}
                  </Fact>
                  <Fact label="Khu trọ">{property.name}</Fact>
                  <Fact label="Dãy">
                    {row
                      ? `${row.code} · ${row.name}`
                      : room.rowId
                        ? rowsQuery.isLoading
                          ? "Đang tải..."
                          : "Không xác định"
                        : "Chưa có dãy"}
                  </Fact>
                  <Fact label="Tầng">{floorText(room.floor)}</Fact>
                  <Fact label="Tình trạng">
                    <Status roomStatus={room.status} />
                  </Fact>
                </div>
                {room.description && (
                  <div className="rd-description">
                    <h3>Mô tả</h3>
                    <p>{room.description}</p>
                  </div>
                )}
              </section>

              <section
                className={`content-card rd-card${missingPrevMeter && !readingsQuery.isLoading ? " rd-meters-card--alert" : ""}`}
                aria-labelledby="rd-meters-title"
              >
                <div className="rd-meters-head">
                  <div className="rd-card__heading">
                    <Zap size={20} aria-hidden="true" />
                    <div>
                      <h2 id="rd-meters-title">Lịch sử điện nước</h2>
                      <p>Theo dõi chỉ số chốt hàng tháng của phòng</p>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="rd-meter-link-btn"
                    onClick={goToMeterInput}
                  >
                    Nhập / sửa chỉ số
                  </button>
                </div>

                {!readingsQuery.isLoading &&
                  !readingsQuery.error &&
                  missingPrevMeter && (
                    <div className="rd-meter-warning" role="status">
                      <AlertTriangle size={18} aria-hidden="true" />
                      <div className="rd-meter-warning__text">
                        <strong>
                          Tháng vừa qua ({prevPeriod.month}/{prevPeriod.year})
                          chưa nhập đủ chỉ số: thiếu{" "}
                          {missingPrevTypes.join(" và ")}
                        </strong>
                        <span>
                          Hãy bổ sung chỉ số kỳ {prevPeriod.month}/
                          {prevPeriod.year} để đảm bảo số liệu khi lập hóa đơn
                          hoặc tạo hợp đồng mới.
                        </span>
                      </div>
                      <button
                        type="button"
                        className="rd-meter-warning__btn"
                        onClick={goToMeterInput}
                      >
                        Nhập chỉ số tháng {prevPeriod.month}/{prevPeriod.year}
                      </button>
                    </div>
                  )}

                {readingsQuery.isLoading ? (
                  <p className="rd-empty" role="status">
                    Đang tải lịch sử điện nước...
                  </p>
                ) : readingsQuery.error ? (
                  <Feedback
                    error={readingsQuery.error}
                    onRetry={readingsQuery.reload}
                  />
                ) : sortedReadings.length ? (
                  <div className="rd-meter-table-wrap">
                    <table className="rd-meter-table">
                      <thead>
                        <tr>
                          <th>Loại</th>
                          <th>Kỳ</th>
                          <th>Chỉ số đầu → cuối</th>
                          <th style={{ textAlign: "right" }}>Tiêu thụ</th>
                        </tr>
                      </thead>
                      <tbody>
                        {sortedReadings.slice(0, 12).map((item) => {
                          const isElec = item.type === "Electricity";
                          return (
                            <tr key={item.id}>
                              <td>
                                <span className="rd-meter-type">
                                  {isElec ? (
                                    <Zap size={14} aria-hidden="true" />
                                  ) : (
                                    <Droplets size={14} aria-hidden="true" />
                                  )}
                                  {isElec ? "Điện" : "Nước"}
                                </span>
                              </td>
                              <td>
                                Tháng {item.month}/{item.year}
                              </td>
                              <td>
                                {item.previousValue} →{" "}
                                <strong>{item.currentValue}</strong>
                              </td>
                              <td style={{ textAlign: "right" }}>
                                <strong>
                                  {item.used} {isElec ? "kWh" : "m³"}
                                </strong>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p className="rd-empty">
                    Phòng chưa có lịch sử ghi chỉ số điện nước.
                  </p>
                )}
              </section>
            </div>

            <aside className="rd-sidebar">
              <section
                className="content-card rd-card"
                aria-labelledby="rd-occupants-title"
              >
                <div className="rd-card__heading">
                  <Users size={20} aria-hidden="true" />
                  <div>
                    <h2 id="rd-occupants-title">Người đang ở</h2>
                    <p>
                      {occupants.length} / {room.maxOccupants} người
                    </p>
                  </div>
                </div>
                {occupants.length ? (
                  <ul className="rd-occupants">
                    {occupants.map((person) => (
                      <li key={person.tenantId}>{person.fullName}</li>
                    ))}
                  </ul>
                ) : (
                  <p className="rd-empty">Phòng chưa có người ở.</p>
                )}
              </section>
              <section
                className="content-card rd-card"
                aria-labelledby="rd-actions-title"
              >
                <div className="rd-card__heading">
                  <FileText size={20} aria-hidden="true" />
                  <div>
                    <h2 id="rd-actions-title">Quản lý phòng</h2>
                    <p>Hợp đồng và hóa đơn</p>
                  </div>
                </div>
                <div className="rd-hero__actions">
                  {room.status === "Available" && (
                    <button
                      className="rd-action rd-action--primary"
                      type="button"
                      onClick={() =>
                        navigate(
                          `landlord/contracts/new/${propertyId}/${room.id}`,
                        )
                      }
                    >
                      <FileText size={17} aria-hidden="true" /> Tạo hợp đồng
                    </button>
                  )}
                  {room.contractId && (
                    <button
                      className="rd-action"
                      type="button"
                      onClick={() =>
                        navigate(
                          `landlord/contracts/${propertyId}/${room.contractId}`,
                        )
                      }
                    >
                      <FileText size={17} aria-hidden="true" /> Xem hợp đồng
                      hiện tại
                    </button>
                  )}
                  <button
                    className="rd-action"
                    type="button"
                    onClick={() =>
                      navigate(
                        `landlord/billing/rooms/${propertyId}/${room.id}`,
                      )
                    }
                  >
                    <ReceiptText size={17} aria-hidden="true" /> Xem hóa đơn
                    phòng
                  </button>
                </div>
              </section>
            </aside>
          </div>
        </>
      ) : (
        <div className="pm-feedback" role="alert">
          Không tìm thấy phòng.
        </div>
      )}
    </div>
  );
}
