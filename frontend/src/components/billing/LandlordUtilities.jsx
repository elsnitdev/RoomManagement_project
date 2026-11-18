import { useEffect, useState } from "react";
import { billingApi } from "../../services/api/billingApi";
import BillingInputs from "./BillingInputs";
import MeterReadingImport from "./MeterReadingImport";
import { previousBillingMonth } from "./billingPeriod";
import "../../styles/billing-live.css";

export default function LandlordUtilities({ initialPropertyId, initialTab, context }) {
  const [properties, setProperties] = useState(null);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  const [propertyId, setPropertyId] = useState(initialPropertyId || "");
  const [tab, setTab] = useState(initialTab === "inputs" ? "inputs" : "meters");
  const [month, setMonth] = useState(() =>
    initialTab === "meters" && /^\d{4}-(0[1-9]|1[0-2])$/.test(context) ? context : previousBillingMonth());
  const [roomId, setRoomId] = useState(initialTab === "inputs" ? context : "");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    billingApi.properties(controller.signal)
      .then(({ data }) => {
        if (controller.signal.aborted) return;
        const available = data || [];
        setProperties(available);
        setPropertyId((id) => available.some((property) => property.id === id) ? id : available[0]?.id || "");
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(cause.message || "Không tải được danh sách khu trọ.");
      });
    return () => controller.abort();
  }, [retry]);

  return (
    <div className="bill-page">
      <header className="bill-header">
        <div><h1>Điện nước và dịch vụ</h1></div>
        <label>
          Khu trọ
          <select value={propertyId} disabled={saving || !properties?.length} onChange={(event) => {
            setRoomId("");
            setPropertyId(event.target.value);
          }}>
            <option value="">Chọn khu trọ</option>
            {properties?.map((property) => <option key={property.id} value={property.id}>{property.name}</option>)}
          </select>
        </label>
      </header>
      <div className="bill-tabs bill-tabs--billing" role="group" aria-label="Điện nước và dịch vụ">
        {[["meters", "Chỉ số điện nước"], ["inputs", "Giá và dịch vụ"]].map(([id, label]) => (
          <button key={id} type="button" aria-pressed={tab === id} className={tab === id ? "is-active" : ""}
            disabled={saving} onClick={() => setTab(id)}>{label}</button>
        ))}
      </div>
      {error ? (
        <div>
          <p className="bill-error" role="alert">{error}</p>
          <button className="bill-button bill-button--secondary" type="button" onClick={() => {
            setError("");
            setRetry((value) => value + 1);
          }}>Thử lại</button>
        </div>
      ) : !properties ? (
        <p role="status">Đang tải khu trọ…</p>
      ) : !propertyId ? (
        <p className="bill-empty">Chọn khu trọ để quản lý điện nước và dịch vụ.</p>
      ) : tab === "meters" ? (
        <MeterReadingImport key={propertyId} propertyId={propertyId} month={month} onMonthChange={setMonth}
          saving={saving} onSavingChange={setSaving} />
      ) : (
        <UtilitySettings key={propertyId} propertyId={propertyId}
          initialRoomId={propertyId === initialPropertyId ? roomId : ""} />
      )}
    </div>
  );
}

function UtilitySettings({ propertyId, initialRoomId }) {
  const [rooms, setRooms] = useState(null);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    billingApi.rooms(propertyId, controller.signal)
      .then(({ data }) => {
        if (!controller.signal.aborted) setRooms(data || []);
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(cause.message || "Không tải được danh sách phòng.");
      });
    return () => controller.abort();
  }, [propertyId, retry]);

  if (error) return (
    <div>
      <p className="bill-error" role="alert">{error}</p>
      <button className="bill-button bill-button--secondary" type="button" onClick={() => {
        setError("");
        setRetry((value) => value + 1);
      }}>Thử lại</button>
    </div>
  );
  if (!rooms) return <p role="status">Đang tải danh sách phòng…</p>;
  return <BillingInputs propertyId={propertyId} rooms={rooms} initialRoomId={initialRoomId} />;
}
