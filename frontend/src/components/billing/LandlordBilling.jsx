import { useEffect, useState } from "react";
import { billingApi } from "../../services/api/billingApi";
import BillingInputs from "./BillingInputs";
import BillingPriorityBoard from "./BillingPriorityBoard";
import OneOffInvoiceForm from "./OneOffInvoiceForm";
import {
  DraftItemsEditor,
  InvoiceAdjustmentEditor,
  InvoiceAdjustmentHistory,
} from "./InvoiceEditing";
import LandlordPaymentPanel from "./LandlordPaymentPanel";
import InvoicePaymentHistory from "./InvoicePaymentHistory";
import InvoiceActionDialog from "./InvoiceActionDialog";
import OverpaymentRefund from "./OverpaymentRefund";
import { paymentAppliedToInvoice } from "./invoiceDisplay";
import "../../styles/billing-live.css";

const money = (value) =>
  new Intl.NumberFormat("vi-VN").format(Number(value || 0)) + " ₫";
const errorOf = (error) => error?.message || "Không thể hoàn tất thao tác.";
const previousBillingMonth = () => {
  const vietnamToday = new Date(Date.now() + 7 * 3600_000)
    .toISOString()
    .slice(0, 7);
  const [year, month] = vietnamToday.split("-").map(Number);
  return new Date(Date.UTC(year, month - 2, 1)).toISOString().slice(0, 7);
};
const monthLabel = (invoice) =>
  invoice.billingMonth
    ? `${invoice.billingMonth}/${invoice.billingYear}`
    : invoice.type === "Deposit"
      ? "Tiền cọc"
      : "Hóa đơn khác";
const statusLabel = {
  Draft: "Bản nháp",
  Issued: "Đã phát hành",
  Cancelled: "Đã hủy",
  Paid: "Đã thanh toán",
  Partial: "Đã thu một phần",
  Unpaid: "Chưa thanh toán",
  Overdue: "Quá hạn",
  Credit: "Dư tiền",
};

export default function LandlordBilling({
  invoiceId,
  initialPropertyId,
  initialRoomId,
  navigate,
}) {
  return invoiceId ? (
    <InvoiceDetailPage
      key={invoiceId}
      invoiceId={invoiceId}
      navigate={navigate}
    />
  ) : (
    <LandlordBillingList
      key={`${initialPropertyId || ""}:${initialRoomId || ""}`}
      initialPropertyId={initialPropertyId}
      initialRoomId={initialRoomId}
      navigate={navigate}
    />
  );
}

