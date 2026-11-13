import { useRef, useState } from "react";
import { AlertTriangle, ArrowLeft, DoorOpen, Plus, Search } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { useApiQuery } from "../hooks/useApiQuery";
import {
  deleteRoom,
  getProperty,
  getRoom,
  listRoomRows,
  listRooms,
} from "../services/api";
import RoomForm from "../components/rooms/RoomForm";
import RoomTable from "../components/rooms/RoomTable";
import RoomRowManager from "../components/rooms/RoomRowManager";
import {
  ConfirmationToast,
  Feedback,
  IconButton,
  Pagination,
  messageOf,
} from "../components/property/ManagementUi";
import "../styles/property-management.css";
import "../components/contracts/contractPages.css";

export default function RoomManagementPage({ propertyId, navigate, notify }) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [rowId, setRowId] = useState("");
  const [floor, setFloor] = useState("");
  const [editingRoom, setEditingRoom] = useState(null);
  const [pendingDelete, setPendingDelete] = useState(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState("");
  const headingRef = useRef(null);

  const propertyQuery = useApiQuery(
    `properties:${user?.id}:detail:${propertyId}`,
    () => getProperty(propertyId),
    { enabled: Boolean(user && propertyId) },
  );
  const roomsQuery = useApiQuery(
    `rooms:${propertyId}:${user?.id}:${page}:${search}:${status}:${rowId}:${floor}`,
    () =>
      listRooms(propertyId, {
        page,
        pageSize: 10,
        search: search || undefined,
        status: status === "all" ? undefined : status,
        rowId: rowId || undefined,
        floor: floor || undefined,
      }),
    { enabled: Boolean(user && propertyId) },
  );
  const rowsQuery = useApiQuery(
    `room-rows:${propertyId}:${user?.id}`,
    () => listRoomRows(propertyId),
    { enabled: Boolean(user && propertyId) },
  );
  const property = propertyQuery.data?.data;
  const rooms = roomsQuery.data?.data || [];
  const meta = roomsQuery.data?.meta;
  const roomRows = rowsQuery.data || [];
  const filtering = Boolean(search || status !== "all" || rowId || floor);

  function applySearch(event) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  async function openRoomEdit(roomId) {
    setBusy(true);
    setActionError("");
    try {
      const response = await getRoom(propertyId, roomId);
      setEditingRoom(response.data);
    } catch (cause) {
      setActionError(messageOf(cause));
    } finally {
      setBusy(false);
    }
  }

  function openRoomDetail(roomId) {
    navigate(`landlord/properties/${propertyId}/rooms/${roomId}`);
  }

  function askDeleteRoom(event, room) {
    setActionError("");
    setPendingDelete({
      kind: "room",
      room,
      trigger: event.currentTarget,
      error: "",
    });
  }

  function cancelDelete() {
    if (busy) return;
    const trigger = pendingDelete?.trigger;
    setPendingDelete(null);
    trigger?.focus();
  }

  async function confirmDelete() {
    if (!pendingDelete || busy) return;
    const request = pendingDelete;
    setBusy(true);
    setPendingDelete({ ...request, error: "" });
    try {
      await deleteRoom(propertyId, request.room.id);
      setPendingDelete(null);
      notify(`Đã xóa phòng ${request.room.roomNumber}.`);
      if (rooms.length === 1 && page > 1) setPage(page - 1);
      else roomsQuery.reload();
      requestAnimationFrame(() => headingRef.current?.focus());
    } catch (cause) {
      setPendingDelete({ ...request, error: messageOf(cause) });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="pm-page pm-page--rooms">
      <div className="pm-detail-topline">
        <IconButton
          label="Quay về chi tiết khu trọ"
          icon={ArrowLeft}
          onClick={() => navigate(`landlord/properties/${propertyId}`)}
        />
        <span>Khu trọ / Quản lý phòng</span>
      </div>
      {propertyQuery.isLoading ? (
        <div className="pm-loading" role="status">
          Đang tải khu trọ...
        </div>
      ) : propertyQuery.error ? (
        <Feedback error={propertyQuery.error} onRetry={propertyQuery.reload} />
      ) : (
        property && (
          <>
            <header className="pm-hero pm-room-page-hero">
              <div>
                <span className="pm-eyebrow">{property.name}</span>
                <h1>Quản lý phòng</h1>
                <p>{property.address}</p>
              </div>
              <IconButton
                label="Tạo nhiều phòng"
                icon={Plus}
                tone="primary"
                onClick={() =>
                  navigate(`landlord/properties/${propertyId}/rooms/new`)
                }
                disabled={!property.isActive}
              />
            </header>
            {!property.isActive && (
              <p className="pm-inactive-note">
                <AlertTriangle size={17} aria-hidden="true" /> Khu trọ đang tạm
                ngừng. Hãy kích hoạt khu trọ trước khi tạo phòng.
              </p>
            )}
            {actionError && (
              <p className="form-error pm-action-error" role="alert">
                {actionError}
              </p>
            )}
            {editingRoom && (
              <RoomForm
                key={editingRoom.id}
                propertyId={propertyId}
                initial={editingRoom}
                onCancel={() => setEditingRoom(null)}
                onSaved={() => {
                  setEditingRoom(null);
                  roomsQuery.reload();
                  notify("Đã cập nhật phòng.");
                }}
              />
            )}
            <section
              className="pm-room-panel content-card"
              aria-labelledby="pm-rooms-title"
            >
              <div className="pm-section-heading pm-room-heading">
                <div>
                  <span className="pm-eyebrow">PHÒNG THUỘC KHU TRỌ</span>
                  <h2 id="pm-rooms-title" ref={headingRef} tabIndex={-1}>
                    Danh sách phòng
                  </h2>
                  <p>
                    {meta
                      ? `${meta.totalItems} phòng${filtering ? " phù hợp" : ""}`
                      : "Danh sách phòng của khu trọ này"}
                  </p>
                </div>
              </div>
              <div className="pm-toolbar">
                <form
                  className="pm-search"
                  onSubmit={applySearch}
                  role="search"
                >
                  <Search size={18} aria-hidden="true" />
                  <input
                    aria-label="Tìm phòng"
                    placeholder="Tìm theo mã hoặc tên phòng"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                  />
                  <IconButton
                    type="submit"
                    label="Tìm phòng"
                    icon={Search}
                    tone="soft"
                  />
                </form>
                <label className="pm-filter">
                  <span>Trạng thái</span>
                  <select
                    value={status}
                    onChange={(event) => {
                      setStatus(event.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="all">Tất cả</option>
                    <option value="Available">Còn trống</option>
                    <option value="Occupied">Đã thuê</option>
                    <option value="Maintenance">Bảo trì</option>
                    <option value="Inactive">Ngừng sử dụng</option>
                  </select>
                </label>
                <label className="pm-filter">
                  <span>Dãy</span>
                  <select
                    value={rowId}
                    onChange={(event) => {
                      setRowId(event.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="">Tất cả dãy</option>
                    {roomRows.map((row) => (
                      <option key={row.id} value={row.id}>
                        {row.code} · {row.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="pm-filter">
                  <span>Tầng</span>
                  <input
                    type="number"
                    min="1"
                    step="1"
                    placeholder="Tất cả"
                    value={floor}
                    onChange={(event) => {
                      setFloor(event.target.value);
                      setPage(1);
                    }}
                  />
                </label>
              </div>
              {roomsQuery.isLoading ? (
                <div className="pm-loading" role="status">
                  Đang tải danh sách phòng...
                </div>
              ) : roomsQuery.error ? (
                <Feedback
                  error={roomsQuery.error}
                  onRetry={roomsQuery.reload}
                />
              ) : rooms.length === 0 ? (
                <div className="pm-empty">
                  <span className="pm-empty-icon">
                    <DoorOpen size={28} aria-hidden="true" />
                  </span>
                  <strong>
                    {filtering
                      ? "Không tìm thấy phòng phù hợp"
                      : "Khu trọ chưa có phòng"}
                  </strong>
                  <p>
                    {filtering
                      ? "Thử thay đổi từ khóa hoặc bộ lọc."
                      : property.isActive
                        ? "Tạo phòng đầu tiên để bắt đầu quản lý."
                        : "Kích hoạt khu trọ để có thể tạo phòng."}
                  </p>
                  {!filtering && property.isActive && (
                    <IconButton
                      label="Tạo phòng đầu tiên"
                      icon={Plus}
                      tone="primary"
                      onClick={() =>
                        navigate(`landlord/properties/${propertyId}/rooms/new`)
                      }
                    />
                  )}
                </div>
              ) : (
                <RoomTable
                  rooms={rooms}
                  busy={busy}
                  onView={openRoomDetail}
                  onEdit={openRoomEdit}
                  onDelete={askDeleteRoom}
                  onContracts={(room) =>
                    navigate(
                      `landlord/contracts/${propertyId}/${room.contractId}`,
                    )
                  }
                  onCreateContract={(room) =>
                    navigate(`landlord/contracts/new/${propertyId}/${room.id}`)
                  }
                />
              )}
              <Pagination page={page} meta={meta} onChange={setPage} />
            </section>
            <RoomRowManager
              propertyId={propertyId}
              rows={roomRows}
              queryError={rowsQuery.error}
              onRetry={rowsQuery.reload}
              onChanged={rowsQuery.reload}
              notify={notify}
              disabled={!property.isActive}
            />
            <ConfirmationToast
              request={pendingDelete}
              busy={busy}
              onCancel={cancelDelete}
              onConfirm={confirmDelete}
            />
          </>
        )
      )}
    </div>
  );
}
