import { useState } from "react";
import { contracts, formatCurrency, invoiceHistory, todayLabel } from "./mockData";
import { Badge, Button, Empty, Field, Heading, Icon, Stat } from "./ui";
import RoomDetail from "./RoomDetail";
import InvoicePaymentPage from "./components/billing/InvoicePaymentPage";
import TenantManager from "./TenantManager";
import TenantDetailPage from "./pages/TenantDetailPage";
import LandlordContracts from "./components/contracts/LandlordContracts";
import LandlordRequestsPage from "./pages/LandlordRequestsPage";

const roomStatus = {
  occupied: "Đã thuê",
  vacant: "Còn trống",
  maintenance: "Bảo trì",
};

export default function Landlord({
  page,
  roomId,
  contractId,
  properties,
  setProperties,
  rooms,
  setRooms,
  requests,
  setRequests,
  invoices,
  setInvoices,
  selectedProperty,
  setSelectedProperty,
  navigate,
  notify,
}) {
  const [roomFilter, setRoomFilter] = useState("all");
  const [search, setSearch] = useState("");
  const [showPropertyForm, setShowPropertyForm] = useState(false);
  const [showRoomForm, setShowRoomForm] = useState(false);
  const [propertyDraft, setPropertyDraft] = useState({ name: "", address: "" });
  const [roomDraft, setRoomDraft] = useState({
    number: "",
    propertyId: "P-01",
    floor: "1",
    area: "",
    price: "",
  });
  const filteredRooms = rooms.filter(
    (room) =>
      (selectedProperty === "all" || room.propertyId === selectedProperty) &&
      (roomFilter === "all" || room.status === roomFilter) &&
      `${room.number} ${room.tenant}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  const scopedRooms = rooms.filter(
    (room) =>
      selectedProperty === "all" || room.propertyId === selectedProperty,
  );
  const scopedInvoices = invoices.filter(
    (item) =>
      selectedProperty === "all" ||
      scopedRooms.some((room) => room.number === item.room),
  );
  const scopedRequests = requests.filter(
    (item) =>
      selectedProperty === "all" ||
      scopedRooms.some((room) => room.number === item.room),
  );
  const scopedContracts = contracts.filter(
    (item) =>
      selectedProperty === "all" ||
      scopedRooms.some((room) => room.number === item.room),
  );
  const totalRooms = scopedRooms.length;
  const occupied = scopedRooms.filter(
    (room) => room.status === "occupied",
  ).length;
  const vacant = scopedRooms.filter((room) => room.status === "vacant").length;
  const maintenance = scopedRooms.filter(
    (room) => room.status === "maintenance",
  ).length;
  function addInvoiceAdjustment({ invoiceIds, direction, amount, reason }) {
    const targets = new Set(invoiceIds);
    const batchId = Date.now();
    setInvoices((current) => current.map((invoice) => {
      if (!targets.has(invoice.id)) return invoice;
      const delta = direction === "add" ? amount : -amount;
      return {
        ...invoice,
        amount: invoice.amount + delta,
        adjustments: [
          ...(invoice.adjustments || []),
          { id: `ADJ-${batchId}-${invoice.id}`, direction, amount, reason, createdAt: new Date().toISOString() },
        ],
      };
    }));
    notify(`Đã thêm điều chỉnh cho ${invoiceIds.length} hóa đơn mẫu.`);
  }
  const propertyName = (id) =>
    properties.find((item) => item.id === id)?.name || "—";
  const propertySelect = (
    <label className="filter-select">
      <span className="sr-only">Chọn khu trọ</span>
      <Icon name="building" size={17} />
      <select
        value={selectedProperty}
        onChange={(e) => setSelectedProperty(e.target.value)}
      >
        <option value="all">Tất cả khu trọ</option>
        {properties.map((item) => (
          <option key={item.id} value={item.id}>
            {item.name}
          </option>
        ))}
      </select>
      <Icon name="down" size={15} />
    </label>
  );
  function addProperty(event) {
    event.preventDefault();
    const id = `P-${String(properties.length + 1).padStart(2, "0")}`;
    setProperties((current) => [
      ...current,
      { ...propertyDraft, id, rooms: 0, occupied: 0, status: "Đang hoạt động" },
    ]);
    setPropertyDraft({ name: "", address: "" });
    setShowPropertyForm(false);
    notify("Đã thêm khu trọ mẫu.");
  }
  function addRoom(event) {
    event.preventDefault();
    setRooms((current) => [
      ...current,
      {
        ...roomDraft,
        id: `R-${Date.now()}`,
        floor: Number(roomDraft.floor),
        area: Number(roomDraft.area),
        price: Number(roomDraft.price),
        tenant: "",
        status: "vacant",
        due: "",
      },
    ]);
    setProperties((current) =>
      current.map((item) =>
        item.id === roomDraft.propertyId
          ? { ...item, rooms: item.rooms + 1 }
          : item,
      ),
    );
    setRoomDraft({
      number: "",
      propertyId: properties[0]?.id || "",
      floor: "1",
      area: "",
      price: "",
    });
    setShowRoomForm(false);
    notify("Đã thêm phòng mẫu.");
  }
  if (page === "room")
    return (
      <RoomDetail
        room={rooms.find((room) => room.id === roomId)}
        property={properties.find(
          (item) =>
            item.id === rooms.find((room) => room.id === roomId)?.propertyId,
        )}
        navigate={navigate}
      />
    );
  if (page === "properties")
    return (
      <>
        <Heading
          eyebrow="QUẢN LÝ BẤT ĐỘNG SẢN"
          title="Khu trọ của bạn"
          description="Theo dõi các khu trọ và tình trạng lấp đầy."
          action={
            <Button
              icon="plus"
              onClick={() => setShowPropertyForm(!showPropertyForm)}
            >
              Thêm khu trọ
            </Button>
          }
        />
        {showPropertyForm && (
          <section className="content-card inline-form">
            <div className="detail-header">
              <h2>Thêm khu trọ mẫu</h2>
              <button
                className="icon-button"
                type="button"
                aria-label="Đóng biểu mẫu"
                onClick={() => setShowPropertyForm(false)}
              >
                <Icon name="close" />
              </button>
            </div>
            <form onSubmit={addProperty} className="form-grid">
              <Field label="Tên khu trọ" required>
                <input
                  required
                  value={propertyDraft.name}
                  onChange={(e) =>
                    setPropertyDraft({ ...propertyDraft, name: e.target.value })
                  }
                  placeholder="Ví dụ: An Bình Residence"
                />
              </Field>
              <Field label="Địa chỉ" required>
                <input
                  required
                  value={propertyDraft.address}
                  onChange={(e) =>
                    setPropertyDraft({
                      ...propertyDraft,
                      address: e.target.value,
                    })
                  }
                  placeholder="Số nhà, đường, quận/huyện..."
                />
              </Field>
              <div className="form-action">
                <button className="button button--primary" type="submit">
                  Lưu khu trọ mẫu
                </button>
              </div>
            </form>
          </section>
        )}
        <div className="property-grid">
          {properties.map((item) => (
            <article className="property-card" key={item.id}>
              <div className="property-illustration">
                <Icon name="building" size={40} />
                <span className="property-illustration-line" />
              </div>
              <div className="property-content">
                <div className="property-title">
                  <h2>{item.name}</h2>
                  <Badge kind="approved">{item.status}</Badge>
                </div>
                <p>{item.address}</p>
                <div className="property-divider" />
                <div className="property-metrics">
                  <span>
                    <strong>{item.rooms}</strong>
                    <small>Tổng phòng</small>
                  </span>
                  <span>
                    <strong>{item.occupied}</strong>
                    <small>Đã thuê</small>
                  </span>
                  <span>
                    <strong>
                      {
                        rooms.filter(
                          (room) =>
                            room.propertyId === item.id &&
                            room.status === "vacant",
                        ).length
                      }
                    </strong>
                    <small>Còn trống</small>
                  </span>
                </div>
                <button
                  className="property-link"
                  type="button"
                  onClick={() => {
                    setSelectedProperty(item.id);
                    navigate("landlord/rooms");
                  }}
                >
                  Xem danh sách phòng <Icon name="arrow" size={16} />
                </button>
              </div>
            </article>
          ))}
        </div>
      </>
    );
  if (page === "rooms")
    return (
      <>
        <Heading
          eyebrow="QUẢN LÝ PHÒNG"
          title="Danh sách phòng"
          description="Tìm phòng nhanh theo khu trọ, tình trạng hoặc người thuê."
          action={
            <Button icon="plus" onClick={() => setShowRoomForm(!showRoomForm)}>
              Thêm phòng
            </Button>
          }
        />
        {showRoomForm && (
          <section className="content-card inline-form">
            <div className="detail-header">
              <h2>Thêm phòng mẫu</h2>
              <button
                className="icon-button"
                type="button"
                aria-label="Đóng biểu mẫu"
                onClick={() => setShowRoomForm(false)}
              >
                <Icon name="close" />
              </button>
            </div>
            <form onSubmit={addRoom} className="form-grid">
              <Field label="Mã phòng" required>
                <input
                  required
                  value={roomDraft.number}
                  onChange={(e) =>
                    setRoomDraft({ ...roomDraft, number: e.target.value })
                  }
                  placeholder="Ví dụ: A301"
                />
              </Field>
              <Field label="Khu trọ">
                <select
                  value={roomDraft.propertyId}
                  onChange={(e) =>
                    setRoomDraft({ ...roomDraft, propertyId: e.target.value })
                  }
                >
                  {properties.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Tầng">
                <input
                  type="number"
                  min="0"
                  value={roomDraft.floor}
                  onChange={(e) =>
                    setRoomDraft({ ...roomDraft, floor: e.target.value })
                  }
                />
              </Field>
              <Field label="Diện tích (m²)" required>
                <input
                  required
                  type="number"
                  min="1"
                  value={roomDraft.area}
                  onChange={(e) =>
                    setRoomDraft({ ...roomDraft, area: e.target.value })
                  }
                />
              </Field>
              <Field label="Giá thuê/tháng (VNĐ)" required>
                <input
                  required
                  type="number"
                  min="0"
                  value={roomDraft.price}
                  onChange={(e) =>
                    setRoomDraft({ ...roomDraft, price: e.target.value })
                  }
                />
              </Field>
              <div className="form-action">
                <button className="button button--primary" type="submit">
                  Lưu phòng mẫu
                </button>
              </div>
            </form>
          </section>
        )}
        <section className="content-card">
          <div className="card-toolbar card-toolbar--wrap">
            {propertySelect}
            <div
              className="segmented"
              role="group"
              aria-label="Lọc trạng thái phòng"
            >
              <button
                className={roomFilter === "all" ? "selected" : ""}
                type="button"
                onClick={() => setRoomFilter("all")}
              >
                Tất cả
              </button>
              <button
                className={roomFilter === "occupied" ? "selected" : ""}
                type="button"
                onClick={() => setRoomFilter("occupied")}
              >
                Đã thuê
              </button>
              <button
                className={roomFilter === "vacant" ? "selected" : ""}
                type="button"
                onClick={() => setRoomFilter("vacant")}
              >
                Còn trống
              </button>
            </div>
            <div className="search-box">
              <Icon name="search" size={18} />
              <input
                aria-label="Tìm phòng hoặc người thuê"
                placeholder="Tìm phòng hoặc người thuê..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
          {filteredRooms.length ? (
            <div className="responsive-table">
              <table>
                <thead>
                  <tr>
                    <th>Phòng</th>
                    <th>Khu trọ</th>
                    <th>Người thuê</th>
                    <th>Giá thuê</th>
                    <th>Trạng thái</th>
                    <th>
                      <span className="sr-only">Thao tác</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRooms.map((room) => (
                    <tr key={room.id}>
                      <td>
                        <strong className="room-number">{room.number}</strong>
                        <small className="table-sub">
                          Tầng {room.floor} · {room.area} m²
                        </small>
                      </td>
                      <td>{propertyName(room.propertyId)}</td>
                      <td>
                        {room.tenant || (
                          <span className="muted">Chưa có người thuê</span>
                        )}
                      </td>
                      <td className="nowrap">{formatCurrency(room.price)}</td>
                      <td>
                        <Badge kind={room.status}>
                          {roomStatus[room.status]}
                        </Badge>
                      </td>
                      <td className="align-right">
                        <button
                          className="table-link"
                          type="button"
                          onClick={() => navigate("landlord/room/" + room.id)}
                        >
                          Chi tiết <Icon name="chevron" size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty
              title="Không có phòng phù hợp"
              description="Thử đổi khu trọ, trạng thái hoặc từ khóa tìm kiếm."
            />
          )}
        </section>
      </>
    );
  if (page === "tenants")
    if (roomId) return <TenantDetailPage tenantId={roomId} navigate={navigate} />;
  if (page === "tenants")
    return (
      <TenantManager
        navigate={navigate}
        notify={notify}
      />
    );
  if (page === "contracts")
    return <LandlordContracts notify={notify} initialPropertyId={roomId} initialContractId={contractId} />;
  if (page === "billing")
    return <InvoicePaymentPage invoices={scopedInvoices} historyInvoices={invoiceHistory} invoiceId={roomId} navigate={navigate} onAddAdjustment={addInvoiceAdjustment} />;
  if (page === "requests")
    return <LandlordRequestsPage notify={notify} />;
  return (
    <>
      <Heading
        eyebrow={todayLabel}
        title="Chào buổi sáng, anh An 👋"
        description="Cùng xem tình hình khu trọ của bạn hôm nay."
        action={propertySelect}
      />
      <div className="stats-grid">
        <Stat
          icon="door"
          label="Tổng số phòng"
          value={totalRooms.toString()}
          note={`Thuộc ${selectedProperty === "all" ? properties.length : 1} khu trọ`}
        />
        <Stat
          icon="check"
          label="Đang cho thuê"
          value={occupied.toString()}
          note={`${Math.round((occupied / Math.max(totalRooms, 1)) * 100)}% công suất`}
          tint="green"
        />
        <Stat
          icon="building"
          label="Phòng còn trống"
          value={vacant.toString()}
          note="Sẵn sàng cho thuê"
          tint="purple"
        />
      </div>
      <div className="dashboard-grid landlord-dashboard-grid">
        <section className="content-card">
          <div className="card-title-row">
            <div>
              <h2>Tình trạng phòng</h2>
              <p>Tổng hợp nhanh từ các khu trọ.</p>
            </div>
            <button
              className="text-link"
              type="button"
              onClick={() => navigate("landlord/rooms")}
            >
              Xem phòng <Icon name="arrow" size={16} />
            </button>
          </div>
          <div className="occupancy-row">
            <span>Đã cho thuê</span>
            <strong>
              {occupied}/{totalRooms} phòng
            </strong>
          </div>
          <div
            className="progress-track"
            role="progressbar"
            aria-label="Công suất phòng"
            aria-valuenow={occupied}
            aria-valuemin="0"
            aria-valuemax={totalRooms}
          >
            <span
              style={{
                width: `${(occupied / Math.max(totalRooms, 1)) * 100}%`,
              }}
            />
          </div>
          <div className="occupancy-legend">
            <span>
              <i className="legend-dot legend-dot--blue" /> Đã thuê{" "}
              <strong>{occupied}</strong>
            </span>
            <span>
              <i className="legend-dot legend-dot--pale" /> Còn trống{" "}
              <strong>{vacant}</strong>
            </span>
            <span>
              <i className="legend-dot legend-dot--purple" /> Bảo trì{" "}
              <strong>{maintenance}</strong>
            </span>
          </div>
          <div className="mini-room-list">
            {scopedRooms
              .filter((room) => room.status === "vacant")
              .slice(0, 3)
              .map((room) => (
                <div className="mini-room" key={room.id}>
                  <span className="mini-room-icon">
                    <Icon name="door" size={18} />
                  </span>
                  <span>
                    <strong>Phòng {room.number}</strong>
                    <small>{propertyName(room.propertyId)}</small>
                  </span>
                  <Badge kind="vacant">Còn trống</Badge>
                </div>
              ))}
          </div>
        </section>
        <section className="content-card task-card">
          <div className="card-title-row">
            <div>
              <h2>Việc cần chú ý</h2>
              <p>Những mục cần theo dõi trong tuần.</p>
            </div>
          </div>
          <button
            className="task-row"
            type="button"
            onClick={() => navigate("landlord/billing")}
          >
            <span className="task-icon task-icon--amber">
              <Icon name="receipt" size={19} />
            </span>
            <span>
              <strong>Hóa đơn chưa thanh toán</strong>
              <small>Xem hóa đơn và công nợ mới nhất</small>
            </span>
            <Icon name="chevron" size={17} />
          </button>
          <button
            className="task-row"
            type="button"
            onClick={() => navigate("landlord/requests")}
          >
            <span className="task-icon task-icon--blue">
              <Icon name="chat" size={19} />
            </span>
            <span>
              <strong>Yêu cầu người thuê</strong>
              <small>
                {scopedRequests.filter((item) => item.status !== "done").length}{" "}
                yêu cầu cần phản hồi
              </small>
            </span>
            <Icon name="chevron" size={17} />
          </button>
          <button
            className="task-row"
            type="button"
            onClick={() => navigate("landlord/contracts")}
          >
            <span className="task-icon task-icon--purple">
              <Icon name="file" size={19} />
            </span>
            <span>
              <strong>Hợp đồng sắp hết hạn</strong>
              <small>
                {
                  scopedContracts.filter((item) => item.status === "expiring")
                    .length
                }{" "}
                hợp đồng cần gia hạn
              </small>
            </span>
            <Icon name="chevron" size={17} />
          </button>
        </section>
      </div>
      <section className="content-card recent-card">
        <div className="card-title-row">
          <div>
            <h2>Hóa đơn gần đây</h2>
            <p>Dữ liệu hóa đơn được cập nhật trong mục Hóa đơn.</p>
          </div>
          <button
            className="text-link"
            type="button"
            onClick={() => navigate("landlord/billing")}
          >
            Xem tất cả <Icon name="arrow" size={16} />
          </button>
        </div>
        <p>Vào mục Hóa đơn để xem danh sách và số dư từ máy chủ.</p>
      </section>
    </>
  );
}
