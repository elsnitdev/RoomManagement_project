import { useEffect, useRef, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'

const maxSize = 10 * 1024 * 1024

export function EvidenceSelector({ selection, onChange, disabled }) {
  const [error, setError] = useState('')
  const [preview, setPreview] = useState('')
  const input = useRef(null)
  useEffect(() => {
    if (!selection) { setPreview(''); if (input.current) input.current.value = ''; return }
    const url = URL.createObjectURL(selection.file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [selection])
  function choose(event) {
    const file = event.target.files?.[0]
    setError('')
    if (!file) { onChange(null); return }
    if (file.size <= 0 || file.size > maxSize || !['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setError('Chọn ảnh JPEG, PNG hoặc WebP, dung lượng tối đa 10 MiB.')
      onChange(null); event.target.value = ''; return
    }
    onChange({ file, id: crypto.randomUUID() })
  }
  return <div className="bill-evidence-selector bill-field--wide">
    <label>Ảnh bằng chứng (không bắt buộc)<input ref={input} type="file" accept="image/jpeg,image/png,image/webp" disabled={disabled} onChange={choose} /></label>
    <small>JPEG, PNG hoặc WebP · tối đa 10 MiB · chỉ chủ trọ được xem.</small>
    {error && <p className="bill-error" role="alert">{error}</p>}
    {selection && preview && <div className="bill-evidence-preview"><img src={preview} alt="Xem trước ảnh bằng chứng" /><span>{selection.file.name}</span><button type="button" className="bill-link" disabled={disabled} onClick={() => onChange(null)}>Bỏ ảnh</button></div>}
  </div>
}

function EvidenceImage({ paymentId, image }) {
  const [url, setUrl] = useState('')
  const [error, setError] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    let objectUrl = ''
    billingApi.paymentEvidenceContent(paymentId, image.id, controller.signal).then(blob => {
      if (!controller.signal.aborted) { objectUrl = URL.createObjectURL(blob); setUrl(objectUrl) }
    }).catch(cause => { if (cause.name !== 'AbortError') setError(true) })
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl) }
  }, [paymentId, image.id])
  return <li>{error ? <span>Không tải được ảnh {image.fileName}.</span> : url ? <a href={url} target="_blank" rel="noreferrer"><img src={url} alt={`Bằng chứng: ${image.fileName}`} /><span>{image.fileName}</span></a> : <span role="status">Đang tải ảnh…</span>}</li>
}

export function EvidenceManager({ paymentId }) {
  const [open, setOpen] = useState(false)
  const [images, setImages] = useState([])
  const [selection, setSelection] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    if (!busy) return
    const warn = event => { event.preventDefault(); event.returnValue = '' }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [busy])
  useEffect(() => {
    if (!open) return
    const controller = new AbortController()
    setError('')
    billingApi.paymentEvidence(paymentId, controller.signal).then(rows => { if (!controller.signal.aborted) setImages(rows || []) })
      .catch(cause => { if (cause.name !== 'AbortError') setError(cause.message) })
    return () => controller.abort()
  }, [open, paymentId, revision])
  async function upload() {
    if (!selection) return
    setBusy(true); setError('')
    try { await billingApi.uploadPaymentEvidence(paymentId, selection); setSelection(null); setRevision(value => value + 1) }
    catch (cause) { setError(`Chưa tải được ảnh: ${cause.message}. Bạn có thể thử lại.`) }
    finally { setBusy(false) }
  }
  return <div className="bill-evidence-manager"><button type="button" className="bill-link" aria-expanded={open} onClick={() => setOpen(value => !value)}>{open ? 'Ẩn ảnh bằng chứng' : 'Xem / bổ sung ảnh bằng chứng'}</button>
    {open && <div><p className="bill-hint">Ảnh chỉ hiển thị cho chủ trọ của hóa đơn.</p>{images.length > 0 && <ul className="bill-evidence-gallery">{images.map(image => <EvidenceImage key={image.id} paymentId={paymentId} image={image} />)}</ul>}<EvidenceSelector selection={selection} onChange={setSelection} disabled={busy} />{error && <p className="bill-error" role="alert">{error} <button type="button" className="bill-link" onClick={() => setRevision(value => value + 1)}>Tải lại danh sách</button></p>}<button type="button" className="bill-button bill-button--secondary" disabled={!selection || busy} onClick={upload}>{busy ? 'Đang tải ảnh…' : 'Thêm ảnh'}</button></div>}
  </div>
}
