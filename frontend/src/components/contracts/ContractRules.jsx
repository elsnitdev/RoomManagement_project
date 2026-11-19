import { useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Download,
  ExternalLink,
  FilePlus2,
  FileText,
  Pencil,
  RefreshCw,
  ShieldCheck,
  ZoomIn,
} from "lucide-react";
import { contractApi } from "../../services/api/contractApi";
import ImageLightbox from "../common/ImageLightbox";
import "./contractRules.css";

const errorText = (error) =>
  error?.message || "Không thể thực hiện thao tác. Vui lòng thử lại.";
const formatTime = (value) =>
  value
    ? new Intl.DateTimeFormat("vi-VN", {
        dateStyle: "short",
        timeStyle: "short",
        timeZone: "Asia/Ho_Chi_Minh",
      }).format(new Date(value))
    : "—";

const isImageEvidence = (item) => {
  const type = String(item?.contentType || "").toLowerCase();
  const name = String(item?.fileName || "").toLowerCase();
  return (
    type.startsWith("image/") ||
    /\.(jpe?g|png|webp)$/i.test(name)
  );
};

function Evidence({ contractId, linkId, propertyId, canUpload }) {
  const [files, setFiles] = useState([]);
  const [thumbs, setThumbs] = useState({});
  const [thumbFailed, setThumbFailed] = useState({});
  const [lightboxIndex, setLightboxIndex] = useState(-1);
  const [file, setFile] = useState(null);
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    const request = propertyId
      ? contractApi.tenantEvidence(
          propertyId,
          contractId,
          linkId,
          controller.signal,
        )
      : contractApi.myEvidence(contractId, linkId, controller.signal);
    request
      .then((rows) => {
        if (!controller.signal.aborted) setFiles(rows || []);
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorText(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, contractId, linkId, revision]);

  useEffect(() => {
    const imageItems = (files || []).filter(isImageEvidence);
    setThumbs({});
    setThumbFailed({});
    if (!imageItems.length) return undefined;

    const controller = new AbortController();
    const createdUrls = [];

    imageItems.forEach(async (item) => {
      try {
        const blob = propertyId
          ? await contractApi.tenantEvidenceContent(
              propertyId,
              contractId,
              linkId,
              item.id,
              controller.signal,
            )
          : await contractApi.myEvidenceContent(
              contractId,
              linkId,
              item.id,
              controller.signal,
            );
        const url = URL.createObjectURL(blob);
        if (controller.signal.aborted) {
          URL.revokeObjectURL(url);
          return;
        }
        createdUrls.push(url);
        setThumbs((prev) => ({ ...prev, [item.id]: url }));
      } catch (cause) {
        if (!controller.signal.aborted && cause.name !== "AbortError") {
          setThumbFailed((prev) => ({ ...prev, [item.id]: true }));
        }
      }
    });

    return () => {
      controller.abort();
      createdUrls.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [files, propertyId, contractId, linkId]);

  const lightboxItems = useMemo(
    () =>
      (files || [])
        .filter((item) => isImageEvidence(item) && thumbs[item.id])
        .map((item) => ({
          id: item.id,
          src: thumbs[item.id],
          title: item.fileName,
          downloadName: item.fileName,
        })),
    [files, thumbs],
  );

  function openImage(itemId) {
    const idx = lightboxItems.findIndex((entry) => entry.id === itemId);
    if (idx >= 0) setLightboxIndex(idx);
  }

  async function upload(event) {
    event.preventDefault();
    if (!file) return;
    if (
      !["image/jpeg", "image/png", "image/webp", "application/pdf"].includes(
        file.type,
      ) ||
      file.size > 10 * 1024 * 1024
    ) {
      setError(
        "Chọn ảnh JPEG, PNG, WebP hoặc PDF có dung lượng tối đa 10 MiB.",
      );
      return;
    }
    setBusy(true);
    setError("");
    try {
      await contractApi.uploadEvidence(contractId, linkId, file);
      setFile(null);
      event.target.reset();
      setRevision((x) => x + 1);
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setBusy(false);
    }
  }

  async function fetchContentBlob(item) {
    return propertyId
      ? await contractApi.tenantEvidenceContent(
          propertyId,
          contractId,
          linkId,
          item.id,
        )
      : await contractApi.myEvidenceContent(contractId, linkId, item.id);
  }

  async function download(item) {
    setError("");
    setBusy(true);
    try {
      const blob = await fetchContentBlob(item);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = item.fileName;
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setBusy(false);
    }
  }

  async function openPdf(item) {
    setError("");
    setBusy(true);
    try {
      const blob = await fetchContentBlob(item);
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener,noreferrer");
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rules-evidence">
      <strong>
        Bằng chứng bản ký giấy <span>(không bắt buộc)</span>
      </strong>
      {loading ? (
        <p role="status">Đang tải bằng chứng…</p>
      ) : files?.length ? (
        <ul className="rules-evidence-grid">
          {files.map((item) => {
            const isImage = isImageEvidence(item);
            const thumbUrl = thumbs[item.id];
            const metaText = `${
              item.isLegacyContractRules
                ? "Nội quy hợp đồng cũ"
                : "Nội quy khu trọ"
            } · Phiên bản ${item.versionNumber} · ${formatTime(item.createdAt)}`;

            if (isImage) {
              return (
                <li key={item.id} className="rules-evidence-card">
                  <div className="rules-evidence-card__thumb">
                    {thumbUrl ? (
                      <button
                        type="button"
                        className="rules-evidence-card__preview"
                        aria-label={`Xem phóng to ${item.fileName}`}
                        onClick={() => openImage(item.id)}
                      >
                        <img src={thumbUrl} alt={item.fileName} />
                        <span
                          className="rules-evidence-card__zoom"
                          aria-hidden="true"
                        >
                          <ZoomIn size={17} />
                        </span>
                      </button>
                    ) : (
                      <div className="rules-evidence-card__loading">
                        {thumbFailed[item.id]
                          ? "Không tải được ảnh"
                          : "Đang tải ảnh…"}
                      </div>
                    )}
                  </div>
                  <div className="rules-evidence-card__body">
                    <div className="rules-evidence-card__title-row">
                      <span
                        className="rules-evidence-card__name"
                        title={item.fileName}
                      >
                        {item.fileName}
                      </span>
                      <button
                        type="button"
                        className="rules-evidence-card__dl"
                        aria-label={`Tải xuống ${item.fileName}`}
                        title="Tải về"
                        disabled={busy}
                        onClick={() => download(item)}
                      >
                        <Download size={14} aria-hidden="true" />
                      </button>
                    </div>
                    <small>{metaText}</small>
                  </div>
                </li>
              );
            }

            return (
              <li
                key={item.id}
                className="rules-evidence-card rules-evidence-card--pdf"
              >
                <div className="rules-evidence-card__pdf-head">
                  <span className="rules-evidence-card__pdf-icon">
                    <FileText size={20} aria-hidden="true" />
                  </span>
                  <span className="rules-evidence-card__pdf-badge">PDF</span>
                </div>
                <div className="rules-evidence-card__body">
                  <span
                    className="rules-evidence-card__name"
                    title={item.fileName}
                  >
                    {item.fileName}
                  </span>
                  <small>{metaText}</small>
                  <div className="rules-evidence-card__pdf-actions">
                    <button
                      type="button"
                      className="rules-evidence-card__action-btn"
                      disabled={busy}
                      onClick={() => openPdf(item)}
                    >
                      <ExternalLink size={13} aria-hidden="true" />
                      Mở xem
                    </button>
                    <button
                      type="button"
                      className="rules-evidence-card__action-btn"
                      disabled={busy}
                      onClick={() => download(item)}
                    >
                      <Download size={13} aria-hidden="true" />
                      Tải xuống
                    </button>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      ) : (
        <p>Chưa có bằng chứng được tải lên.</p>
      )}
      {error && (
        <p className="rules-error" role="alert">
          {error}{" "}
          <button type="button" onClick={() => setRevision((x) => x + 1)}>
            Thử tải lại
          </button>
        </p>
      )}
      <ImageLightbox
        items={lightboxItems}
        index={lightboxIndex}
        onClose={() => setLightboxIndex(-1)}
        onIndexChange={setLightboxIndex}
      />
      {!propertyId &&
        (canUpload ? (
          <form onSubmit={upload}>
            <label>
              Chọn ảnh hoặc PDF (tối đa 10 MiB)
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp,application/pdf"
                onChange={(event) => setFile(event.target.files?.[0] || null)}
              />
            </label>
            <button
              type="submit"
              className="contract-button contract-button--secondary"
              disabled={!file || busy}
            >
              {busy ? "Đang tải lên…" : "Tải bằng chứng"}
            </button>
          </form>
        ) : (
          <p>Chấp thuận phiên bản hiện tại trước khi tải bằng chứng.</p>
        ))}
    </div>
  );
}

export function LandlordRules({ propertyId, contractId, detail, navigate }) {
  return (
    <section
      className="ct-card rules-panel"
      aria-labelledby="owner-rules-title"
    >
      <div className="rules-heading">
        <div>
          <span className="rules-kicker">NỘI QUY KHU TRỌ</span>
          <h2 id="owner-rules-title">Chấp thuận nội quy</h2>
          <p>Nội quy chung áp dụng cho tất cả hợp đồng tại khu trọ.</p>
        </div>
        <button
          type="button"
          className="contract-button contract-button--secondary"
          onClick={() => navigate(`landlord/properties/${propertyId}/rules`)}
        >
          Quản lý nội quy khu trọ
        </button>
      </div>
      <div className="rules-people">
        <h3>Chấp thuận của người thuê</h3>
        {detail.tenants?.length ? (
          <ul>
            {detail.tenants.map((person) => (
              <li key={person.id}>
                <div>
                  <strong>{person.fullName}</strong>
                  <small>
                    {person.leaveDate
                      ? "Đã ghi ngày rời"
                      : person.hasAcceptedCurrentRules
                        ? "Đã chấp thuận phiên bản hiện tại"
                        : "Chưa chấp thuận phiên bản hiện tại"}
                  </small>
                </div>
                <details>
                  <summary>Bằng chứng bản ký</summary>
                  <Evidence
                    propertyId={propertyId}
                    contractId={contractId}
                    linkId={person.id}
                  />
                </details>
              </li>
            ))}
          </ul>
        ) : (
          <p>Thêm người thuê vào hợp đồng để theo dõi chấp thuận.</p>
        )}
      </div>
    </section>
  );
}

export function PropertyRules({ propertyId, navigate, embedded = false }) {
  const [rules, setRules] = useState(null);
  const [content, setContent] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [editing, setEditing] = useState(false);
  const [notice, setNotice] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    setNotice("");
    setEditing(false);
    contractApi
      .rules(propertyId, controller.signal)
      .then((result) => {
        setRules(result);
        setContent(result.content || "");
      })
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorText(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [propertyId, revision]);
  async function save(event) {
    event.preventDefault();
    if (
      rules.isPublished &&
      content.trim() !== rules.content &&
      !window.confirm(
        "Lưu phiên bản mới sẽ yêu cầu người thuê chấp thuận lại. Tiếp tục?",
      )
    )
      return;
    setBusy(true);
    setError("");
    try {
      const result = await contractApi.saveRules(propertyId, {
        content,
        expectedVersionId: rules.versionId || null,
      });
      setRules(result);
      setContent(result.content);
      setEditing(false);
      setNotice("Đã lưu điều khoản đang áp dụng cho khu trọ.");
    } catch (cause) {
      setError(
        cause.status === 409
          ? "Điều khoản đã thay đổi ở nơi khác. Hãy sao chép nội dung đang soạn rồi tải lại trước khi lưu."
          : errorText(cause),
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className={`ct-card rules-panel property-rules${embedded ? " property-rules--embedded" : ""}`}
      aria-labelledby="owner-rules-title"
    >
      {!embedded && (
        <button
          type="button"
          className="contract-button contract-button--secondary"
          onClick={() => navigate(`landlord/properties/${propertyId}`)}
        >
          ← Quay lại khu trọ
        </button>
      )}
      <div className="rules-heading property-rules__heading">
        <div className="property-rules__intro">
          <span className="property-rules__icon">
            <FileText size={21} aria-hidden="true" />
          </span>
          <div>
            <span className="rules-kicker">QUY ĐỊNH CHUNG</span>
            <h2 id="owner-rules-title">Nội quy & điều khoản</h2>
            <p>Áp dụng cho các hợp đồng trong khu trọ này.</p>
          </div>
        </div>
        {!loading && rules && (
          <span
            className={`rules-badge ${rules.isPublished ? "rules-badge--accepted" : ""}`}
          >
            {rules.isPublished
              ? `Đang áp dụng · Phiên bản ${rules.versionNumber}`
              : "Chưa thiết lập"}
          </span>
        )}
      </div>
      {loading ? (
        <div className="property-rules__state" role="status">
          Đang tải điều khoản…
        </div>
      ) : rules ? (
        <>
          {notice && (
            <div className="property-rules__notice" role="status">
              <CheckCircle2 size={17} />
              {notice}
            </div>
          )}
          {editing ? (
            <form className="property-rules__editor" onSubmit={save}>
              <label htmlFor="rules-content">Nội dung điều khoản</label>
              <p>Chỉnh sửa nội dung phù hợp với khu trọ trước khi lưu.</p>
              <textarea
                id="rules-content"
                rows={11}
                maxLength={10000}
                value={content}
                disabled={busy}
                onChange={(event) => setContent(event.target.value)}
              />
              <div className="rules-actions">
                <small>
                  {content.length.toLocaleString("vi-VN")}/10.000 ký tự
                  {rules.isPublished &&
                    " · Người thuê cần chấp thuận phiên bản mới."}
                </small>
                <div className="property-rules__buttons">
                  <button
                    type="button"
                    className="contract-button contract-button--secondary"
                    disabled={busy}
                    onClick={() => {
                      setContent(rules.content || "");
                      setEditing(false);
                      setError("");
                    }}
                  >
                    Hủy
                  </button>
                  <button
                    className="contract-button contract-button--primary"
                    disabled={
                      busy ||
                      !content.trim() ||
                      (rules.isPublished && content.trim() === rules.content)
                    }
                  >
                    {busy
                      ? "Đang lưu…"
                      : rules.isPublished
                        ? "Lưu phiên bản mới"
                        : "Tạo điều khoản"}
                  </button>
                </div>
              </div>
            </form>
          ) : rules.isPublished ? (
            <>
              <div className="property-rules__content">
                <span>NỘI DUNG ĐANG ÁP DỤNG</span>
                <div>{rules.content}</div>
              </div>
              <div className="property-rules__footer">
                <p>Người thuê sẽ cần chấp thuận lại khi nội dung thay đổi.</p>
                <button
                  type="button"
                  className="contract-button contract-button--secondary"
                  onClick={() => {
                    setEditing(true);
                    setNotice("");
                  }}
                >
                  <Pencil size={16} /> Chỉnh sửa
                </button>
              </div>
            </>
          ) : (
            <div className="property-rules__empty">
              <span className="property-rules__empty-icon">
                <FilePlus2 size={24} aria-hidden="true" />
              </span>
              <div>
                <h3>Chưa có điều khoản cho khu trọ</h3>
                <p>
                  Tạo nội quy chung để người thuê đọc và chấp thuận trước khi
                  kích hoạt hợp đồng.
                </p>
                <button
                  type="button"
                  className="contract-button contract-button--primary"
                  onClick={() => setEditing(true)}
                >
                  <FilePlus2 size={16} /> Thêm điều khoản
                </button>
              </div>
            </div>
          )}
        </>
      ) : null}
      {error && (
        <div className="property-rules__error" role="alert">
          <AlertCircle size={18} />
          <div>
            <strong>Không thể tải hoặc lưu điều khoản</strong>
            <p>
              {error === "Not Found"
                ? "Không tìm thấy API hoặc khu trọ. Hãy kiểm tra backend đã được cập nhật, khởi động lại và tài khoản có quyền truy cập."
                : error}
            </p>
            <button type="button" onClick={() => setRevision((x) => x + 1)}>
              <RefreshCw size={14} /> Thử tải lại
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

export function TenantRules({
  filterContractId = "",
  embedded = false,
  onAccepted,
} = {}) {
  const [rules, setRules] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState("");
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    contractApi
      .myRules(controller.signal)
      .then(setRules)
      .catch((cause) => {
        if (!controller.signal.aborted) setError(errorText(cause));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [revision]);
  async function accept(item) {
    setBusy(item.contractTenantId);
    setError("");
    try {
      await contractApi.acceptRules(
        item.contractId,
        item.contractTenantId,
        item.versionId,
      );
      setRevision((x) => x + 1);
      onAccepted?.(item);
    } catch (cause) {
      setError(
        cause.status === 409
          ? "Nội quy đã thay đổi. Vui lòng tải lại và đọc phiên bản mới trước khi chấp thuận."
          : errorText(cause),
      );
    } finally {
      setBusy("");
    }
  }
  const visibleRules = filterContractId
    ? (rules || []).filter((item) => item.contractId === filterContractId)
    : rules || [];
  return (
    <section
      className={`rules-tenant${embedded ? " rules-tenant--embedded" : ""}`}
      aria-labelledby="tenant-rules-title"
    >
      {!embedded && (
        <div className="rules-heading">
          <div>
            <span className="rules-kicker">THỎA THUẬN CỦA BẠN</span>
            <h2 id="tenant-rules-title">Nội quy & điều khoản</h2>
            <p>Đọc và chấp thuận nội quy gắn với hợp đồng của bạn.</p>
          </div>
          <ShieldCheck size={26} aria-hidden="true" />
        </div>
      )}
      {loading ? (
        <p role="status">Đang tải nội quy…</p>
      ) : error ? null : !visibleRules.length ? (
        <p>
          {filterContractId
            ? "Hợp đồng này chưa có nội quy khu trọ cần xem."
            : "Bạn chưa có nội quy hợp đồng cần xem."}
        </p>
      ) : (
        visibleRules.map((item) => (
          <article className="rules-tenant-card" key={item.contractTenantId}>
            <div className="rules-heading">
              <div>
                <h3>Hợp đồng {item.contractCode}</h3>
                <p>Người thuê: {item.tenantName}</p>
              </div>
              <span
                className={`rules-badge ${item.acceptedCurrentVersion ? "rules-badge--accepted" : ""}`}
              >
                {!item.versionId
                  ? "Chờ chủ trọ thiết lập"
                  : item.acceptedCurrentVersion
                    ? "Đã chấp thuận"
                    : "Cần chấp thuận"}
              </span>
            </div>
            {item.versionId ? (
              <>
                <p className="rules-version">
                  <FileText size={15} /> Phiên bản {item.versionNumber}
                  {item.acceptedAt &&
                    ` · Chấp thuận lúc ${formatTime(item.acceptedAt)}`}
                </p>
                <div className="rules-content" tabIndex={0}>
                  {item.content}
                </div>
                {!item.acceptedCurrentVersion && (
                  <button
                    type="button"
                    className="contract-button contract-button--primary"
                    disabled={Boolean(busy)}
                    onClick={() => accept(item)}
                  >
                    {busy === item.contractTenantId
                      ? "Đang xác nhận…"
                      : "Tôi đã đọc và chấp thuận"}
                  </button>
                )}
                <Evidence
                  contractId={item.contractId}
                  linkId={item.contractTenantId}
                  canUpload={item.acceptedCurrentVersion}
                />
              </>
            ) : (
              <p>Chủ trọ chưa lưu nội quy cho hợp đồng này.</p>
            )}
          </article>
        ))
      )}
      {error && (
        <div className="rules-error" role="alert">
          {error}{" "}
          <button type="button" onClick={() => setRevision((x) => x + 1)}>
            <RefreshCw size={14} /> Tải lại
          </button>
        </div>
      )}
    </section>
  );
}
