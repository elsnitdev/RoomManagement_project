import { useEffect, useMemo, useState } from "react";
import {
  ArrowLeft,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  FileText,
  Plus,
  Search,
} from "lucide-react";
import { billingApi } from "../../services/api/billingApi";
import { contractApi } from "../../services/api/contractApi";
import { LandlordRules } from "./ContractRules";
import EarlyExitFlow, { SettlementRefundEvidencePanel } from "./EarlyExitFlow";
import { getLandlordTenants } from "../../services/api/landlordTenantApi";
import { listRoomRows } from "../../services/api/RoomRowApi";
import { endOfMonth, unavailableTenantIds } from "./contractForm";
import "./contracts.css";
import "./contractPages.css";

const today = () =>
  new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
const money = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
const displayDate = (value) =>
  value ? value.split("-").reverse().join("/") : "—";
const statusText = {
  Draft: "Bản nháp",
  Active: "Đang hiệu lực",
  PendingMoveOut: "Chờ bàn giao",
  PendingSettlement: "Chờ tất toán",
  Expired: "Hết hạn",
  Terminated: "Đã kết thúc",
  Cancelled: "Đã hủy",
};
const isUpcoming = (contract) =>
  contract?.status === "Active" && contract.startDate > today();
const needsSettlement = (contract) =>
  ["PendingSettlement", "Expired", "Terminated"].includes(contract?.status) &&
  (Number(contract.outstandingAmount) > 0 ||
    Number(contract.depositHeld) > 0 ||
    contract.hasDraftInvoices ||
    !contract.hasSettlement);
const errorText = (error) =>
  error?.message || "Không thể thực hiện thao tác. Vui lòng thử lại.";
const newForm = () => ({
  roomId: "",
  startDate: today(),
  endMonth: "",
  monthlyRent: "",
  depositAmount: "0",
  depositPaymentStatus: "Unpaid",
  depositPaymentMethod: "Cash",
  paymentDueDay: "",
  tenantId: "",
  tenancyMode: "CoTenancy",
  additionalTenantIds: [],
  roomServices: [],
  servicesConfirmed: false,
  note: "",
});
const routeTo = (navigate, path) => navigate(`landlord/contracts${path}`);

async function allTenants(signal) {
  const result = [];
  for (let page = 1; ; page += 1) {
    const response = await getLandlordTenants(
      { page, pageSize: 100 },
      { signal },
    );
    result.push(...(response.data || []));
    if (
      result.length >= (response.meta?.total ?? result.length) ||
      !response.data?.length
    )
      return result;
  }
}

function DatePicker({ value, onChange, min }) {
  const [open, setOpen] = useState(false);
  const [manual, setManual] = useState(displayDate(value));
  const [month, setMonth] = useState(value?.slice(0, 7) || today().slice(0, 7));
  useEffect(() => {
    setManual(displayDate(value));
    if (value) setMonth(value.slice(0, 7));
  }, [value]);
  const [year, mm] = month.split("-").map(Number);
  const firstWeekday = (new Date(year, mm - 1, 1).getDay() + 6) % 7;
  const days = new Date(year, mm, 0).getDate();
  const monthLabel = new Intl.DateTimeFormat("vi-VN", {
    month: "long",
    year: "numeric",
  }).format(new Date(year, mm - 1, 1));
  function shift(amount) {
    const next = new Date(year, mm - 1 + amount, 1);
    setMonth(
      `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, "0")}`,
    );
  }
  function commitManual() {
    const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(manual.trim());
    if (!match) {
      setManual(displayDate(value));
      return;
    }
    const iso = `${match[3]}-${match[2].padStart(2, "0")}-${match[1].padStart(2, "0")}`;
    const check = new Date(`${iso}T12:00:00`);
    if (
      Number.isNaN(check.getTime()) ||
      check.getDate() !== Number(match[1]) ||
      check.getMonth() + 1 !== Number(match[2]) ||
      (min && iso < min)
    ) {
      setManual(displayDate(value));
      return;
    }
    onChange(iso);
  }
  return (
    <div className="ct-date">
      <div className="ct-date-input">
        <input
          aria-label="Ngày, định dạng ngày/tháng/năm"
          value={manual}
          onChange={(event) => setManual(event.target.value)}
          onBlur={commitManual}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              commitManual();
              setOpen(false);
            }
          }}
          placeholder="dd/mm/yyyy"
        />
        <button
          type="button"
          aria-label="Mở lịch"
          aria-expanded={open}
          onClick={() => setOpen(!open)}
        >
          <CalendarDays size={18} />
        </button>
      </div>
      {open && (
        <div
          className="ct-calendar"
          role="group"
          aria-label="Chọn ngày bắt đầu"
        >
          <div className="ct-calendar-head">
            <button
              type="button"
              aria-label="Tháng trước"
              onClick={() => shift(-1)}
            >
              <ChevronLeft size={17} />
            </button>
            <strong>{monthLabel}</strong>
            <button
              type="button"
              aria-label="Tháng sau"
              onClick={() => shift(1)}
            >
              <ChevronRight size={17} />
            </button>
          </div>
          <div className="ct-calendar-grid">
            {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map((label) => (
              <span key={label}>{label}</span>
            ))}
            {Array.from({ length: firstWeekday }, (_, index) => (
              <span key={`empty-${index}`} />
            ))}
            {Array.from({ length: days }, (_, index) => {
              const iso = `${month}-${String(index + 1).padStart(2, "0")}`;
              return (
                <button
                  key={iso}
                  type="button"
                  aria-label={displayDate(iso)}
                  aria-pressed={value === iso}
                  disabled={Boolean(min && iso < min)}
                  onClick={() => {
                    onChange(iso);
                    setOpen(false);
                  }}
                >
                  {index + 1}
                </button>
              );
            })}
          </div>
          <button
            type="button"
            className="ct-calendar-today"
            disabled={Boolean(min && today() < min)}
            onClick={() => {
              onChange(today());
              setOpen(false);
            }}
          >
            Hôm nay
          </button>
        </div>
      )}
    </div>
  );
}

function MonthPicker({ value, min, onChange }) {
  const currentYear = Number(today().slice(0, 4));
  const minYear = Number(min?.slice(0, 4) || currentYear);
  const [year, setYear] = useState(() => Number(value?.slice(0, 4) || minYear));
  const month = value?.slice(5, 7) || "";
  useEffect(() => {
    if (value) setYear(Number(value.slice(0, 4)));
    else setYear((previous) => Math.max(previous, minYear));
  }, [value, minYear]);
  const startYear = Math.min(minYear, year);
  const endYear = Math.min(9999, Math.max(minYear + 15, year));
  const years = Array.from(
    { length: endYear - startYear + 1 },
    (_, index) => startYear + index,
  );
  return (
    <div className="ct-month">
      <select
        aria-label="Tháng kết thúc"
        value={month}
        onChange={(event) =>
          onChange(event.target.value ? `${year}-${event.target.value}` : "")
        }
      >
        <option value="">Chọn tháng</option>
        {Array.from({ length: 12 }, (_, index) =>
          String(index + 1).padStart(2, "0"),
        ).map((item) => (
          <option
            key={item}
            value={item}
            disabled={Boolean(min && `${year}-${item}` < min)}
          >
            Tháng {Number(item)}
          </option>
        ))}
      </select>
      <select
        aria-label="Năm kết thúc"
        value={year}
        onChange={(event) => {
          const next = Number(event.target.value);
          setYear(next);
          onChange(
            month && (!min || `${next}-${month}` >= min)
              ? `${next}-${month}`
              : "",
          );
        }}
      >
        {years.map((item) => (
          <option key={item} value={item} disabled={item < minYear}>
            {item}
          </option>
        ))}
      </select>
    </div>
  );
}