function LandlordBillingList({ initialPropertyId, initialRoomId, navigate }) {
  const [properties, setProperties] = useState([]);
  const [propertyId, setPropertyId] = useState(initialPropertyId || "");
  const [rooms, setRooms] = useState([]);
  const [roomId, setRoomId] = useState(initialRoomId || "");
  const [listStatus, setListStatus] = useState("");
  const [listPeriod, setListPeriod] = useState("");
  const [contracts, setContracts] = useState([]);
  const [tab, setTab] = useState("invoices");
  const [month, setMonth] = useState(previousBillingMonth);
  const [worklist, setWorklist] = useState([]);
  const [worklistError, setWorklistError] = useState("");
  const [worklistLoading, setWorklistLoading] = useState(false);
  const [attention, setAttention] = useState({
    unpaid: [],
    pendingPayments: [],
  });
  const [attentionError, setAttentionError] = useState("");
  const [attentionLoading, setAttentionLoading] = useState(false);
  const [previewRequested, setPreviewRequested] = useState(false);
  const [contractId, setContractId] = useState("");
  const [discountAmount, setDiscountAmount] = useState("");
  const [discountReason, setDiscountReason] = useState("");
  const [preview, setPreview] = useState(null);
  const [invoices, setInvoices] = useState([]);
  const [meta, setMeta] = useState(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    billingApi
      .properties(controller.signal)
      .then((response) => {
        setProperties(response.data || []);
        setPropertyId((current) =>
          current && response.data?.some((item) => item.id === current)
            ? current
            : response.data?.[0]?.id || "",
        );
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setError(errorOf(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, []);
  useEffect(() => {
    if (!propertyId) {
      setRooms([]);
      setContracts([]);
      setInvoices([]);
      return;
    }
    const controller = new AbortController();
    setLoading(true);
    setError("");
    Promise.all([
      billingApi.rooms(propertyId, controller.signal),
      billingApi.contracts(propertyId, controller.signal),
      billingApi.invoices(
        propertyId,
        page,
        controller.signal,
        roomId || undefined,
        {
          status: listStatus || undefined,
          year: listPeriod ? Number(listPeriod.slice(0, 4)) : undefined,
          month: listPeriod ? Number(listPeriod.slice(5, 7)) : undefined,
        },
      ),
    ])
      .then(([roomResponse, contractResponse, invoiceResponse]) => {
        setRooms(roomResponse.data || []);
        setContracts(contractResponse.data || []);
        setInvoices(invoiceResponse.data || []);
        setMeta(invoiceResponse.meta);
      })
      .catch((cause) => {
        if (cause.name !== "AbortError") setError(errorOf(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, roomId, listStatus, listPeriod, page, revision]);
  useEffect(() => {
    if (!propertyId || !month) {
      setWorklist([]);
      setWorklistError("");
      setWorklistLoading(false);
      return;
    }
    const selectedProperty = properties.find((item) => item.id === propertyId);
    if (selectedProperty && !selectedProperty.isActive) {
      setWorklist([]);
      setWorklistError("");
      setWorklistLoading(false);
      return;
    }
    const controller = new AbortController();
    setWorklistLoading(true);
    setWorklist([]);
    setWorklistError("");
    const [year, billingMonth] = month.split("-").map(Number);
    billingApi
      .monthlyWorklist(propertyId, year, billingMonth, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setWorklist(result.items || []);
          setWorklistError("");
        }
      })
      .catch((cause) => {
        if (!controller.signal.aborted && cause.name !== "AbortError") {
          setWorklist([]);
          setWorklistError(
            cause.status === 404
              ? cause.details?.code === "PROPERTY_NOT_FOUND"
                ? "Không tìm thấy khu trọ đang hoạt động. Hãy tải lại danh sách khu trọ."
                : "API đang chạy chưa có chức năng Việc cần chốt. Hãy cập nhật hoặc khởi động lại API."
              : errorOf(cause),
          );
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setWorklistLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, month, revision, properties]);
  useEffect(() => {
    if (!propertyId) {
      setAttention({ unpaid: [], pendingPayments: [] });
      setAttentionError("");
      setAttentionLoading(false);
      return;
    }
    const controller = new AbortController();
    setAttentionLoading(true);
    setAttention({ unpaid: [], pendingPayments: [] });
    setAttentionError("");
    billingApi
      .invoiceAttention(propertyId, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setAttention(result || { unpaid: [], pendingPayments: [] });
          setAttentionError("");
        }
      })
      .catch((cause) => {
        if (!controller.signal.aborted && cause.name !== "AbortError") {
          setAttention({ unpaid: [], pendingPayments: [] });
          setAttentionError(errorOf(cause));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setAttentionLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, revision]);
  useEffect(() => {
    if (
      !previewRequested ||
      !propertyId ||
      !contractId ||
      !month ||
      tab !== "invoices"
    )
      return;
    const [year, billingMonth] = month.split("-").map(Number);
    billingApi
      .preview(propertyId, {
        contractId,
        year,
        month: billingMonth,
        discountAmount: Number(discountAmount || 0),
        discountReason:
          Number(discountAmount || 0) > 0 ? discountReason.trim() : null,
      })
      .then(setPreview)
      .catch(() => setPreview(null));
    setPreviewRequested(false);
  }, [
    previewRequested,
    propertyId,
    contractId,
    month,
    tab,
    discountAmount,
    discountReason,
  ]);

  function changeProperty(value) {
    setPropertyId(value);
    setRoomId("");
    setListStatus("");
    setListPeriod("");
    setPage(1);
    setPreview(null);
    setContractId("");
  }
  const period = month.split("-").map(Number);
  const monthlyPayload = {
    contractId,
    year: period[0],
    month: period[1],
    discountAmount: Number(discountAmount || 0),
    discountReason:
      Number(discountAmount || 0) > 0 ? discountReason.trim() : null,
  };
  const discountInvalid =
    !Number.isFinite(Number(discountAmount || 0)) ||
    Number(discountAmount || 0) < 0 ||
    (Number(discountAmount || 0) > 0 && !discountReason.trim());
  async function action(request, message, after) {
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      const result = await request();
      setSuccess(message);
      setRevision((value) => value + 1);
      after?.(result);
    } catch (cause) {
      setError(errorOf(cause));
    } finally {
      setBusy(false);
    }
  }
  const roomName = (id) =>
    rooms.find((room) => room.id === id)?.roomNumber || id?.slice(0, 8) || "—";
  const selectedContract = contracts.find((item) => item.id === contractId);
  const activeContracts = contracts.filter(
    (item) => item.status === "Active" || item.status === "PendingSettlement",
  );
  const tenantsFor = (contractId) =>
    contracts.find((contract) => contract.id === contractId)?.tenants;
  function openWorkItem(item) {
    if (item.invoiceId) {
      navigate(`landlord/billing/${item.invoiceId}`);
      return;
    }
    setRoomId(item.roomId);
    setContractId(item.contractId);
    setPreview(null);
    if (item.status === "MissingInputs" || item.status === "NeedsAttention")
      setTab("inputs");
    else {
      setTab("invoices");
      setPreviewRequested(true);
    }
  }

  return (
    <div className="bill-page">
      <header className="bill-header">
        <div>
          <span>QUẢN LÝ THU TIỀN</span>
          <h1>Hóa đơn</h1>
          <p>
            Nhập dữ liệu tính tiền, xem trước rồi phát hành hóa đơn cho người
            thuê.
          </p>
        </div>
        <label>
          Khu trọ
          <select
            value={propertyId}
            onChange={(event) => changeProperty(event.target.value)}
          >
            <option value="">Chọn khu trọ</option>
            {properties.map((item) => (
              <option value={item.id} key={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>
      </header>
      <div className="bill-tabs" role="group" aria-label="Quản lý hóa đơn">
        <button
          type="button"
          aria-pressed={tab === "invoices"}
          className={tab === "invoices" ? "is-active" : ""}
          onClick={() => setTab("invoices")}
        >
          Hóa đơn
        </button>
        <button
          type="button"
          aria-pressed={tab === "inputs"}
          className={tab === "inputs" ? "is-active" : ""}
          onClick={() => setTab("inputs")}
        >
          Giá, chỉ số và dịch vụ
        </button>
      </div>
      {!propertyId && !loading && (
        <p className="bill-empty">Bạn chưa có khu trọ để lập hóa đơn.</p>
      )}
      {propertyId && tab === "inputs" && (
        <BillingInputs
          propertyId={propertyId}
          rooms={rooms}
          initialRoomId={roomId}
          initialPeriod={month}
          onChanged={() => {
            setPreview(null);
            setRevision((value) => value + 1);
          }}
          onReadingSaved={() => {
            setTab("invoices");
            setPreviewRequested(true);
          }}
        />
      )}
      {propertyId && tab === "invoices" && (
        <>
          <BillingPriorityBoard
            month={month}
            onMonthChange={(value) => {
              setMonth(value);
              setPreview(null);
            }}
            worklist={worklist}
            worklistError={worklistError}
            worklistLoading={worklistLoading}
            attention={attention}
            attentionError={attentionError}
            attentionLoading={attentionLoading}
            roomName={roomName}
            onWorkItem={openWorkItem}
            onOpenInvoice={(id) => navigate(`landlord/billing/${id}`)}
          />
          <div className="bill-grid bill-grid--two">
            <section className="bill-card">
              <h2>Tạo hóa đơn tháng</h2>
              <p>
                Hợp đồng đang hoạt động hoặc chờ tất toán có thể lập hóa đơn
                tháng. Hợp đồng chờ tất toán chỉ được lập kỳ bàn giao.
              </p>
              <div className="bill-form bill-form--inline">
                <label>
                  Hợp đồng
                  <select
                    value={contractId}
                    onChange={(event) => {
                      setContractId(event.target.value);
                      const contract = contracts.find(
                        (item) => item.id === event.target.value,
                      );
                      if (
                        contract?.status === "PendingSettlement" &&
                        contract.handoverOn
                      )
                        setMonth(contract.handoverOn.slice(0, 7));
                      setPreview(null);
                    }}
                  >
                    <option value="">Chọn hợp đồng</option>
                    {activeContracts.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.contractCode} · Phòng {roomName(item.roomId)}
                        {item.status === "PendingSettlement"
                          ? " · Hóa đơn cuối"
                          : ""}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Kỳ hóa đơn
                  <input
                    type="month"
                    min={
                      selectedContract?.status === "PendingSettlement" &&
                      selectedContract.handoverOn
                        ? selectedContract.handoverOn.slice(0, 7)
                        : "2000-01"
                    }
                    max={
                      selectedContract?.status === "PendingSettlement" &&
                      selectedContract.handoverOn
                        ? selectedContract.handoverOn.slice(0, 7)
                        : "2100-12"
                    }
                    value={month}
                    onChange={(event) => {
                      setMonth(event.target.value);
                      setPreview(null);
                    }}
                  />
                </label>
                <label>
                  Giảm trừ thủ công (₫)
                  <input
                    type="number"
                    min="0"
                    step="1"
                    value={discountAmount}
                    onChange={(event) => {
                      setDiscountAmount(event.target.value);
                      setPreview(null);
                    }}
                    placeholder="0"
                  />
                </label>
                {Number(discountAmount || 0) > 0 && (
                  <label>
                    Lý do giảm trừ *
                    <input
                      type="text"
                      maxLength={255}
                      value={discountReason}
                      onChange={(event) => {
                        setDiscountReason(event.target.value);
                        setPreview(null);
                      }}
                      placeholder="Ví dụ: hỗ trợ phí dịch vụ tháng đầu"
                    />
                  </label>
                )}
                <button
                  className="bill-button"
                  type="button"
                  disabled={!contractId || !month || busy || discountInvalid}
                  onClick={() =>
                    action(
                      () => billingApi.preview(propertyId, monthlyPayload),
                      "Đã tính bản xem trước.",
                      setPreview,
                    )
                  }
                >
                  Xem trước
                </button>
              </div>
              {preview && (
                <div className="bill-preview">
                  <h3>
                    Bản xem trước · {preview.month}/{preview.year}
                  </h3>
                  <p>Hạn thanh toán: {preview.dueDate}</p>
                  <MeterReadingSummary preview={preview} />
                  <InvoiceItems items={preview.items} />
                  {Number(discountAmount || 0) > 0 && (
                    <p className="bill-hint">
                      Giảm trừ {money(discountAmount)}: {discountReason}
                    </p>
                  )}
                  <div className="bill-total">
                    <span>Tổng cộng</span>
                    <strong>{money(preview.totalAmount)}</strong>
                  </div>
                  <button
                    className="bill-button"
                    type="button"
                    disabled={busy}
                    onClick={() =>
                      action(
                        () =>
                          billingApi.createMonthly(propertyId, monthlyPayload),
                        "Đã tạo hóa đơn nháp.",
                        (result) => {
                          setPreview(null);
                          navigate(`landlord/billing/${result.id}`);
                        },
                      )
                    }
                  >
                    Tạo bản nháp
                  </button>
                </div>
              )}
              {!preview && (
                <p className="bill-hint">
                  Cần có giá điện nước và chỉ số của cả điện, nước cho kỳ này.
                  Chọn tab “Giá, chỉ số và dịch vụ” để nhập.
                </p>
              )}
            </section>
            <section className="bill-card">
              <h2>Hóa đơn tiền cọc</h2>
              <p>
                Hợp đồng mới có tiền cọc sẽ tự phát hành hóa đơn. Với hợp đồng
                cũ chưa có hóa đơn cọc, mở chi tiết hợp đồng để tạo bản nháp và
                kiểm tra trước khi phát hành.
              </p>
              <div className="bill-actions">
                <button
                  className="bill-button bill-button--secondary"
                  type="button"
                  onClick={() => navigate("landlord/contracts/new")}
                >
                  Tạo hợp đồng mới
                </button>
                <button
                  className="bill-button bill-button--secondary"
                  type="button"
                  onClick={() => navigate("landlord/contracts")}
                >
                  Xem hợp đồng cũ
                </button>
              </div>
            </section>
          </div>
          <OneOffInvoiceForm
            propertyId={propertyId}
            contracts={contracts}
            roomName={roomName}
            onCreated={(result) => {
              setRevision((value) => value + 1);
              navigate(`landlord/billing/${result.id}`);
            }}
          />
          <section className="bill-card">
            <div className="bill-list-heading">
              <div>
                <h2>Danh sách hóa đơn</h2>
                {roomId && <p>Đang xem hóa đơn phòng {roomName(roomId)}.</p>}
              </div>
              <div className="bill-list-filters">
                <label>
                  Phòng
                  <select
                    value={roomId}
                    onChange={(event) => {
                      setRoomId(event.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="">Tất cả phòng</option>
                    {rooms.map((room) => (
                      <option key={room.id} value={room.id}>
                        {room.roomNumber}
                        {room.roomName ? ` · ${room.roomName}` : ""}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Trạng thái
                  <select
                    value={listStatus}
                    onChange={(event) => {
                      setListStatus(event.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="">Tất cả</option>
                    <option value="Draft">Bản nháp</option>
                    <option value="Issued">Đã phát hành</option>
                    <option value="Cancelled">Đã hủy</option>
                  </select>
                </label>
                <label>
                  Kỳ tháng
                  <input
                    type="month"
                    value={listPeriod}
                    onChange={(event) => {
                      setListPeriod(event.target.value);
                      setPage(1);
                    }}
                  />
                </label>
              </div>
            </div>
            {loading ? (
              <p role="status">Đang tải...</p>
            ) : invoices.length ? (
              <div className="bill-table-wrap">
                <table className="bill-table">
                  <thead>
                    <tr>
                      <th>Mã / Kỳ</th>
                      <th>Phòng</th>
                      <th>Người thuê theo hợp đồng</th>
                      <th>Hạn thanh toán</th>
                      <th>Trạng thái</th>
                      <th>Còn phải thu</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {invoices.map((item) => (
                      <tr key={item.id}>
                        <td>
                          <strong>{item.code}</strong>
                          <small>{monthLabel(item)}</small>
                        </td>
                        <td>{roomName(item.roomId)}</td>
                        <td className="bill-tenant-cell">
                          {tenantsFor(item.contractId)
                            ?.map((tenant) => tenant.fullName)
                            .filter(Boolean)
                            .join(", ") || "Chưa có thông tin"}
                        </td>
                        <td>{item.dueDate}</td>
                        <td>
                          <span
                            className="bill-payment-status"
                            data-status={item.displayStatus}
                          >
                            {statusLabel[item.displayStatus] ||
                              item.displayStatus}
                          </span>
                        </td>
                        <td>{money(item.amountDue)}</td>
                        <td>
                          <button
                            className="bill-link"
                            type="button"
                            onClick={() =>
                              navigate(`landlord/billing/${item.id}`)
                            }
                          >
                            Xem chi tiết
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="bill-empty">
                {roomId ? "Phòng này chưa có hóa đơn." : "Chưa có hóa đơn."}
              </p>
            )}
            <div className="bill-pagination">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((value) => value - 1)}
              >
                Trang trước
              </button>
              <span>
                Trang {page} / {Math.max(1, meta?.totalPages || 1)}
              </span>
              <button
                type="button"
                disabled={page >= (meta?.totalPages || 1)}
                onClick={() => setPage((value) => value + 1)}
              >
                Trang sau
              </button>
            </div>
          </section>
        </>
      )}
      {error && (
        <p className="bill-error" role="alert">
          {error}
        </p>
      )}
      {success && (
        <p className="bill-success" role="status">
          {success}
        </p>
      )}
      {selectedContract && (
        <span className="sr-only">
          Hợp đồng đã chọn: {selectedContract.contractCode}
        </span>
      )}
    </div>
  );
}

function InvoiceDetailPage({ invoiceId, navigate }) {
  const [detail, setDetail] = useState(null);
  const [location, setLocation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [revision, setRevision] = useState(0);
  const [selectedAction, setSelectedAction] = useState("");
  const [pendingPayments, setPendingPayments] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    billingApi
      .landlordPayments(invoiceId, controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted)
          setPendingPayments(
            (rows || []).filter((row) => row.status === "Pending").length,
          );
      })
      .catch(() => {
        if (!controller.signal.aborted) setPendingPayments(0);
      });
    return () => controller.abort();
  }, [invoiceId, revision]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    billingApi
      .invoice(invoiceId, controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) setDetail(response);
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorOf(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [invoiceId, revision]);

  useEffect(() => {
    if (!detail?.summary?.roomId) return;
    const controller = new AbortController();
    billingApi
      .properties(controller.signal)
      .then(async (response) => {
        const results = await Promise.allSettled(
          (response.data || []).map(async (property) => {
            const rooms = await billingApi.rooms(
              property.id,
              controller.signal,
            );
            const room = (rooms.data || []).find(
              (item) => item.id === detail.summary.roomId,
            );
            return room ? { property, room } : null;
          }),
        );
        if (!controller.signal.aborted)
          setLocation(
            results.find(
              (result) => result.status === "fulfilled" && result.value,
            )?.value || null,
          );
      })
      .catch(() => {
        if (!controller.signal.aborted) setLocation(null);
      });
    return () => controller.abort();
  }, [detail?.summary?.roomId]);

  async function action(request, message) {
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await request();
      setSuccess(message);
      setSelectedAction("");
      setRevision((value) => value + 1);
    } catch (cause) {
      setError(errorOf(cause));
    } finally {
      setBusy(false);
    }
  }

  const summary = detail?.summary;
  return (
    <div className="bill-page bill-page--detail">
      <button
        className="bill-back"
        type="button"
        onClick={() => navigate("landlord/billing")}
      >
        ← Danh sách hóa đơn
      </button>
      <header className="bill-header">
        <div>
          <span>CHI TIẾT HÓA ĐƠN</span>
          <h1>{summary ? summary.code : "Hóa đơn"}</h1>
          <p>
            {summary
              ? `${location?.property.name || "Khu trọ"} · Phòng ${location?.room.roomNumber || summary.roomId.slice(0, 8)} · ${monthLabel(summary)}`
              : "Xem các khoản tiền và trạng thái thanh toán."}
          </p>
        </div>
        {summary && (
          <strong
            className="bill-detail-status"
            data-status={summary.displayStatus}
          >
            {statusLabel[summary.displayStatus] || summary.displayStatus}
          </strong>
        )}
      </header>
      {loading && (
        <p className="bill-card" role="status">
          Đang tải chi tiết hóa đơn…
        </p>
      )}
      {error && (
        <p className="bill-error" role="alert">
          {error}{" "}
          {!summary && (
            <button
              className="bill-link"
              type="button"
              onClick={() => setRevision((value) => value + 1)}
            >
              Thử lại
            </button>
          )}
        </p>
      )}
      {success && (
        <p className="bill-success" role="status">
          {success}
        </p>
      )}
      {summary && (
        <>
          <div className="bill-detail-actions" aria-label="Xử lý hóa đơn">
            <button
              type="button"
              className="bill-button"
              disabled={
                summary.status === "Cancelled" ||
                (summary.status === "Issued" &&
                  !["Monthly", "Damage", "Other"].includes(summary.type))
              }
              onClick={() =>
                setSelectedAction(
                  summary.status === "Draft" ? "edit" : "adjustment",
                )
              }
            >
              Phụ thu giảm trừ
            </button>
            <button
              type="button"
              className={`bill-button bill-button--secondary ${pendingPayments ? "bill-button--attention" : ""}`}
              disabled={summary.status !== "Issued"}
              onClick={() => setSelectedAction("review")}
            >
              Duyệt yêu cầu thanh toán
              {pendingPayments > 0 && (
                <span className="bill-action-count">{pendingPayments}</span>
              )}
            </button>
            <button
              type="button"
              className="bill-button bill-button--secondary"
              disabled={summary.status !== "Issued"}
              onClick={() => setSelectedAction("record")}
            >
              Ghi nhận thanh toán thủ công
            </button>
            {pendingPayments > 0 && (
              <p className="bill-action-note" role="status">
                Có {pendingPayments} yêu cầu thanh toán đang chờ duyệt.
              </p>
            )}
          </div>
          <section className="bill-card" aria-labelledby="bill-detail-title">
            <div className="bill-detail-head">
              <h2 id="bill-detail-title">Thông tin hóa đơn</h2>
              {location && (
                <button
                  className="bill-link"
                  type="button"
                  onClick={() =>
                    navigate(
                      `landlord/billing/rooms/${location.property.id}/${location.room.id}`,
                    )
                  }
                >
                  Xem hóa đơn của phòng này
                </button>
              )}
            </div>
            {Number(summary.creditToRefund) > 0 ? (
              <div className="bill-flow-hero bill-flow-hero--credit">
                <div className="bill-flow-hero__column">
                  <span>
                    {summary.type === "Deposit" ? "TIỀN CỌC" : "HÓA ĐƠN"}
                  </span>
                  <strong>{money(0)}</strong>
                  <small>Đã thanh toán thành công</small>
                </div>
                <div className="bill-flow-hero__column bill-flow-hero__credit">
                  <span>TIỀN DƯ</span>
                  <strong>{money(summary.creditToRefund)}</strong>
                  <small>Cần đối soát và hoàn tiền</small>
                </div>
              </div>
            ) : (
              <div className="bill-flow-hero">
                <span>
                  {summary.type === "Deposit" ? "TIỀN CỌC" : "HÓA ĐƠN"}
                </span>
                <strong>{money(Math.max(0, Number(summary.amountDue)))}</strong>
                <small>
                  {Number(summary.amountDue) > 0
                    ? "Còn phải thu"
                    : "Đã thanh toán đủ"}
                </small>
              </div>
            )}
            <InvoiceSummary
              summary={summary}
              roomName={(id) =>
                id === location?.room.id ? location.room.roomNumber : id
              }
            />
          </section>
          <section
            className="bill-card bill-primary-context"
            aria-labelledby="bill-primary-context-title"
          >
            <h2 id="bill-primary-context-title">Phòng và người thuê chính</h2>
            <dl className="bill-summary">
              <div>
                <dt>Khu trọ</dt>
                <dd>{location?.property.name || "Đang cập nhật"}</dd>
              </div>
              <div>
                <dt>Phòng</dt>
                <dd>
                  {location?.room.roomNumber || summary.roomId.slice(0, 8)}
                </dd>
              </div>
              <div>
                <dt>Người thuê chính</dt>
                <dd>
                  {detail.tenants?.find((person) => person.isContactPerson)
                    ?.fullName ||
                    detail.tenants?.[0]?.fullName ||
                    "Chưa có thông tin"}
                </dd>
              </div>
            </dl>
          </section>
          <section className="bill-card">
            <h2>Các khoản trên hóa đơn</h2>
            <MeterReadingSummary preview={detail} />
            <InvoiceItems items={detail.items} />
          </section>
          <section
            className="bill-card"
            aria-labelledby="bill-adjustment-history-title"
          >
            <h2 id="bill-adjustment-history-title">
              Lịch sử phụ thu, giảm trừ
            </h2>
            <InvoiceAdjustmentHistory adjustments={detail.adjustments || []} />
          </section>
          <InvoicePaymentHistory
            key={`${invoiceId}:${revision}`}
            invoiceId={invoiceId}
          />
          {summary.status !== "Cancelled" && (
            <div className="bill-detail-secondary-actions">
              {summary.status === "Draft" && (
                <button
                  className="bill-link"
                  type="button"
                  onClick={() => setSelectedAction("issue")}
                >
                  Phát hành hóa đơn
                </button>
              )}
              {summary.status === "Issued" &&
                Number(summary.creditToRefund) > 0 && (
                  <button
                    className="bill-link"
                    type="button"
                    onClick={() => setSelectedAction("refund")}
                  >
                    Hoàn tiền thu vượt
                  </button>
                )}
              <button
                className="bill-link bill-link--danger"
                type="button"
                onClick={() => setSelectedAction("cancel")}
              >
                Hủy hóa đơn
              </button>
            </div>
          )}
          {selectedAction && (
            <InvoiceActionDialog
              title={
                {
                  edit: "Chỉnh các khoản hóa đơn nháp",
                  adjustment: "Phụ thu và giảm trừ",
                  review: "Duyệt yêu cầu thanh toán",
                  record: "Ghi nhận thanh toán thủ công",
                  refund: "Hoàn tiền thu vượt",
                  issue: "Phát hành hóa đơn",
                  cancel: "Hủy hóa đơn",
                }[selectedAction]
              }
              onClose={() => setSelectedAction("")}
            >
              {selectedAction === "edit" && summary.status === "Draft" && (
                <DraftItemsEditor
                  key={`${invoiceId}:${revision}`}
                  invoiceId={invoiceId}
                  items={detail.items}
                  initialOpen
                  onChanged={(message) => {
                    setSuccess(message);
                    setRevision((value) => value + 1);
                    setSelectedAction("");
                  }}
                />
              )}
              {selectedAction === "adjustment" &&
                ["Monthly", "Damage", "Other"].includes(summary.type) &&
                summary.status === "Issued" && (
                  <InvoiceAdjustmentEditor
                    invoiceId={invoiceId}
                    adjustments={detail.adjustments || []}
                    summary={summary}
                    onChanged={(message) => {
                      setSuccess(message);
                      setRevision((value) => value + 1);
                    }}
                  />
                )}
              {(selectedAction === "review" || selectedAction === "record") &&
                summary.status === "Issued" && (
                  <LandlordPaymentPanel
                    invoiceId={invoiceId}
                    amountDue={summary.amountDue}
                    creditToRefund={summary.creditToRefund}
                    mode={selectedAction}
                    onChanged={(message) => {
                      setSuccess(message);
                      setRevision((value) => value + 1);
                    }}
                    onRefresh={() => setRevision((value) => value + 1)}
                  />
                )}
              {selectedAction === "refund" && summary.status === "Issued" && (
                <OverpaymentRefund
                  invoiceId={invoiceId}
                  creditToRefund={summary.creditToRefund}
                  onChanged={(message) => {
                    setSuccess(message);
                    setSelectedAction("");
                    setRevision((value) => value + 1);
                  }}
                />
              )}
              {(selectedAction === "issue" || selectedAction === "cancel") && (
                <div className="bill-card bill-actions">
                  {selectedAction === "issue" && summary.status === "Draft" && (
                    <button
                      type="button"
                      className="bill-button"
                      disabled={busy}
                      onClick={() => {
                        if (
                          window.confirm(
                            "Phát hành hóa đơn này? Người thuê sẽ nhìn thấy hóa đơn.",
                          )
                        )
                          action(
                            () => billingApi.issue(invoiceId),
                            "Đã phát hành hóa đơn.",
                          );
                      }}
                    >
                      Phát hành
                    </button>
                  )}
                  {selectedAction === "cancel" &&
                    summary.status !== "Cancelled" && (
                      <button
                        type="button"
                        className="bill-button bill-button--danger"
                        disabled={busy}
                        onClick={() => {
                          if (
                            window.confirm(
                              "Hủy hóa đơn này? Thao tác không thể hoàn tác.",
                            )
                          )
                            action(
                              () => billingApi.cancel(invoiceId),
                              "Đã hủy hóa đơn.",
                            );
                        }}
                      >
                        Hủy hóa đơn
                      </button>
                    )}
                </div>
              )}
            </InvoiceActionDialog>
          )}
        </>
      )}
    </div>
  );
}

export function InvoiceTenants({ tenants, selfOnly = false }) {
  return (
    <section
      className="bill-card bill-tenants"
      aria-label={
        selfOnly ? "Thông tin người thuê của bạn" : "Người thuê trong hợp đồng"
      }
    >
      <div className="bill-tenants-heading">
        <div>
          <h2>
            {selfOnly ? "Thông tin người thuê" : "Người thuê trong hợp đồng"}
          </h2>
          <p>
            {selfOnly
              ? "Thông tin của bạn trong hợp đồng gắn với hóa đơn."
              : "Thông tin thành viên được ghi nhận trong hợp đồng gắn với hóa đơn."}
          </p>
        </div>
      </div>
      {!Array.isArray(tenants) ? (
        <p className="bill-empty">
          Không có dữ liệu người thuê. Vui lòng tải lại trang hoặc kiểm tra hợp
          đồng.
        </p>
      ) : tenants.length === 0 ? (
        <p className="bill-empty">Hợp đồng chưa có thông tin người thuê.</p>
      ) : (
        <ul className="bill-tenant-list">
          {tenants.map((tenant, index) => (
            <li
              key={`${tenant.fullName || "tenant"}-${tenant.joinDate || ""}-${index}`}
            >
              <span className="bill-tenant-avatar" aria-hidden="true">
                {tenant.fullName?.trim().charAt(0).toLocaleUpperCase("vi-VN") ||
                  "N"}
              </span>
              <div className="bill-tenant-content">
                <div className="bill-tenant-name">
                  <strong>{tenant.fullName || "Chưa có tên"}</strong>
                  {tenant.isContactPerson && <span>Người liên hệ</span>}
                </div>
                <dl>
                  <div>
                    <dt>Số điện thoại</dt>
                    <dd>{tenant.phoneNumber || "Chưa có"}</dd>
                  </div>
                  <div>
                    <dt>Email</dt>
                    <dd>{tenant.email || "Chưa có"}</dd>
                  </div>
                  <div>
                    <dt>Ngày tham gia</dt>
                    <dd>
                      {tenant.joinDate
                        ? tenant.joinDate.split("-").reverse().join("/")
                        : "Chưa có"}
                    </dd>
                  </div>
                  {tenant.leaveDate && (
                    <div>
                      <dt>Ngày rời</dt>
                      <dd>{tenant.leaveDate.split("-").reverse().join("/")}</dd>
                    </div>
                  )}
                </dl>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function MeterReadingSummary({ preview }) {
  const readings = [
    ["Điện", preview?.electricityReading, "kWh"],
    ["Nước", preview?.waterReading, "m³"],
  ].filter(([, reading]) => reading);
  if (!readings.length) return null;
  return (
    <div className="bill-table-wrap">
      <table className="bill-table">
        {/* <caption>Chỉ số và tiền điện nước của kỳ</caption> */}
        <thead>
          <tr>
            <th>Loại</th>
            <th>Chỉ số đầu → cuối</th>
            <th>Tiêu thụ</th>
            <th>Đơn giá</th>
            <th>Thành tiền</th>
          </tr>
        </thead>
        <tbody>
          {readings.map(([label, reading, unit]) => (
            <tr key={label}>
              <td>{label}</td>
              <td>
                {reading.previousValue} → {reading.currentValue}
              </td>
              <td>
                {reading.usage} {unit}
              </td>
              <td>{money(reading.unitPrice)}</td>
              <td>{money(reading.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function InvoiceItems({ items = [] }) {
  return (
    <div className="bill-table-wrap">
      <table className="bill-table">
        <thead>
          <tr>
            <th>Khoản tiền</th>
            <th>Số lượng</th>
            <th>Đơn giá</th>
            <th>Thành tiền</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, index) => (
            <tr key={`${item.id || item.type}-${index}`}>
              <td>{item.description}</td>
              <td>{item.quantity}</td>
              <td>{money(item.unitPrice)}</td>
              <td>{money(item.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function InvoiceSummary({ summary, roomName }) {
  const applied = paymentAppliedToInvoice(summary);
  return (
    <dl className="bill-summary">
      <div>
        <dt>Mã</dt>
        <dd>{summary.code}</dd>
      </div>
      <div>
        <dt>Phòng</dt>
        <dd>{roomName?.(summary.roomId) || "Xem trong hợp đồng"}</dd>
      </div>
      <div>
        <dt>Trạng thái</dt>
        <dd>{statusLabel[summary.displayStatus] || summary.displayStatus}</dd>
      </div>
      <div>
        <dt>Hạn thanh toán</dt>
        <dd>{summary.dueDate}</dd>
      </div>
      <div>
        <dt>Tổng ban đầu</dt>
        <dd>{money(summary.baseTotal)}</dd>
      </div>
      <div>
        <dt>Tổng hiệu lực</dt>
        <dd>{money(summary.effectiveTotal)}</dd>
      </div>
      <div>
        <dt>Tổng tiền đã nhận</dt>
        <dd>{money(summary.totalPaid)}</dd>
      </div>
      <div>
        <dt>Đã thanh toán vào hóa đơn</dt>
        <dd>{money(applied)}</dd>
      </div>
      <div>
        <dt>Đã hoàn tiền</dt>
        <dd>{money(summary.totalRefunded)}</dd>
      </div>
      <div>
        <dt>Đã dùng cọc</dt>
        <dd>{money(summary.totalDepositApplied)}</dd>
      </div>
      <div>
        <dt>Còn phải thu</dt>
        <dd>
          <strong>{money(summary.amountDue)}</strong>
        </dd>
      </div>
      {Number(summary.creditToRefund) > 0 && (
        <div>
          <dt>Tiền thu vượt cần hoàn</dt>
          <dd>{money(summary.creditToRefund)}</dd>
        </div>
      )}
    </dl>
  );
}
