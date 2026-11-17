import { useEffect, useState } from 'react'
import { FileText, Trash2, Upload } from 'lucide-react'
import { contractApi } from '../../services/api/contractApi'
import { CONTRACT_DOCUMENT_ACCEPT, validateContractFiles } from './contractDocumentFiles'
import './contractDocuments.css'

export default function ContractDocuments({ contractId, propertyId, offerVersion, owner = false, canEdit = false }) {
  const [documents, setDocuments] = useState([])
  const [selectedFiles, setSelectedFiles] = useState([])
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => setError(''), [contractId, propertyId])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setDocuments([])
    const request = owner
      ? contractApi.documents(propertyId, contractId, controller.signal)
      : contractApi.myDocuments(contractId, controller.signal)
    request.then(items => {
      if (!controller.signal.aborted) setDocuments(items || [])
    }).catch(cause => {
      if (!controller.signal.aborted) setError(cause.message || 'Không thể tải tệp hợp đồng.')
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false)
    })
    return () => controller.abort()
  }, [contractId, propertyId, offerVersion, owner, revision])

  async function openDocument(item) {
    setBusy(true)
    setError('')
    try {
      const blob = owner
        ? await contractApi.documentContent(propertyId, contractId, item.id)
        : await contractApi.myDocumentContent(contractId, item.id)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.target = '_blank'
      anchor.rel = 'noopener noreferrer'
      anchor.click()
      setTimeout(() => URL.revokeObjectURL(url), 10 * 60 * 1000)
    } catch (cause) {
      setError(cause.message || 'Không thể mở tệp hợp đồng.')
    } finally {
      setBusy(false)
    }
  }

  async function upload(event) {
    event.preventDefault()
    const selected = Array.from(selectedFiles)
    const problem = validateContractFiles(selected)
    if (!selected.length || problem) { setError(problem || 'Vui lòng chọn tệp.'); return }
    if (documents.some(item => item.offerVersion == null && item.contentType === 'application/pdf') ||
        selected.some(file => file.type === 'application/pdf') && documents.some(item => item.offerVersion == null)) {
      setError('Chọn nhiều ảnh hoặc một PDF, không trộn PDF với ảnh.')
      return
    }
    setBusy(true)
    setError('')
    let uploaded = 0
    try {
      for (const file of selected) {
        await contractApi.uploadDocument(propertyId, contractId, file)
        uploaded += 1
      }
      setSelectedFiles([])
      event.target.reset()
    } catch (cause) {
      setError(`${uploaded ? `Đã tải ${uploaded}/${selected.length} tệp. Hãy chọn lại các tệp chưa tải. ` : ''}${cause.message || 'Không thể tải tệp hợp đồng.'}`)
      setSelectedFiles([])
      event.target.reset()
    } finally {
      setBusy(false)
      setRevision(value => value + 1)
    }
  }

  async function remove(item) {
    setBusy(true)
    setError('')
    try {
      await contractApi.deleteDocument(propertyId, contractId, item.id)
      setRevision(value => value + 1)
    } catch (cause) {
      setError(cause.message || 'Không thể xóa tệp hợp đồng.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="ct-card ct-documents" aria-label="Tệp hợp đồng">
      <h2>Ảnh hoặc PDF hợp đồng giấy</h2>
      <p className="ct-member-note">{owner
        ? 'Tệp hợp đồng đã ký là tùy chọn. Có thể tải nhiều ảnh hoặc một PDF trước khi gửi lời mời.'
        : 'Nếu chủ trọ đã đính kèm bản hợp đồng giấy, bạn có thể mở để xem.'}</p>
      {loading ? <p role="status">Đang tải tệp hợp đồng…</p> : documents.length ? (
        <ul className="ct-documents__list">
          {documents.map(item => (
            <li key={item.id}>
              <FileText size={18} aria-hidden="true" />
              <div>
                <strong>{item.fileName}</strong>
                <small>{item.offerVersion ? `Phiên bản lời mời ${item.offerVersion}` : 'Bản nháp'} · {item.contentType === 'application/pdf' ? 'PDF' : 'Ảnh'}</small>
              </div>
              <button type="button" className="contract-button contract-button--secondary" disabled={busy} onClick={() => openDocument(item)}>Mở tệp</button>
              {owner && canEdit && item.offerVersion == null && (
                <button type="button" className="contract-button contract-button--ghost" disabled={busy} onClick={() => remove(item)} aria-label={`Xóa ${item.fileName}`}><Trash2 size={16} aria-hidden="true" /></button>
              )}
            </li>
          ))}
        </ul>
      ) : <p>Không có tệp đính kèm. Người thuê vẫn có thể xác nhận lời mời.</p>}
      {owner && canEdit && (
        <form className="ct-documents__upload" onSubmit={upload}>
          <label>Thêm ảnh hoặc PDF
            <input type="file" accept={CONTRACT_DOCUMENT_ACCEPT} multiple onChange={event => { const files = Array.from(event.target.files || []); setSelectedFiles(files); setError(validateContractFiles(files)) }} />
          </label>
          <button type="submit" className="contract-button contract-button--secondary" disabled={busy || !selectedFiles.length}><Upload size={16} aria-hidden="true" /> {busy ? 'Đang tải lên…' : 'Tải tệp lên'}</button>
        </form>
      )}
      {error && <p className="contract-message contract-message--error" role="alert">{error} <button type="button" onClick={() => { setError(''); setRevision(value => value + 1) }}>Thử tải lại</button></p>}
    </section>
  )
}