function Status({ contract }) {
  const upcoming = isUpcoming(contract);
  return (
    <span
      className={`ct-status ct-status--${upcoming ? "upcoming" : contract?.status?.toLowerCase()}`}
    >
      {upcoming
        ? "Sắp bắt đầu"
        : statusText[contract?.status] || contract?.status}
    </span>
  );
}
function Back({ navigate }) {
  return (
    <button
      className="ct-back"
      type="button"
      onClick={() => routeTo(navigate, "")}
    >
      <ArrowLeft size={17} /> Danh sách hợp đồng
    </button>
  );
}
function RoomLabel({ room, rows, property }) {
  const row = rows.find((item) => item.id === room?.rowId);
  return (
    <>
      {room?.roomNumber || "—"}
      {room?.roomName && ` · ${room.roomName}`} ·{" "}
      {row ? `Dãy ${row.code || row.name}` : "Chưa có dãy"} ·{" "}
      {room?.floor == null
        ? "Chưa có tầng"
        : room.floor === 0
          ? "Tầng trệt"
          : `Tầng ${room.floor}`}
      {property && ` · ${property.name}`}
    </>
  );
}

export default function LandlordContracts({
  notify,
  navigate,
  routeParts = [],
}) {
  const [first, second, third] = routeParts;
  const mode =
    first === "new"
      ? "new"
      : second
        ? third === "edit"
          ? "edit"
          : "detail"
        : "list";
  const routePropertyId =
    mode === "new" ? second : mode === "list" ? "" : first;
  const routeRoomId = mode === "new" ? third : "";
  const contractId = mode === "detail" || mode === "edit" ? second : "";
  const [properties, setProperties] = useState([]);
  const [propertyId, setPropertyId] = useState(routePropertyId || "");
  const [rooms, setRooms] = useState([]);
  const [rows, setRows] = useState([]);
  const [contracts, setContracts] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [serviceCatalog, setServiceCatalog] = useState([]);
  const [roomServiceLoading, setRoomServiceLoading] = useState(false);
  const [serviceLoadError, setServiceLoadError] = useState("");
  const [activeContracts, setActiveContracts] = useState([]);
  const [tenantAvailabilityLoading, setTenantAvailabilityLoading] = useState(
    mode !== "list",
  );
  const [tenantAvailabilityError, setTenantAvailabilityError] = useState(false);
  const [showAddTenant, setShowAddTenant] = useState(false);
  const [detail, setDetail] = useState(null);
  const [depositInvoice, setDepositInvoice] = useState(null);
  const [depositInvoiceLoading, setDepositInvoiceLoading] = useState(false);
  const [depositInvoiceError, setDepositInvoiceError] = useState(false);
  const [depositRevision, setDepositRevision] = useState(0);
  const [form, setForm] = useState(newForm);
  const [roomSearch, setRoomSearch] = useState("");
  const [roomRow, setRoomRow] = useState("");
  const [roomFloor, setRoomFloor] = useState("");
  const [tenantSearch, setTenantSearch] = useState("");
  const [extraTenantId, setExtraTenantId] = useState("");
  const [extraTenantRole, setExtraTenantRole] = useState("ContractParty");
  const [extraJoinDate, setExtraJoinDate] = useState(today);
  const [editForm, setEditForm] = useState(null);
  const [contractSearch, setContractSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    setPropertyId(routePropertyId || "");
  }, [routePropertyId]);
  useEffect(() => {
    const controller = new AbortController();
    billingApi
      .properties(controller.signal)
      .then((response) => {
        const items = response.data || [];
        setProperties(items);
        setPropertyId((current) => current || items[0]?.id || "");
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorText(cause));
      });
    return () => controller.abort();
  }, []);
  useEffect(() => {
    if (!propertyId) {
      setLoading(false);
      return;
    }
    const controller = new AbortController();
    setLoading(true);
    setError("");
    Promise.all([
      billingApi.rooms(propertyId, controller.signal),
      listRoomRows(propertyId),
      contractApi.list(propertyId, controller.signal),
      mode !== "list" ? allTenants(controller.signal) : Promise.resolve([]),
    ])
      .then(([roomResponse, roomRows, contractResponse, tenantItems]) => {
        if (controller.signal.aborted) return;
        setRooms(roomResponse.data || []);
        setRows(roomRows || []);
        setContracts(contractResponse.data || []);
        setTenants(tenantItems);
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorText(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, mode]);
  useEffect(() => {
    if (!propertyId || mode !== "new") return;
    const controller = new AbortController();
    setServiceLoadError("");
    billingApi.propertyServices(propertyId, controller.signal)
      .then((items) => { if (!controller.signal.aborted) setServiceCatalog(items || []); })
      .catch((cause) => { if (!controller.signal.aborted) setServiceLoadError(errorText(cause)); });
    return () => controller.abort();
  }, [propertyId, mode]);
  useEffect(() => {
    if (!propertyId || !form.roomId || mode !== "new") return;
    const controller = new AbortController();
    setServiceLoadError("");
    setRoomServiceLoading(true);
    billingApi.roomServices(propertyId, form.roomId, controller.signal)
      .then((items) => {
        if (controller.signal.aborted) return;
        const selected = (items || []).filter((item) => item.startDate <= form.startDate && (!item.endDate || item.endDate >= form.startDate));
        setForm((current) => ({ ...current, roomServices: selected.map((item) => ({ serviceId: item.serviceId, quantity: String(item.quantity), customPrice: item.customPrice == null ? "" : String(item.customPrice) })), servicesConfirmed: false }));
      })
      .catch((cause) => { if (!controller.signal.aborted) setServiceLoadError(errorText(cause)); })
      .finally(() => { if (!controller.signal.aborted) setRoomServiceLoading(false); });
    return () => controller.abort();
  }, [propertyId, form.roomId, form.startDate, mode]);
  useEffect(() => {
    if (mode === "list" || !properties.length) return;
    const controller = new AbortController();
    setTenantAvailabilityLoading(true);
    setTenantAvailabilityError(false);
    Promise.all(
      properties.map((item) => contractApi.list(item.id, controller.signal)),
    )
      .then((results) => {
        if (!controller.signal.aborted)
          setActiveContracts(results.flatMap((result) => result.data || []));
      })
      .catch((cause) => {
        if (!controller.signal.aborted) {
          setTenantAvailabilityError(true);
          setError(errorText(cause));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setTenantAvailabilityLoading(false);
      });
    return () => controller.abort();
  }, [mode, properties]);
  useEffect(() => {
    if (!propertyId || !contractId) {
      setDetail(null);
      return;
    }
    const controller = new AbortController();
    contractApi
      .detail(propertyId, contractId, controller.signal)
      .then((item) => {
        if (!controller.signal.aborted) {
          setDetail(item);
          setEditForm({
            startDate: item.startDate,
            endMonth: item.endDate?.slice(0, 7) || "",
            monthlyRent: String(item.monthlyRent),
            depositAmount: String(item.depositAmount),
            paymentDueDay: String(item.paymentDueDay || ""),
            note: item.note || "",
          });
          setExtraJoinDate(item.startDate > today() ? item.startDate : today());
        }
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorText(cause));
      });
    return () => controller.abort();
  }, [propertyId, contractId]);
  useEffect(() => {
    if (!contractId || !detail || Number(detail.depositAmount) <= 0) {
      setDepositInvoice(null);
      return;
    }
    const controller = new AbortController();
    setDepositInvoiceLoading(true);
    setDepositInvoiceError(false);
    setDepositInvoice(null);
    billingApi
      .depositInvoice(contractId, controller.signal)
      .then((invoice) => {
        if (!controller.signal.aborted) setDepositInvoice(invoice);
      })
      .catch((cause) => {
        if (!controller.signal.aborted) {
          setDepositInvoiceError(true);
          setError(errorText(cause));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setDepositInvoiceLoading(false);
      });
    return () => controller.abort();
  }, [contractId, detail?.depositAmount, depositRevision]);
  useEffect(() => {
    setForm((current) => ({
      ...current,
      roomId: routeRoomId || current.roomId,
    }));
  }, [routeRoomId]);
  useEffect(() => {
    if (!routeRoomId || !rooms.length) return;
    const routeRoom = rooms.find((item) => item.id === routeRoomId);
    if (routeRoom)
      setForm((current) => ({
        ...current,
        roomId: routeRoomId,
        monthlyRent: current.monthlyRent || String(routeRoom.basePrice || 0),
      }));
  }, [routeRoomId, rooms]);

  const property = properties.find((item) => item.id === propertyId);
  const chosenRoom = rooms.find((item) => item.id === form.roomId);
  const detailRoom = rooms.find((item) => item.id === detail?.roomId);
  const matchingRooms = useMemo(
    () =>
      rooms.filter(
        (room) =>
          room.status === "Available" &&
          (!roomRow || room.rowId === roomRow) &&
          (!roomFloor || String(room.floor) === roomFloor) &&
          `${room.roomNumber} ${room.roomName || ""}`
            .toLocaleLowerCase("vi-VN")
            .includes(roomSearch.trim().toLocaleLowerCase("vi-VN")),
      ),
    [rooms, roomRow, roomFloor, roomSearch],
  );
  const unavailableIds = useMemo(
    () =>
      unavailableTenantIds(
        activeContracts,
        mode === "new" ? form.startDate : extraJoinDate,
        mode === "new"
          ? form.endMonth
            ? endOfMonth(form.endMonth)
            : null
          : detail?.endDate,
        mode === "new" ? null : detail,
      ),
    [
      activeContracts,
      mode,
      form.startDate,
      form.endMonth,
      extraJoinDate,
      detail,
    ],
  );
  const matchingTenants = useMemo(
    () =>
      (mode === "new" || tenantSearch.trim()) &&
      !tenantAvailabilityLoading &&
      !tenantAvailabilityError
        ? tenants
            .filter(
              (item) =>
                !unavailableIds.has(item.id) &&
                `${item.fullName} ${item.email || ""} ${item.phoneNumber || ""}`
                  .toLocaleLowerCase("vi-VN")
                  .includes(tenantSearch.trim().toLocaleLowerCase("vi-VN")),
            )
            .slice(0, 8)
        : [],
    [
      tenants,
      tenantSearch,
      unavailableIds,
      tenantAvailabilityLoading,
      tenantAvailabilityError,
      mode,
    ],
  );
  useEffect(() => {
    if (unavailableIds.has(form.tenantId))
      setForm((current) => ({ ...current, tenantId: "" }));
    if (unavailableIds.has(extraTenantId)) setExtraTenantId("");
    setForm((current) => current.additionalTenantIds.some((id) => unavailableIds.has(id))
      ? { ...current, additionalTenantIds: current.additionalTenantIds.filter((id) => !unavailableIds.has(id)) }
      : current);
  }, [unavailableIds, form.tenantId, extraTenantId]);
  const chosenTenant = tenants.find((item) => item.id === form.tenantId);
  const extraTenant = tenants.find((item) => item.id === extraTenantId);
  const familyMode = form.tenancyMode === "PrimaryTenantWithFamily";
  const selectedAdditional = tenants.filter((item) => form.additionalTenantIds.includes(item.id));
  const detailFamilyMode = detail?.tenancyMode === "PrimaryTenantWithFamily";
  const detailHasParty = detail?.tenants?.some((item) => !item.leaveDate && item.role === "ContractParty");
  const alertContracts = contracts.filter(needsSettlement);
  const shownContracts = contracts.filter(
    (item) =>
      (filter === "all" || item.status === filter) &&
      `${item.contractCode} ${item.roomNumber || ""} ${item.tenants?.map((tenant) => tenant.fullName).join(" ") || ""}`
        .toLocaleLowerCase("vi-VN")
        .includes(contractSearch.trim().toLocaleLowerCase("vi-VN")),
  );
  const patchForm = (key, value) =>
    setForm((current) => ({ ...current, [key]: value }));
  const toggleAdditionalTenant = (id) => setForm((current) => ({
    ...current,
    additionalTenantIds: current.additionalTenantIds.includes(id)
      ? current.additionalTenantIds.filter((value) => value !== id)
      : [...current.additionalTenantIds, id],
  }));

  async function create(event) {
    event.preventDefault();
    if (tenantAvailabilityLoading || tenantAvailabilityError) {
      setError(
        "Chưa kiểm tra được hợp đồng hiện có của người thuê. Vui lòng thử lại.",
      );
      return;
    }
    if (!chosenRoom) {
      setError("Hãy chọn một phòng còn trống trước khi lưu.");
      return;
    }
    if (!form.endMonth || form.endMonth < form.startDate.slice(0, 7)) {
      setError("Tháng kết thúc phải từ tháng bắt đầu trở đi.");
      return;
    }
    if (!chosenTenant?.userId) {
      setError("Người đứng tên cần có tài khoản liên kết để tự chấp thuận nội quy.");
      return;
    }
    if (1 + selectedAdditional.length > chosenRoom.maxOccupants) {
      setError("Số người được chọn vượt sức chứa của phòng.");
      return;
    }
    if (!familyMode && selectedAdditional.some((item) => !item.userId)) {
      setError("Mỗi người đồng thuê cần có tài khoản liên kết.");
      return;
    }
    if (roomServiceLoading || serviceLoadError || !form.servicesConfirmed) { setError("Hãy tải, kiểm tra và xác nhận dịch vụ của phòng trước khi lưu hợp đồng."); return; }
    if (form.roomServices.some((item) => !Number.isFinite(Number(item.quantity)) || Number(item.quantity) <= 0 || item.customPrice !== "" && (!Number.isFinite(Number(item.customPrice)) || Number(item.customPrice) < 0))) { setError("Số lượng hoặc đơn giá dịch vụ không hợp lệ."); return; }
    setBusy(true);
    setError("");
    try {
      // The API still requires ContractCode. Generate it without asking the landlord to enter it.
      const code =
        `HD-${chosenRoom.roomNumber.replace(/[^a-zA-Z0-9]/g, "").toUpperCase()}-${form.startDate.slice(0, 4)}-${Date.now().toString(36).toUpperCase()}`.slice(
          0,
          50,
        );
      const depositPaid =
        Number(form.depositAmount) > 0 && form.depositPaymentStatus === "Paid";
      const created = await contractApi.create(propertyId, {
        roomId: form.roomId,
        contractCode: code,
        startDate: form.startDate,
        endDate: endOfMonth(form.endMonth),
        monthlyRent: Number(form.monthlyRent),
        depositAmount: Number(form.depositAmount),
        depositPaymentStatus: depositPaid ? "Paid" : "Unpaid",
        depositPaymentMethod: depositPaid ? form.depositPaymentMethod : null,
        paymentDueDay: form.paymentDueDay ? Number(form.paymentDueDay) : null,
        note: form.note.trim() || null,
        tenancyMode: form.tenancyMode,
        roomServices: form.roomServices.map((item) => ({ serviceId: item.serviceId, quantity: Number(item.quantity), customPrice: item.customPrice === "" ? null : Number(item.customPrice) })),
      });
      try {
        for (const [index, item] of [chosenTenant, ...selectedAdditional].entries()) {
          await contractApi.addTenant(propertyId, created.id, {
            tenantId: item.id,
            joinDate: form.startDate,
            isContactPerson: index === 0,
            role: index === 0 || !familyMode ? "ContractParty" : "AuthorizedOccupant",
          });
        }
      } catch (cause) {
          notify?.(
            `Đã tạo hợp đồng${created.depositInvoiceId ? " và hóa đơn cọc" : ""}, nhưng chưa thêm đủ người: ${errorText(cause)}`,
          );
          routeTo(navigate, `/${propertyId}/${created.id}`);
          return;
      }
      notify?.(
        created.depositInvoiceId
          ? "Đã tạo hợp đồng và phát hành hóa đơn cọc."
          : "Đã tạo hợp đồng nháp.",
      );
      if (created.depositInvoiceId)
        navigate(`landlord/billing/${created.depositInvoiceId}`);
      else routeTo(navigate, `/${propertyId}/${created.id}`);
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setBusy(false);
    }
  }

  async function run(action, message) {
    setBusy(true);
    setError("");
    try {
      await action();
      setDetail(await contractApi.detail(propertyId, contractId));
      setDepositRevision((value) => value + 1);
      notify?.(message);
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setBusy(false);
    }
  }

  async function createLegacyDeposit() {
    setBusy(true);
    setError("");
    try {
      const invoice = await billingApi.createDeposit(contractId);
      notify?.("Đã tạo hóa đơn cọc nháp. Hãy kiểm tra rồi phát hành.");
      navigate(`landlord/billing/${invoice.id}`);
    } catch (cause) {
      setError(errorText(cause));
      setDepositRevision((value) => value + 1);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="contract-page ct-pages">
      {mode !== "list" && <Back navigate={navigate} />}
      {error && (
        <div className="contract-message contract-message--error" role="alert">
          {error}
        </div>
      )}
      {loading && (
        <p className="contract-loading" role="status">
          Đang tải dữ liệu…
        </p>
      )}
      {!loading && mode === "list" && (
        <>
          <header className="ct-page-head">
            <div>
              <span className="contract-eyebrow">QUẢN LÝ CHO THUÊ</span>
              <h1>Hợp đồng thuê</h1>
              <p>Danh sách hợp đồng theo khu trọ.</p>
            </div>
            <button
              className="contract-button contract-button--primary"
              type="button"
              onClick={() => routeTo(navigate, "/new")}
            >
              <Plus size={17} /> Tạo hợp đồng
            </button>
          </header>
          <div className="ct-list-toolbar">
            <label>
              Khu trọ
              <select
                value={propertyId}
                onChange={(event) => setPropertyId(event.target.value)}
              >
                {properties.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Tìm hợp đồng
              <input
                type="search"
                placeholder="Mã, phòng, người thuê…"
                value={contractSearch}
                onChange={(event) => setContractSearch(event.target.value)}
              />
            </label>
            <label>
              Trạng thái
              <select
                value={filter}
                onChange={(event) => setFilter(event.target.value)}
              >
                <option value="all">Tất cả</option>
                {Object.entries(statusText).map(([value, text]) => (
                  <option key={value} value={value}>
                    {text}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {alertContracts.length > 0 && (
            <div
              className="contract-message contract-message--warning"
              role="status"
            >
              <strong>{alertContracts.length} hợp đồng cần đối chiếu.</strong>{" "}
              Kiểm tra công nợ, tiền cọc, hóa đơn nháp và tất toán.
            </div>
          )}
          <section className="ct-list" aria-label="Danh sách hợp đồng">
            {shownContracts.length ? (
              shownContracts.map((item) => (
                <button
                  className="ct-list-card"
                  key={item.id}
                  type="button"
                  onClick={() => routeTo(navigate, `/${propertyId}/${item.id}`)}
                >
                  <span>
                    <strong>{item.contractCode}</strong>
                    <Status contract={item} />
                  </span>
                  <span>
                    Phòng{" "}
                    {item.roomNumber ||
                      rooms.find((room) => room.id === item.roomId)
                        ?.roomNumber ||
                      "—"}{" "}
                    ·{" "}
                    {item.tenants
                      ?.map((tenant) => tenant.fullName)
                      .join(", ") || "Chưa có người thuê"}
                  </span>
                  <span>
                    {displayDate(item.startDate)} – {displayDate(item.endDate)}{" "}
                    · {money(item.monthlyRent)}/tháng
                  </span>
                  {needsSettlement(item) && (
                    <span className="ct-list-warning">
                      Cần đối chiếu tất toán
                    </span>
                  )}
                </button>
              ))
            ) : (
              <div className="contract-empty">
                <FileText size={28} />
                <strong>
                  {contracts.length
                    ? "Không tìm thấy hợp đồng"
                    : "Chưa có hợp đồng"}
                </strong>
                <p>
                  {contracts.length
                    ? "Thử đổi bộ lọc hoặc từ khóa."
                    : "Tạo hợp đồng đầu tiên cho khu trọ."}
                </p>
              </div>
            )}
          </section>
        </>
      )}
      {!loading && mode === "new" && (
        <>
          <header className="ct-page-head">
            <div>
              <span className="contract-eyebrow">HỢP ĐỒNG MỚI</span>
              <h1>Tạo hợp đồng</h1>
              <p>Chọn phòng trước, sau đó điền điều khoản thuê.</p>
            </div>
          </header>
          <form className="ct-form" onSubmit={create}>
            <section className="ct-card">
              <div className="ct-card-head">
                <span className="ct-step">1</span>
                <div>
                  <h2>
                    Chọn phòng <em>*</em>
                  </h2>
                  <p>Vị trí phòng được hiển thị theo dãy và tầng.</p>
                </div>
              </div>
              <div className="ct-room-filters">
                <label>
                  Khu trọ
                  <select
                    value={propertyId}
                    onChange={(event) => {
                      setPropertyId(event.target.value);
                      setRoomRow("");
                      setRoomFloor("");
                      setRoomSearch("");
                      setForm((current) => ({
                        ...current,
                        roomId: "",
                        monthlyRent: "",
                      }));
                    }}
                    required
                  >
                    {properties.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Tìm phòng
                  <input
                    type="search"
                    placeholder="Mã hoặc tên phòng"
                    value={roomSearch}
                    onChange={(event) => setRoomSearch(event.target.value)}
                  />
                </label>
                <label>
                  Dãy
                  <select
                    value={roomRow}
                    onChange={(event) => setRoomRow(event.target.value)}
                  >
                    <option value="">Tất cả dãy</option>
                    {rows.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} · {item.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Tầng
                  <select
                    value={roomFloor}
                    onChange={(event) => setRoomFloor(event.target.value)}
                  >
                    <option value="">Tất cả tầng</option>
                    {[
                      ...new Set(
                        rooms
                          .map((item) => item.floor)
                          .filter((item) => item !== null),
                      ),
                    ]
                      .sort((a, b) => a - b)
                      .map((item) => (
                        <option key={item} value={item}>
                          {item === 0 ? "Tầng trệt" : `Tầng ${item}`}
                        </option>
                      ))}
                  </select>
                </label>
              </div>
              <div
                className="ct-room-list"
                role="radiogroup"
                aria-label="Phòng còn trống"
              >
                {matchingRooms.length ? (
                  matchingRooms.map((room) => (
                    <label
                      className={`ct-room-option${form.roomId === room.id ? " is-selected" : ""}`}
                      key={room.id}
                    >
                      <input
                        type="radio"
                        name="contract-room"
                        checked={form.roomId === room.id}
                        onChange={() =>
                          setForm((current) => ({
                            ...current,
                            roomId: room.id,
                            monthlyRent: String(room.basePrice || ""),
                          }))
                        }
                      />
                      <span>
                        <strong>
                          {room.roomNumber}
                          {room.roomName && ` · ${room.roomName}`}
                        </strong>
                        <small>
                          <RoomLabel room={room} rows={rows} /> ·{" "}
                          {room.area ? `${room.area} m²` : "Diện tích chưa có"}{" "}
                          · {money(room.basePrice)}/tháng
                        </small>
                      </span>
                      <b>Còn trống</b>
                    </label>
                  ))
                ) : (
                  <p className="ct-empty-text">Không có phòng trống phù hợp.</p>
                )}
              </div>
            </section>
            {chosenRoom && (
              <>
                <section className="ct-card">
                  <div className="ct-card-head">
                    <span className="ct-step">2</span>
                    <div>
                      <h2>Thời hạn</h2>
                      <p>
                        Phòng đã chọn:{" "}
                        <RoomLabel
                          room={chosenRoom}
                          rows={rows}
                          property={property}
                        />
                      </p>
                    </div>
                  </div>
                  <div className="ct-fields">
                    <label>
                      Ngày bắt đầu *
                      <DatePicker
                        value={form.startDate}
                        onChange={(value) =>
                          setForm((current) => ({
                            ...current,
                            startDate: value,
                            endMonth:
                              current.endMonth < value.slice(0, 7)
                                ? ""
                                : current.endMonth,
                          }))
                        }
                      />
                    </label>
                    <label>
                      Tháng kết thúc *
                      <MonthPicker
                        value={form.endMonth}
                        min={form.startDate.slice(0, 7)}
                        onChange={(value) => patchForm("endMonth", value)}
                      />
                      {form.endMonth && (
                        <small>
                          Hợp đồng kết thúc ngày{" "}
                          {displayDate(endOfMonth(form.endMonth))}
                        </small>
                      )}
                    </label>
                  </div>
                </section>
                <section className="ct-card ct-finance-card">
                  <h2>Tài chính</h2>
                  <div className="ct-fields">
                    <label>
                      Tiền thuê/tháng *
                      <input
                        type="number"
                        min="0"
                        step="1000"
                        required
                        value={form.monthlyRent}
                        onChange={(event) =>
                          patchForm("monthlyRent", event.target.value)
                        }
                      />
                      <small>Gợi ý từ giá niêm yết của phòng.</small>
                    </label>
                    <label>
                      Tiền cọc *
                      <input
                        type="number"
                        min="0"
                        step="1000"
                        required
                        value={form.depositAmount}
                        onChange={(event) =>
                          patchForm("depositAmount", event.target.value)
                        }
                      />
                      <small>
                        Cọc lớn hơn 0 sẽ tự phát hành hóa đơn khi lưu hợp đồng.
                      </small>
                    </label>
                    <label>
                      Hạn thanh toán
                      <select
                        value={form.paymentDueDay}
                        onChange={(event) =>
                          patchForm("paymentDueDay", event.target.value)
                        }
                      >
                        <option value="">Chưa thiết lập</option>
                        {Array.from(
                          { length: 15 },
                          (_, index) => index + 1,
                        ).map((day) => (
                          <option key={day} value={day}>
                            Ngày {String(day).padStart(2, "0")} hằng tháng
                          </option>
                        ))}
                      </select>
                    </label>
                    {Number(form.depositAmount) > 0 && (
                      <div className="ct-deposit-fields">
                        <label>
                          Trạng thái tiền cọc
                          <select
                            value={form.depositPaymentStatus}
                            onChange={(event) =>
                              patchForm(
                                "depositPaymentStatus",
                                event.target.value,
                              )
                            }
                          >
                            <option value="Unpaid">
                              Chưa thanh toán (mặc định)
                            </option>
                            <option value="Paid">Đã nhận đủ tiền cọc</option>
                          </select>
                        </label>
                        {form.depositPaymentStatus === "Paid" && (
                          <>
                            <label>
                              Phương thức đã nhận
                              <select
                                value={form.depositPaymentMethod}
                                onChange={(event) =>
                                  patchForm(
                                    "depositPaymentMethod",
                                    event.target.value,
                                  )
                                }
                              >
                                <option value="Cash">Tiền mặt</option>
                                <option value="BankTransfer">
                                  Chuyển khoản
                                </option>
                              </select>
                            </label>
                          </>
                        )}
                        <p>
                          {form.depositPaymentStatus === "Paid"
                            ? "Hóa đơn cọc sẽ được phát hành và ghi nhận đã thanh toán đủ."
                            : "Hóa đơn cọc sẽ được phát hành với số tiền còn phải thu bằng tiền cọc."}
                        </p>
                      </div>
                    )}
                  </div>
                </section>
                <section className="ct-card">
                  <h2>Người tham gia hợp đồng</h2>
                  <fieldset className="ct-mode-options">
                    <legend>Chế độ thuê</legend>
                    {[
                      ["CoTenancy", "Đồng thuê", "Mỗi người đứng tên cần tài khoản và tự chấp thuận nội quy."],
                      ["PrimaryTenantWithFamily", "Một người đứng tên, người thân cùng ở", "Người đứng tên chấp thuận; người thân được duyệt cư trú và không cần tài khoản."],
                    ].map(([value, label, help]) => (
                      <label key={value} className={`ct-mode-option ${form.tenancyMode === value ? "is-selected" : ""}`}>
                        <input type="radio" name="tenancyMode" value={value}
                          checked={form.tenancyMode === value}
                          onChange={() => patchForm("tenancyMode", value)} />
                        <strong>{label}</strong><small>{help}</small>
                      </label>
                    ))}
                  </fieldset>
                  <div className="ct-member-section">
                    <div className="ct-member-heading"><h3>Người đứng tên *</h3></div>
                    <p className="ct-member-note">Cần hồ sơ có tài khoản liên kết để chấp thuận nội quy.</p>
                  <label>
                    Tìm hồ sơ người thuê
                    <div className="ct-search">
                      <Search size={17} />
                      <input
                        type="search"
                        placeholder="Tên, email hoặc số điện thoại…"
                        value={tenantSearch}
                        onChange={(event) =>
                          setTenantSearch(event.target.value)
                        }
                      />
                    </div>
                  </label>
                  {chosenTenant && (
                    <div className="ct-picked">
                      <strong>Người đứng tên: {chosenTenant.fullName}</strong>
                      <span>
                        {chosenTenant.email || "Chưa có email"} ·{" "}
                        {chosenTenant.phoneNumber || "Chưa có SĐT"}
                      </span>
                      <button
                        type="button"
                        onClick={() => patchForm("tenantId", "")}
                      >
                        Bỏ chọn
                      </button>
                    </div>
                  )}
                  <div className="ct-tenant-list">
                    {matchingTenants.length ? (
                      matchingTenants.map((item) => (
                        <button
                          key={item.id}
                          type="button"
                          className={
                            form.tenantId === item.id ? "is-selected" : ""
                          }
                          onClick={() => patchForm("tenantId", item.id)}
                          disabled={!item.userId || form.additionalTenantIds.includes(item.id)}
                        >
                          <strong>{item.fullName}</strong>
                          <span>
                            {item.email || "Chưa có email"} ·{" "}
                            {item.phoneNumber || "Chưa có SĐT"} · {item.userId ? "Có tài khoản" : "Chưa có tài khoản"}
                          </span>
                          <small>
                            {form.tenantId === item.id ? "Đã chọn" : item.userId ? "Chọn" : "Cần liên kết tài khoản"}
                          </small>
                        </button>
                      ))
                    ) : (
                      <p className="ct-empty-text">
                        Không tìm thấy người thuê.{" "}
                        <button
                          type="button"
                          onClick={() => navigate("landlord/tenants")}
                        >
                          Đến danh mục người thuê
                        </button>
                      </p>
                    )}
                  </div>
                  </div>
                  <div className="ct-member-section">
                    <div className="ct-member-heading">
                      <h3>{familyMode ? "Người thân cùng ở" : "Người đồng thuê khác"}</h3>
                      <span>{chosenRoom ? `${Math.max(0, chosenRoom.maxOccupants - 1 - selectedAdditional.length)} chỗ còn lại` : "Chọn phòng để xem sức chứa"}</span>
                    </div>
                    <p className="ct-member-note">{familyMode
                      ? "Người thân không phải bên ký và không bị ghi là đã tự chấp thuận; chủ trọ duyệt cư trú khi thêm."
                      : "Mỗi người đồng thuê cần tài khoản liên kết và tự chấp thuận nội quy."}</p>
                    {selectedAdditional.length > 0 && (
                      <ul className="ct-member-list">{selectedAdditional.map((item) => (
                        <li className="ct-member-chip" key={item.id}>{item.fullName}
                          <button type="button" onClick={() => toggleAdditionalTenant(item.id)} aria-label={`Bỏ ${item.fullName}`}>Bỏ</button>
                        </li>
                      ))}</ul>
                    )}
                    <div className="ct-tenant-list">{matchingTenants.filter((item) => item.id !== form.tenantId).map((item) => (
                      <button key={item.id} type="button"
                        className={form.additionalTenantIds.includes(item.id) ? "is-selected" : ""}
                        disabled={!familyMode && !item.userId ||
                          !form.additionalTenantIds.includes(item.id) && chosenRoom && 1 + selectedAdditional.length >= chosenRoom.maxOccupants}
                        onClick={() => toggleAdditionalTenant(item.id)}>
                        <strong>{item.fullName}</strong>
                        <span>{item.email || "Chưa có email"} · {item.userId ? "Có tài khoản" : "Chưa có tài khoản"}</span>
                        <small>{form.additionalTenantIds.includes(item.id) ? "Bỏ chọn" : "Thêm"}</small>
                      </button>
                    ))}</div>
                  </div>
                </section>
                <section className="ct-card">
                  <h2>Dịch vụ áp dụng cho phòng</h2>
                  <p className="ct-member-note">Kiểm tra dịch vụ trước khi lưu hợp đồng. Nếu bắt đầu giữa tháng, phí dịch vụ tháng đầu được tính đủ tháng.</p>
                  {roomServiceLoading ? <p role="status">Đang tải dịch vụ của phòng…</p> : serviceCatalog.filter((item) => item.isActive || form.roomServices.some((entry) => entry.serviceId === item.id)).map((service) => {
                    const selected = form.roomServices.find((entry) => entry.serviceId === service.id);
                    return <div className="ct-service-row" key={service.id}>
                      <label><input type="checkbox" checked={!!selected} onChange={(event) => setForm((current) => ({ ...current, servicesConfirmed: false, roomServices: event.target.checked ? [...current.roomServices, { serviceId: service.id, quantity: "1", customPrice: String(service.defaultPrice ?? 0) }] : current.roomServices.filter((entry) => entry.serviceId !== service.id) }))} /> {service.name} · {service.unit} · {money(service.defaultPrice)}</label>
                      {selected && <><label>Số lượng<input type="number" min="1" step="0.01" required value={selected.quantity} onChange={(event) => setForm((current) => ({ ...current, servicesConfirmed: false, roomServices: current.roomServices.map((entry) => entry.serviceId === service.id ? { ...entry, quantity: event.target.value } : entry) }))} /></label><label>Đơn giá riêng<input type="number" min="0" step="1" value={selected.customPrice} onChange={(event) => setForm((current) => ({ ...current, servicesConfirmed: false, roomServices: current.roomServices.map((entry) => entry.serviceId === service.id ? { ...entry, customPrice: event.target.value } : entry) }))} /></label></>}
                    </div>;
                  })}
                  {!serviceCatalog.length && !roomServiceLoading && <p>Phòng chưa có danh mục dịch vụ. Bạn có thể xác nhận không dùng dịch vụ.</p>}
                  {serviceLoadError && <p role="alert" className="contract-message contract-message--warning">Không tải được dịch vụ: {serviceLoadError}</p>}
                  <label><input type="checkbox" checked={form.servicesConfirmed} disabled={roomServiceLoading || !!serviceLoadError || !form.roomId} onChange={(event) => patchForm("servicesConfirmed", event.target.checked)} /> Tôi đã kiểm tra cấu hình dịch vụ cho hợp đồng này.</label>
                </section>
                <section className="ct-card">
                  <label>
                    Ghi chú
                    <textarea
                      rows={3}
                      maxLength={1000}
                      value={form.note}
                      onChange={(event) =>
                        patchForm("note", event.target.value)
                      }
                    />
                  </label>
                </section>
                <div className="ct-form-footer">
                  <span>Mã hợp đồng được hệ thống tạo khi lưu.</span>
                  <div>
                    <button
                      type="button"
                      className="contract-button contract-button--ghost"
                      onClick={() => routeTo(navigate, "")}
                    >
                      Hủy
                    </button>
                    <button
                      type="submit"
                      className="contract-button contract-button--primary"
                      disabled={busy || !form.endMonth}
                    >
                      {busy ? "Đang lưu…" : "Lưu bản nháp"}
                    </button>
                  </div>
                </div>
              </>
            )}
          </form>
        </>
      )}
      {!loading &&
        (mode === "detail" || mode === "edit") &&
        (detail ? (
          <>
            <header className="ct-page-head">
              <div>
                <span className="contract-eyebrow">CHI TIẾT HỢP ĐỒNG</span>
                <h1>{detail.contractCode}</h1>
                <p>
                  Phòng{" "}
                  <RoomLabel
                    room={detailRoom || { roomNumber: detail.roomNumber }}
                    rows={rows}
                    property={property}
                  />
                </p>
              </div>
              <Status contract={detail} />
            </header>
            {mode === "edit" ? (
              <section className="ct-card">
                <h2>Chỉnh sửa bản nháp</h2>
                <p className="ct-api-note">
                  Bạn có thể chuẩn bị thay đổi tại đây. API hiện chưa hỗ trợ lưu
                  nội dung hợp đồng; nút Lưu sẽ khả dụng sau khi API được bổ
                  sung.
                </p>
                {detail.status !== "Draft" ? (
                  <p>Chỉ có thể chỉnh sửa hợp đồng nháp.</p>
                ) : (
                  editForm && (
                    <div className="ct-fields">
                      <label>
                        Ngày bắt đầu
                        <DatePicker
                          value={editForm.startDate}
                          onChange={(value) =>
                            setEditForm((current) => ({
                              ...current,
                              startDate: value,
                              endMonth:
                                current.endMonth < value.slice(0, 7)
                                  ? ""
                                  : current.endMonth,
                            }))
                          }
                        />
                      </label>
                      <label>
                        Tháng kết thúc
                        <MonthPicker
                          value={editForm.endMonth}
                          min={editForm.startDate.slice(0, 7)}
                          onChange={(value) =>
                            setEditForm((current) => ({
                              ...current,
                              endMonth: value,
                            }))
                          }
                        />
                      </label>
                      <label>
                        Tiền thuê/tháng
                        <input
                          type="number"
                          min="0"
                          step="1000"
                          value={editForm.monthlyRent}
                          onChange={(event) =>
                            setEditForm((current) => ({
                              ...current,
                              monthlyRent: event.target.value,
                            }))
                          }
                        />
                      </label>
                      <label>
                        Tiền cọc
                        <input
                          type="number"
                          min="0"
                          step="1000"
                          value={editForm.depositAmount}
                          onChange={(event) =>
                            setEditForm((current) => ({
                              ...current,
                              depositAmount: event.target.value,
                            }))
                          }
                        />
                      </label>
                      <label>
                        Hạn thanh toán
                        <select
                          value={editForm.paymentDueDay}
                          onChange={(event) =>
                            setEditForm((current) => ({
                              ...current,
                              paymentDueDay: event.target.value,
                            }))
                          }
                        >
                          <option value="">Chưa thiết lập</option>
                          {Array.from(
                            { length: 15 },
                            (_, index) => index + 1,
                          ).map((day) => (
                            <option key={day} value={day}>
                              Ngày {day}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label>
                        Ghi chú
                        <textarea
                          rows={3}
                          value={editForm.note}
                          onChange={(event) =>
                            setEditForm((current) => ({
                              ...current,
                              note: event.target.value,
                            }))
                          }
                        />
                      </label>
                    </div>
                  )
                )}
                <div className="ct-form-footer">
                  <button
                    type="button"
                    className="contract-button contract-button--ghost"
                    onClick={() =>
                      routeTo(navigate, "/" + propertyId + "/" + contractId)
                    }
                  >
                    Hủy thay đổi
                  </button>
                  <button
                    type="button"
                    className="contract-button contract-button--primary"
                    disabled
                    title="API cập nhật hợp đồng chưa có"
                  >
                    Lưu thay đổi
                  </button>
                </div>
              </section>
            ) : (
              <>
                <div className="ct-detail-grid">
                  <section className="ct-card">
                    <div className="ct-card-title">
                      <h2>Thông tin hợp đồng</h2>
                      {detail.status === "Draft" && (
                        <button
                          type="button"
                          className="contract-button contract-button--secondary"
                          onClick={() =>
                            routeTo(
                              navigate,
                              "/" + propertyId + "/" + contractId + "/edit",
                            )
                          }
                        >
                          Chỉnh sửa
                        </button>
                      )}
                    </div>
                    <dl className="ct-facts">
                      <div>
                        <dt>Thời hạn</dt>
                        <dd>
                          {displayDate(detail.startDate)} –{" "}
                          {displayDate(detail.endDate)}
                        </dd>
                      </div>
                      <div>
                        <dt>Tiền thuê/tháng</dt>
                        <dd>{money(detail.monthlyRent)}</dd>
                      </div>
                      <div>
                        <dt>Tiền cọc</dt>
                        <dd>{money(detail.depositAmount)}</dd>
                      </div>
                      {Number(detail.depositAmount) > 0 && (
                        <div>
                          <dt>Hóa đơn cọc</dt>
                          <dd>
                            {depositInvoiceLoading ? (
                              "Đang kiểm tra hóa đơn…"
                            ) : depositInvoiceError ? (
                              <button
                                type="button"
                                className="contract-button contract-button--secondary"
                                onClick={() =>
                                  setDepositRevision((value) => value + 1)
                                }
                              >
                                Không tải được hóa đơn · Thử lại
                              </button>
                            ) : depositInvoice ? (
                              <button
                                type="button"
                                className="contract-button contract-button--secondary"
                                onClick={() =>
                                  navigate(
                                    `landlord/billing/${depositInvoice.id}`,
                                  )
                                }
                              >
                                Xem hóa đơn cọc ·{" "}
                                {depositInvoice.status === "Draft"
                                  ? "Bản nháp"
                                  : depositInvoice.status === "Cancelled"
                                    ? "Đã hủy"
                                    : "Đã phát hành"}
                              </button>
                            ) : ["Draft", "Active"].includes(detail.status) ? (
                              <button
                                type="button"
                                className="contract-button contract-button--secondary"
                                disabled={busy}
                                onClick={() => {
                                  if (
                                    window.confirm(
                                      "Hợp đồng cũ chưa có hóa đơn cọc. Tạo hóa đơn nháp để kiểm tra trước khi phát hành?",
                                    )
                                  )
                                    createLegacyDeposit();
                                }}
                              >
                                Tạo hóa đơn cọc nháp cho hợp đồng cũ
                              </button>
                            ) : (
                              "Chưa có hóa đơn cọc"
                            )}
                          </dd>
                        </div>
                      )}
                      <div>
                        <dt>Hạn thanh toán</dt>
                        <dd>
                          {detail.paymentDueDay
                            ? "Ngày " + detail.paymentDueDay + " hằng tháng"
                            : "Chưa thiết lập"}
                        </dd>
                      </div>
                      {detail.note && (
                        <div>
                          <dt>Ghi chú</dt>
                          <dd>{detail.note}</dd>
                        </div>
                      )}
                    </dl>
                  </section>
                  <aside className="ct-card">
                    <h2>Trạng thái và thao tác</h2>
                    <Status contract={detail} />
                    <div className="ct-actions">
                      {detail.status === "Draft" && (
                        <>
                          <button
                            type="button"
                            className="contract-button contract-button--primary"
                            disabled={busy || !detail.tenants?.length}
                            onClick={() =>
                              run(
                                () =>
                                  contractApi.activate(propertyId, contractId),
                                "Đã kích hoạt hợp đồng.",
                              )
                            }
                          >
                            Kích hoạt hợp đồng
                          </button>
                          <button
                            type="button"
                            className="contract-button contract-button--danger"
                            disabled={busy}
                            onClick={() => {
                              if (
                                window.confirm(
                                  "Hủy bản nháp và hóa đơn cọc chưa có giao dịch? Nếu đã có giao dịch, hệ thống sẽ chặn để đối soát.",
                                )
                              )
                                run(
                                  () =>
                                    contractApi.cancel(propertyId, contractId),
                                  "Đã hủy bản nháp và hóa đơn cọc chưa có giao dịch.",
                                );
                            }}
                          >
                            Hủy bản nháp
                          </button>
                        </>
                      )}
                      {detail.status === "Active" && (
                        <button
                          type="button"
                          className="contract-button contract-button--ghost"
                          disabled={busy}
                          onClick={() => {
                            const reason = window.prompt(
                              "Lý do đề nghị kết thúc sớm",
                            );
                            if (reason?.trim())
                              run(
                                () =>
                                  contractApi.terminate(
                                    propertyId,
                                    contractId,
                                    reason.trim(),
                                  ),
                                "Hợp đồng đã chuyển sang chờ bàn giao. Phòng chưa được mở cho thuê lại.",
                              );
                          }}
                        >
                          Bắt đầu kết thúc sớm
                        </button>
                      )}
                    </div>
                  </aside>
                </div>
                <LandlordRules
                  propertyId={propertyId}
                  contractId={contractId}
                  detail={detail}
                  navigate={navigate}
                />
                {["PendingMoveOut", "PendingSettlement"].includes(
                  detail.status,
                ) && (
                  <EarlyExitFlow
                    propertyId={propertyId}
                    contract={detail}
                    navigate={navigate}
                    onChanged={async () => {
                      setDetail(
                        await contractApi.detail(propertyId, contractId),
                      );
                      setDepositRevision((value) => value + 1);
                    }}
                  />
                )}
                {detail.status === "Terminated" && detail.hasSettlement && (
                  <SettlementRefundEvidencePanel
                    propertyId={propertyId}
                    contractId={contractId}
                  />
                )}
                {needsSettlement(detail) && (
                  <div
                    className="contract-message contract-message--warning"
                    role="status"
                  >
                    <strong>Cần đối chiếu tất toán.</strong> Còn nợ{" "}
                    {money(detail.outstandingAmount)} · Cọc đang giữ{" "}
                    {money(detail.depositHeld)}
                    {detail.hasDraftInvoices ? " · Có hóa đơn nháp" : ""}
                    {!detail.hasSettlement ? " · Chưa có bản ghi tất toán" : ""}
                  </div>
                )}
                <section className="ct-card">
                  <h2>Người tham gia hợp đồng</h2>
                  <p className="ct-member-note">{detailFamilyMode ? "Một người đứng tên; người thân chỉ được duyệt cư trú." : "Mỗi người đồng thuê đứng tên và tự chấp thuận nội quy."}</p>
                  <div className="ct-service-review"><span>Khi thêm hoặc ghi ngày rời của một người, hãy rà lại số lượng dịch vụ tính theo người hoặc phương tiện. Mở tab “Giá, chỉ số và dịch vụ” để chỉnh cho kỳ sau.</span><button type="button" className="contract-button contract-button--secondary" onClick={() => navigate(`landlord/billing/rooms/${propertyId}/${detail.roomId}`)}>Rà dịch vụ phòng</button></div>
                  {detail.tenants?.length ? (
                    <ul className="ct-people">
                      {detail.tenants.map((item) => (
                        <li key={item.id}>
                          <strong>
                            {item.fullName}
                            {item.isContactPerson ? " · Người liên hệ" : ""}
                          </strong>
                          <span className={`ct-role-badge ct-role-badge--${item.role === "AuthorizedOccupant" ? "occupant" : "party"}`}>
                            {item.role === "AuthorizedOccupant" ? "Người cùng ở" : "Người đứng tên"}
                          </span>
                          <span className={`ct-approval-badge ct-approval-badge--${item.role === "AuthorizedOccupant" ? item.residenceApprovedAt ? "approved" : "pending" : item.hasAcceptedCurrentRules ? "accepted" : "pending"}`}>
                            {item.role === "AuthorizedOccupant" ? item.residenceApprovedAt ? "Được duyệt cư trú" : "Chờ duyệt cư trú" : item.hasAcceptedCurrentRules ? "Đã chấp thuận nội quy" : "Chưa chấp thuận nội quy"}
                          </span>
                          <span>
                            {item.email || "Chưa có email"} · Vào ở{" "}
                            {displayDate(item.joinDate)}
                            {item.leaveDate &&
                              " · Rời " + displayDate(item.leaveDate)}
                          </span>
                          {detail.status === "Active" &&
                            !item.leaveDate &&
                            !item.isContactPerson && item.role !== "AuthorizedOccupant" && (
                              <button
                                type="button"
                                className="contract-button contract-button--secondary"
                                disabled={busy}
                                onClick={() =>
                                  run(
                                    () =>
                                      contractApi.setContact(
                                        propertyId,
                                        contractId,
                                        item.id,
                                      ),
                                    "Đã chuyển người liên hệ.",
                                  )
                                }
                              >
                                Đặt làm người liên hệ
                              </button>
                            )}
                          {detail.status === "Active" &&
                            !item.leaveDate &&
                            detail.tenants.filter((tenant) => !tenant.leaveDate)
                              .length > 1 && (
                              <button
                                type="button"
                                className="contract-button contract-button--ghost"
                                disabled={busy}
                                onClick={() => {
                                  const leaveDate = window.prompt(
                                    "Ngày rời (yyyy-mm-dd)",
                                    today(),
                                  );
                                  if (
                                    leaveDate &&
                                    /^\d{4}-\d{2}-\d{2}$/.test(leaveDate)
                                  )
                                    run(
                                      () =>
                                        contractApi.leave(
                                          propertyId,
                                          contractId,
                                          item.id,
                                          leaveDate,
                                        ),
                                      "Đã ghi ngày rời. Hãy rà lại dịch vụ của phòng nếu số lượng cần đổi.",
                                    );
                                }}
                              >
                                Ghi ngày rời
                              </button>
                            )}
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p>
                      Chưa có người thuê. Cần thêm người thuê trước khi kích
                      hoạt.
                    </p>
                  )}
                  {detail.status === "Active" &&
                    detail.tenants?.filter((tenant) => !tenant.leaveDate)
                      .length === 1 && (
                      <p className="ct-exit-help">
                        Người thuê cuối cùng rời phòng: dùng “Bắt đầu kết thúc
                        sớm” để bàn giao và quyết toán hợp đồng.
                      </p>
                    )}
                  {["Draft", "Active"].includes(detail.status) && (
                    <div className="ct-add-tenant">
                      <button
                        type="button"
                        className="contract-button contract-button--secondary ct-add-tenant-toggle"
                        aria-expanded={showAddTenant}
                        aria-controls="ct-add-tenant-panel"
                        onClick={() => {
                          setShowAddTenant((value) => !value);
                          setTenantSearch("");
                          setExtraTenantId("");
                          setExtraTenantRole(detailFamilyMode && detailHasParty ? "AuthorizedOccupant" : "ContractParty");
                        }}
                      >
                        <Plus size={16} />{" "}
                        {showAddTenant
                          ? "Đóng phần thêm người thuê"
                          : "Thêm người thuê"}
                      </button>
                      {showAddTenant && (
                        <div
                          id="ct-add-tenant-panel"
                          className="ct-add-tenant-panel"
                        >
                          <label>
                            Tìm bằng tên, email hoặc số điện thoại
                            <input
                              type="search"
                              value={tenantSearch}
                              onChange={(event) => {
                                setTenantSearch(event.target.value);
                                setExtraTenantId("");
                              }}
                              placeholder="Nhập tên, email hoặc số điện thoại…"
                            />
                          </label>
                          {detailFamilyMode && <p className="ct-member-note">{detailHasParty
                            ? "Người đứng tên đã được chọn. Người thêm vào sẽ là người thân được duyệt cư trú, không cần tài khoản."
                            : "Chọn người đứng tên trước. Người này cần tài khoản liên kết."}</p>}
                          <div className="ct-tenant-list">
                            {tenantAvailabilityLoading ? (
                              <p className="ct-empty-text">
                                Đang kiểm tra hợp đồng của người thuê…
                              </p>
                            ) : tenantAvailabilityError ? (
                              <p className="ct-empty-text">
                                Không thể kiểm tra hợp đồng hiện có. Vui lòng
                                tải lại trang.
                              </p>
                            ) : !tenantSearch.trim() ? (
                              <p className="ct-empty-text">
                                Nhập thông tin để tìm người ở cùng.
                              </p>
                            ) : matchingTenants.length ? (
                              matchingTenants.map((item) => (
                                <button
                                  key={item.id}
                                  type="button"
                                  className={
                                    extraTenantId === item.id
                                      ? "is-selected"
                                      : ""
                                  }
                                  onClick={() => setExtraTenantId(item.id)}
                                  disabled={(extraTenantRole === "ContractParty" && !item.userId) ||
                                    detail.tenants?.some((tenant) => tenant.tenantId === item.id && !tenant.leaveDate)}
                                >
                                  <strong>{item.fullName}</strong>
                                  <span>
                                    {item.email || "Chưa có email"} ·{" "}
                                    {item.phoneNumber || "Chưa có SĐT"} · {item.userId ? "Có tài khoản" : "Chưa có tài khoản"}
                                  </span>
                                  <small>
                                    {extraTenantId === item.id
                                      ? "Đã chọn"
                                      : "Chọn"}
                                  </small>
                                </button>
                              ))
                            ) : (
                              <p className="ct-empty-text">
                                Không tìm thấy người thuê chưa có hợp đồng trùng
                                thời gian.
                              </p>
                            )}
                          </div>
                          {extraTenant && (
                            <p>Đã chọn: {extraTenant.fullName}</p>
                          )}
                          <div className="ct-add-tenant-actions">
                            <label>
                              Ngày vào ở
                              <DatePicker
                                value={extraJoinDate}
                                min={detail.startDate}
                                onChange={setExtraJoinDate}
                              />
                            </label>
                            <button
                              className="contract-button contract-button--secondary"
                              type="button"
                              disabled={
                                busy ||
                                tenantAvailabilityLoading ||
                                tenantAvailabilityError ||
                                !extraTenantId ||
                                unavailableIds.has(extraTenantId)
                              }
                              onClick={() =>
                                run(async () => {
                                  await contractApi.addTenant(
                                    propertyId,
                                    contractId,
                                    {
                                      tenantId: extraTenantId,
                                      joinDate: extraJoinDate,
                                      isContactPerson: !detail.tenants?.length,
                                      role: extraTenantRole,
                                    },
                                  );
                                  setExtraTenantId("");
                                  setTenantSearch("");
                                  setShowAddTenant(false);
                                }, extraTenantRole === "AuthorizedOccupant" ? "Đã duyệt người cùng ở. Hãy rà lại dịch vụ của phòng." : "Đã thêm người đứng tên. Hãy rà lại dịch vụ của phòng.")
                              }
                            >
                              Xác nhận thêm
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </section>
              </>
            )}
          </>
        ) : (
          <div className="contract-empty">
            <strong>Không tìm thấy hợp đồng</strong>
          </div>
        ))}
    </div>
  );
}
